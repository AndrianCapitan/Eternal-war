using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// A part of the body that came off whole (a severed arm, a hand off a destroyed forearm).
/// A sharp tool takes it apart: the bones, skin, muscles and vessels inside fall out one by one
/// and nothing of the part itself is left.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SeveredPartComponent : Component
{
    /// <summary>
    /// A tool of this quality (a knife, a scalpel) takes the part apart. Melee weapons that slash can too.
    /// </summary>
    [DataField]
    public ProtoId<ToolQualityPrototype> DismantleQuality = "Slicing";

    [DataField]
    public TimeSpan DismantleDelay = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Taking a severed part apart with a sharp tool.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class PartDismantleDoAfterEvent : SimpleDoAfterEvent;
