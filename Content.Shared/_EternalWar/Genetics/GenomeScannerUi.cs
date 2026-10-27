using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.Genetics;

[Serializable, NetSerializable]
public enum GenomeScannerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GenomeScannerBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly NetEntity? Target;
    public readonly string TargetName;

    /// <summary>
    /// Null when the target has no genome the scanner can read.
    /// </summary>
    public readonly int[]? Blocks;

    public GenomeScannerBoundUserInterfaceState(NetEntity? target, string targetName, int[]? blocks)
    {
        Target = target;
        TargetName = targetName;
        Blocks = blocks;
    }
}

/// <summary>
/// Writes a new genome into the scanned creature.
/// </summary>
[Serializable, NetSerializable]
public sealed class GenomeScannerApplyMessage : BoundUserInterfaceMessage
{
    public readonly int[] Blocks;

    public GenomeScannerApplyMessage(int[] blocks)
    {
        Blocks = blocks;
    }
}
