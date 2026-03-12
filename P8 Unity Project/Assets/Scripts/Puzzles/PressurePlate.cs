using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to a pressure plate.
/// Fires OnPlateActivated when the correct object enters the trigger volume, and OnPlateDeactivated when it leaves.
/// Wire these to DoorTrigger.Activate() / Deactivate() in the Inspector to integrate with the door system.
/// </summary>
public class PressurePlate : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("Tag on the VR Player that activates the plate when stepping on it. Set to empty string to disable.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Events")]
    public UnityEvent OnPlateActivated;
    public UnityEvent OnPlateDeactivated;

    private bool       _isActivated  = false;
    private GameObject _currentBlock = null;

    private void OnTriggerEnter(Collider other)
    {
        if (_isActivated) return;

        PressurePlateTrigger linker = other.GetComponentInParent<PressurePlateTrigger>();
        bool isLinkedBlock = linker != null && linker.TargetPlate == this;

        bool isPlayer = !string.IsNullOrEmpty(playerTag)
                     && (other.CompareTag(playerTag)
                      || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag)));

        if (!isLinkedBlock && !isPlayer) return;

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

        bool isTracked = exiting == _currentBlock;
        bool isPlayer  = !string.IsNullOrEmpty(playerTag)
                      && (other.CompareTag(playerTag)
                       || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag)));

        if (!isTracked && !isPlayer) return;

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
