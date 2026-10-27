using System.Globalization;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.Genetics;

/// <summary>
/// A range of values of the race block (block 2) of the genome.
/// </summary>
[Prototype]
public sealed partial class GenomeRacePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// First value of the range, in hex as the scanner shows it.
    /// </summary>
    [DataField(required: true)]
    public string From = default!;

    /// <summary>
    /// Last value of the range, inclusive, in hex.
    /// </summary>
    [DataField(required: true)]
    public string To = default!;

    /// <summary>
    /// What a human turns into with this race. Null for humans themselves.
    /// </summary>
    [DataField]
    public ProtoId<PolymorphPrototype>? Polymorph;

    public int FromValue => int.Parse(From, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    public int ToValue => int.Parse(To, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    public bool Contains(int value)
    {
        return value >= FromValue && value <= ToValue;
    }
}
