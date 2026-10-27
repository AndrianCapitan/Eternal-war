using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// The circulation of a body with <see cref="BodyZonesComponent"/>. The heart pumps blood that the lungs filled with
/// oxygen (the respirator's saturation) into the chest; the blood vessels of each zone pass it on to the zones
/// attached to it, so every zone gets blood only while the vessels on the way to it are whole and no tourniquet
/// is in between. The muscles, bones and organs of a zone live off that oxygen: without it the zone uses up its
/// reserve, goes numb and, if it stays without blood long enough, dies.
/// The brain is in the head: when it runs out of oxygen the body suffocates.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CirculationComponent : Component
{
    /// <summary>
    /// How often the circulation is updated.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// How many seconds a zone lives on the oxygen it already has once blood stops reaching it.
    /// </summary>
    [DataField]
    public float OxygenReserve = 60f;

    /// <summary>
    /// The brain uses oxygen fast: it lasts only this many seconds without fresh blood.
    /// </summary>
    [DataField]
    public float BrainOxygenReserve = 5f;

    /// <summary>
    /// The zone holding the brain.
    /// </summary>
    [DataField]
    public BodyZone BrainZone = BodyZone.Head;

    /// <summary>
    /// A limb part that has been out of oxygen this many seconds (after its reserve ran out) dies for good.
    /// Only the limbs die like this: the head and the torso kill the body by suffocation first.
    /// </summary>
    [DataField]
    public float NecrosisTime = 180f;

    /// <summary>
    /// Pumps the blood. Without it, or while the body is dead, no blood goes anywhere.
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype> Heart = "Heart";

    /// <summary>
    /// With less blood than this (share of the normal volume) the heart has nothing to pump: no zone gets oxygen.
    /// </summary>
    [DataField]
    public float MinBloodLevel = 0.5f;

    /// <summary>
    /// Damage every update while the brain has no oxygen. This is the body's suffocation:
    /// the respirator itself deals none (see <c>species_base.yml</c>).
    /// </summary>
    [DataField]
    public DamageSpecifier BrainHypoxiaDamage = new() { DamageDict = new() { ["Asphyxiation"] = FixedPoint2.New(0.5) } };

    /// <summary>
    /// Healing every update while the brain gets oxygen again.
    /// </summary>
    [DataField]
    public DamageSpecifier BrainRecovery = new() { DamageDict = new() { ["Asphyxiation"] = FixedPoint2.New(-0.5) } };

    /// <summary>
    /// Damage every update for every dead limb part still on the body: it rots and poisons the blood until cut off.
    /// </summary>
    [DataField]
    public DamageSpecifier NecrosisDamage = new() { DamageDict = new() { ["Poison"] = FixedPoint2.New(0.05) } };

    /// <summary>
    /// Share of the blood from a cut artery that sprays a step away instead of pouring down at the feet.
    /// </summary>
    [DataField]
    public float FountainSprayShare = 0.4f;

    /// <summary>
    /// How often the "blood gushes out" popup is shown while a stump bleeds.
    /// </summary>
    [DataField]
    public TimeSpan FountainPopupInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan NextFountainPopup;

    /// <summary>
    /// What a tourniquet taken off the body turns back into.
    /// </summary>
    [DataField]
    public EntProtoId TourniquetPrototype = "Tourniquet";

    /// <summary>
    /// Limb zones with a tourniquet. A tourniquet on a missing zone sits on its stump.
    /// No blood passes it: the stump below stops bleeding, but the zone and everything below it get no oxygen.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<BodyZone> Tourniquets = new();

    /// <summary>
    /// Lost limb parts whose stump bleeds, with the blood lost per second.
    /// The key is the topmost lost part, e.g. the forearm when the arm was lost below the elbow.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<BodyZone, float> Stumps = new();

    /// <summary>
    /// Limb zones that ran out of oxygen: they don't work until blood comes back.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<BodyZone> Numb = new();

    /// <summary>
    /// Limb zones that died without oxygen. They never work again and poison the body until cut off.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<BodyZone> Necrotic = new();

    /// <summary>
    /// Seconds of oxygen left in each zone that blood doesn't reach. Zones with blood aren't listed (full).
    /// </summary>
    [DataField]
    public Dictionary<BodyZone, float> Oxygen = new();

    /// <summary>
    /// How long each zone has been without oxygen after its reserve ran out.
    /// </summary>
    [DataField]
    public Dictionary<BodyZone, float> Hypoxia = new();
}
