using UnityEngine;

public class VRAvatarScaler : MonoBehaviour
{
    [Header("Avatar References")]
    [SerializeField] Transform avatarHead;
    [SerializeField] Transform leftUpperArm;
    [SerializeField] Transform leftForearm;
    [SerializeField] Transform leftHand;
    [SerializeField] Transform rightUpperArm;
    [SerializeField] Transform rightForearm;
    [SerializeField] Transform rightHand;
    [SerializeField] Transform leftThigh;
    [SerializeField] Transform leftShin;
    [SerializeField] Transform rightThigh;
    [SerializeField] Transform rightShin;
    [SerializeField] Transform avatarFeet;

    [Header("XR References")]
    public Transform xrHead;

    [Header("Scaling Settings")]
    [SerializeField] float minHeight = 1.2f;
    [SerializeField] float maxHeight = 2.2f;
    [SerializeField] float minArmScale = 0.85f;
    [SerializeField] float maxArmScale = 1.15f;
    [SerializeField] float minLegScale = 0.85f;
    [SerializeField] float maxLegScale = 1.15f;

    float previousHeight;

    public void ApplyScaling()
    {
        previousHeight = xrHead.position.y - avatarFeet.position.y;
        // Player height measured from headset to floor
        var playerHeight = Mathf.Clamp(xrHead.position.y, minHeight, maxHeight);

        // Compute scale factor
        var heightScale = playerHeight / previousHeight;

        // Scale arms proportionally and clamp for reach
        var armScale = Mathf.Clamp(heightScale, minArmScale, maxArmScale);
        leftUpperArm.localScale = AdjustScale(armScale);
        leftForearm.localScale = AdjustScale(armScale);
        rightUpperArm.localScale = AdjustScale(armScale);
        rightForearm.localScale = AdjustScale(armScale);

        // Scale legs proportionally and clamp for height
        var legScale = Mathf.Clamp(heightScale, minLegScale, maxLegScale);
        leftThigh.localScale = AdjustScale(legScale);
        leftShin.localScale = AdjustScale(legScale);
        rightThigh.localScale = AdjustScale(legScale);
        rightShin.localScale = AdjustScale(legScale);

        // Scale hands to compensate for arm scaling
        var handScale = Mathf.Abs(Mathf.Abs(1 - armScale) - 1);
        leftHand.localScale = AdjustScale(handScale);
        rightHand.localScale = AdjustScale(handScale);
    }

    Vector3 AdjustScale(float scale)
    {
        return new Vector3(scale, 1, 1);
    }
}
