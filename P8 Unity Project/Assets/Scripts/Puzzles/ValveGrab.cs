using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Valve puzzle component. Attach to a valve wheel GameObject alongside an XRGrabInteractable
/// and a Rigidbody (kinematic — ValveGrab owns the transform).
///
/// Supports grab via HOMER, Go-Go, and DAOM arm techniques. On grab, the nearest grab point
/// (child Transform on the valve rim) is selected and the virtual hand snaps to it each frame,
/// identical to LeverGrab's arc-lock pattern.
///
/// Tracks cumulative rotation about the spin axis; fires OnValveActivated once the total
/// spin exceeds activationRotation degrees.
///
/// cumulativeRotation persists across grabs — re-grab continues from where the player left off.
///
/// Execution order 200 — runs AFTER all technique LateUpdates (order 100).
/// </summary>
[DefaultExecutionOrder(200)]
public class ValveGrab : MonoBehaviour, IRotaryGrabbable
{
    [Header("Technique References")]
    public HOMERRaycast       homer;
    public GoGoExtend         goGoExtend;
    [Tooltip("XRDirectInteractor on GoGo's virtual hand.")]
    public XRDirectInteractor goGoInteractor;
    // DAOM resolved at runtime via DAOMArm.ActiveInstance

    [Header("Spin Axis")]
    [Tooltip("Local-space spin axis of the valve. Blue disc in Scene view should align with the valve face.")]
    public Vector3 spinAxisLocal = Vector3.up;

    [Header("Grab Points")]
    [Tooltip("Child Transforms at each physical handle on the valve rim. On grab, the nearest one is selected and the virtual hand snaps to it each frame (arc-lock). Leave empty to fall back to free-hand tracking.")]
    [SerializeField] private List<Transform> grabPoints = new();

    [Header("Activation")]
    [Tooltip("Total degrees to spin before OnValveActivated fires.")]
    public float activationRotation = 360f;
    [Tooltip("If true, only the first-chosen spin direction counts toward activation.")]
    public bool  lockSpinDirection  = false;

    [Header("Events")]
    public UnityEvent OnValveActivated;

    // ── Runtime state ──────────────────────────────────────────────────────
    private enum ActiveTechnique { None, Homer, GoGo, Daom }
    private ActiveTechnique activeTechnique = ActiveTechnique.None;

    private bool isGrabbed   = false;
    private bool isActivated = false;

    private Rigidbody          valveRb;
    private XRGrabInteractable valveGrabbable;

    private Vector3    valveFixedPosition; // world-space position cached at Awake — always locked
    private Quaternion restWorldRotation;  // world-space rotation at Awake (defines angle = 0)
    private Vector3    spinWorldAxis;      // (restWorldRotation * spinAxisLocal).normalized

    private float   cumulativeRotation;    // total degrees spun — persists across grabs
    private Vector3 grabRefDirProjected;   // projected reference direction from previous frame
    private bool    isFirstGrabFrame;      // skip delta on first grabbed LateUpdate

    private int  lockedSpinSign;           // +1 or -1 when lockSpinDirection = true
    private bool spinDirectionLocked;

    private Transform activeGrabPoint;     // nearest grab point selected at StartGrab; null if none assigned

    private int _dbgFrame;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        valveRb        = GetComponent<Rigidbody>();
        valveGrabbable = GetComponent<XRGrabInteractable>();

        valveFixedPosition = transform.position;
        restWorldRotation  = transform.rotation;
        spinWorldAxis      = (restWorldRotation * spinAxisLocal.normalized).normalized;

        if (valveRb != null)
            valveRb.isKinematic = true;

