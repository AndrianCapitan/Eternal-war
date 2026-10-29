using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.Breasts;

/// <summary>
/// The breast size, chosen in the character editor as a <see cref="Humanoid.HumanoidVisualLayers.Breasts"/> marking
/// on the torso, is told on examine. Without such a marking the breasts are the medium size of the torso sprite and
/// nothing is told.
/// </summary>
[RegisterComponent]
public sealed partial class BreastSizeComponent : Component
{
    /// <summary>
    /// What examining tells for each size marking.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<MarkingPrototype>, LocId> Examine = new();
}
