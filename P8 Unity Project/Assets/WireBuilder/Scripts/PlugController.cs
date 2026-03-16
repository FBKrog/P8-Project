using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;


public class PlugController : MonoBehaviour
{
    public bool isConected = false;
    public UnityEvent OnWirePlugged;
    public Transform plugPosition;

    [HideInInspector]
    public Transform endAnchor;
    [HideInInspector]
    public Rigidbody endAnchorRB;
    [HideInInspector]
    public WireController wireController;

    public void OnPlugged()
    {
        OnWirePlugged.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isConected) return;
        if (endAnchor == null || other.gameObject != endAnchor.gameObject) return;
        StartCoroutine(SnapWire());
    }

    private IEnumerator SnapWire()
    {
        isConected = true; // guard against re-entry immediately

        var grab = endAnchor.GetComponent<XRGrabInteractable>();

        // Force-release from the player's hand
        if (grab != null && grab.isSelected)
        {
            var interactors = new List<IXRSelectInteractor>(grab.interactorsSelecting);
            foreach (var interactor in interactors)
                grab.interactionManager.SelectExit(interactor, grab);
        }

        // Wait until XRI has cleared the selection
        int safetyFrames = 0;
        while (grab != null && grab.isSelected && safetyFrames < 10)
        {
            yield return null;
            safetyFrames++;
        }

        // Snap transform
        endAnchorRB.linearVelocity  = Vector3.zero;
        endAnchorRB.angularVelocity = Vector3.zero;
        endAnchorRB.isKinematic = true;
        endAnchor.position = plugPosition.position;
        Vector3 euler = new Vector3(transform.eulerAngles.x + 90,
                                    transform.eulerAngles.y,
                                    transform.eulerAngles.z);
        endAnchor.rotation = Quaternion.Euler(euler);

        // Permanently disable re-grabbing
        if (grab != null)
            grab.enabled = false;

        OnPlugged();
    }

    private void Update()
    {
        if (isConected && endAnchorRB != null)
            endAnchorRB.isKinematic = true;
    }
}
