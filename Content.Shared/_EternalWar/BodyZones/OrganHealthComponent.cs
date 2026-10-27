using Robust.Shared.GameStates;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// The health of one piece of a body: the skin of a zone, its muscles or vessels, its bone, an organ.
/// Hits on the zone go through its layers (see <see cref="LayerPenetration"/>) and take this health away:
/// the skin is torn off and the muscles and vessels torn at 0, a bone cracks and breaks
/// (<see cref="BoneComponent"/>), an organ fails.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganHealthComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MaxHealth = 20f;

    [DataField, AutoNetworkedField]
    public float Damage;

    /// <summary>
    /// The organ was crushed to nothing and doesn't work any more: a heart doesn't pump, a lung doesn't breathe,
    /// an eye doesn't see. It doesn't come back by healing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Failed;

    public float Health => MaxHealth - Damage;
}
