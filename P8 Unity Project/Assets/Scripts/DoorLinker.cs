using System.Collections;
using UnityEngine;

public class DoorLinker : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PlugController to listen to. Auto-found by 'Plug' tag if left empty.")]
    [SerializeField] private PlugController plug;

    [Tooltip("Left door panel. Auto-found as first child of the 'Door' tagged object if left empty.")]
    [SerializeField] private Transform leftPanel;

    [Tooltip("Right door panel. Auto-found as second child of the 'Door' tagged object if left empty.")]
    [SerializeField] private Transform rightPanel;

    [Header("Slide Settings")]
    [Tooltip("How far each panel slides in its direction (in local units).")]
    [SerializeField] private float slideDistance = 1.5f;

    [Tooltip("How long the slide animation takes in seconds.")]
    [SerializeField] private float slideDuration = 0.8f;

    [Tooltip("Local-space axis along which the panels slide. Right panel moves positive, left panel moves negative.")]
    [SerializeField] private Vector3 slideAxis = Vector3.right;

    private bool _isOpen = false;

    private void Start()
    {
        if (plug == null)
        {
            GameObject plugObj = GameObject.FindGameObjectWithTag("Plug");
            if (plugObj != null)
                plug = plugObj.GetComponent<PlugController>();
            else
                Debug.LogWarning("[DoorLinker] No GameObject tagged 'Plug' found.");
        }

        if (leftPanel == null || rightPanel == null)
        {
            GameObject doorObj = GameObject.FindGameObjectWithTag("Door");
            if (doorObj != null)
            {
                if (doorObj.transform.childCount >= 2)
                {
                    if (leftPanel == null)  leftPanel  = doorObj.transform.GetChild(0);
                    if (rightPanel == null) rightPanel = doorObj.transform.GetChild(1);
                }
                else
                {
                    Debug.LogWarning("[DoorLinker] 'Door' object needs at least two children (left and right panels).");
                }
            }
            else
            {
                Debug.LogWarning("[DoorLinker] No GameObject tagged 'Door' found.");
            }
        }

        if (plug != null)
            plug.OnWirePlugged.AddListener(OnPlugged);
    }

    private void OnDestroy()
    {
        if (plug != null)
            plug.OnWirePlugged.RemoveListener(OnPlugged);
    }

    private void OnPlugged()
    {
        if (!_isOpen)
            StartCoroutine(SlideDoor());
    }

    private IEnumerator SlideDoor()
    {
        _isOpen = true;

        Vector3 leftStart  = leftPanel.localPosition;
        Vector3 rightStart = rightPanel.localPosition;
        Vector3 axis       = slideAxis.normalized;
        Vector3 leftEnd    = leftStart  - axis * slideDistance;
        Vector3 rightEnd   = rightStart + axis * slideDistance;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            leftPanel.localPosition  = Vector3.Lerp(leftStart,  leftEnd,  t);
            rightPanel.localPosition = Vector3.Lerp(rightStart, rightEnd, t);
            yield return null;
        }

        leftPanel.localPosition  = leftEnd;
        rightPanel.localPosition = rightEnd;
    }
}
