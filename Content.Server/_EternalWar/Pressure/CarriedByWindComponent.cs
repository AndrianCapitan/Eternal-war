namespace Content.Server._EternalWar.Pressure;

/// <summary>
/// Space wind carries this right now (added and taken off by the atmosphere, <c>AtmosphereSystem.HighPressureDelta</c>).
/// What it flies into gets hit, and it may break (<see cref="PressureBreakageSystem"/>).
/// </summary>
[RegisterComponent]
public sealed partial class CarriedByWindComponent : Component
{
    /// <summary>
    /// The people it already hit on this flight.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> Hit = new();

    /// <summary>
    /// An item got a fixture to hit people with, as thrown items do.
    /// </summary>
    [ViewVariables]
    public bool HasHitFixture;
}
