using UnityEngine;

public class StretchArms : MonoBehaviour
{
    [SerializeField] Transform leftUpperArm;
    [SerializeField] Transform leftLowerArm;
    [SerializeField] Transform leftTip;
    [SerializeField] Transform leftIKTarget;

    [SerializeField] Transform rightUpperArm;
    [SerializeField] Transform rightLowerArm;
    [SerializeField] Transform rightTip;
    [SerializeField] Transform rightIKTarget;

    [SerializeField] [Tooltip("The closer the value is to 1, the more the lower arm will stretch. Closer to 0, means the upper arm will do more of the stretching.")] [Range(0,1)] float armStretchWeight = 0.5f;
    [SerializeField] float stretchAmount = 0.5f;

    bool canStretch = true;

    void Awake()
    {
        canStretch = true;
    }

    void OnEnable()
    {
        LaunchArm.ArmLaunched += ToggleStretch;
        LaunchArm.ArmRecalled += ToggleStretch;
    }

    void OnDisable()
    {
        LaunchArm.ArmLaunched -= ToggleStretch;
        LaunchArm.ArmRecalled -= ToggleStretch;
    }

    void LateUpdate()
    {
        StretchArm(leftUpperArm, leftLowerArm, leftTip, leftIKTarget);
        if (!canStretch) return;
        StretchArm(rightUpperArm, rightLowerArm, rightTip, rightIKTarget);
    }

    void StretchArm(Transform upperArm, Transform lowerArm, Transform tip, Transform ikTarget)
    {
        var direction = ikTarget.position - tip.position;
        direction = Vector3.ClampMagnitude(direction, stretchAmount);
        upperArm.position += direction * (1 - armStretchWeight);
        lowerArm.position += direction * armStretchWeight;
    }

    void ToggleStretch()
    {
        canStretch = !canStretch;
    }
}
