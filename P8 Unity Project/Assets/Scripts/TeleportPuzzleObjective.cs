using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Detects the three puzzle-intro steps and advances TutorialManager.
/// Not auto-started — activated by ObjectivesManager.onObjectiveCompleted via Inspector.
/// Steps (relative to TutorialManager index, starting after OrbObjective):
///   0 → Teleported      → step 6
///   1 → Pad activated   → step 7
///   2 → Orb on pedestal → step 8 (completesObjective = "PuzzleIntroObjective")
/// </summary>
public class TeleportPuzzleObjective : MonoBehaviour
{
    [SerializeField] private TutorialManager       tutorialManager;
    [SerializeField] private TeleportationProvider teleportProvider;
    [SerializeField] private PressurePlate         pressurePlate;
    [SerializeField] private OrbPedestal           orbPedestal;

    private int _step = -1;

    /// <summary>
    /// Called via Inspector from ObjectivesManager.onObjectiveCompleted.
    /// Filters so only "OrbObjective" triggers this objective.
    /// </summary>
    public void ActivateIfName(string objectiveName)
    {
        if (objectiveName == "OrbObjective") StartObjective();
    }

    public void StartObjective()
    {
        if (_step >= 0) return;
        teleportProvider.locomotionEnded          += OnTeleported;
        pressurePlate.OnPlateActivated.AddListener(OnPadActivated);
        orbPedestal.OrbPlaced.AddListener(OnOrbPlaced);
        _step = 0;
        tutorialManager.AdvanceToNextStep();   // shows step 5
    }

    private void OnTeleported(LocomotionProvider provider)
    {
        if (_step != 0) return;
        _step = 1;
        teleportProvider.locomotionEnded -= OnTeleported;
        tutorialManager.AdvanceToNextStep();   // shows step 6
    }

    private void OnPadActivated()
    {
        if (_step != 1) return;
        _step = 2;
        tutorialManager.AdvanceToNextStep();   // shows step 7
    }

    private void OnOrbPlaced()
    {
        if (_step != 2) return;
        _step = 3;
        pressurePlate.OnPlateActivated.RemoveListener(OnPadActivated);
        orbPedestal.OrbPlaced.RemoveListener(OnOrbPlaced);
        tutorialManager.AdvanceToNextStep();   // shows step 8 (completesObjective = "PuzzleIntroObjective")
    }
}
