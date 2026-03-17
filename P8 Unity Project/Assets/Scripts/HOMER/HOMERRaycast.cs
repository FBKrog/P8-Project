using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// HOMER technique — input, ray-casting, extension, and grab/release phase.
/// Attach to the Arm GameObject that has a child Transform tagged "Hand".
///
/// Trigger press   → begin aiming (ray-cast line appears)
/// Trigger release → fire ray; if hit, hand extends to surface
/// Select press    → grab XRGrabInteractable at hand position (if any)
/// Select release  → drop object; hand stays extended
/// Trigger press   → retract hand back to controller
/// </summary>
public class HOMERRaycast : MonoBehaviour
{
    [Header("Input")]
    public InputActionProperty triggerAction;
    public InputActionProperty selectAction;    // grip / XRI Select

    [Header("Ray-cast")]
    public float     maxRayDistance = 25f;
    public LayerMask raycastMask    = ~0;

    [Header("Extension")]
    public float extendSpeed  = 30f;
    public float retractSpeed = 30f;

    [Header("Line Visual")]
    public Material lineMaterial;
    public float    lineWidth = 0.02f;

    // ── Public API (read by HOMERManipulator) ────────────────────────────
    public bool       IsGrabbing     => state == State.Grabbed;
    /// <summary>True while the hand is extended (moving freely or holding an object).</summary>
    public bool       IsHandExtended => state == State.Extended || state == State.Grabbed;
    public Transform  VirtualHand    => virtualHand;
    public Transform  PhysicalHand   => transform;
    public GameObject GrabbedObject  => grabbedObject;

    /// <summary>Fired when the hand finishes extending to the surface. HOMERManipulator uses
    /// this to compute scaleFactor for free-hand movement.</summary>
    public event System.Action             ExtendStarted;
    public event System.Action<GameObject> GrabStarted;
    public event System.Action             GrabEnded;
    public event System.Action             RetractStarted;

    // ── Internals ─────────────────────────────────────────────────────────
    private enum State { Idle, Aiming, Extending, Extended, Grabbed, Retracting }
    private State state = State.Idle;

    private Transform  virtualHand;
    private Vector3    handLocalPos;
    private Quaternion handLocalRot;

    private Vector3              targetWorldPos;
    private XRGrabInteractable   grabbableAtTarget;

    private GameObject grabbedObject;
    private Rigidbody  grabbedRb;
    private bool       rbWasKinematic;

    private LineRenderer line;
    private GameObject   lineObj;

