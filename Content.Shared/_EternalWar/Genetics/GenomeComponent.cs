using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.Genetics;

/// <summary>
/// The genome of a human. Added the first time the human is scanned, read from their appearance.
/// </summary>
[RegisterComponent]
public sealed partial class GenomeComponent : Component
{
    /// <summary>
    /// Block values, indexed by <see cref="GenomeBlock"/>.
    /// </summary>
    [DataField]
    public int[] Blocks = new int[Genome.BlockCount];

    /// <summary>
    /// The animal this human was turned into by the race block, if any.
    /// While it exists the human waits on the paused map.
    /// </summary>
    [DataField]
    public EntityUid? Form;

    [DataField]
    public ProtoId<PolymorphPrototype>? FormPolymorph;
}

/// <summary>
/// An animal made from a human by the race block. Its genome is kept on <see cref="Original"/>.
/// </summary>
[RegisterComponent]
public sealed partial class GenomeFormComponent : Component
{
    [DataField]
    public EntityUid Original;
}

/// <summary>
/// Marks a monkey accent that was added by a low sentience block, so only that one is removed again.
/// </summary>
[RegisterComponent]
public sealed partial class GenomeMonkeyAccentComponent : Component;
