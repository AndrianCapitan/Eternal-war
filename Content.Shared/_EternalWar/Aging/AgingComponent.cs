using Content.Shared.Body;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._EternalWar.Aging;

/// <summary>
/// The age of a humanoid tells on its body: an old one is slower, tires sooner, breaks bones easier and heals slower,
/// a frail one even more so, and at <see cref="DeathAge"/> the heart gives out, then the brain, and it dies for good.
/// The age itself is <see cref="Content.Shared.Humanoid.HumanoidProfileComponent.Age"/>; the time ray adds years to it.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class AgingComponent : Component
{
    #region Stages

    /// <summary>
    /// From this age the body is old.
    /// </summary>
    [DataField]
    public int OldAge = 60;

    /// <summary>
    /// From this age the body is frail.
    /// </summary>
    [DataField]
    public int FrailAge = 80;

    /// <summary>
    /// At this age the organs give out one by one (<see cref="FailingOrgans"/>) and the body dies for good.
    /// </summary>
    [DataField]
    public int DeathAge = 120;

    [DataField]
    public float OldSpeed = 0.85f;

    [DataField]
    public float FrailSpeed = 0.7f;

    /// <summary>
    /// How much stamina damage knocks an old body down, as a share of the usual.
    /// </summary>
    [DataField]
    public float OldStamina = 0.8f;

    [DataField]
    public float FrailStamina = 0.6f;

    /// <summary>
    /// Damage to the bones of an old body is multiplied by this.
    /// </summary>
    [DataField]
    public float OldBoneFragility = 1.25f;

    [DataField]
    public float FrailBoneFragility = 1.5f;

    /// <summary>
    /// Wounds heal by themselves and splinted bones grow together this much as fast as usual in an old body.
    /// </summary>
    [DataField]
    public float OldHealing = 0.75f;

    [DataField]
    public float FrailHealing = 0.5f;

    #endregion

    #region Looks

    /// <summary>
    /// The hair starts going grey when the body is aged past this...
    /// </summary>
    [DataField]
    public int GreyingAge = 40;

    /// <summary>
    /// ...and is all <see cref="GreyHair"/> at this age.
    /// </summary>
    [DataField]
    public int GreyAge = 90;

    [DataField]
    public Color GreyHair = Color.FromHex("#D6D6D2");

    /// <summary>
    /// The share of the skin color saturation lost by <see cref="GreyAge"/>: old skin goes sallow.
    /// </summary>
    [DataField]
    public float SkinFading = 0.3f;

    #endregion

    #region Dying of old age

    /// <summary>
    /// These organs give out in this order once the body reaches <see cref="DeathAge"/>.
    /// Losing the brain kills for good.
    /// </summary>
    [DataField]
    public List<ProtoId<OrganCategoryPrototype>> FailingOrgans = new() { "Heart", "Brain" };

    /// <summary>
    /// Damage a second to the organ that is giving out.
    /// </summary>
    [DataField]
    public float OrganDecay = 2f;

    [DataField]
    public TimeSpan DyingInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDyingUpdate;

    /// <summary>
    /// The body reached <see cref="DeathAge"/> and its organs are giving out.
    /// </summary>
    [DataField]
    public bool Dying;

    #endregion

    #region What the body was before it was aged

    /// <summary>
    /// The age before anything aged the body. Rejuvenating brings it back.
    /// </summary>
    [DataField]
    public int? OriginalAge;

    [DataField]
    public Color? OriginalHair;

    [DataField]
    public Color? OriginalBeard;

    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, Color>? OriginalSkin;

    /// <summary>
    /// How often the body heals by itself (<see cref="Content.Shared.Damage.Components.PassiveDamageComponent.Interval"/>)
    /// at full speed, before age slowed it.
    /// </summary>
    [DataField]
    public TimeSpan? BaseRegenInterval;

    /// <summary>
    /// Parts of a year the body was aged by that don't make a whole year yet.
    /// </summary>
    [DataField]
    public float PendingYears;

    /// <summary>
    /// Whether dying of old age made the body unrevivable (so rejuvenating can undo it).
    /// </summary>
    [DataField]
    public bool MadeUnrevivable;

    #endregion
}

public enum AgeStage : byte
{
    Adult,
    Old,
    Frail,
    Dying,
}
