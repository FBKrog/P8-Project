using UnityEngine;
using UnityEngine.VFX;

public class DestroyVisualEffect : MonoBehaviour
{
    [SerializeField] VisualEffect effect;

    void Awake()
    {
        if (effect == null)
        {
            effect = GetComponent<VisualEffect>();
        }
        effect.Play();
    }

    void Update()
    {
        if(effect.aliveParticleCount == 0)
        {
            Destroy(gameObject);
        }
    }
}
