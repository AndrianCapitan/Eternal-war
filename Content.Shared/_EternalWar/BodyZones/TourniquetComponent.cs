using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// Tied around the arm or leg the user aims at, or around the stump of a lost one: stops the blood going past it.
/// Taken off again with a verb on the patient.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TourniquetComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Tying it on yourself with one hand takes longer.
    /// </summary>
    [DataField]
    public float SelfDelayMultiplier = 2f;

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/Medical/brutepack_end.ogg");
}

/// <summary>
/// Marks the blood vessels of a body zone: blood reaches the zone and the zones attached to it only through them.
/// </summary>
[RegisterComponent]
public sealed partial class BloodVesselComponent : Component;

[Serializable, NetSerializable]
public sealed partial class TourniquetDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public BodyZone Zone;

    public TourniquetDoAfterEvent(BodyZone zone)
    {
        Zone = zone;
    }
}
