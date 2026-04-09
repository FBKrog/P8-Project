using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Place on Physical button for the Simon Says puzzle. 
/// Attach an XRGrabInteractable
/// Rigidbody (kinematic).
///
/// Supports grab via HOMER, Go-Go, and DAOM arm techniques. 
/// 
/// While grabbed, arc-locks the virtual hand to the nearest grab point and slides the button along the configuredn press axis. When the button travels pressDistance metres it fires OnButtonPressed, force-releases the player's hand, and awaits LockPressed() or UnlockAndSnapBack() from SimonSaysPuzzle.
/// </summary>
[DefaultExecutionOrder(200)]
public class SimonButton : MonoBehaviour, IRotaryGrabbable
{
    [Header("Technique References")]
    public HOMERRaycast homer;
    public GoGoExtend goGoExtend;
    [Tooltip("The XRDirectInteractor that sits on GoGo's virtual hand GameObject.")]
    public XRDirectInteractor goGoInteractor;
    // DAOM resolved at runtime via DAOMArm.ActiveInstance

    [Header("Grab Points")]
    [Tooltip("Child Transforms on the button surface. On grab, the nearest one is selected and the virtual hand snaps to it each frame (arc-lock).")]
    [SerializeField] private List<Transform> grabPoints = new();

    [Header("Press Settings")]
    [Tooltip("Local-space direction the button travels when pressed (e.g. Vector3.down).")]
    [SerializeField] private Vector3 pressDirectionLocal = Vector3.down;
    [Tooltip("Travel distance in metres to trigger activation.")]
    [SerializeField] private float pressDistance = 0.025f;
    [Tooltip("Duration of snap-back animation in seconds.")]
    [SerializeField] private float snapBackDuration = 0.2f;

    [Header("Visual Feedback")]
    [Tooltip("Renderer whose _EmissionColor is driven by SetLightState (URP Lit with Emission enabled).")]
    [SerializeField] private Renderer buttonRenderer;
    [Tooltip("Optional point light inside the button cap.")]
    [SerializeField] private Light buttonLight;

    [Header("Events")]
    public UnityEvent OnButtonPressed;

    // ── Runtime state ──────────────────────────────────────────────────────────
    private enum ActiveTechnique { None, Homer, GoGo, Daom }
    private ActiveTechnique activeTechnique = ActiveTechnique.None;

    private bool isGrabbed = false;
    private bool isPressed = false; // true once OnButtonPressed has fired
    private bool isLocked = false; // true when puzzle manager locks the pressed state
    private bool isFirstGrabFrame = false;

    private Rigidbody buttonRb;
    private XRGrabInteractable buttonGrabbable;

    private Vector3 _restLocalPos;            // local-space rest position (relative to parent at Awake)
    private Vector3 pressWorldDir;            // normalised world-space press direction (fixed at Awake)
    private Vector3 _grabPointRestWorldPos;   // grab point world pos at currentTravel=0, cached on grab
    private Vector3 buttonFixedWorldPos;      // cached world rest position; updated each frame when not grabbed
    private float currentTravel;       // metres depressed [0, pressDistance]

    private Transform activeGrabPoint;     // nearest grab point selected at StartGrab

    private MaterialPropertyBlock _mpb;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorID     = Shader.PropertyToID("_BaseColor");
    private Color _originalBaseColor;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Awake()
    {
        buttonRb = GetComponent<Rigidbody>();
        buttonGrabbable = GetComponent<XRGrabInteractable>();

        // Cache base colour before any property-block overrides are applied.
        if (buttonRenderer != null)
            _originalBaseColor = buttonRenderer.sharedMaterial.GetColor(BaseColorID);

        // Cache rest state before anything can move the button.
        _restLocalPos        = transform.localPosition;
        buttonFixedWorldPos  = transform.position;
        pressWorldDir        = (transform.rotation * pressDirectionLocal.normalized).normalized;

        if (buttonRb != null)
            buttonRb.isKinematic = true;

        if (buttonGrabbable != null)
        {
            buttonGrabbable.trackPosition = false;
            buttonGrabbable.trackRotation = false;
            buttonGrabbable.throwOnDetach = false;
            buttonGrabbable.movementType = XRBaseInteractable.MovementType.Instantaneous;

            if (grabPoints.Count > 0 && grabPoints[0] != null)
                buttonGrabbable.attachTransform = grabPoints[0];

            buttonGrabbable.selectEntered.AddListener(OnSelectEntered);
            buttonGrabbable.selectExited.AddListener(OnSelectExited);
        }

        _mpb = new MaterialPropertyBlock();
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
        if (buttonGrabbable != null)
        {
            buttonGrabbable.selectEntered.RemoveListener(OnSelectEntered);
            buttonGrabbable.selectExited.RemoveListener(OnSelectExited);
        }
    }

    // ── LateUpdate (order 200) ─────────────────────────────────────────────────

