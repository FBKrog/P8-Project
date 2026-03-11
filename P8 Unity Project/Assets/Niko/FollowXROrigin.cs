using UnityEngine;

public class FollowXROrigin : MonoBehaviour
{
    [SerializeField] Transform xrOrigin;
    
    [Header("Head")]
    [SerializeField] Transform head;
    [SerializeField] Transform headTarget;

    [Header("Foot")]
    [SerializeField] Transform leftFoot;
    [SerializeField] Transform leftFootTarget;
    [SerializeField] Transform rightFoot;
    [SerializeField] Transform rightFootTarget;

    [Header("Hand")]
    [SerializeField] Transform leftHand;
    [SerializeField] Transform rightHand;

    [SerializeField] float smoothMovement = 0.1f;
    void LateUpdate()
    {
        Translate(head, headTarget);
    }

    void Translate(Transform bodyPart, Transform targetBodyPart)
    {
        var startPos = bodyPart.position;
        var targetPos = targetBodyPart.position;
        bodyPart.position = Vector3.Lerp(startPos, targetPos, smoothMovement * Time.deltaTime);
    }
}
