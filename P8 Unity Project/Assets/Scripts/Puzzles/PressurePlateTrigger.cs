using UnityEngine;

/// <summary>
/// Add to a block to designate which PressurePlate it activates.
/// The plate checks for this component on collision.
/// </summary>
public class PressurePlateTrigger : MonoBehaviour
{
    [Tooltip("The PressurePlate this object should activate/trigger.")]
    [SerializeField] private PressurePlate targetPlate;

    public PressurePlate TargetPlate => targetPlate;
}
