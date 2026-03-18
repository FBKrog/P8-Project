/// <summary>
/// Marker interface for transform-owning rotary puzzle components (LeverGrab, ValveGrab, etc.).
/// HOMER guards check for this interface to skip position teleport and delta-movement.
/// </summary>
public interface IRotaryGrabbable { }
