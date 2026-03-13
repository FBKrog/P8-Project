using UnityEngine;

public class FollowXROrigin : MonoBehaviour
{
    [Header("Head-Body Offset")]
    [SerializeField] Vector3 headBodyPositionOffset;
    //[SerializeField] float turnSmoothness = 5f;

    //[Header("Torso Rotation")]
    //[SerializeField] float torsoRotationSpeed = 5f;
    //[SerializeField] float torsoRotationMaxAngle = 60f;

    [Header("Mapping")]
    [SerializeField] VRMap head;
    //[SerializeField] VRMap spine;
    [SerializeField] VRMap leftHand;
    [SerializeField] VRMap rightHand;

    void LateUpdate()
    {
        ApplyHeadBodyOffset();
        //RotateTorsoTowardsHands();
        Mapping();
    }

    void ApplyHeadBodyOffset()
    {
        var targetPosition = head.ikTarget.position + headBodyPositionOffset;
        transform.position = targetPosition;

        //var newY = head.ikTarget.eulerAngles.y;
        //var targetRotation = Quaternion.Euler(transform.eulerAngles.x, newY, transform.eulerAngles.z);
        //transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, turnSmoothness);
    }

    void Mapping()
    {
        head.Map();
        leftHand.Map();
        rightHand.Map();

        // Spine should only rotate, not move
        //spine.Map(false);
    }

    /// <summary>
    /// Rotates the avatar's torso horizontally to face the midpoint between the left and right hands, within a
    /// specified angular limit.
    /// </summary>
    //void RotateTorsoTowardsHands()
    //{
    //    // Compute midpoint between left and right hand positions
    //    var leftPos = leftHand.xrTarget.position;
    //    var rightPos = rightHand.xrTarget.position;
    //    var handMidpoint = (leftPos + rightPos) * 0.5f;

    //    // Direction from avatar root to the hand midpoint (ignore vertical)
    //    var torsoDir = handMidpoint - spine.xrTarget.position;
    //    torsoDir.y = 0f;

    //    // Avoid division by zero
    //    if (torsoDir.sqrMagnitude < 0.001f)
    //        return;

    //    torsoDir.Normalize();

    //    // Calculate angle between head forward and torso direction
    //    float angle = Vector3.SignedAngle(spine.xrTarget.forward, torsoDir, Vector3.up);

    //    // Clamp angle to prevent unnatural twisting
    //    angle = Mathf.Clamp(angle, -torsoRotationMaxAngle, torsoRotationMaxAngle);

    //    // Compute final target rotation: head forward + clamped twist
    //    Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.up) * Quaternion.LookRotation(spine.xrTarget.forward, Vector3.up);

    //    // Smoothly rotate the avatar root toward target rotation
    //    spine.xrTarget.rotation = Quaternion.Slerp(spine.xrTarget.rotation, targetRotation, torsoRotationSpeed * Time.deltaTime
    //    );
    //}
}

/// <summary>
/// Helper class to map XR target transforms to IK targets on the avatar, with optional position and rotation offsets.
/// </summary>
[System.Serializable]
public class VRMap
{
    public Transform xrTarget;
    public Transform ikTarget;
    public Vector3 positionOffset;
    public Vector3 rotationOffset;

    public void Map(bool mapPosition = true, bool mapRotation = true)
    {
        if (mapPosition)
            ikTarget.position = xrTarget.position + positionOffset;

        if (mapRotation)
            ikTarget.rotation = xrTarget.rotation * Quaternion.Euler(rotationOffset);
    }
}