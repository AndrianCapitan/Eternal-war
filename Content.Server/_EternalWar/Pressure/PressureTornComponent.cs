using Content.Shared._EternalWar.Pressure;
using Robust.Shared.Prototypes;

namespace Content.Server._EternalWar.Pressure;

/// <summary>
/// A container that burst or was crushed by pressure but is still there: a can split along the seam,
/// a carton torn open. It doesn't hold pressure any more.
/// </summary>
[RegisterComponent]
public sealed partial class PressureTornComponent : Component
{
    [DataField]
    public ProtoId<PressureMaterialPrototype> Material;

    /// <summary>
    /// Burst from inside, or crushed from outside.
    /// </summary>
    [DataField]
    public bool Crushed;
}
