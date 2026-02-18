using System.Collections;
using UnityEngine;

public class DAOMArm : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] GameObject playerRoot;
    [SerializeField] GameObject playerIKTarget;

    [Header("DAOM")]
    [SerializeField] GameObject daomRoot;
    [SerializeField] GameObject daomIKTarget;
    [SerializeField] [Tooltip("Only shown for debugging purposes")] bool isTraveling = false;
    [SerializeField] [Tooltip("Only shown for debugging purposes")] bool isAttachedToSurface = false;
    [SerializeField] [Tooltip("Only shown for debugging purposes")] bool recalling = false;

    public void Initialize(GameObject root, GameObject IKTarget, Vector3 point, Vector3 normal)
    {
        playerRoot = root;
        playerIKTarget = IKTarget;
        StartCoroutine(TravelToPoint(point, normal));
    }

    IEnumerator TravelToPoint(Vector3 point, Vector3 normal)
    {
        Debug.Log("Traveling to point: " + point + " with normal: " + normal);
        if (isTraveling) yield break;
        isTraveling = true;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.LookRotation(normal);
        float travelTime = 1f;
        float elapsedTime = 0f;
        while (true) {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / travelTime);
            transform.position = Vector3.Lerp(startPos, point, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            if (t >= 1f) break;
            if (recalling)
            {
                Destroy(gameObject);
                yield break;
            }
            isTraveling = false;
            isAttachedToSurface = true;
            yield return null;
        }
    }

    public void RecallArm(Vector3 point, Vector3 normal)
    {
        if (isTraveling || !isAttachedToSurface || recalling) return;
        recalling = true;
        isAttachedToSurface = false;
        StartCoroutine(TravelToPoint(point, normal));
    }

    void FindPlayerHand()
    {
        // TODO: Find the player hand transform in the hierarchy, instead of assigning it in the inspector, and assign it to the playerHand variable - maybe through an event.
    }

    void LateUpdate()
    {
        if (!isAttachedToSurface) return;       
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
