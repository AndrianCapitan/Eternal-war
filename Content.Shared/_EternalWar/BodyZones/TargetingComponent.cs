using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// Lets a mob choose which body zone its attacks (and topical healing) go to.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TargetingComponent : Component
{
    [DataField, AutoNetworkedField]
    public BodyZone Selected = BodyZone.Chest;
}

/// <summary>
/// Sent by the client when the player picks a zone on the targeting doll.
/// </summary>
[Serializable, NetSerializable]
public sealed class SelectTargetZoneEvent(BodyZone zone) : EntityEventArgs
{
    public readonly BodyZone Zone = zone;
}
