using System;
using UnityEngine;

/// <summary>
/// Relay component. Drop one on each trigger source (Plug, Battery Socket, etc.)
/// and wire the source's UnityEvent (OnWirePlugged / OnBatteryPlaced / …) to
/// this component's Activate() method in the Inspector.
///
/// DoorLinker subscribes to the internal Activated event in code and opens
/// the door only when every DoorTrigger in its list is activated.
/// </summary>
public class DoorTrigger : MonoBehaviour
{
    public bool IsActivated { get; private set; }

    // C# event — subscribed to by DoorLinker at runtime
    internal event Action Activated;

    /// <summary>
    /// Mark this condition as satisfied. Wire this to a UnityEvent in the Inspector.
    /// Safe to call multiple times; only fires on the first call.
    /// </summary>
    public void Activate()
    {
        if (IsActivated) return;
        IsActivated = true;
        Activated?.Invoke();
    }

    /// <summary>
    /// Reset this condition (e.g. if the battery is removed from the socket).
    /// </summary>
    public void Deactivate()
    {
        IsActivated = false;
    }
}
