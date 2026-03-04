using UnityEngine;

/// <summary>
/// Placed on the teleport reticle prefab. Instantiates the TeleportLegs model
/// as a child at the aimed teleport destination, applies the leg dissolve material,
/// and lets you tune scale and vertical offset from the Inspector.
/// </summary>
public class TeleportLegsReticle : MonoBehaviour
{
    [SerializeField] private GameObject legsPrefab;

    [Tooltip("Material with the LegTeleportDissolve shader to force onto all renderers.")]
    [SerializeField] private Material legsMaterial;

    [Tooltip("Uniform scale applied to the legs model. Tune in the Inspector.")]
    [SerializeField] private float modelScale = 0.3f;

    [Tooltip("Local-space offset applied to the legs model after instantiation. " +
             "Increase Y if the legs sink below the floor.")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;

    private void Awake()
    {
        if (legsPrefab == null)
            return;

        var instance = Instantiate(legsPrefab, transform);
        instance.transform.localPosition = positionOffset;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one * modelScale;

        if (legsMaterial != null)
        {
            foreach (var mr in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = new Material[mr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = legsMaterial;
                mr.sharedMaterials = mats;
            }

            foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = new Material[smr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = legsMaterial;
                smr.sharedMaterials = mats;
            }
        }
    }
}
