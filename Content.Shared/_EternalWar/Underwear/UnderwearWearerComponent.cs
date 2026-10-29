using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.Underwear;

/// <summary>
/// A mob that spawns as a player wearing underwear items in its underwear slots, instead of underwear drawn on
/// the body. The underwear chosen in the character editor (an undergarment marking) becomes the matching item;
/// without one the defaults for the sex are worn.
/// </summary>
[RegisterComponent]
public sealed partial class UnderwearWearerComponent : Component
{
    /// <summary>
    /// The item worn for each undergarment marking of the character editor.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<MarkingPrototype>, EntProtoId> Items = new();

    /// <summary>
    /// What is worn in the slots that no chosen marking fills.
    /// </summary>
    [DataField]
    public Dictionary<Sex, List<EntProtoId>> Defaults = new();
}
