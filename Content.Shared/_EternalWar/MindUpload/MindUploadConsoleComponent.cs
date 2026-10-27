using Robust.Shared.Audio;

namespace Content.Shared._EternalWar.MindUpload;

/// <summary>
/// Console that copies a mind from a brain or a personal AI into a switched off positronic brain.
/// </summary>
[RegisterComponent]
public sealed partial class MindUploadConsoleComponent : Component
{
    /// <summary>
    /// Item slot for the positronic brain that receives the mind.
    /// </summary>
    [DataField]
    public string PositronicSlotId = "positronic_slot";

    /// <summary>
    /// Item slot for the brain or personal AI the mind is taken from.
    /// </summary>
    [DataField]
    public string SourceSlotId = "source_slot";

    /// <summary>
    /// How long it takes to upload a mind from an organic brain.
    /// </summary>
    [DataField]
    public TimeSpan BrainUploadTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long it takes to upload a mind from a personal AI.
    /// </summary>
    [DataField]
    public TimeSpan PaiUploadTime = TimeSpan.FromSeconds(15);

    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    [DataField]
    public SoundSpecifier CompleteSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");

    [DataField]
    public SoundSpecifier FailSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    /// <summary>
    /// When the current upload started. Null if nothing is being uploaded.
    /// </summary>
    [ViewVariables]
    public TimeSpan? UploadStart;

    /// <summary>
    /// When the current upload will finish. Null if nothing is being uploaded.
    /// </summary>
    [ViewVariables]
    public TimeSpan? UploadEnd;
}
