using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.Inserter;

/// <summary>
/// A Factorio-style inserter: takes an item from the tile behind it and puts it on the tile in front of it.
/// Works with the floor (and so with conveyors), crates and lockers, bags and boxes, and machines that take materials.
/// Needs power.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class InserterComponent : Component
{
    /// <summary>
    /// Container that holds the item while the arm carries it.
    /// </summary>
    [DataField]
    public string HandContainerId = "inserter_hand";

    /// <summary>
    /// How long the arm takes to swing from one side to the other.
    /// </summary>
    [DataField]
    public TimeSpan SwingTime = TimeSpan.FromSeconds(0.6);

    /// <summary>
    /// How often to look again when there is nothing to take or no room to put it.
    /// </summary>
    [DataField]
    public TimeSpan RetryTime = TimeSpan.FromSeconds(0.5);

    [ViewVariables]
    public InserterState State = InserterState.AtSource;

    [ViewVariables]
    public TimeSpan NextAction;
}

[Serializable, NetSerializable]
public enum InserterState : byte
{
    AtSource,
    ToTarget,
    AtTarget,
    ToSource,
}

[Serializable, NetSerializable]
public enum InserterVisuals : byte
{
    State,
}
