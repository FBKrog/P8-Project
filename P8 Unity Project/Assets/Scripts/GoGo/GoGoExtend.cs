using UnityEngine;

/// <summary>
/// Go-Go interaction technique (Poupyrev et al. 1996).
/// Attach to the Right Hand controller GameObject.
///
/// Coordinate system (user-centred, origin at chest):
///   R_r = distance from chest to physical controller
///   R_v = distance from chest to virtual hand
///
/// Mapping F(R_r):
///   R_v = R_r               if R_r &lt; D          (linear zone, 1:1)
///   R_v = R_r + k(R_r-D)²   otherwise             (non-linear zone)
///
/// where D = 2/3 × armLength, and k ≈ 16.67 in metre units
/// (equivalent to the paper's k = 1/6 in centimetre units).
/// </summary>
[DefaultExecutionOrder(100)]
public class GoGoExtend : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform at the chest/torso — the user-centred origin for Go-Go vectors. " +
             "If null, the position is estimated as main camera position offset down by chestHeadOffset.")]
    public Transform chestTransform;

    [Header("Go-Go Parameters")]
    [Tooltip("Physical arm length in metres. The linear-zone threshold D = 2/3 × armLength.")]
    public float armLength = 0.6f;

    [Tooltip("Maximum virtual reach in metres when the physical hand is fully extended " +
             "(R_r = armLength). The Go-Go coefficient k is derived automatically from this.")]
    public float maxReachDistance = 5f;

    [Header("Rotation")]
    [Tooltip("Euler offset applied on top of the controller rotation. " +
             "Adjust Z to correct a clockwise/counter-clockwise roll misalignment.")]
    public Vector3 rotationOffset = new Vector3(0f, 0f, 120f);

    [Header("Chest Estimation")]
    [Tooltip("Vertical offset below the HMD used to estimate chest position when chestTransform is null.")]
    public float chestHeadOffset = 0.3f;

    [Tooltip("Slerp speed for mirroring the controller rotation onto the virtual hand.")]
    public float rotationSmoothing = 12f;

    // Read-only accessors for other components (e.g. interactors, grabbing).
    public Transform VirtualHand => virtualHand;
    /// <summary>Current physical arm length R_r from chest to controller.</summary>
    public float CurrentRr { get; private set; }
    /// <summary>Current virtual arm length R_v after Go-Go mapping.</summary>
    public float CurrentRv { get; private set; }

    private Transform virtualHand;
    private Vector3   handLocalPos;
    private Quaternion handLocalRot;

    void Awake()
    {
        // Find the first child tagged "Hand" — same convention as ExtendRaycast.
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            if (t != transform && t.CompareTag("Hand"))
            {
                virtualHand = t;
                break;
            }
        }

        if (virtualHand == null)
        {
            Debug.LogError("[GoGoExtend] No child Transform tagged 'Hand' found. " +
                           "Tag the hand visual/interactor GameObject as 'Hand'.", this);
            return;
        }

        handLocalPos = virtualHand.localPosition;
        handLocalRot = virtualHand.localRotation;
    }

    // LateUpdate runs after XR pose updates, ensuring we read the final controller position.
    void LateUpdate()
    {
        if (virtualHand == null) return;

        Vector3 chestPos     = GetChestPosition();
        Vector3 controllerPos = transform.position;
        Vector3 toController  = controllerPos - chestPos;

        float R_r = toController.magnitude;
        if (R_r < 1e-5f) return;   // avoid division by zero / NaN

        Vector3 dir = toController / R_r;
        float   D   = (2f / 3f) * armLength;

        // Derive k so that at full arm extension (R_r = armLength) the virtual hand
        // reaches maxReachDistance. Formula: maxReach = armLength + k*(armLength/3)²
        float armOverThree = armLength / 3f;
        float k = armOverThree > 1e-5f
            ? Mathf.Max(0f, maxReachDistance - armLength) / (armOverThree * armOverThree)
            : 0f;

        // --- Go-Go mapping F(R_r) ---
        float R_v = R_r < D
            ? R_r                              // linear zone
            : R_r + k * (R_r - D) * (R_r - D); // non-linear zone

        R_v = Mathf.Min(R_v, maxReachDistance);

        CurrentRr = R_r;
        CurrentRv = R_v;

        // Place the virtual hand along the same direction as the physical hand,
        // but at the mapped (potentially extended) distance from the chest.
        virtualHand.position = chestPos + dir * R_v;

        // Mirror the controller rotation with smoothing, applying a correction offset.
        Quaternion target = transform.rotation * Quaternion.Euler(rotationOffset);
        virtualHand.rotation = Quaternion.Slerp(
            virtualHand.rotation,
            target,
            rotationSmoothing * Time.deltaTime);
    }

    private Vector3 GetChestPosition()
    {
        if (chestTransform != null)
            return chestTransform.position;

        Camera cam = Camera.main;
        if (cam != null)
            return cam.transform.position + Vector3.down * chestHeadOffset;

        return transform.position;  // last-resort fallback
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Visualise the two Go-Go zones in the Scene view.
        Vector3 chest = Application.isPlaying
            ? GetChestPosition()
            : (chestTransform != null
                ? chestTransform.position
                : transform.position + Vector3.down * chestHeadOffset);

        float D = (2f / 3f) * armLength;

        // Linear zone (green, solid fill)
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.10f);
        Gizmos.DrawSphere(chest, D);
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.80f);
        Gizmos.DrawWireSphere(chest, D);

        // Full arm length (orange, for reference)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.05f);
        Gizmos.DrawSphere(chest, armLength);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.60f);
        Gizmos.DrawWireSphere(chest, armLength);

        // Draw current R_r and R_v vectors when playing.
        if (!Application.isPlaying || virtualHand == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(chest, transform.position);  // R_r (physical)

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(chest, virtualHand.position); // R_v (virtual)
    }
#endif
}
