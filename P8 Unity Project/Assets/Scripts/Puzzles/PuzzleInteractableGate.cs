using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PuzzleInteractableGate : MonoBehaviour
{
    [SerializeField] private XRBaseInteractable[] _interactables;

    private void Awake()
    {
        foreach (var i in _interactables)
            if (i != null) i.enabled = false;
    }

    public void Unlock()
    {
        foreach (var i in _interactables)
            if (i != null) i.enabled = true;
    }
}
