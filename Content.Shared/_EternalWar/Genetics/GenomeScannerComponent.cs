using Robust.Shared.GameStates;

namespace Content.Shared._EternalWar.Genetics;

/// <summary>
/// Admin-only device that reads and rewrites the genome of whoever it is used on.
/// Only admins can pick it up.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GenomeScannerComponent : Component
{
    /// <summary>
    /// The creature scanned last, shown in the interface.
    /// </summary>
    [ViewVariables]
    public EntityUid? Target;
}
