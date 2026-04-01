using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Lever puzzle component. Attach to the lever GameObject alongside an XRGrabInteractable
/// and a Rigidbody (no HingeJoint needed — LeverGrab owns the transform).
///
/// Supports grab via HOMER, Go-Go, and DAOM arm techniques. While grabbed, arc-locks
/// the virtual hand to the lever's rotation plane and rotates the lever around the
/// configured hinge axis. When the lever is pulled past activationAngle it snaps
/// to snapToAngle and fires OnLeverActivated.
///
/// Execution order 200 ensures this runs AFTER all technique LateUpdates (order 100),
/// so we safely read then override virtual hand positions each frame.
/// </summary>
[DefaultExecutionOrder(200)]
public class LeverGrab : MonoBehaviour, IRotaryGrabbable
{
    [Header("Technique References")]
    public HOMERRaycast homer;
    public GoGoExtend goGoExtend;
    [Tooltip("The XRDirectInteractor that sits on GoGo's virtual hand GameObject.")]
    public XRDirectInteractor goGoInteractor;
    // DAOM resolved at runtime via DAOMArm.ActiveInstance

    [Header("Lever Geometry")]
    [Tooltip("Empty child at the grip end of the lever arm. If null, uses the lever pivot.")]
    public Transform leverHandlePoint;

    [Header("Hinge Axis")]
    [Tooltip("Local-space axis of the cylindrical hinge shaft. Check the gizmo in Scene view to confirm the blue disc aligns with the lever's swing plane.")]
    public Vector3 hingeAxisLocal = Vector3.right;

    [Header("Angle Settings")]
    [Tooltip("Pull past this angle (degrees) to trigger the snap.")]
    public float activationAngle = 75f;
    [Tooltip("Angle the lever snaps to on activation.")]
    public float snapToAngle = 90f;
    [Tooltip("Duration of the snap animation in seconds.")]
    public float snapDuration = 0.15f;

    [Header("Events")]
    public UnityEvent OnLeverActivated;

    // ── Runtime state ──────────────────────────────────────────────────────
    private enum ActiveTechnique { None, Homer, GoGo, Daom }
    private ActiveTechnique activeTechnique = ActiveTechnique.None;

    private bool isGrabbed = false;
    private bool isActivated = false;

    private Rigidbody leverRb;
    private XRGrabInteractable leverGrabbable;

    private Vector3 leverFixedPosition; // world-space position locked at Awake (Fix 1)
    private Quaternion restWorldRotation;  // world-space rotation when grab started (Fix 3)
    private Vector3 hingeWorldAxis;     // world-space hinge axis (fixed per grab) (Fix 3)
    private Vector3 restDir;            // world-space rest direction of lever arm (set on grab start)
    private float currentAngle;       // current lever angle this frame

    private Vector3 grabRefDirProjected; // reference direction for next frame's incremental delta
    private bool isFirstGrabFrame;    // true on the first grabbed LateUpdate; skips angle update

    private int _dbgFrame; // throttle per-frame logs

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        leverRb = GetComponent<Rigidbody>();
        leverGrabbable = GetComponent<XRGrabInteractable>();

        // Fix 1: cache pivot position so HOMER's BeginGrab teleport can be repaired each frame.
        leverFixedPosition = transform.position;

        if (leverHandlePoint == null)
            leverHandlePoint = transform;

        // Make kinematic immediately so gravity cannot tumble the lever before the first grab.
        if (leverRb != null)
            leverRb.isKinematic = true;

        // Cache rest state here (before physics runs) so angle-0 always matches the designed pose.
        restWorldRotation = transform.rotation;
        hingeWorldAxis = (restWorldRotation * hingeAxisLocal.normalized).normalized;
        Vector3 toHandleAwake = leverHandlePoint.position - transform.position;
        restDir = toHandleAwake.sqrMagnitude > 1e-6f
            ? toHandleAwake.normalized
            : transform.TransformDirection(Vector3.down);

