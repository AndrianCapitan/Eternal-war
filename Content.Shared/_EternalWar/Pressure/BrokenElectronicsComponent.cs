using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.Pressure;

/// <summary>
/// The electronics of a device were crushed by pressure or broken by a blow: it doesn't switch on and its screen
/// doesn't open until someone opens it up and puts it right with a screwdriver.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BrokenElectronicsComponent : Component
{
    [DataField]
    public ProtoId<ToolQualityPrototype> RepairQuality = "Screwing";

    [DataField]
    public TimeSpan RepairTime = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Putting broken electronics right.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RepairElectronicsDoAfterEvent : SimpleDoAfterEvent;
