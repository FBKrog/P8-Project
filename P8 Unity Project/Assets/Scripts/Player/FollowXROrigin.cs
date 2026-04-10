using UnityEngine;

public class FollowXROrigin : MonoBehaviour
{
    [Header("Head-Body Offset")]
    [SerializeField] Vector3 headBodyPositionOffset;

    [Header("Body Rotation")]
    [SerializeField] float bodyRotationSpeed = 4f;
    [SerializeField] float bodyRotationMaxAngle = 60f;
    [SerializeField] [Tooltip("How much the head influences the body rotation. 0 is no influence, 1 is max.")] [Range(0,1)] float headInfluence = 0.4f;

    [Header("Mapping")]
    [SerializeField] VRMap head;
    [SerializeField] VRMap leftHand;
    [SerializeField] VRMap rightHand;

    void LateUpdate()
    {
        ApplyHeadBodyOffset();
        RotateSpineTowardsHands();
        Mapping();
    }

    void ApplyHeadBodyOffset()
    {
        var targetPosition = head.ikTarget.position + headBodyPositionOffset;
        transform.position = targetPosition;
    }

    void Mapping()
    {
        head.Map();
        leftHand.Map();
        rightHand.Map();
    }

    /// <summary>
    /// Rotates the body to face a direction blended between the midpoint of the hands and the head's forward direction.
    /// </summary>
    void RotateSpineTowardsHands()
    {
        // Get positions
        var leftPos = leftHand.ikTarget.position;
        var rightPos = rightHand.ikTarget.position;

        // Midpoint of hands
        var handMid = (leftPos + rightPos) * 0.5f;

        // Directions (flattened)
        var directionToHands = handMid - transform.position;
        var headForward = head.ikTarget.forward;

        directionToHands.y = 0f;
        headForward.y = 0f;

        if (directionToHands.sqrMagnitude < 0.001f)
            return;

        directionToHands.Normalize();
        headForward.Normalize();

        // Clamp the angle between head forward and hands direction to prevent unnatural twisting
        var angleDifference = Vector3.Angle(headForward, directionToHands);
        Vector3 clampedDir;
        if(angleDifference > bodyRotationMaxAngle)
        {
            clampedDir = Vector3.RotateTowards(headForward, directionToHands, Mathf.Deg2Rad * bodyRotationMaxAngle, 0f);
        }
        else
        {
            clampedDir = directionToHands;
        }

        // Blend between clamped hand direction and head forward based on head influence
        var blendedDir = Vector3.Slerp(clampedDir, headForward, headInfluence);

        // Safety check to prevent NaNs
        if (blendedDir.sqrMagnitude < 0.001f)
            return;

        blendedDir.Normalize();

        var targetRotation = Quaternion.LookRotation(blendedDir, Vector3.up);

        // Smoothly lerp to the target rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, bodyRotationSpeed * Time.deltaTime);
    }
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