    // ── Unity lifecycle ───────────────────────────────────────────────────
    void Awake()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            if (t != transform && t.CompareTag("Hand"))
            {
                virtualHand = t;
                break;
            }
        }

        if (virtualHand == null)
            Debug.LogError("[HOMERRaycast] No child Transform tagged 'Hand' found.", this);
        else
        {
            handLocalPos = virtualHand.localPosition;
            handLocalRot = virtualHand.localRotation;
        }

        lineObj = new GameObject("HOMERLine");
        line    = lineObj.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth    = lineWidth;
        line.endWidth      = lineWidth;
        line.useWorldSpace = true;
        line.alignment     = LineAlignment.View;
        if (lineMaterial != null) line.material = lineMaterial;
        line.enabled = false;

    }

    void OnEnable()
    {
        triggerAction.action?.Enable();
        selectAction.action?.Enable();
    }

    void OnDisable()
    {
        triggerAction.action?.Disable();
        selectAction.action?.Disable();
    }

    void Update()
    {
        bool triggerPressed  = triggerAction.action.WasPressedThisFrame();
        bool triggerReleased = triggerAction.action.WasReleasedThisFrame();
        bool selectPressed   = selectAction.action.WasPressedThisFrame();
        bool selectReleased  = selectAction.action.WasReleasedThisFrame();

        switch (state)
        {
            case State.Idle:
                if (triggerPressed)
                {
                    state        = State.Aiming;
                    line.enabled = true;
                }
                break;

            case State.Aiming:
                if (triggerReleased)
                {
                    if (TryRaycast(out Vector3 hitPoint, out XRGrabInteractable grabbable))
                    {
                        targetWorldPos    = hitPoint;
                        grabbableAtTarget = grabbable;
                        virtualHand.SetParent(null);
                        line.enabled = false;
                        state = State.Extending;
                    }
                    else
                    {
                        CancelAim();
                    }
                }
                break;

            case State.Extending:
                if (triggerPressed)
                {
                    BeginRetract();
                    break;
                }
                MoveHandToward(targetWorldPos, extendSpeed);
                if (Vector3.Distance(virtualHand.position, targetWorldPos) < 0.01f)
                {
                    virtualHand.position = targetWorldPos;
                    state                = State.Extended;
                    ExtendStarted?.Invoke();
                }
                break;

            case State.Extended:
                if (triggerPressed)
                {
                    BeginRetract();
                    break;
                }
                if (selectPressed && grabbableAtTarget != null && grabbableAtTarget.enabled)
                    BeginGrab(grabbableAtTarget.gameObject);
                break;

            case State.Grabbed:
                if (selectReleased)
                    EndGrab();
                else if (triggerPressed)
                {
                    EndGrab();
                    BeginRetract();
                }
                break;

            case State.Retracting:
                Vector3 armTip = transform.TransformPoint(handLocalPos);
                MoveHandToward(armTip, retractSpeed);
                if (Vector3.Distance(virtualHand.position, armTip) < 0.01f)
                {
                    virtualHand.SetParent(transform);
                    virtualHand.localPosition = handLocalPos;
                    virtualHand.localRotation = handLocalRot;
                    // line.enabled           = false;
                    state                     = State.Idle;
                }
                break;
        }
    }

    void LateUpdate()
    {
        UpdateLine();
    }

    void OnDestroy()
    {
        if (lineObj) Destroy(lineObj);
    }

    // ── Grab / Release ────────────────────────────────────────────────────
    private void BeginGrab(GameObject obj)
    {
        grabbedObject = obj;

        grabbedRb = obj.GetComponent<Rigidbody>();
        if (grabbedRb != null)
        {
            rbWasKinematic        = grabbedRb.isKinematic;
            grabbedRb.isKinematic = true;
        }

        // Object joins the virtual hand (which may have moved since the initial extension).
        // Hand is already unparented from the Extending phase; don't move it.
        // LeverGrab owns its own position — skip the teleport to avoid fighting LateUpdate.
        if (obj.GetComponent<LeverGrab>() == null)
            obj.transform.position = virtualHand.position;
        virtualHand.rotation   = transform.rotation;

        state = State.Grabbed;

        GrabStarted?.Invoke(obj);
    }

    public void EndGrab()
    {
        if (grabbedRb != null)
        {
            grabbedRb.isKinematic = rbWasKinematic;
            grabbedRb             = null;
        }

        grabbedObject = null;

        // Stay extended — user retracts with trigger. Virtual hand stays where it is.
        state = State.Extended;

        GrabEnded?.Invoke();
    }

    private void BeginRetract()
    {
        RetractStarted?.Invoke();
        state = State.Retracting;
    }

    private void CancelAim()
    {
        line.enabled = false;
        state        = State.Idle;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private bool TryRaycast(out Vector3 hitPoint, out XRGrabInteractable grabbable)
    {
        if (Physics.Raycast(transform.position, transform.forward,
                out RaycastHit hit, maxRayDistance, raycastMask))
        {
            hitPoint = hit.point;
            // Walk up the hierarchy in case the collider is on a child of the interactable.
            var found = hit.collider.GetComponentInParent<XRGrabInteractable>();
            grabbable = (found != null && found.enabled) ? found : null;
            return true;
        }
        hitPoint  = Vector3.zero;
        grabbable = null;
        return false;
    }

    private void MoveHandToward(Vector3 target, float speed)
    {
        virtualHand.position = Vector3.MoveTowards(virtualHand.position, target, speed * Time.deltaTime);
    }

    private void UpdateLine()
    {
        if (!line.enabled) return;

        // Only runs during Aiming — all other states have line.enabled = false.
        Vector3 armTip = transform.TransformPoint(handLocalPos);
        line.SetPosition(0, armTip);
        if (Physics.Raycast(transform.position, transform.forward,
                out RaycastHit hit, maxRayDistance, raycastMask))
            line.SetPosition(1, hit.point);
        else
            line.SetPosition(1, transform.position + transform.forward * maxRayDistance);
    }

}
