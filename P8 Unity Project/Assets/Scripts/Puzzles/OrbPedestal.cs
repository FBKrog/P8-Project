using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Place on the pedestal's snap-point child transform.
/// When a TPOrb-tagged XRGrabInteractable enters the snap radius,
/// it is force-released and snapped to this point.
/// Fires OrbPlaced when snap completes.
/// </summary>
public class OrbPedestal : MonoBehaviour
{
    [SerializeField] private float            snapRadius    = 0.15f;
    [SerializeField] private string           orbTag        = "TPOrb";
    [SerializeField] private HandTPOrbConnect handTPConnect; // optional; assign in Inspector
    public UnityEvent OrbPlaced;

    private bool _hasOrb;

    private void Update()
    {
        if (_hasOrb) return;
        foreach (Collider col in Physics.OverlapSphere(transform.position, snapRadius))
        {
            if (!col.CompareTag(orbTag)) continue;
            XRGrabInteractable grab = col.GetComponentInParent<XRGrabInteractable>();
            if (grab == null) continue;

            bool heldByXRI  = grab.isSelected;
            bool heldByHand = handTPConnect != null && handTPConnect.SnappedOrb == grab;

            if (heldByXRI || heldByHand)
            {
                StartCoroutine(SnapOrb(grab));
                break;
            }
        }
    }

    private IEnumerator SnapOrb(XRGrabInteractable orb)
    {
        _hasOrb = true;

        // Disconnect from HandTPOrbConnect FIRST — prevents Update() repositioning during yield
        handTPConnect?.DisconnectOrb();

        // Force-release if XRI-selected
        var interactor = orb.firstInteractorSelecting;
        if (interactor != null)
            orb.interactionManager.SelectExit(interactor, orb);

        yield return null;

        Rigidbody rb = orb.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = rb.angularVelocity = Vector3.zero; rb.isKinematic = true; }

        orb.transform.position = transform.position;
        orb.transform.rotation = transform.rotation;

        orb.GetComponent<TeleportOrb>()?.OnPlacedOnPad();

        OrbPlaced.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, snapRadius);
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(OrbPedestal))]
public class OrbPedestalEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        UnityEditor.EditorGUILayout.Space();
        if (GUILayout.Button("Fire OrbPlaced (Test)"))
        {
            var pedestal = (OrbPedestal)target;
            pedestal.OrbPlaced.Invoke();
        }
    }
}
#endif
