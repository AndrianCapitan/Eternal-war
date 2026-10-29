namespace Content.Shared._EternalWar.Underwear;

/// <summary>
/// An underwear item, worn in the underwearTop or underwearBottom slot. It can't be put on or taken off while
/// clothes over it are worn: the jumpsuit has to come off first.
/// </summary>
[RegisterComponent]
public sealed partial class UnderwearComponent : Component
{
    /// <summary>
    /// The slots whose clothes cover the underwear.
    /// </summary>
    [DataField]
    public List<string> CoveredBy = new() { "jumpsuit", "outerClothing" };
}
