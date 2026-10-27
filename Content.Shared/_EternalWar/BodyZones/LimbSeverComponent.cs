using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// A heavy blade (an axe, a sabre, a sword) that can cut a limb off with a single blow.
/// A melee hit on a limb part that deals <see cref="DamageType"/> cuts it off with <see cref="Chance"/>:
/// the part falls off whole, with everything hanging on it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LimbSeverComponent : Component
{
    /// <summary>
    /// Chance of a hit to cut the limb off, from 0 to 1.
    /// </summary>
    [DataField]
    public float Chance = 0.1f;

    /// <summary>
    /// The chance is multiplied by this when the limb is already broken or out of health.
    /// </summary>
    [DataField]
    public float DamagedMultiplier = 2f;

    /// <summary>
    /// The hit has to actually deal this damage (armor can stop it).
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> DamageType = "Slash";
}
