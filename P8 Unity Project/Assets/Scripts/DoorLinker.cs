using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Slides a door open when every DoorTrigger in the Triggers list has been
/// activated. Works with any number of conditions: one trigger opens immediately
/// on that trigger; two triggers require both to be true; and so on.
///
/// Add a DoorTrigger component to each source object (Plug, Battery Socket, …)
/// and wire the source's UnityEvent to DoorTrigger.Activate() in the Inspector,
/// then add each DoorTrigger to this component's Triggers list.
/// </summary>
public class DoorLinker : MonoBehaviour
{
    [Header("Triggers — ALL must be activated to open the door")]
    [SerializeField] private List<DoorTrigger> triggers = new();

    [Header("Door Panels")]
    [Tooltip("The door transform to slide (and all its children will move with it). " +
             "Leave empty to auto-find the 'Door' tagged object, or falls back to this GameObject.")]
    [SerializeField] private Transform leftPanel;
    [Tooltip("Second panel for split/double doors — slides in the NEGATIVE slideAxis direction. " +
             "Leave empty for a single-door that slides as one unit.")]
    [SerializeField] private Transform rightPanel;

    [Header("Slide Settings")]
    [Tooltip("How far each panel slides in its direction (local units).")]
    [SerializeField] private float slideDistance = 1.5f;
    [Tooltip("How long the slide animation takes in seconds.")]
    [SerializeField] private float slideDuration = 0.8f;
    [Tooltip("Local-space axis along which the panels slide. Right panel moves positive, left panel moves negative.")]
    [SerializeField] private Vector3 slideAxis = Vector3.right;

    [Header("Events")]
    public UnityEvent OnDoorOpened;

    private bool _isOpen = false;

    // -------------------------------------------------------------------------

    private void Start()
    {
        if (leftPanel == null)
        {
            // Try to find a Door-tagged object and slide its whole transform (children follow).
            GameObject doorObj = GameObject.FindGameObjectWithTag("Door");
            if (doorObj != null)
            {
                leftPanel = doorObj.transform;
            }
            else
            {
                // Fall back: slide this GameObject itself so placing DoorLinker directly
                // on the door object requires no manual panel assignment.
                leftPanel = transform;
                Debug.LogWarning("[DoorLinker] No 'Door' tagged object found — sliding this GameObject.");
            }
        }

        foreach (var trigger in triggers)
        {
            if (trigger != null)
                trigger.Activated += CheckAndOpen;
        }
    }

    private void OnDestroy()
    {
        foreach (var trigger in triggers)
        {
            if (trigger != null)
                trigger.Activated -= CheckAndOpen;
        }
    }

    // -------------------------------------------------------------------------

    [ContextMenu("Fire Door Trigger")]
    private void TestFireDoor()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DoorLinker] Fire Door Trigger only works in Play mode.");
            return;
        }
        if (!_isOpen)
        {
            StartCoroutine(SlideDoor());
            OnDoorOpened.Invoke();
        }
    }

    private void CheckAndOpen()
    {
        if (_isOpen) return;

        foreach (var trigger in triggers)
        {
            // Any null entry or unactivated trigger blocks the door
            if (trigger == null || !trigger.IsActivated)
                return;
        }

        StartCoroutine(SlideDoor());
        OnDoorOpened.Invoke();
    }

    private IEnumerator SlideDoor()
    {
        _isOpen = true;

        Vector3 axis = slideAxis.normalized;
        Vector3 leftStart = leftPanel.localPosition;
        Vector3 leftEnd = rightPanel != null
            ? leftStart - axis * slideDistance   // two-panel: left moves negative
            : leftStart + axis * slideDistance;  // single-panel: moves positive

        Vector3 rightStart = rightPanel != null ? rightPanel.localPosition : Vector3.zero;
        Vector3 rightEnd = rightPanel != null ? rightStart + axis * slideDistance : Vector3.zero;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            leftPanel.localPosition = Vector3.Lerp(leftStart, leftEnd, t);
            if (rightPanel != null)
                rightPanel.localPosition = Vector3.Lerp(rightStart, rightEnd, t);
            yield return null;
        }

        leftPanel.localPosition = leftEnd;
        if (rightPanel != null)
            rightPanel.localPosition = rightEnd;
    }
}