        if (valveGrabbable != null)
        {
            valveGrabbable.trackPosition = false;
            valveGrabbable.trackRotation = false;
            valveGrabbable.throwOnDetach  = false;
            valveGrabbable.movementType  = XRBaseInteractable.MovementType.Instantaneous;

            // If grab points are assigned, give XRI an initial attach transform so the hand
            // snaps to the rim on grab. Updated per-grab to the nearest point in StartGrab.
            if (grabPoints.Count > 0 && grabPoints[0] != null)
                valveGrabbable.attachTransform = grabPoints[0];

            valveGrabbable.selectEntered.AddListener(OnSelectEntered);
            valveGrabbable.selectExited.AddListener(OnSelectExited);
        }

        Debug.Log($"[ValveGrab:{name}] Awake — fixedPos={valveFixedPosition:F3}  spinAxisLocal={spinAxisLocal}  spinAxisWorld={spinWorldAxis:F3}  grabPoints={grabPoints.Count}");
    }

    void OnEnable()
    {
        if (homer != null)
        {
            homer.GrabStarted += OnHomerGrabStarted;
            homer.GrabEnded   += OnHomerGrabEnded;
        }
    }

    void OnDisable()
    {
        if (homer != null)
        {
            homer.GrabStarted -= OnHomerGrabStarted;
            homer.GrabEnded   -= OnHomerGrabEnded;
        }
    }

    void OnDestroy()
    {
        if (valveGrabbable != null)
        {
            valveGrabbable.selectEntered.RemoveListener(OnSelectEntered);
            valveGrabbable.selectExited.RemoveListener(OnSelectExited);
        }
    }

    // ── LateUpdate (order 200) ─────────────────────────────────────────────

    void LateUpdate()
    {
        // ALWAYS lock valve position — unconditional, before any early returns.
        transform.position = valveFixedPosition;

        if (isActivated) return;

        if (!isGrabbed)
        {
            transform.rotation = Quaternion.AngleAxis(cumulativeRotation, spinWorldAxis) * restWorldRotation;
            return;
        }

        // 1. Read virtual hand position after technique LateUpdates (order 100) have run.
        Vector3 virtualHandPos = GetVirtualHandPosition();
        if (virtualHandPos == Vector3.zero)
        {
            Debug.LogWarning($"[ValveGrab:{name}] LateUpdate — virtual hand zero, skipping (technique={activeTechnique})");
            return;
        }

        // 2. Project pivot-to-hand onto the spin plane (normal = spinWorldAxis).
        Vector3 pivotToHand = virtualHandPos - transform.position;
        Vector3 projected   = Vector3.ProjectOnPlane(pivotToHand, spinWorldAxis);
        if (projected.sqrMagnitude < 1e-6f)
        {
            Debug.LogWarning($"[ValveGrab:{name}] LateUpdate — projected vector near-zero (hand on spin axis?), skipping");
            return;
        }

        // 3. First grabbed frame: capture reference direction, skip rotation update.
        if (isFirstGrabFrame)
        {
            isFirstGrabFrame    = false;
            spinDirectionLocked = false;

            if (activeGrabPoint != null)
            {
                OverrideVirtualHandPosition(activeGrabPoint.position);
                // HOMER respects the arc-lock next frame → reference = grab point direction.
                // GoGo/DAOM recompute from scratch next frame → reference = their actual current direction.
                grabRefDirProjected = (activeTechnique == ActiveTechnique.Homer)
                    ? GetActiveGrabPointDirProjected()
                    : projected.normalized;
            }
            else
            {
                grabRefDirProjected = projected.normalized; // fallback: no grab points
            }

            Debug.Log($"[ValveGrab:{name}] LateUpdate FRAME-1 — reference captured  technique={activeTechnique}  grabPoint={(activeGrabPoint != null ? activeGrabPoint.name : "none")}  ref={grabRefDirProjected:F3}  cumulative={cumulativeRotation:F1}°");
            return;
        }

        // 4. Compute angular delta since last frame.
        float angleDelta = Vector3.SignedAngle(grabRefDirProjected, projected.normalized, spinWorldAxis);

        // 5. Apply direction lock if requested.
        float contribution;
        if (lockSpinDirection)
        {
            if (!spinDirectionLocked && Mathf.Abs(angleDelta) > 0.5f)
            {
                lockedSpinSign      = (int)Mathf.Sign(angleDelta);
                spinDirectionLocked = true;
            }
            contribution = spinDirectionLocked ? Mathf.Max(0f, angleDelta * lockedSpinSign) : 0f;
        }
        else
        {
            contribution = Mathf.Abs(angleDelta);
        }

        cumulativeRotation += contribution;
        transform.rotation  = Quaternion.AngleAxis(cumulativeRotation, spinWorldAxis) * restWorldRotation;

        // 6. Arc-lock: snap virtual hand back to active grab point, then update reference.
        if (activeGrabPoint != null)
        {
            OverrideVirtualHandPosition(activeGrabPoint.position);
            grabRefDirProjected = (activeTechnique == ActiveTechnique.Homer)
                ? GetActiveGrabPointDirProjected()
                : projected.normalized;
        }
        else
        {
            grabRefDirProjected = projected.normalized; // fallback: no grab points
        }

        _dbgFrame++;
        if (_dbgFrame % 10 == 0)
            Debug.Log($"[ValveGrab:{name}] LateUpdate (frame {_dbgFrame}) — technique={activeTechnique}  delta={angleDelta:+0.0;-0.0}°  contribution={contribution:F2}°  cumulative={cumulativeRotation:F1}°  target={activationRotation:F1}°");

        // 7. Check activation.
        if (!isActivated && cumulativeRotation >= activationRotation)
            Activate();
    }

    // ── Activation ────────────────────────────────────────────────────────

    private void Activate()
    {
        Debug.Log($"[ValveGrab:{name}] Activate — cumulativeRotation={cumulativeRotation:F1}°  firing OnValveActivated");
        isActivated = true;
        ForceRelease();
        if (valveGrabbable != null)
            valveGrabbable.enabled = false;
        OnValveActivated.Invoke();
    }

    // ── HOMER event handlers ───────────────────────────────────────────────

    private void OnHomerGrabStarted(GameObject obj)
    {
        if (obj != gameObject)
        {
            Debug.Log($"[ValveGrab:{name}] OnHomerGrabStarted — ignored (grabbed '{obj?.name}', not this valve)");
            return;
        }
        Debug.Log($"[ValveGrab:{name}] OnHomerGrabStarted — matched, calling StartGrab(Homer)");
        StartGrab(ActiveTechnique.Homer);
    }

    private void OnHomerGrabEnded()
    {
        if (activeTechnique == ActiveTechnique.Homer && isGrabbed && !isActivated)
        {
            Debug.Log($"[ValveGrab:{name}] OnHomerGrabEnded — releasing at cumulative={cumulativeRotation:F1}°");
            EndGrab();
        }
        else
        {
            Debug.Log($"[ValveGrab:{name}] OnHomerGrabEnded — ignored (technique={activeTechnique} isGrabbed={isGrabbed} isActivated={isActivated})");
        }
    }

    // ── XRI select event handlers (GoGo + DAOM) ───────────────────────────

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (isActivated)
        {
            Debug.Log($"[ValveGrab:{name}] OnSelectEntered — ignored (already activated)");
            return;
        }

        ActiveTechnique technique = ActiveTechnique.None;
        string interName = args.interactorObject?.transform?.name ?? "null";

        if (goGoInteractor != null &&
            args.interactorObject as XRDirectInteractor == goGoInteractor)
        {
            technique = ActiveTechnique.GoGo;
            Debug.Log($"[ValveGrab:{name}] OnSelectEntered — matched GoGo interactor '{interName}'");
        }
        else if (DAOMArm.ActiveInstance != null &&
                 args.interactorObject as XRDirectInteractor == DAOMArm.ActiveInstance.Interactor)
        {
            technique = ActiveTechnique.Daom;
            Debug.Log($"[ValveGrab:{name}] OnSelectEntered — matched DAOM interactor '{interName}'");
        }
        else
        {
            Debug.Log($"[ValveGrab:{name}] OnSelectEntered — ignored (interactor='{interName}')");
        }

        if (technique != ActiveTechnique.None)
            StartGrab(technique);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        string interName = args.interactorObject?.transform?.name ?? "null";
        // Only handle GoGo/DAOM releases — HOMER uses its own GrabEnded event.
        if ((activeTechnique == ActiveTechnique.GoGo || activeTechnique == ActiveTechnique.Daom)
            && isGrabbed && !isActivated)
        {
            Debug.Log($"[ValveGrab:{name}] OnSelectExited — releasing {activeTechnique} (interactor '{interName}') at cumulative={cumulativeRotation:F1}°");
            EndGrab();
        }
        else
        {
            Debug.Log($"[ValveGrab:{name}] OnSelectExited — ignored (technique={activeTechnique} isGrabbed={isGrabbed} isActivated={isActivated} interactor='{interName}')");
        }
    }

    // ── Grab management ────────────────────────────────────────────────────

    private void StartGrab(ActiveTechnique technique)
    {
        if (isGrabbed)
        {
            Debug.LogWarning($"[ValveGrab:{name}] StartGrab({technique}) — IGNORED, already grabbed by {activeTechnique}");
            return;
        }

        activeTechnique  = technique;
        isGrabbed        = true;
        isFirstGrabFrame = true;
        _dbgFrame        = 0;

        transform.position = valveFixedPosition;

        // Select nearest grab point and update XRI attach transform so the hand snaps to the rim.
        activeGrabPoint = FindNearestGrabPoint();
        if (activeGrabPoint != null && valveGrabbable != null)
            valveGrabbable.attachTransform = activeGrabPoint;

        Debug.Log($"[ValveGrab:{name}] StartGrab — technique={technique}  grabPoint={activeGrabPoint?.name ?? "none"}  cumulativeRotation={cumulativeRotation:F1}°");
    }

    private void EndGrab()
    {
        if (!isGrabbed) return;

        Debug.Log($"[ValveGrab:{name}] EndGrab — technique={activeTechnique}  cumulativeRotation={cumulativeRotation:F1}°");

        isGrabbed       = false;
        activeTechnique = ActiveTechnique.None;
        activeGrabPoint = null;
    }

    private void ForceRelease()
    {
        if (!isGrabbed) return;

        ActiveTechnique technique = activeTechnique;
        Debug.Log($"[ValveGrab:{name}] ForceRelease — technique={technique}");

        isGrabbed       = false;  // guard against re-entry FIRST
        activeTechnique = ActiveTechnique.None;
        activeGrabPoint = null;

        switch (technique)
        {
            case ActiveTechnique.Homer:
                Debug.Log($"[ValveGrab:{name}] ForceRelease — calling homer.EndGrab()");
                if (homer != null) homer.EndGrab();
                break;

            case ActiveTechnique.GoGo:
                if (goGoInteractor != null && valveGrabbable != null)
                {
                    Debug.Log($"[ValveGrab:{name}] ForceRelease — calling SelectExit on GoGo interactor");
                    valveGrabbable.interactionManager.SelectExit((IXRSelectInteractor)goGoInteractor, (IXRSelectInteractable)valveGrabbable);
                }
                else
                {
                    Debug.LogWarning($"[ValveGrab:{name}] ForceRelease — GoGo release skipped (goGoInteractor={goGoInteractor != null} valveGrabbable={valveGrabbable != null})");
                }
                break;

            case ActiveTechnique.Daom:
                var daom = DAOMArm.ActiveInstance;
                if (daom?.Interactor != null && valveGrabbable != null)
                {
                    Debug.Log($"[ValveGrab:{name}] ForceRelease — calling SelectExit on DAOM interactor");
                    valveGrabbable.interactionManager.SelectExit((IXRSelectInteractor)daom.Interactor, (IXRSelectInteractable)valveGrabbable);
                }
                else
                {
                    Debug.LogWarning($"[ValveGrab:{name}] ForceRelease — DAOM release skipped (daom={daom != null} interactor={daom?.Interactor != null} valveGrabbable={valveGrabbable != null})");
                }
                break;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Returns the nearest grab point to the current virtual hand position.</summary>
    private Transform FindNearestGrabPoint()
    {
        if (grabPoints.Count == 0) return null;
        Vector3 handPos = GetVirtualHandPosition();
        if (handPos == Vector3.zero) return grabPoints[0];

        Transform nearest  = null;
        float     bestDist = float.MaxValue;
        foreach (var pt in grabPoints)
        {
            if (pt == null) continue;
            float d = Vector3.Distance(handPos, pt.position);
            if (d < bestDist) { bestDist = d; nearest = pt; }
        }
        return nearest;
    }

    /// <summary>Returns the projected direction from the valve pivot to the active grab point.</summary>
    private Vector3 GetActiveGrabPointDirProjected()
    {
        if (activeGrabPoint == null) return grabRefDirProjected;
        Vector3 d = Vector3.ProjectOnPlane(activeGrabPoint.position - transform.position, spinWorldAxis);
        return d.sqrMagnitude > 1e-6f ? d.normalized : grabRefDirProjected;
    }

    private Vector3 GetVirtualHandPosition()
    {
        switch (activeTechnique)
        {
            case ActiveTechnique.Homer:
                return homer != null && homer.VirtualHand != null ? homer.VirtualHand.position : Vector3.zero;
            case ActiveTechnique.GoGo:
                return goGoExtend != null && goGoExtend.VirtualHand != null ? goGoExtend.VirtualHand.position : Vector3.zero;
            case ActiveTechnique.Daom:
                var daomInst = DAOMArm.ActiveInstance;
                return daomInst != null && daomInst.DaomIKTarget != null
                    ? daomInst.DaomIKTarget.position
                    : Vector3.zero;
            default:
                return Vector3.zero;
        }
    }

    private void OverrideVirtualHandPosition(Vector3 position)
    {
        switch (activeTechnique)
        {
            case ActiveTechnique.Homer:
                if (homer?.VirtualHand != null)
                    homer.VirtualHand.position = position;
                break;
            case ActiveTechnique.GoGo:
                if (goGoExtend?.VirtualHand != null)
                    goGoExtend.VirtualHand.position = position;
                break;
            case ActiveTechnique.Daom:
                var daomArmInst = DAOMArm.ActiveInstance;
                if (daomArmInst != null && daomArmInst.DaomIKTarget != null)
                    daomArmInst.DaomIKTarget.position = position;
                break;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 pivotPos  = transform.position;
        Vector3 axisWorld = (transform.rotation * spinAxisLocal.normalized).normalized;
        float   radius    = 0.3f;

        // Cyan line: spin axis through pivot.
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pivotPos - axisWorld * radius, pivotPos + axisWorld * radius);

        // Blue disc (transparent): valve rotation plane (face of wheel).
        Handles.color = new Color(0f, 0.5f, 1f, 0.3f);
        Handles.DrawWireDisc(pivotPos, axisWorld, radius);

        // White spheres: grab points on the valve rim.
        Gizmos.color = Color.white;
        foreach (var pt in grabPoints)
            if (pt != null) Gizmos.DrawWireSphere(pt.position, 0.03f);

        // Yellow: required rotation zone.
        Vector3 perpendicular = Vector3.Cross(axisWorld, Vector3.up);
        if (perpendicular.sqrMagnitude < 1e-4f)
            perpendicular = Vector3.Cross(axisWorld, Vector3.right);
        perpendicular = perpendicular.normalized;

        Handles.color = new Color(1f, 0.9f, 0f, 0.9f);
        if (activationRotation >= 360f)
            Handles.DrawWireDisc(pivotPos, axisWorld, radius * 0.85f);
        else
            Handles.DrawWireArc(pivotPos, axisWorld, perpendicular, activationRotation, radius * 0.85f);
    }
#endif
}
