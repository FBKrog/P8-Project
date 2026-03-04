using UnityEngine;

/// <summary>
/// HOMER technique — manipulation phase.
/// Drives the virtual hand (and grabbed object) using velocity-scaled,
/// torso-based delta movement whenever the hand is extended.
///
/// ── Extension phase (hand free, no object) ───────────────────────────────
///   scaleFactor computed once on ExtendStarted:
///     scaleFactor = |virtualHand.position − torsoPosition| / |handPosition − torsoPosition|
///
///   Each frame:
///     handDelta   = handPosition − prevHandPosition
///     speedScale  = Lerp(minSpeedScale, 1.0, InverseLerp(minVelocity, maxVelocity, velocity))
///     virtualHand.position += handDelta × scaleFactor × speedScale
///
/// ── Grab phase (object held) ─────────────────────────────────────────────
///   rotationOffset computed once on GrabStarted:
///     rotationOffset = objectRotation × Inverse(handRotation)
///
///   Each frame (in addition to virtual hand movement above):
///     objectPosition += scaledDelta     ← same delta as virtual hand, NOT forced to hand pos
///     objectRotation  = handRotation × rotationOffset
///
///   Using delta (not absolute) lets snap zones override position each frame without fighting.
/// </summary>
public class HOMERManipulator : MonoBehaviour
{
    [Header("References")]
    public HOMERRaycast homer;

    [Header("Torso Estimation")]
    [Tooltip("Vertical offset below the HMD used to estimate torso position (metres).")]
    public float torsoHeadOffset = 0.15f;

    [Header("Velocity Scaling")]
    [Tooltip("Controller speed (m/s) at which speedScale is at its minimum.")]
    public float minVelocity = 0.05f;
    [Tooltip("Controller speed (m/s) at which speedScale reaches 1.0 (full amplification).")]
    public float maxVelocity = 1.5f;
    [Tooltip("Movement scale applied at low/zero velocity — keeps fine control responsive.")]
    [Range(0f, 1f)]
    public float minSpeedScale = 0.1f;

    [Header("Edge Cases")]
    [Tooltip("Minimum hand distance from torso — prevents division by zero (metres).")]
    public float minHandDistance = 0.05f;

    // ── Stored constants ──────────────────────────────────────────────────
    private float      scaleFactor;           // set on ExtendStarted, reused through grab
    private Quaternion rotationOffset;        // set on GrabStarted (object relative to hand)
    private Quaternion handViewRotOffset;     // hand model's natural rotation offset from controller
    private Vector3    prevHandPos;           // tracked every frame while extended

    // ── Unity lifecycle ───────────────────────────────────────────────────
    void OnEnable()
    {
        if (homer != null)
        {
            homer.ExtendStarted += OnExtendBegin;
            homer.GrabStarted   += OnGrabBegin;
            homer.GrabEnded     += OnGrabEnd;
        }
    }

    void OnDisable()
    {
        if (homer != null)
        {
            homer.ExtendStarted -= OnExtendBegin;
            homer.GrabStarted   -= OnGrabBegin;
            homer.GrabEnded     -= OnGrabEnd;
        }
    }

    void LateUpdate()
    {
        if (homer == null || !homer.IsHandExtended) return;

        // Compute the scaled delta once — shared by virtual hand and grabbed object.
        Vector3 handPos     = homer.PhysicalHand.position;
        Vector3 handDelta   = handPos - prevHandPos;
        prevHandPos = handPos;

        float velocity    = handDelta.magnitude / Time.deltaTime;
        float t           = Mathf.Clamp01(Mathf.InverseLerp(minVelocity, maxVelocity, velocity));
        float speedScale  = Mathf.Lerp(minSpeedScale, 1f, t);
        Vector3 scaledDelta = handDelta * scaleFactor * speedScale;

        // Move the virtual hand.
        homer.VirtualHand.position += scaledDelta;
        homer.VirtualHand.rotation  = homer.PhysicalHand.rotation * handViewRotOffset;

        // Move the grabbed object by the same delta (not clamped to hand position).
        // This lets snap zones set the object's position without us overriding it.
        if (homer.IsGrabbing && homer.GrabbedObject != null)
        {
            homer.GrabbedObject.transform.position += scaledDelta;
            homer.GrabbedObject.transform.rotation  = homer.PhysicalHand.rotation * rotationOffset;
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────

    /// <summary>
    /// Called once when the hand finishes extending to the surface.
    /// Computes the torso-based scaleFactor that stays constant for this entire
    /// extended session (free movement and any subsequent grab).
    /// </summary>
    private void OnExtendBegin()
    {
        Vector3 torsoPosition  = GetTorsoPosition();
        Vector3 handPosition   = homer.PhysicalHand.position;
        Vector3 virtualHandPos = homer.VirtualHand.position;   // surface hit point

        Vector3 handVector   = handPosition - torsoPosition;
        float   handDistance = Mathf.Max(handVector.magnitude, minHandDistance);
        float   virtualDist  = (virtualHandPos - torsoPosition).magnitude;

        scaleFactor = virtualDist / handDistance;
        prevHandPos = handPosition;

        // Capture the hand model's rotation offset relative to the controller.
        // SetParent(null) preserved the world rotation, so this offset encodes the
        // hand mesh's natural orientation (e.g. a 90° palm-down tilt baked into the rig).
        handViewRotOffset = Quaternion.Inverse(homer.PhysicalHand.rotation) * homer.VirtualHand.rotation;
    }

    /// <summary>
    /// Called when the user grabs an XRGrabInteractable.
    /// Computes rotationOffset; scaleFactor is already set from OnExtendBegin.
    /// </summary>
    private void OnGrabBegin(GameObject obj)
    {
        rotationOffset = obj.transform.rotation * Quaternion.Inverse(homer.PhysicalHand.rotation);
    }

    private void OnGrabEnd() { }

    // ── Helpers ───────────────────────────────────────────────────────────
    private Vector3 GetTorsoPosition()
    {
        Camera cam = Camera.main;
        if (cam != null)
            return cam.transform.position + Vector3.down * torsoHeadOffset;
        return transform.position;
    }
}
