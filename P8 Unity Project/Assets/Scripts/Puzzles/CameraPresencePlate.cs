using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Camera-position-based pressure plate for VR.
/// Replaces PressurePlate on any plate GameObject.
/// Fires OnPlateActivated / OnPlateDeactivated when the player's head camera
/// enters or exits the detection volume above the plate surface.
///
/// Detection logic operates in the plate's local space, so rotation is handled
/// correctly without relying on Physics triggers or collider tags.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class CameraPresencePlate : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Camera Source")]
    [Tooltip("The camera to track. Leave empty to use Camera.main at runtime.")]
    [SerializeField] private Camera overrideCamera;

    [Header("Detection Volume")]
    [Tooltip("Minimum height above the plate's top surface (metres, world-space). " +
             "0 means the head must be at or above surface level.")]
    [SerializeField] private float minHeightAbovePlate = 0f;

    [Tooltip("Maximum height above the plate's top surface (metres, world-space). " +
             "2.5 m comfortably covers a standing adult in VR.")]
    [SerializeField] private float maxHeightAbovePlate = 2.5f;

    [Header("Events")]
    public UnityEvent OnPlateActivated;
    public UnityEvent OnPlateDeactivated;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = false;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private Camera      _camera;
    private BoxCollider _col;
    private bool        _isActivated;

    // Cached collider data so Update() is allocation-free.
    private Vector3 _localCenter;
    private float   _halfX;
    private float   _halfZ;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _col = GetComponent<BoxCollider>();
        _localCenter = _col.center;
        _halfX       = _col.size.x * 0.5f;
        _halfZ       = _col.size.z * 0.5f;
    }

    private void Start()
    {
        _camera = overrideCamera != null ? overrideCamera : Camera.main;

        if (_camera == null)
            Debug.LogWarning($"[CameraPresencePlate] '{gameObject.name}': no camera found. " +
                             "Tag a camera 'MainCamera' or assign Override Camera in the Inspector.");
    }

    private void Update()
    {
        if (_camera == null) return;

        bool nowInside = IsInsideVolume(_camera.transform.position);

        if (nowInside && !_isActivated)
        {
            _isActivated = true;
            if (logStateChanges)
                Debug.Log($"[CameraPresencePlate] '{gameObject.name}' ACTIVATED.");
            OnPlateActivated.Invoke();
        }
        else if (!nowInside && _isActivated)
        {
            _isActivated = false;
            if (logStateChanges)
                Debug.Log($"[CameraPresencePlate] '{gameObject.name}' DEACTIVATED.");
            OnPlateDeactivated.Invoke();
        }
    }

    // -------------------------------------------------------------------------
    // Detection logic
    // -------------------------------------------------------------------------

    private bool IsInsideVolume(Vector3 worldPos)
    {
        // XZ footprint check in plate local space — handles any Y rotation correctly.
        Vector3 local = transform.InverseTransformPoint(worldPos);

        if (Mathf.Abs(local.x - _localCenter.x) > _halfX) return false;
        if (Mathf.Abs(local.z - _localCenter.z) > _halfZ) return false;

        // Height check in world space so the metre values are intuitive for designers.
        float plateTopWorldY = GetPlateTopWorldY();
        float height = worldPos.y - plateTopWorldY;
        return height >= minHeightAbovePlate && height <= maxHeightAbovePlate;
    }

    /// <summary>
    /// World-space Y of the plate's top surface, accounting for rotation and scale.
    /// </summary>
    private float GetPlateTopWorldY()
    {
        Vector3 localTop = new Vector3(_localCenter.x,
                                       _localCenter.y + _col.size.y * 0.5f,
                                       _localCenter.z);
        return transform.TransformPoint(localTop).y;
    }

    // -------------------------------------------------------------------------
    // Editor Gizmos
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return;

        Vector3 lc = col.center;
        float   hx = col.size.x * 0.5f;
        float   hy = col.size.y * 0.5f;
        float   hz = col.size.z * 0.5f;

        // Apply TRS so gizmos rotate/scale with the object.
        Gizmos.matrix = transform.localToWorldMatrix;

        // 1. Collider footprint slab — green idle, yellow when active.
        Gizmos.color = _isActivated
            ? new Color(1f, 0.9f, 0f, 0.5f)
            : new Color(0f, 1f, 0.4f, 0.3f);
        Gizmos.DrawCube(lc, col.size);

        Gizmos.color = _isActivated
            ? new Color(1f, 0.9f, 0f, 1f)
            : new Color(0f, 1f, 0.4f, 1f);
        Gizmos.DrawWireCube(lc, col.size);

        // 2. Detection volume above the plate (cyan).
        // maxHeightAbovePlate is in world metres; convert to local Y units via Y scale.
        float scaleY    = transform.lossyScale.y > 0.0001f ? transform.lossyScale.y : 1f;
        float localMinH = minHeightAbovePlate / scaleY;
        float localMaxH = maxHeightAbovePlate / scaleY;
        float detHeight = localMaxH - localMinH;
        float detCenterY = lc.y + hy + localMinH + detHeight * 0.5f;

        Vector3 detCenter = new Vector3(lc.x, detCenterY, lc.z);
        Vector3 detSize   = new Vector3(hx * 2f, detHeight, hz * 2f);

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.12f);
        Gizmos.DrawCube(detCenter, detSize);

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireCube(detCenter, detSize);

        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
