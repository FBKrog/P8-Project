using UnityEngine;

public class VRAvatarScaler : MonoBehaviour
{
    [Header("Avatar References")]
    [SerializeField] Transform avatarHead;
    [SerializeField] Transform leftUpperArm;
    [SerializeField] Transform leftForearm;
    [SerializeField] Transform rightUpperArm;
    [SerializeField] Transform rightForearm;
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

        // Scale arms proportionally and clamp
        var armScale = Mathf.Clamp(heightScale, minArmScale, maxArmScale);
        leftUpperArm.localScale = AdjustScale(armScale);
        leftForearm.localScale = AdjustScale(armScale);
        rightUpperArm.localScale = AdjustScale(armScale);
        rightForearm.localScale = AdjustScale(armScale);

        // Scale legs proportionally and clamp
        var legScale = Mathf.Clamp(heightScale, minLegScale, maxLegScale);
        leftThigh.localScale = AdjustScale(legScale);
        leftShin.localScale = AdjustScale(legScale);
        rightThigh.localScale = AdjustScale(legScale);
        rightShin.localScale = AdjustScale(legScale);
    }

    Vector3 AdjustScale(float scale)
    {
        return new Vector3(scale, 1, 1);
    }
}
