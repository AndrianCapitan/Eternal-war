using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.Hygiene;

/// <summary>
/// Walking through a puddle leaves footprints of its color on the next tiles: shoe prints or bare feet.
/// Stepping into blood also stains the shoes.
/// </summary>
[RegisterComponent]
public sealed partial class FootprintsComponent : Component
{
    /// <summary>
    /// How many footprints a puddle is good for. They fade as they run out.
    /// </summary>
    [DataField]
    public int MaxPrints = 12;

    /// <summary>
    /// A puddle smaller than this leaves no prints.
    /// </summary>
    [DataField]
    public FixedPoint2 MinPuddleVolume = 2;

    /// <summary>
    /// How much blood the shoes take from stepping into a bloody puddle.
    /// </summary>
    [DataField]
    public float ShoeStain = 0.3f;

    /// <summary>
    /// Reagents that are blood: stepping into them stains the shoes.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> BloodReagents = new()
    {
        "Blood", "InsectBlood", "Slime", "CopperBlood", "AmmoniaBlood", "SulfurBlood", "ZombieBlood",
    };

    [DataField]
    public string ShoeDecal = "FootprintShoe";

    [DataField]
    public string BareDecal = "FootprintBare";

    /// <summary>
    /// The feet are this far from the middle of the body, across the way it walks.
    /// </summary>
    [DataField]
    public float FootOffset = 0.1f;

    /// <summary>
    /// Prints left before the feet are clean.
    /// </summary>
    [DataField]
    public int PrintsLeft;

    [DataField]
    public Color PrintColor = Color.FromHex("#800000");

    /// <summary>
    /// Which foot goes next.
    /// </summary>
    [DataField]
    public bool LeftFoot;

    [ViewVariables]
    public EntityUid? LastGrid;

    [ViewVariables]
    public Vector2i LastTile;

    [ViewVariables]
    public EntityCoordinates? LastPosition;
}
