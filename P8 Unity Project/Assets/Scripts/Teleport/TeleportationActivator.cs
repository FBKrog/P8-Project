using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
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

    [HideInInspector] public bool orbConnected = false;

    void Start()
    {
        teleportInteractor.gameObject.SetActive(false);
        teleportActivatorAction.action.performed += Action_performed;
    }

    private void Action_performed(InputAction.CallbackContext obj)
    {
        if (!orbConnected) return;
        teleportInteractor.gameObject.SetActive(true);
    }

    void Update()
    {
        if (!teleportActivatorAction.action.WasReleasedThisFrame())
            return;

        if (!orbConnected) return;

        if (onBeforeTeleport != null)
            onBeforeTeleport(ExecuteTeleport);
        else
            ExecuteTeleport();
    }

    /// <summary>
    /// Fired after the teleport interactor is deactivated (position change queued).
    /// </summary>
    public System.Action onAfterTeleport;

    private void ExecuteTeleport()
    {
        // Programmatically select then immediately deselect the hovered TeleportationArea.
        // StartManualInteraction → EndManualInteraction fires OnSelectExited on the area,
        // which is the trigger (TeleportTrigger=OnSelectExited) that queues the position
        // change in TeleportationProvider. This runs while the screen is black from TeleportBlink.
        var hovered = teleportInteractor.interactablesHovered;
        if (hovered.Count > 0)
        {
            var target = hovered[0] as IXRSelectInteractable;
            if (target != null)
            {
                teleportInteractor.StartManualInteraction(target);
                teleportInteractor.EndManualInteraction();
            }
        }
        teleportInteractor.gameObject.SetActive(false);
        onAfterTeleport?.Invoke();
    }
}