        if (leverGrabbable != null)
        {
            // Disable XRI position/rotation tracking — we own the lever transform entirely.
            leverGrabbable.trackPosition = false;
            leverGrabbable.trackRotation = false;
            leverGrabbable.throwOnDetach = false;
            leverGrabbable.movementType = XRBaseInteractable.MovementType.Instantaneous;

            // Fix 2: snap interactor to handle point, not object origin.
            leverGrabbable.attachTransform = leverHandlePoint;

            leverGrabbable.selectEntered.AddListener(OnSelectEntered);
            leverGrabbable.selectExited.AddListener(OnSelectExited);
        }

        Debug.Log($"[LeverGrab:{name}] Awake — fixedPos={leverFixedPosition:F3}  hingeAxisLocal={hingeAxisLocal}  hingeAxisWorld={hingeWorldAxis:F3}  restDir={restDir:F3}  handle={(leverHandlePoint == transform ? "pivot (none assigned)" : leverHandlePoint.name)}");
    }

    void OnEnable()
    {
        if (homer != null)
        {
            homer.GrabStarted += OnHomerGrabStarted;
            homer.GrabEnded += OnHomerGrabEnded;
        }
    }

    void OnDisable()
    {
        if (homer != null)
        {
            homer.GrabStarted -= OnHomerGrabStarted;
            homer.GrabEnded -= OnHomerGrabEnded;
        }
    }

    void OnDestroy()
    {
        if (leverGrabbable != null)
        {
            leverGrabbable.selectEntered.RemoveListener(OnSelectEntered);
            leverGrabbable.selectExited.RemoveListener(OnSelectExited);
        }
    }

    // ── LateUpdate (order 200) ─────────────────────────────────────────────

    void LateUpdate()
    {
        // ALWAYS lock lever position — nothing external is allowed to move it.
        transform.position = leverFixedPosition;

        if (isActivated) return; // snap coroutine owns rotation

        if (!isGrabbed)
        {
            // Hold the lever at whatever angle it was released at.
            transform.rotation = Quaternion.AngleAxis(currentAngle, hingeWorldAxis) * restWorldRotation;
            return;
        }

        // 1. Read virtual hand position AFTER the technique has updated it this frame.
        Vector3 virtualHandPos = GetVirtualHandPosition();
        if (virtualHandPos == Vector3.zero)
        {
            Debug.LogWarning($"[LeverGrab:{name}] LateUpdate — virtual hand returned zero, skipping (technique={activeTechnique})");
            return;
        }

        // 2. Arc-lock math — project onto the rotation plane (normal = hinge axis).
        Vector3 pivotToHand = virtualHandPos - transform.position;
        Vector3 projected = Vector3.ProjectOnPlane(pivotToHand, hingeWorldAxis);
        if (projected.sqrMagnitude < 1e-6f)
        {
            Debug.LogWarning($"[LeverGrab:{name}] LateUpdate — projected vector near-zero (hand directly on hinge axis?), skipping");
            return;
        }

        // First grabbed frame: all technique LateUpdates (order 100) have now run.
        // Capture the reference direction here — this is the only moment where the virtual hand
        // is in its actual first-frame position (before any arc-lock correction).
        if (isFirstGrabFrame)
        {
            isFirstGrabFrame = false;
            OverrideVirtualHandPosition(leverHandlePoint.position);
            // HOMER respects arc-lock (starts from handle next frame) → reference = handle direction.
            // GoGo/DAOM recompute from scratch next frame → reference = their actual current direction.
            grabRefDirProjected = (activeTechnique == ActiveTechnique.Homer)
                ? GetHandleDirProjected()
                : projected.normalized;
            Debug.Log($"[LeverGrab:{name}] LateUpdate FRAME-1 — reference captured  technique={activeTechnique}  handPos={virtualHandPos:F3}  projectedDir={projected.normalized:F3}  grabRef={grabRefDirProjected:F3}  currentAngle={currentAngle:F1}°");
            return; // Lever holds currentAngle this frame; movement tracking starts next frame.
        }

        // Incremental delta: how much did the projected direction rotate since last frame?
        float angleDelta = Vector3.SignedAngle(grabRefDirProjected, projected.normalized, hingeWorldAxis);
        float lo = Mathf.Min(0f, snapToAngle);
        float hi = Mathf.Max(0f, snapToAngle);
        float angle = Mathf.Clamp(currentAngle + angleDelta, lo, hi);
        currentAngle = angle;

        transform.rotation = Quaternion.AngleAxis(angle, hingeWorldAxis) * restWorldRotation;

        OverrideVirtualHandPosition(leverHandlePoint.position);

        // Update reference for next frame (technique-aware).
        // HOMER respects arc-lock → reference = handle direction (where HOMER will start from).
        // GoGo/DAOM ignore arc-lock → reference = their actual direction (pre-arc-lock projected).
        grabRefDirProjected = (activeTechnique == ActiveTechnique.Homer)
            ? GetHandleDirProjected()
            : projected.normalized;

        // Log every 10 frames to avoid spam.
        _dbgFrame++;
        if (_dbgFrame % 10 == 0)
            Debug.Log($"[LeverGrab:{name}] LateUpdate (frame {_dbgFrame}) — technique={activeTechnique}  handPos={virtualHandPos:F3}  projDir={projected.normalized:F3}  delta={angleDelta:+0.0;-0.0}°  angle={angle:F1}°  activationAngle={activationAngle:F1}°");

        bool triggered = snapToAngle >= 0f ? (angle >= activationAngle) : (angle <= activationAngle);
        if (triggered && !isActivated)
            StartCoroutine(SnapAndActivate());
    }

    // ── HOMER event handlers ───────────────────────────────────────────────

    private void OnHomerGrabStarted(GameObject obj)
    {
        if (obj != gameObject)
        {
            Debug.Log($"[LeverGrab:{name}] OnHomerGrabStarted — ignored (grabbed '{obj?.name}', not this lever)");
            return;
        }
        Debug.Log($"[LeverGrab:{name}] OnHomerGrabStarted — matched, calling StartGrab(Homer)");
        StartGrab(ActiveTechnique.Homer);
    }

    private void OnHomerGrabEnded()
    {
        if (activeTechnique == ActiveTechnique.Homer && isGrabbed && !isActivated)
        {
            Debug.Log($"[LeverGrab:{name}] OnHomerGrabEnded — releasing at angle={currentAngle:F1}°");
            EndGrab();
        }
        else
        {
            Debug.Log($"[LeverGrab:{name}] OnHomerGrabEnded — ignored (technique={activeTechnique} isGrabbed={isGrabbed} isActivated={isActivated})");
        }
    }

    // ── XRI select event handlers (GoGo + DAOM) ───────────────────────────

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (isActivated)
        {
            Debug.Log($"[LeverGrab:{name}] OnSelectEntered — ignored (already activated)");
            return;
        }

        ActiveTechnique technique = ActiveTechnique.None;
        string interactorName = args.interactorObject?.transform?.name ?? "null";

        if (goGoInteractor != null &&
            args.interactorObject as XRDirectInteractor == goGoInteractor)
        {
            technique = ActiveTechnique.GoGo;
            Debug.Log($"[LeverGrab:{name}] OnSelectEntered — matched GoGo interactor '{interactorName}'");
        }
        else if (DAOMArm.ActiveInstance != null &&
                 args.interactorObject as XRDirectInteractor == DAOMArm.ActiveInstance.Interactor)
        {
            technique = ActiveTechnique.Daom;
            Debug.Log($"[LeverGrab:{name}] OnSelectEntered — matched DAOM interactor '{interactorName}'");
        }
        else
        {
            Debug.Log($"[LeverGrab:{name}] OnSelectEntered — ignored (interactor='{interactorName}')");
        }

        if (technique != ActiveTechnique.None)
            StartGrab(technique);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        string interactorName = args.interactorObject?.transform?.name ?? "null";
        // Only handle GoGo/DAOM physical releases — HOMER uses its own GrabEnded event.
        if ((activeTechnique == ActiveTechnique.GoGo || activeTechnique == ActiveTechnique.Daom)
            && isGrabbed && !isActivated)
        {
            Debug.Log($"[LeverGrab:{name}] OnSelectExited — releasing {activeTechnique} (interactor '{interactorName}') at angle={currentAngle:F1}°");
            EndGrab();
        }
        else
        {
            Debug.Log($"[LeverGrab:{name}] OnSelectExited — ignored (technique={activeTechnique} isGrabbed={isGrabbed} isActivated={isActivated} interactor='{interactorName}')");
        }
    }

    // ── Grab management ────────────────────────────────────────────────────

    private void StartGrab(ActiveTechnique technique)
    {
        if (isGrabbed)
        {
            Debug.LogWarning($"[LeverGrab:{name}] StartGrab({technique}) — IGNORED, already grabbed by {activeTechnique}");
            return;
        }

        activeTechnique = technique;
        isGrabbed = true;
        isFirstGrabFrame = true; // reference captured in first grabbed LateUpdate (after technique scripts)
        _dbgFrame = 0;

        transform.position = leverFixedPosition;

        Debug.Log($"[LeverGrab:{name}] StartGrab — technique={technique}  currentAngle={currentAngle:F1}°  leverPos={leverFixedPosition:F3}");
    }

    private void EndGrab()
    {
        if (!isGrabbed) return;

        Debug.Log($"[LeverGrab:{name}] EndGrab — technique={activeTechnique}  heldAngle={currentAngle:F1}°");

        isGrabbed = false;
        activeTechnique = ActiveTechnique.None;

        // Leave kinematic — no HingeJoint means free physics would tumble the lever under gravity.
    }

    /// <summary>
    /// Programmatically releases the active technique's virtual hand from the lever.
    /// Sets isGrabbed = false BEFORE calling release so event callbacks don't re-enter.
    /// </summary>
    private void ForceRelease()
    {
        if (!isGrabbed) return;

        ActiveTechnique technique = activeTechnique;
        Debug.Log($"[LeverGrab:{name}] ForceRelease — technique={technique}  angle={currentAngle:F1}°");

        isGrabbed = false;  // guard against re-entry FIRST
        activeTechnique = ActiveTechnique.None;

        switch (technique)
        {
            case ActiveTechnique.Homer:
                Debug.Log($"[LeverGrab:{name}] ForceRelease — calling homer.EndGrab()");
                homer?.EndGrab();
                break;

            case ActiveTechnique.GoGo:
                if (goGoInteractor != null && leverGrabbable != null)
                {
                    Debug.Log($"[LeverGrab:{name}] ForceRelease — calling SelectExit on GoGo interactor");
                    leverGrabbable.interactionManager.SelectExit((IXRSelectInteractor)goGoInteractor, (IXRSelectInteractable)leverGrabbable);
                }
                else
                {
                    Debug.LogWarning($"[LeverGrab:{name}] ForceRelease — GoGo release skipped (goGoInteractor={goGoInteractor != null} leverGrabbable={leverGrabbable != null})");
                }
                break;

            case ActiveTechnique.Daom:
                var daom = DAOMArm.ActiveInstance;
                if (daom?.Interactor != null && leverGrabbable != null)
                {
                    Debug.Log($"[LeverGrab:{name}] ForceRelease — calling SelectExit on DAOM interactor");
                    leverGrabbable.interactionManager.SelectExit((IXRSelectInteractor)daom.Interactor, (IXRSelectInteractable)leverGrabbable);
                }
                else
                {
                    Debug.LogWarning($"[LeverGrab:{name}] ForceRelease — DAOM release skipped (daom={daom != null} interactor={daom?.Interactor != null} leverGrabbable={leverGrabbable != null})");
                }
                break;
        }
    }

    // ── Snap coroutine ─────────────────────────────────────────────────────

    private IEnumerator SnapAndActivate()
    {
        Debug.Log($"[LeverGrab:{name}] SnapAndActivate — START  currentAngle={currentAngle:F1}°  snapToAngle={snapToAngle:F1}°");

        isActivated = true;
        ForceRelease();

        float startAngle = currentAngle;
        float elapsed = 0f;

        while (elapsed < snapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / snapDuration));
            float angle = Mathf.Lerp(startAngle, snapToAngle, t);
            transform.rotation = Quaternion.AngleAxis(angle, hingeWorldAxis) * restWorldRotation;
            yield return null;
        }

        transform.rotation = Quaternion.AngleAxis(snapToAngle, hingeWorldAxis) * restWorldRotation;

        if (leverGrabbable != null)
            leverGrabbable.enabled = false;

        Debug.Log($"[LeverGrab:{name}] SnapAndActivate — COMPLETE  finalAngle={snapToAngle:F1}°  firing OnLeverActivated");
        OnLeverActivated.Invoke();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Returns the projected direction from the lever pivot to the handle point.</summary>
    private Vector3 GetHandleDirProjected()
    {
        Vector3 d = Vector3.ProjectOnPlane(leverHandlePoint.position - transform.position, hingeWorldAxis);
        return d.sqrMagnitude > 1e-6f ? d.normalized : grabRefDirProjected;
    }

    private Vector3 GetVirtualHandPosition()
    {
        switch (activeTechnique)
        {
            case ActiveTechnique.Homer:
                return homer?.VirtualHand != null ? homer.VirtualHand.position : Vector3.zero;
            case ActiveTechnique.GoGo:
                return goGoExtend?.VirtualHand != null ? goGoExtend.VirtualHand.position : Vector3.zero;
            case ActiveTechnique.Daom:
                return DAOMArm.ActiveInstance?.DaomIKTarget != null
                    ? DAOMArm.ActiveInstance.DaomIKTarget.position
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
                var daomTarget = DAOMArm.ActiveInstance?.DaomIKTarget;
                if (daomTarget != null)
                    daomTarget.position = position;
                break;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Transform pivot = transform;
        Transform handle = leverHandlePoint != null ? leverHandlePoint : transform;

        Vector3 pivotPos = pivot.position;
        Vector3 handlePos = handle.position;
        float armLength = Vector3.Distance(pivotPos, handlePos);
        if (armLength < 0.001f) armLength = 0.3f;

        // World-space hinge axis for gizmos (use current rotation, not grab-cached one).
        Vector3 axisWorld = (pivot.rotation * hingeAxisLocal.normalized).normalized;

        // Cyan axis line through pivot.
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pivotPos - axisWorld * armLength * 0.5f,
                        pivotPos + axisWorld * armLength * 0.5f);

        // White line: pivot → handle (rest arm).
        Gizmos.color = Color.white;
        Gizmos.DrawLine(pivotPos, handlePos);
        Gizmos.DrawWireSphere(handlePos, 0.03f);

        // Rest direction projected onto hinge plane.
        Vector3 toHandle = handlePos - pivotPos;
        Vector3 restDirLocal = toHandle.sqrMagnitude > 1e-6f
            ? Vector3.ProjectOnPlane(toHandle.normalized, axisWorld)
            : Vector3.ProjectOnPlane(pivot.TransformDirection(Vector3.down), axisWorld);
        if (restDirLocal.sqrMagnitude < 1e-6f) return;
        restDirLocal = restDirLocal.normalized;

        // Blue disc: rotation plane.
        Handles.color = new Color(0f, 0.5f, 1f, 0.3f);
        Handles.DrawWireDisc(pivotPos, axisWorld, armLength);

        // Yellow arc: 0° → activationAngle (pull zone).
        Handles.color = new Color(1f, 0.9f, 0f, 0.9f);
        Handles.DrawWireArc(pivotPos, axisWorld, restDirLocal, activationAngle, armLength);

        // Red arc: activationAngle → snapToAngle (snap zone).
        Vector3 activationDir = Quaternion.AngleAxis(activationAngle, axisWorld) * restDirLocal;
        Handles.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Handles.DrawWireArc(pivotPos, axisWorld, activationDir, snapToAngle - activationAngle, armLength);
    }
#endif
}
