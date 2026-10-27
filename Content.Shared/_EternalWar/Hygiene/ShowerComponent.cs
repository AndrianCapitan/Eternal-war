using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.Hygiene;

/// <summary>
/// A shower: turned on and off by hand. While it runs, everyone standing under it (on its tile) is washed:
/// the blood comes off their clothes and things, their feet stop leaving prints and fire goes out.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShowerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    /// <summary>
    /// How much blood (from 0 to 1) comes off every item each second.
    /// </summary>
    [DataField]
    public float WashRate = 0.25f;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan NextUpdate;

    [DataField]
    public SoundSpecifier? ToggleSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");
}

[Serializable, NetSerializable]
public enum ShowerVisuals : byte
{
    Enabled,
}