    void LateUpdate()
    {
        // Track parent movement (e.g. a sliding door) each frame, but only while not
        // grabbed — XRI reparents on grab, which would corrupt the value.
        if (!isGrabbed)
            buttonFixedWorldPos = transform.parent != null
                ? transform.parent.TransformPoint(_restLocalPos)
                : transform.position;

        // Unconditional position lock — always enforces current travel depth.
        transform.position = buttonFixedWorldPos + pressWorldDir * currentTravel;

        if (isLocked) return; // puzzle manager owns this button
        if (!isGrabbed) return;

        // First grabbed frame: snap button to rest, cache grab point rest position,
        // override virtual hand to match — absolute tracking starts next frame.
        if (isFirstGrabFrame)
        {
            isFirstGrabFrame = false;
            currentTravel = 0f;
            transform.position = buttonFixedWorldPos; // ensure grab point is at rest world pos
            if (activeGrabPoint != null)
            {
                _grabPointRestWorldPos = activeGrabPoint.position;
                OverrideVirtualHandPosition(activeGrabPoint.position);
            }
            return;
        }

        Vector3 virtualHandPos = GetVirtualHandPosition();
        if (virtualHandPos == Vector3.zero) return;

        // Absolute tracking: travel = how far the virtual hand is from the grab point's
        // rest position along the press axis. No accumulation avoids frame-1 jumps caused
        // by GoGo/DAOM recomputing hand position from physical hand each frame.
        float projectedTravel = Vector3.Dot(virtualHandPos - _grabPointRestWorldPos, pressWorldDir);
        currentTravel = Mathf.Clamp(projectedTravel, 0f, pressDistance);

        // Apply updated position.
        transform.position = buttonFixedWorldPos + pressWorldDir * currentTravel;

        // Arc-lock: snap virtual hand back to the grab point at its new world position.
        OverrideVirtualHandPosition(activeGrabPoint.position);

        if (!isPressed && currentTravel >= pressDistance)
            ActivatePress();
    }

    // ── Public API (called by SimonSaysPuzzle) ─────────────────────────────────

    /// <summary>Sets emission color on buttonRenderer and optional buttonLight (used for sequence display flash).</summary>
    public void SetLightState(Color color, bool on)
    {
        if (buttonRenderer != null)
        {
            buttonRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorID, on ? color : Color.black);
            buttonRenderer.SetPropertyBlock(_mpb);
        }

