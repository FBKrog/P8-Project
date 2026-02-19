using System.Collections;
using System.Drawing;
using UnityEngine;

public class DAOMArm : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] GameObject playerRoot;
    [SerializeField] GameObject playerIKTarget;

    [Header("DAOM")]
    [SerializeField] GameObject daomRoot;
    [SerializeField] GameObject daomIKTarget;
    [SerializeField] float travelTime = 1f;
    [SerializeField] float rotationTime = 0.5f;

    Quaternion targetRot;
    Quaternion startRot;

    bool isTraveling = false;

    bool isAttachedToSurface = false;
    public bool IsAttachedToSurface => isAttachedToSurface;

    bool recalling = false;
    public bool Recalling => recalling;

    public void Initialize(GameObject root, GameObject IKTarget, Vector3 point, Vector3 normal)
    {
        playerRoot = root;
        playerIKTarget = IKTarget;
        StartCoroutine(TravelToPoint(point, normal));
    }

    public void RecallArm(Vector3 point, Vector3 normal)
    {
        if (isTraveling) return;
        if (!isAttachedToSurface) return;
        if (recalling) return;
        recalling = true;
        StartCoroutine(TravelToPoint(point, normal));
        StartCoroutine(RotateToNormal());
    }

    IEnumerator TravelToPoint(Vector3 point, Vector3 normal)
    {
        if (isTraveling) yield break;
        isTraveling = true;
        Vector3 startPos = transform.position;

        startRot = transform.rotation;
        targetRot = Quaternion.LookRotation(normal);

        float elapsedTime = 0f;
        while (true) {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / travelTime);
            transform.position = Vector3.Lerp(startPos, point, t);
            if (elapsedTime >= travelTime) 
            {
                ArmAttaching();
                break;
            }
            yield return null;
        }
        yield break;
    }

    void ArmAttaching()
    {
        isTraveling = false;
        isAttachedToSurface = true;
        StartCoroutine(RotateToNormal());
        if (recalling)
        {
            LaunchArm.ArmRecalled();
            return;
        }
    }

    IEnumerator RotateToNormal()
    {
        float elapsedTime = 0f;
        while (true)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / rotationTime);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            if (elapsedTime >= travelTime)
            {
                break;
            }
            yield return null;
        }
        yield break;
    }

    void LateUpdate()
    {
        if (!isAttachedToSurface || recalling) return;       
        TransformToPlayerHand();
        RotateToPlayerHand();
    }

    void TransformToPlayerHand()
    {
        // Get the position of the player hand relative to the player root, and apply that same relative position to the daom root to find the position for the daom hand.
        Vector3 playerHandOffset = playerRoot.transform.InverseTransformPoint(playerIKTarget.transform.position);
        daomIKTarget.transform.position = daomRoot.transform.TransformPoint(playerHandOffset);
    }

    void RotateToPlayerHand()
    {
        // Get the rotation of the player hand relative to the player root, and apply that same relative rotation to the daom root to find the rotation for the daom hand.
        Quaternion relativeRot = Quaternion.Inverse(playerRoot.transform.rotation) * playerIKTarget.transform.rotation;
        daomIKTarget.transform.rotation = daomRoot.transform.rotation * relativeRot;
    }
}
