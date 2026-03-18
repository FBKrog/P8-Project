using UnityEngine;

public class FeetToGround : MonoBehaviour
{
    [SerializeField] Transform headTransform;
    [SerializeField] GameObject[] feet;
    [SerializeField] LayerMask ground;
    [SerializeField] float groundOffset;
    float distanceToGround;
    
    void LateUpdate()
    {
        PlaceFeetAtGround();
    }

    void PlaceFeetAtGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(headTransform.position, Vector3.down, out hit, Mathf.Infinity, ground))
        {
            if (hit.distance != distanceToGround)
            {
                foreach (var feet in feet)
                {
                    distanceToGround = hit.distance;
                    feet.transform.position = new Vector3(feet.transform.position.x, hit.point.y + groundOffset, feet.transform.position.z);
                }
            }
        }
    }
}
