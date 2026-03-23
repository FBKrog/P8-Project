using UnityEngine;

public class StretchArms : MonoBehaviour
{
    [SerializeField] bool daomArm = false;
    [SerializeField] ArmStretchData[] arms;

    [SerializeField] [Tooltip("The closer the value is to 1, the more the lower arm will stretch. Closer to 0, means the upper arm will do more of the stretching.")] [Range(0,1)] float armStretchWeight = 0.5f;
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
        foreach (var arm in arms)
        {
            if (!canStretch && arm == arms[1]) continue;
            StretchArm(arm.upperArm, arm.lowerArm, arm.tip, arm.ikTarget);
        }
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

[System.Serializable]
public class ArmStretchData
{
    public Transform upperArm;
    public Transform lowerArm;
    public Transform tip;
    public Transform ikTarget;
}
