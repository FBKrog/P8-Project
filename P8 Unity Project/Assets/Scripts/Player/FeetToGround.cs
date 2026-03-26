using UnityEngine;

public class FeetToGround : MonoBehaviour
{
    [SerializeField] Transform headTransform;
    [SerializeField] GameObject[] feetGrounds;
    [SerializeField] GameObject[] feet;
    [SerializeField] GameObject[] legs;
    [SerializeField] LayerMask ground;
    [SerializeField] float groundOffset;
    [SerializeField] float modelDistance = 1.6f;
    [SerializeField] float scaleMultiplier = 1.15f;
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
            for (int i = 0; i < feet.Length; i++)
            {
                distanceToGround = hit.distance;
                var offset = Vector3.Distance(feetGrounds[i].transform.position, feet[i].transform.position);
                var hitPointOffset = hit.point.y + offset;
                var feetPos = feet[i].transform.position;
                feetPos.y = hitPointOffset;
                feet[i].transform.position = feetPos;
            }
        }
    }

    public void ScaleLegs()
    {
        var scale = (distanceToGround - modelDistance) * scaleMultiplier;
        foreach (var leg in legs)
        {
            leg.transform.localScale = new Vector3(scale + 1, scale + 1, scale + 1);
        }
    }
}
