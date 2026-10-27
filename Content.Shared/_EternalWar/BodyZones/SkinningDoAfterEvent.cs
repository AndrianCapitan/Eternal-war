using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// Taking the skin off a body zone with a sharp tool.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SkinningDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public BodyZone Zone;

    public SkinningDoAfterEvent(BodyZone zone)
    {
        Zone = zone;
    }
}
