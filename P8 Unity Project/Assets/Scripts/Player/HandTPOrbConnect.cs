using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach to a child transform of Left Arm named "OrbSnapPoint".
/// Detects when a TPOrb (tagged "TPOrb") enters the snap radius,
/// force-releases it from the right hand, snaps it to this transform,
/// and enables teleportation via TeleportationActivator.
/// Grabbing the orb off the snap point disables teleportation again.
/// </summary>
public class HandTPOrbConnect : MonoBehaviour
{
    [SerializeField] private TeleportationActivator teleportationActivator;
    [SerializeField] private float snapRadius = 0.12f;
    [SerializeField] private string orbTag = "TPOrb";

    public event System.Action OrbSnapped;

    // Read by OrbPedestal to check whether this hand controls a given orb
    public XRGrabInteractable SnappedOrb => _snappedOrb;

    private XRGrabInteractable _snappedOrb;
    private bool _isSnapping;
    private float _snapCooldown;

    private void Update()
    {
        if (_snappedOrb != null)
        {
            _snappedOrb.transform.position = transform.position;
            _snappedOrb.transform.rotation = transform.rotation;
            return;
        }

        if (_snapCooldown > 0f)
        {
            _snapCooldown -= Time.deltaTime;
            return;
        }

        if (_isSnapping)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, snapRadius);
        foreach (Collider col in hits)
        {
            if (!col.CompareTag(orbTag))
                continue;

            XRGrabInteractable grab = col.GetComponentInParent<XRGrabInteractable>();
            if (grab != null && grab.isSelected)
            {
                StartCoroutine(SnapOrb(grab));
                break;
            }
        }
    }

    private IEnumerator SnapOrb(XRGrabInteractable orb)
    {
        _isSnapping = true;

        // Force-release from whichever interactor is holding it
        UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor = orb.firstInteractorSelecting;
        if (interactor != null)
            orb.interactionManager.SelectExit(interactor, orb);

        // Wait one frame for XRI to process the release
        yield return null;

        Rigidbody rb = orb.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        _snappedOrb = orb;
        _isSnapping = false;

        if (teleportationActivator != null)
            teleportationActivator.orbConnected = true;

        OrbSnapped?.Invoke();

        // Listen for re-grab so we can detach
        orb.selectEntered.AddListener(OnOrbRegrabbed);
    }

    /// <summary>
    /// Called by OrbPedestal when the orb is claimed by the pedestal.
    /// Clears the snap without re-enabling physics (pedestal keeps the orb kinematic).
    /// </summary>
    public void DisconnectOrb()
    {
        if (_snappedOrb == null) return;
        _snappedOrb.selectEntered.RemoveListener(OnOrbRegrabbed);
        _snappedOrb = null;
        if (teleportationActivator != null)
            teleportationActivator.orbConnected = false;
    }

    private void OnOrbRegrabbed(SelectEnterEventArgs args)
    {
        if (_snappedOrb == null)
            return;

        _snappedOrb.selectEntered.RemoveListener(OnOrbRegrabbed);

        Rigidbody rb = _snappedOrb.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = false;

        _snappedOrb = null;
        _snapCooldown = 0.5f;

        if (teleportationActivator != null)
            teleportationActivator.orbConnected = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, snapRadius);
    }
}
