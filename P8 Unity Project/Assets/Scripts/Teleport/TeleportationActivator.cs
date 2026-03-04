using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class TeleportationActivator : MonoBehaviour
{
    public XRRayInteractor teleportInteractor;
    public InputActionProperty teleportActivatorAction;

    /// <summary>
    /// Optional hook called when the user releases the teleport button while aiming
    /// at a valid target. The supplied Action, when invoked, performs the actual
    /// teleport (SetActive false on the interactor). If null, the teleport fires
    /// immediately — original behaviour is preserved.
    /// </summary>
    public System.Action<System.Action> onBeforeTeleport;

    void Start()
    {
        teleportInteractor.gameObject.SetActive(false);
        teleportActivatorAction.action.performed += Action_performed;
    }

    private void Action_performed(InputAction.CallbackContext obj)
    {
        teleportInteractor.gameObject.SetActive(true);
    }

    void Update()
    {
        if (!teleportActivatorAction.action.WasReleasedThisFrame())
            return;

        if (onBeforeTeleport != null)
            onBeforeTeleport(ExecuteTeleport);
        else
            ExecuteTeleport();
    }

    private void ExecuteTeleport()
    {
        teleportInteractor.gameObject.SetActive(false);
    }
}
