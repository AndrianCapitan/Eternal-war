using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// Sets a broken bone in the zone the user aims at. Used up afterwards.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SplintComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Splinting yourself is awkward and takes longer.
    /// </summary>
    [DataField]
    public float SelfDelayMultiplier = 2f;

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/Medical/brutepack_end.ogg");
}

[Serializable, NetSerializable]
public sealed partial class SplintDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public BodyZone Zone;

    public SplintDoAfterEvent(BodyZone zone)
    {
        Zone = zone;
    }
}
