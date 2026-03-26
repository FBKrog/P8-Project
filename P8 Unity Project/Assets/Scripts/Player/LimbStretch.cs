using UnityEngine;

public class LimbStretch : MonoBehaviour
{
    [SerializeField] bool daomArm = false;
    [SerializeField] LimbStretchData[] limbs;

    [SerializeField] [Tooltip("The closer the value is to 1, the more the lower limb will stretch. Closer to 0, means the upper limb will do more of the stretching.")] [Range(0,1)] float limbStretchWeight = 0.5f;
    [SerializeField] float stretchAmount = 0.5f;

    bool canStretch = true;

    void Awake()
    {
        canStretch = true;
    }

    void OnEnable()
    {
        if (daomArm) return;
        LaunchArm.ArmLaunched += ToggleStretch;
        LaunchArm.ArmRecalled += ToggleStretch;
    }

    void OnDisable()
    {
        if (daomArm) return;
        LaunchArm.ArmLaunched -= ToggleStretch;
        LaunchArm.ArmRecalled -= ToggleStretch;
    }

    void LateUpdate()
    {
        for (int i = 0; i < limbs.Length; i++)
        {
            if (!canStretch && i == 1) continue;
            StretchLimb(limbs[i].upperLimb, limbs[i].lowerLimb, limbs[i].tip, limbs[i].ikTarget);
        }
    }

    void StretchLimb(Transform upperLimb, Transform lowerLimb, Transform tip, Transform ikTarget)
    {
        var direction = ikTarget.position - tip.position;
        direction = Vector3.ClampMagnitude(direction, stretchAmount);
        upperLimb.position += direction * (1 - limbStretchWeight);
        lowerLimb.position += direction * limbStretchWeight;
    }

    void ToggleStretch()
    {
        canStretch = !canStretch;
    }
}

[System.Serializable]
public class LimbStretchData
{
    public Transform upperLimb;
    public Transform lowerLimb;
    public Transform tip;
    public Transform ikTarget;
}
