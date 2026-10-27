using Robust.Shared.GameStates;

namespace Content.Shared._EternalWar.Movement;

/// <summary>
/// Running (holding the run key, Shift by default) costs stamina, a little food and more water.
/// Out of breath, the mob can only walk until its stamina comes back.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SprintCostComponent : Component
{
    /// <summary>
    /// Stamina damage per second of running.
    /// </summary>
    [DataField]
    public float StaminaPerSecond = 5f;

    /// <summary>
    /// Hunger lost per second of running, on top of the usual decay (0.0167 a second for a human).
    /// </summary>
    [DataField]
    public float HungerPerSecond = 0.03f;

    /// <summary>
    /// Thirst lost per second of running, on top of the usual decay (0.1 a second for a human).
    /// </summary>
    [DataField]
    public float ThirstPerSecond = 0.25f;

    /// <summary>
    /// Out of breath once stamina damage reaches this part of the crit threshold.
    /// </summary>
    [DataField]
    public float ExhaustedAt = 0.9f;

    /// <summary>
    /// Can run again once stamina damage falls to this part of the crit threshold.
    /// </summary>
    [DataField]
    public float RecoveredAt = 0.5f;

    /// <summary>
    /// Out of breath: running is as slow as walking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Exhausted;

    /// <summary>
    /// Seconds spent running since the cost was last taken.
    /// </summary>
    [ViewVariables]
    public float RunTime;

    [ViewVariables]
    public TimeSpan NextUpdate;
}
