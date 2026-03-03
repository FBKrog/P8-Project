using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to a pressure plate. Fires OnPlateActivated when the correct block
/// (one whose PressureLinker.TargetPlate == this) enters the trigger volume,
/// and OnPlateDeactivated when it leaves. Wire these to DoorTrigger.Activate()
/// / Deactivate() in the Inspector to integrate with the door system.
/// </summary>
public class PressurePlate : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent OnPlateActivated;
    public UnityEvent OnPlateDeactivated;

    private bool       _isActivated  = false;
    private GameObject _currentBlock = null;

    private void OnTriggerEnter(Collider other)
    {
        if (_isActivated) return;

        PressurePlateTrigger linker = other.GetComponentInParent<PressurePlateTrigger>();
        if (linker == null || linker.TargetPlate != this) return;

        _isActivated  = true;
        _currentBlock = other.attachedRigidbody != null
                        ? other.attachedRigidbody.gameObject
                        : other.gameObject;

        OnPlateActivated.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_isActivated) return;

        GameObject exiting = other.attachedRigidbody != null
                             ? other.attachedRigidbody.gameObject
                             : other.gameObject;

        if (exiting != _currentBlock) return;

        _isActivated  = false;
        _currentBlock = null;

        OnPlateDeactivated.Invoke();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.2f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
#endif
}
