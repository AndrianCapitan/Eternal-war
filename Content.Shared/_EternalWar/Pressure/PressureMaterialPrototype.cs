using Content.Shared.Damage.Prototypes;
using Content.Shared.Materials;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.Pressure;

/// <summary>
/// How a material stands pressure and blows: when a sealed container of it bursts or is crushed, what is left of it,
/// how fast it may hit something before it breaks. Things get it from what they are made of
/// (<c>PhysicalComposition</c>), so the items themselves need no marking.
/// All pressures are differences across the wall, in kPa.
/// </summary>
[Prototype]
public sealed partial class PressureMaterialPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Things made mostly of these materials are of this one.
    /// </summary>
    [DataField]
    public List<ProtoId<MaterialPrototype>> Materials = new();

    /// <summary>
    /// The least amount of the material (in <c>PhysicalComposition</c> units) for this one: a thin can and a thick flask
    /// are both steel, but only one of them is thin. Of the matches the one with the highest minimum wins.
    /// </summary>
    [DataField]
    public int MinAmount;

    /// <summary>
    /// The inside is this much above the outside: the container bursts.
    /// </summary>
    [DataField]
    public float BurstPressure = float.PositiveInfinity;

    /// <summary>
    /// The outside is this much above the inside: the container is crushed.
    /// A container mostly full of liquid isn't: the liquid holds the walls.
    /// </summary>
    [DataField]
    public float CrushPressure = float.PositiveInfinity;

    /// <summary>
    /// Carried by space wind into a wall or a person at this speed (m/s), it breaks.
    /// </summary>
    [DataField]
    public float ImpactSpeed = float.PositiveInfinity;

    /// <summary>
    /// What is left of a broken thing.
    /// </summary>
    [DataField]
    public PressureBreakage Breakage = PressureBreakage.Destroyed;

    /// <summary>
    /// What it breaks into, left where it broke.
    /// </summary>
    [DataField]
    public EntProtoId? Debris;

    /// <summary>
    /// The share of the energy of a burst that the pieces of the wall carry away (glass flies apart, metal tears).
    /// </summary>
    [DataField]
    public float ShrapnelShare;

    /// <summary>
    /// The damage the flying pieces do.
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> ShrapnelDamage = "Slash";

    [DataField]
    public SoundSpecifier? BreakSound;
}

public enum PressureBreakage : byte
{
    /// <summary>
    /// Smashed to pieces: the thing is gone (glass, plastic).
    /// </summary>
    Destroyed,

    /// <summary>
    /// Torn or crumpled, open and empty, but still there (metal, cardboard).
    /// </summary>
    Torn,

    /// <summary>
    /// Whole, but doesn't work until repaired (electronics).
    /// </summary>
    Disabled,
}
