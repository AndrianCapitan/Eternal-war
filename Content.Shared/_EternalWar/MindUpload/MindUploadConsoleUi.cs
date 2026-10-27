using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.MindUpload;

[Serializable, NetSerializable]
public enum MindUploadConsoleUiKey : byte
{
    Key,
}

/// <summary>
/// What kind of mind carrier sits in the source slot.
/// </summary>
[Serializable, NetSerializable]
public enum MindUploadSourceType : byte
{
    None,
    Brain,
    Pai,
}

[Serializable, NetSerializable]
public sealed class MindUploadConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly NetEntity? Positronic;
    public readonly NetEntity? Source;
    public readonly MindUploadSourceType SourceType;

    /// <summary>
    /// Whether the source actually carries a mind that can be uploaded.
    /// </summary>
    public readonly bool SourceHasMind;

    public readonly bool Powered;
    public readonly TimeSpan? UploadStart;
    public readonly TimeSpan? UploadEnd;

    public MindUploadConsoleBoundUserInterfaceState(
        NetEntity? positronic,
        NetEntity? source,
        MindUploadSourceType sourceType,
        bool sourceHasMind,
        bool powered,
        TimeSpan? uploadStart,
        TimeSpan? uploadEnd)
    {
        Positronic = positronic;
        Source = source;
        SourceType = sourceType;
        SourceHasMind = sourceHasMind;
        Powered = powered;
        UploadStart = uploadStart;
        UploadEnd = uploadEnd;
    }
}

[Serializable, NetSerializable]
public sealed class MindUploadConsoleStartMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MindUploadConsoleEjectPositronicMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MindUploadConsoleEjectSourceMessage : BoundUserInterfaceMessage;
