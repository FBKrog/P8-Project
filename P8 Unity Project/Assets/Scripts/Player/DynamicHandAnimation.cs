using UnityEngine;

public class DynamicHandAnimation : MonoBehaviour
{
    [SerializeField] HandData handData;

    void Awake()
    {
        if (handData == null)
        {
            Debug.LogError("Hand data is not assigned or empty.");
            return;
        }
        for (int i = 0; i < handData.thumb.jointValues.Length; i++)
            handData.thumb.jointValues[i] = SetInitialPhalanx(handData.thumb.joints[i]);
        for (int i = 0; i < handData.index.jointValues.Length; i++)
        {
            handData.index.jointValues[i] = SetInitialPhalanx(handData.index.joints[i]);
            handData.middle.jointValues[i] = SetInitialPhalanx(handData.middle.joints[i]);
            handData.ring.jointValues[i] = SetInitialPhalanx(handData.ring.joints[i]);
            handData.pinky.jointValues[i] = SetInitialPhalanx(handData.pinky.joints[i]);
        }
    }
    void FixedUpdate()
    {
#if UNITY_EDITOR
        Animate();
#endif
    }

    void Animate()
    {
        for (int i = 0; i < handData.thumb.jointValues.Length; i++)
            BendPhalanx(handData.thumb.joints[i], handData.thumb.jointValues[i]);
        for (int i = 0; i < handData.index.jointValues.Length; i++)
        {
            BendPhalanx(handData.index.joints[i], handData.index.jointValues[i]);
            BendPhalanx(handData.middle.joints[i], handData.middle.jointValues[i]);
            BendPhalanx(handData.ring.joints[i], handData.ring.jointValues[i]);
            BendPhalanx(handData.pinky.joints[i], handData.pinky.jointValues[i]);
        }
    }

    void BendPhalanx(Transform phalanx, Vector3 value)
    {
        phalanx.localRotation = Quaternion.Euler(value.x, value.y, value.z);
    }

    Vector3 SetInitialPhalanx(Transform phalanx)
    {
        return phalanx.localRotation.eulerAngles;
    }
}

[System.Serializable]
public class HandData
{
    public FingerData thumb;
    public FingerData index;
    public FingerData middle;
    public FingerData ring;
    public FingerData pinky;
}

[System.Serializable]
public class FingerData
{
    public Transform[] joints;
    public Vector3[] jointValues;
}