        if (buttonLight != null)
        {
            buttonLight.color = color;
            buttonLight.enabled = on;
        }
    }

    /// <summary>
    /// Blends the button's base material colour toward <paramref name="color"/> by
    /// <paramref name="alpha"/>, creating a low-opacity overlay on top of the base material.
    /// Call with <paramref name="on"/>=false to restore the original base colour.
    /// </summary>
    public void SetOverlay(Color color, float alpha, bool on)
    {
        if (buttonRenderer != null)
        {
            buttonRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorID, on ? Color.Lerp(_originalBaseColor, color, alpha) : _originalBaseColor);
            buttonRenderer.SetPropertyBlock(_mpb);
        }

        if (buttonLight != null)
        {
            buttonLight.color  = color;
            buttonLight.enabled = on;
        }
    }

    /// <summary>Enables or disables the XRGrabInteractable so the puzzle manager can gate interaction.</summary>
    public void SetInteractable(bool interactable)
    {
        if (buttonGrabbable != null)
            buttonGrabbable.enabled = interactable;
    }

    /// <summary>
    /// Called by SimonSaysPuzzle on a correct press. Keeps the button depressed permanently
    /// (until UnlockAndSnapBack is called) and releases the player's hand.
    /// </summary>
    public void LockPressed()
    {
        isLocked = true;
        ForceRelease();
        SetInteractable(false);
    }

    /// <summary>
    /// Called by SimonSaysPuzzle on a wrong-order press or puzzle reset.
    /// Clears the locked/pressed state and animates the button back to rest.
    /// </summary>
    public void UnlockAndSnapBack()
    {
        if (!isPressed && !isLocked) return;

        isLocked = false;
        isPressed = false;
        SetInteractable(false); // SimonSaysPuzzle re-enables via SetInteractable(true) after reset
        StartCoroutine(SnapBackCoroutine());
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void ActivatePress()
    {
        if (isPressed) return; // idempotent
        isPressed = true;
        ForceRelease();
        OnButtonPressed.Invoke();
    }

    private IEnumerator SnapBackCoroutine()
    {
        if (snapBackDuration <= 0f)
        {
            currentTravel = 0f;
            yield break;
        }

        float startTravel = currentTravel;
        float elapsed = 0f;

        while (elapsed < snapBackDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / snapBackDuration));
            currentTravel = Mathf.Lerp(startTravel, 0f, t);
            yield return null;
        }

        currentTravel = 0f;
    }

    // ── HOMER event handlers ───────────────────────────────────────────────────

    private void OnHomerGrabStarted(GameObject obj)
    {
        if (obj != gameObject) return;
        StartGrab(ActiveTechnique.Homer);
    }

    private void OnHomerGrabEnded()
    {
        if (activeTechnique == ActiveTechnique.Homer && isGrabbed && !isPressed)
            EndGrab();
    }

    // ── XRI select event handlers (GoGo + DAOM) ───────────────────────────────

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (isPressed || isLocked) return;

        ActiveTechnique technique = ActiveTechnique.None;

        if (goGoInteractor != null &&
            args.interactorObject as XRDirectInteractor == goGoInteractor)
        {
            technique = ActiveTechnique.GoGo;
        }
        else if (DAOMArm.ActiveInstance != null &&
                 args.interactorObject as XRDirectInteractor == DAOMArm.ActiveInstance.Interactor)
        {
            technique = ActiveTechnique.Daom;
        }

        if (technique != ActiveTechnique.None)
            StartGrab(technique);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if ((activeTechnique == ActiveTechnique.GoGo || activeTechnique == ActiveTechnique.Daom)
            && isGrabbed && !isPressed)
        {
            EndGrab();
        }
    }

    // ── Grab management ────────────────────────────────────────────────────────

    private void StartGrab(ActiveTechnique technique)
    {
        if (isGrabbed) return;

        activeTechnique = technique;
        isGrabbed = true;
        isFirstGrabFrame = true;

        transform.position = buttonFixedWorldPos + pressWorldDir * currentTravel;

        activeGrabPoint = FindNearestGrabPoint();
        if (activeGrabPoint != null && buttonGrabbable != null)
            buttonGrabbable.attachTransform = activeGrabPoint;
    }

    private void EndGrab()
    {
        if (!isGrabbed) return;

        isGrabbed = false;
        activeTechnique = ActiveTechnique.None;
        activeGrabPoint = null;
    }

    private void ForceRelease()
    {
        if (!isGrabbed) return;

        ActiveTechnique technique = activeTechnique;

        isGrabbed = false; // guard against re-entry FIRST
        activeTechnique = ActiveTechnique.None;
        activeGrabPoint = null;

        switch (technique)
        {
            case ActiveTechnique.Homer:
                homer?.EndGrab();
                break;

            case ActiveTechnique.GoGo:
                if (goGoInteractor != null && buttonGrabbable != null)
                    buttonGrabbable.interactionManager.SelectExit(
                        (IXRSelectInteractor)goGoInteractor,
                        (IXRSelectInteractable)buttonGrabbable);
                break;

            case ActiveTechnique.Daom:
                var daom = DAOMArm.ActiveInstance;
                if (daom?.Interactor != null && buttonGrabbable != null)
                    buttonGrabbable.interactionManager.SelectExit(
                        (IXRSelectInteractor)daom.Interactor,
                        (IXRSelectInteractable)buttonGrabbable);
                break;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private Transform FindNearestGrabPoint()
    {
        if (grabPoints.Count == 0) return null;
        Vector3 handPos = GetVirtualHandPosition();
        if (handPos == Vector3.zero) return grabPoints[0];

        Transform nearest = null;
        float bestDist = float.MaxValue;
        foreach (var pt in grabPoints)
        {
            if (pt == null) continue;
            float d = Vector3.Distance(handPos, pt.position);
            if (d < bestDist) { bestDist = d; nearest = pt; }
        }
        return nearest;
    }

    private Vector3 GetVirtualHandPosition()
    {
        switch (activeTechnique)
        {
            case ActiveTechnique.Homer:
                return homer != null && homer.VirtualHand != null
                    ? homer.VirtualHand.position : Vector3.zero;
            case ActiveTechnique.GoGo:
                return goGoExtend != null && goGoExtend.VirtualHand != null
                    ? goGoExtend.VirtualHand.position : Vector3.zero;
            case ActiveTechnique.Daom:
                var daomInst = DAOMArm.ActiveInstance;
                return daomInst != null && daomInst.DaomIKTarget != null
                    ? daomInst.DaomIKTarget.position : Vector3.zero;
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
                var daomInst = DAOMArm.ActiveInstance;
                if (daomInst?.DaomIKTarget != null)
                    daomInst.DaomIKTarget.position = position;
                break;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? buttonFixedWorldPos : transform.position;
        Vector3 dir = Application.isPlaying
            ? pressWorldDir
            : (transform.rotation * pressDirectionLocal.normalized).normalized;

        // Press travel line: white = travel range.
        Gizmos.color = Color.white;
        Gizmos.DrawLine(origin, origin + dir * pressDistance);
        Gizmos.DrawWireSphere(origin + dir * pressDistance, 0.005f);

        // Activation point: green sphere at full press.
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origin + dir * pressDistance, 0.008f);

        // Origin: yellow sphere.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, 0.008f);
    }
#endif
}
