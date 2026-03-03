using UnityEngine;
using UnityEngine.InputSystem;
public class ExtendManipulator : MonoBehaviour
{
    [Header("References")]
    public Extend extend;

    [Header("Input")]
    public InputActionProperty thumbstickAction;

    [Header("Scaled HOMER — Velocity Thresholds")]
    [Tooltip("Controller speed (m/s) at which V_multiplier reaches its minimum (0.1) — near 1:1 fine control.")]
    public float minVelocity = 0.05f;
    [Tooltip("Controller speed (m/s) at which V_multiplier reaches its maximum (1.0) — full SF amplification.")]
    public float maxVelocity = 1.5f;

    [Header("Reeling")]
    public float reelSpeed   = 5f;
    public float minDistance = 0.3f;
    public float maxDistance = 25f;

    [Header("Rotation Mirroring")]
    public float rotationSmoothing = 12f;

    private bool    wasExtended;
    private float   scaleFactor;         // SF: distance from ArmTip to hand at moment of extension
    private Vector3 lastControllerPos;

    void OnEnable()  => thumbstickAction.action?.Enable();
    void OnDisable() => thumbstickAction.action?.Disable();

    void Update()
    {
        if (extend == null) return;

        bool isExtended = extend.IsExtended;

        if (!isExtended)
        {
            wasExtended = false;
            return;
        }

        // First frame entering Extended state — capture SF and reference position.
        if (!wasExtended)
        {
            scaleFactor       = Vector3.Distance(extend.Hand.position, extend.ArmTip);
            lastControllerPos = extend.transform.position;
            wasExtended       = true;
        }

        Transform hand    = extend.Hand;
        Vector3   armTip  = extend.ArmTip;     // moves with the controller
        Vector3   ctrlPos = extend.transform.position;

        // ----------------------------------------------------------------
        // 1. Rotation mirroring — arm.transform IS the tracked controller
        // ----------------------------------------------------------------
        hand.rotation = Quaternion.Slerp(
            hand.rotation,
            extend.transform.rotation,
            rotationSmoothing * Time.deltaTime);

        // ----------------------------------------------------------------
        // 2. Scaled HOMER positional movement
        //    Object_Position = prev + (SF × V_multiplier) × ctrlDelta
        // ----------------------------------------------------------------
        Vector3 ctrlDelta = ctrlPos - lastControllerPos;
        lastControllerPos = ctrlPos;

        if (ctrlDelta.sqrMagnitude > 1e-10f)
        {
            float velocity    = ctrlDelta.magnitude / Time.deltaTime;
            float t           = Mathf.Clamp01(Mathf.InverseLerp(minVelocity, maxVelocity, velocity));
            float vMultiplier = Mathf.Lerp(0.1f, 1.0f, t);

            // Apply scaled delta — direction and magnitude both come from ctrlDelta.
            hand.position += ctrlDelta * (scaleFactor * vMultiplier);
            hand.position  = ClampToSphere(hand.position, armTip);
        }

        // ----------------------------------------------------------------
        // 3. Reeling — thumbstick Y moves hand along the arm-to-hand vector
        //    (the direction the arm is extended), not the hand's facing direction.
        //    SF is intentionally NOT updated here so the Homer scale stays
        //    relative to the original grab distance regardless of reel depth.
        // ----------------------------------------------------------------
        float stick = thumbstickAction.action.ReadValue<Vector2>().y;
        if (Mathf.Abs(stick) > 0.1f)
        {
            Vector3 reelDir = (hand.position - armTip).normalized;
            Vector3 newPos  = hand.position + reelDir * stick * reelSpeed * Time.deltaTime;
            hand.position   = ClampToSphere(newPos, armTip);
        }
    }

    // Keeps pos inside the [minDistance, maxDistance] sphere around centre.
    // Falls back to arm-forward if pos coincides with centre (avoid NaN).
    private Vector3 ClampToSphere(Vector3 pos, Vector3 centre)
    {
        Vector3 toPos = pos - centre;
        if (toPos.sqrMagnitude < 1e-10f)
            toPos = extend.transform.forward * minDistance;
        float dist = Mathf.Clamp(toPos.magnitude, minDistance, maxDistance);
        return centre + toPos.normalized * dist;
    }
}
