using Content.Shared.Body;
using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// Bones still held together: a skull with its jaw, a whole spine, a skeleton pulled out of a body.
/// The bones themselves lie in the <see cref="ContainerId"/> container and keep their health and fractures.
/// Each combination of bones is its own prototype; bones that fit none come out one by one.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BoneAssemblyComponent : Component
{
    public const string ContainerId = "bones";

    /// <summary>
    /// The organ categories of the bones this is made of.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<OrganCategoryPrototype>> Bones = new();

    /// <summary>
    /// The bones pulled off by hand (the take-apart key): the skull off a skeleton, the jaw off a skull.
    /// </summary>
    [DataField]
    public List<ProtoId<OrganCategoryPrototype>> TakeOut = new();

    /// <summary>
    /// Too strong to pull apart by hand: these bones come off when it is sawn through (the take-apart key with
    /// a saw in the active hand) or broken with crushing blows.
    /// </summary>
    [DataField]
    public List<ProtoId<OrganCategoryPrototype>> SplitOff = new();

    [DataField]
    public ProtoId<ToolQualityPrototype> SawQuality = "Sawing";

    [DataField]
    public TimeSpan SawDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    /// A crushing blow weaker than this does nothing.
    /// </summary>
    [DataField]
    public float BluntThreshold = 5f;

    /// <summary>
    /// The chance that a crushing blow breaks a whole one in two.
    /// </summary>
    [DataField]
    public float SplitChance = 0.2f;

    /// <summary>
    /// The chance that a crushing blow cracks a whole one, if it didn't break it in two.
    /// </summary>
    [DataField]
    public float BreakChance = 0.3f;

    /// <summary>
    /// The chance that a crushing blow breaks one that is already broken in two.
    /// </summary>
    [DataField]
    public float BrokenSplitChance = 0.5f;

    [DataField, AutoNetworkedField]
    public bool Broken;
}

/// <summary>
/// The take-apart key pressed on a hand of the HUD: works on what that hand holds.
/// </summary>
[Serializable, NetSerializable]
public sealed class TakeApartInHandEvent(string handId) : EntityEventArgs
{
    public readonly string HandId = handId;
}

/// <summary>
/// Sawing bones in two.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BoneSawDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Pulling the bones laid bare out of a body.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BoneExtractDoAfterEvent : SimpleDoAfterEvent;
