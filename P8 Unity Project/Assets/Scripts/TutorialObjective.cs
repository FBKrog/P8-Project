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
        {
            Debug.Log($"[TutorialObjective] '{gameObject.name}' — ActivateIfName matched '{completedObjectiveName}'. Activating.");
            Activate();
        }
        else
        {
            Debug.Log($"[TutorialObjective] '{gameObject.name}' — ActivateIfName ignored '{completedObjectiveName}' (waiting for '{activateOnObjective}').");
        }
    }

    /// <summary>Activate immediately (skip name filter).</summary>
    public void Activate()
    {
        if (_step >= 0)
        {
            Debug.Log($"[TutorialObjective] '{gameObject.name}' — Activate called but already active (step={_step}).");
            return;
        }

        if (tutorialManager == null)
        {
            Debug.LogError($"[TutorialObjective] '{gameObject.name}' has no TutorialManager assigned.");
            return;
        }

        _step = 0;
        Debug.Log($"[TutorialObjective] '{gameObject.name}' activated. Now listening for step advances.");
    }

    /// <summary>
    /// Advance if the TutorialManager is currently on a step whose stepId matches id.
    /// </summary>
    public void AdvanceIfStepId(string id)
    {
        if (_step < 0)
        {
            Debug.Log($"[TutorialObjective] '{gameObject.name}' — AdvanceIfStepId('{id}') ignored: not yet activated.");
            return;
        }
        if (string.IsNullOrEmpty(id) || tutorialManager.CurrentStepId != id)
        {
            Debug.Log($"[TutorialObjective] '{gameObject.name}' — AdvanceIfStepId('{id}') ignored: current step ID is '{tutorialManager.CurrentStepId}'.");
            return;
        }

        _step++;
        Debug.Log($"[TutorialObjective] '{gameObject.name}' — AdvanceIfStepId('{id}') matched. Advancing tutorial (internal step now {_step}).");
        tutorialManager.AdvanceToNextStep();
    }
}
