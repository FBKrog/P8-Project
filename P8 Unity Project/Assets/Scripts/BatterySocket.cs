using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach to the Battery Insert Box. Every frame it checks whether a Battery-tagged
/// object's Plus pole is within snapRadius of the socket's Plus pole. When it is,
/// the battery is force-released from the player's hand and snapped in place so its
/// Plus/Minus poles align exactly with the socket's. Fires OnBatteryPlaced on success.
/// The battery stays grabbable so the player can pull it back out.
///
/// No trigger collider required — the snap zone is driven entirely by snapRadius and
/// is visualised as green/red spheres in the Scene view when this object is selected.
/// </summary>
public class BatterySocket : MonoBehaviour
{
    [Header("Socket Poles")]
    [Tooltip("Plus pole transform on this socket (child of the insert box).")]
    [SerializeField] private Transform socketPlus;
    [Tooltip("Minus pole transform on this socket (child of the insert box).")]
    [SerializeField] private Transform socketMinus;

    [Header("Battery Detection")]
    [Tooltip("Tag on the battery root GameObject.")]
    [SerializeField] private string batteryTag = "Battery";
    [Tooltip("Name of the Plus child on the battery.")]
    [SerializeField] private string plusChildName = "Plus";
    [Tooltip("Name of the Minus child on the battery.")]
    [SerializeField] private string minusChildName = "Minus";

    [Header("Snap Settings")]
    [Tooltip("How close the battery's Plus pole must get to the socket's Plus pole to trigger a snap (metres).")]
    [SerializeField] private float snapRadius = 0.15f;

    [Header("Events")]
    public UnityEvent OnBatteryPlaced;

    private bool _batteryInserted  = false;
    private bool _snapInProgress   = false;
    private GameObject _currentBattery;

    // -------------------------------------------------------------------------

    private void Update()
    {
        if (_batteryInserted || _snapInProgress || socketPlus == null) return;

        // Cast a sphere around the socket's Plus pole to find nearby colliders.
        // Using a 2× radius here so any part of the battery is detected, then we
        // refine with the exact pole-to-pole distance below.
        Collider[] hits = Physics.OverlapSphere(socketPlus.position, snapRadius * 2f);

        foreach (Collider hit in hits)
        {
            GameObject battery = GetBatteryRoot(hit.gameObject);
            if (battery == null) continue;

            // Find the battery's Plus child and measure pole-to-pole distance
            Transform batteryPlus = battery.transform.Find(plusChildName);
            if (batteryPlus == null) continue;

            float dist = Vector3.Distance(batteryPlus.position, socketPlus.position);
            if (dist > snapRadius) continue;

            // Close enough — initiate snap
            _snapInProgress = true;
            StartCoroutine(SnapBattery(battery));
            return;
        }
    }

    // -------------------------------------------------------------------------

    private IEnumerator SnapBattery(GameObject battery)
    {
        // --- 1. Force-release from the player's XR hand ---
        var grab = battery.GetComponent<XRGrabInteractable>();
        if (grab != null && grab.isSelected)
        {
            var interactors = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(grab.interactorsSelecting);
            foreach (var interactor in interactors)
                grab.interactionManager.SelectExit(interactor, grab);
        }

        // Wait one frame so XRI finishes processing the SelectExit before we
        // override the rigidbody and transform.
        yield return null;

        // --- 2. Locate battery poles ---
        Transform batteryPlus  = battery.transform.Find(plusChildName);
        Transform batteryMinus = battery.transform.Find(minusChildName);

        if (batteryPlus == null || batteryMinus == null)
        {
            Debug.LogWarning($"[BatterySocket] Battery '{battery.name}' is missing " +
                             $"a child named '{plusChildName}' or '{minusChildName}'.");
            _snapInProgress = false;
            yield break;
        }

        // --- 3. Compute snap rotation ---
        // Battery Plus→Minus axis in world space (current orientation)
        Vector3 batteryAxisWorld = battery.transform.TransformDirection(
            (batteryMinus.localPosition - batteryPlus.localPosition).normalized);

        // Socket Plus→Minus axis in world space
        Vector3 socketAxisWorld = (socketMinus.position - socketPlus.position).normalized;

        // Rotation that swings the battery axis onto the socket axis
        Quaternion axisAlignment   = Quaternion.FromToRotation(batteryAxisWorld, socketAxisWorld);
        Quaternion snappedRotation = axisAlignment * battery.transform.rotation;

        // --- 4. Compute snap position ---
        // Use the world-space offset between battery root and its Plus child so
        // that non-unit scale on the battery is handled correctly.
        // Only the delta rotation (axisAlignment) is applied, not the full snappedRotation,
        // because the offset is already expressed in world space.
        Vector3 plusOffsetWorld = batteryPlus.position - battery.transform.position;
        Vector3 snappedPosition = socketPlus.position - (axisAlignment * plusOffsetWorld);

        // --- 5. Apply snap ---
        var rb = battery.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = true;
        }

        battery.transform.SetPositionAndRotation(snappedPosition, snappedRotation);
        battery.transform.SetParent(transform, worldPositionStays: true);

        _batteryInserted = true;
        _snapInProgress  = false;
        _currentBattery  = battery;

        // Subscribe so we know when the player pulls the battery back out
        if (grab != null)
            grab.selectEntered.AddListener(OnBatteryRegrabbed);

        OnBatteryPlaced.Invoke();
    }

    // -------------------------------------------------------------------------

    private void OnBatteryRegrabbed(SelectEnterEventArgs args)
    {
        if (_currentBattery == null) return;

        var grab = _currentBattery.GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.selectEntered.RemoveListener(OnBatteryRegrabbed);

        _currentBattery.transform.SetParent(null, worldPositionStays: true);

        var rb = _currentBattery.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = false;

        _batteryInserted = false;
        _currentBattery  = null;
    }

    // -------------------------------------------------------------------------

    private GameObject GetBatteryRoot(GameObject obj)
    {
        Transform t = obj.transform;
        while (t != null)
        {
            if (t.CompareTag(batteryTag))
                return t.gameObject;
            t = t.parent;
        }
        return null;
    }

    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (socketPlus != null)
        {
            // Green sphere = Plus snap zone
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawSphere(socketPlus.position, snapRadius);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(socketPlus.position, snapRadius);
        }

        if (socketMinus != null)
        {
            // Red sphere = Minus reference position
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawSphere(socketMinus.position, snapRadius * 0.5f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(socketMinus.position, snapRadius * 0.5f);
        }

        // Line connecting the two poles
        if (socketPlus != null && socketMinus != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(socketPlus.position, socketMinus.position);
        }
    }
#endif
}
