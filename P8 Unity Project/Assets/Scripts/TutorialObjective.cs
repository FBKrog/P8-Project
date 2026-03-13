using UnityEngine;

/// <summary>
/// General-purpose tutorial objective sequencer. Wire trigger sources to
/// AdvanceIfStep() in the Inspector, setting the expected step index as the
/// static int value on each UnityEvent entry.
/// </summary>
public class TutorialObjective : MonoBehaviour
{
    [Tooltip("Activate when ObjectivesManager fires an event with this name.")]
    [SerializeField] private string activateOnObjective;

    [SerializeField] private TutorialManager tutorialManager;

    private int _step = -1;   // -1 = not yet activated

    /// <summary>Wire to ObjectivesManager.onObjectiveCompleted.</summary>
    public void ActivateIfName(string completedObjectiveName)
    {
        if (completedObjectiveName == activateOnObjective)
            Activate();
    }

    /// <summary>Activate immediately (skip name filter).</summary>
    public void Activate()
    {
        if (_step >= 0) return;

        if (tutorialManager == null)
        {
            Debug.LogError($"[TutorialObjective] '{gameObject.name}' has no TutorialManager assigned.");
            return;
        }

        _step = 0;
        // No longer calls AdvanceToNextStep() — TutorialManager drives its own pacing via displayDuration
        Debug.Log($"[TutorialObjective] '{gameObject.name}' activated.");
    }
    /// <summary>
    /// Advance if the TutorialManager is currently on a step whose stepId matches id.
    /// </summary>
    public void AdvanceIfStepId(string id)
    {
        if (_step < 0) return;
        if (string.IsNullOrEmpty(id) || tutorialManager.CurrentStepId != id) return;

        _step++;
        tutorialManager.AdvanceToNextStep();
        Debug.Log($"[TutorialObjective] '{gameObject.name}' advanced on step ID '{id}' (internal step now {_step}).");
    }
}
