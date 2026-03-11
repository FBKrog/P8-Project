using UnityEngine;

/// <summary>
/// Marker component on the TP Orb. Tag the GameObject "TPOrb".
/// XRGrabInteractable + Rigidbody required (sibling components).
/// HandTPOrbConnect.cs handles snap detection and teleport toggling.
/// Future: expose OnPlacedOnPad() for TeleportPad puzzle integration.
/// </summary>
public class TeleportOrb : MonoBehaviour { }
