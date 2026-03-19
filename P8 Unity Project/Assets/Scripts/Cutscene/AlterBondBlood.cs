using UnityEngine;

public class AlterBondBlood : MonoBehaviour
{
    [SerializeField] private Material theBondBlooder;
    public float bloodAmount = 0f;
    public bool updateBlood = false;

    private void Update()
    {
        if (updateBlood)
            SetBondBlood();
    }


    public void SetBondBlood()
    {
        theBondBlooder.SetFloat("_Current_Fill", bloodAmount);
    }
}
