using Content.Shared.Alert;
using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.Organs;

/// <summary>
/// What the organs of a body do, besides those of the body zones (see BodyZonesComponent):
/// the tongue speaks, the inner ears hear, the liver and kidneys take the poison, the stomach sorts what was eaten
/// between the liver and the intestines, the appendix clogs up, the spleen makes the blood.
/// An organ that is gone or failed (OrganHealthComponent.Failed) does nothing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganFunctionsComponent : Component
{
    [ViewVariables]
    public TimeSpan NextUpdate;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    #region Speech and hearing

    /// <summary>
    /// Without a working tongue the body can't speak (nor whisper, nor talk over the radio). Emotes still work.
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype> Tongue = "Tongue";

    /// <summary>
    /// The inner ears. Each working one hears its share: with one, speech is heard at half the distance and sounds
    /// at half the volume, with none, nothing at all.
    /// </summary>
    [DataField]
    public List<ProtoId<OrganCategoryPrototype>> Ears = new() { "EarLeft", "EarRight" };

    /// <summary>
    /// How well the body hears, from 0 (deaf) to 1. The client turns the game volume down by it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Hearing = 1f;

    #endregion

    #region Poison

    /// <summary>
    /// The damage type the liver and kidneys take on themselves.
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> PoisonDamage = "Poison";

    /// <summary>
    /// The organs that clean the body of poison and how much poison damage a second each one makes harmless.
    /// Poison over that hits these organs first, the body only once they failed.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, float> Detox = new()
    {
        ["Liver"] = 1f,
        ["KidneyLeft"] = 0.5f,
        ["KidneyRight"] = 0.5f,
    };

    /// <summary>
    /// Poison damage that can still be made harmless right now. Refilled every update by the working organs,
    /// up to two updates worth, so poison metabolized a moment before or after the refill counts the same.
    /// </summary>
    [ViewVariables]
    public float DetoxBudget;

    /// <summary>
    /// Reagents of these groups are poisons: the stomach sends them to the liver.
    /// </summary>
    [DataField]
    public HashSet<string> PoisonGroups = new() { "Toxins" };

    /// <summary>
    /// Reagents of the poison groups that aren't sorted out as poison anyway.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> NotPoisons = new() { "Romerol" };

    #endregion

    #region Digestion

    [DataField]
    public ProtoId<OrganCategoryPrototype> Stomach = "Stomach";

    [DataField]
    public ProtoId<OrganCategoryPrototype> Liver = "Liver";

    [DataField]
    public ProtoId<OrganCategoryPrototype> Intestines = "Intestines";

    [DataField]
    public ProtoId<OrganCategoryPrototype> Appendix = "Appendix";

    /// <summary>
    /// Units a second the stomach passes on: the poisons to the liver, the rest to the intestines.
    /// </summary>
    [DataField]
    public FixedPoint2 StomachTransferRate = 2;

    /// <summary>
    /// The eaten poisons the liver holds back (a solution on the body). What doesn't fit goes into the blood.
    /// </summary>
    [DataField]
    public string LiverSolution = "liver";

    [DataField]
    public FixedPoint2 LiverVolume = 10;

    /// <summary>
    /// Units a second of the held back poison the liver breaks down. A failed or missing liver lets it all into
    /// the blood at the stomach's rate.
    /// </summary>
    [DataField]
    public FixedPoint2 LiverBreakdownRate = 0.2;

    /// <summary>
    /// What the intestines absorb (a solution on the body): the intestines digest it as upstream's stomach did,
    /// so food feeds and pills get into the blood from here.
    /// </summary>
    [DataField]
    public string IntestinesSolution = "intestines";

    [DataField]
    public FixedPoint2 IntestinesVolume = 150;

    #endregion

    #region Appendix

    /// <summary>
    /// The appendix's contents, a solution on the body.
    /// </summary>
    [DataField]
    public string AppendixSolution = "appendix";

    [DataField]
    public FixedPoint2 AppendixVolume = 10;

    /// <summary>
    /// Chance that a portion of food passed on by the stomach goes into the appendix instead of the intestines.
    /// </summary>
    [DataField]
    public float AppendixChance = 0.03f;

    /// <summary>
    /// Units a second an inflamed appendix empties by itself. From full to <see cref="AppendixRecoveredAt"/>:
    /// 5 units in 10 minutes.
    /// </summary>
    [DataField]
    public FixedPoint2 AppendixDrainRate = 0.0083;

    /// <summary>
    /// The inflammation passes once the appendix is no fuller than this.
    /// </summary>
    [DataField]
    public float AppendixRecoveredAt = 0.5f;

    /// <summary>
    /// Time between two bouts of vomiting while the appendix is inflamed.
    /// </summary>
    [DataField]
    public TimeSpan AppendixVomitMin = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan AppendixVomitMax = TimeSpan.FromSeconds(180);

    /// <summary>
    /// The first vomiting comes this soon after the appendix got inflamed.
    /// </summary>
    [DataField]
    public TimeSpan AppendixFirstVomit = TimeSpan.FromSeconds(15);

    [DataField]
    public ProtoId<AlertPrototype> AppendixAlert = "EternalWarAppendicitis";

    [DataField]
    public bool Appendicitis;

    [ViewVariables]
    public TimeSpan NextVomit;

    #endregion

    #region Spleen

    [DataField]
    public ProtoId<OrganCategoryPrototype> Spleen = "Spleen";

    /// <summary>
    /// Units of blood the spleen makes every bloodstream update, in place of the body's own regeneration
    /// (BloodstreamComponent.BloodRefreshAmount, set to 0). Null: take it from the bloodstream when the body spawns.
    /// </summary>
    [DataField]
    public FixedPoint2? SpleenBloodRefresh;

    [ViewVariables]
    public TimeSpan NextSpleenUpdate;

    #endregion
}
