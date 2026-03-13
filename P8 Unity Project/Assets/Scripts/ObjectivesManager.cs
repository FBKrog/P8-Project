using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ObjectivesManager : MonoBehaviour
{
    [System.Serializable]
    public class Objective
    {
        public string objectiveName;
        [Tooltip("Display text — reserved for future UI use.")]
        public string description;
        [HideInInspector] public bool isCompleted;
    }

    // -------------------------------------------------------------------------

    [SerializeField] private List<Objective> objectives = new List<Objective>();

    [Space]
    public UnityEvent<string> onObjectiveCompleted;
    public UnityEvent         onAllObjectivesCompleted;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void CompleteObjective(string objectiveName)
    {
        var obj = objectives.Find(o => o.objectiveName == objectiveName);
        if (obj == null)
        {
            Debug.LogWarning($"[ObjectivesManager] Objective '{objectiveName}' not found.");
            return;
        }

        if (obj.isCompleted) return;

        obj.isCompleted = true;
        onObjectiveCompleted?.Invoke(objectiveName);

        if (AllCompleted())
            onAllObjectivesCompleted?.Invoke();
    }

    public bool IsCompleted(string objectiveName)
    {
        var obj = objectives.Find(o => o.objectiveName == objectiveName);
        return obj != null && obj.isCompleted;
    }

    public bool AllCompleted()
    {
        foreach (var obj in objectives)
            if (!obj.isCompleted) return false;
        return true;
    }

    [ContextMenu("Reset All Objectives")]
    public void ResetAll()
    {
        foreach (var obj in objectives)
            obj.isCompleted = false;
    }
}
