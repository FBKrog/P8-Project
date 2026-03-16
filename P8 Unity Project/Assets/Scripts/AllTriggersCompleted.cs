using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Fires OnAllTriggersCompleted once every DoorTrigger in the list has been activated.
/// Use the same DoorTrigger relay components as DoorLinker — wire each source's
/// UnityEvent (OnBatteryPlaced, OnWirePlugged, …) to DoorTrigger.Activate() in
/// the Inspector, then add each DoorTrigger to this component's Triggers list.
/// </summary>
public class AllTriggersCompleted : MonoBehaviour
{
    [Header("Triggers — ALL must be activated to fire the event")]
    [SerializeField] private List<DoorTrigger> triggers = new();

    [Header("Events")]
    public UnityEvent OnAllTriggersCompleted;

    private bool _fired = false;

    // -------------------------------------------------------------------------

    private void Start()
    {
        foreach (var trigger in triggers)
        {
            if (trigger != null)
                trigger.Activated += Check;
        }
    }

    private void OnDestroy()
    {
        foreach (var trigger in triggers)
        {
            if (trigger != null)
                trigger.Activated -= Check;
        }
    }

    // -------------------------------------------------------------------------

    private void Check()
    {
        if (_fired) return;

        foreach (var trigger in triggers)
        {
            if (trigger == null || !trigger.IsActivated)
                return;
        }

        _fired = true;
        OnAllTriggersCompleted.Invoke();
    }
}
