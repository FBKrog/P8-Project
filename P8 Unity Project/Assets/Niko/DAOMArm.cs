using UnityEngine;

public class DAOMArm : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] GameObject playerRoot;
    [SerializeField] GameObject playerIKTarget;
    
    [Header("DAOM")]
    [SerializeField] GameObject daomRoot;
    [SerializeField] GameObject daomIKTarget;

    void FindPlayerHand()
    {
        // TODO: Find the player hand transform in the hierarchy, instead of assigning it in the inspector, and assign it to the playerHand variable - maybe through an event.
    }

    void LateUpdate()
    {
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
