using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.Pressure;

/// <summary>
/// What sudden and big changes of pressure do to a body: they burst the eardrums, tear the lungs and,
/// after a long stay under high pressure, boil the gas out of the blood (decompression sickness).
/// The pressure is the one the body feels: a sealed suit keeps its own.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class PressureBodyComponent : Component
{
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField, AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// The pressure the body felt last update, kPa. NaN until the first one.
    /// </summary>
    [ViewVariables]
    public float LastPressure = float.NaN;

    #region Eardrums

    /// <summary>
    /// A rise of this much pressure within a second may burst an eardrum (the middle ear can't take air in that fast).
    /// </summary>
    [DataField]
    public float EarRiseThreshold = 35f;

    /// <summary>
    /// A fall has to be this much: the air in the middle ear escapes on its own through the eustachian tube.
    /// </summary>
    [DataField]
    public float EarFallThreshold = 100f;

    /// <summary>
    /// This much beyond the threshold bursts the eardrum for sure; in between the chance grows.
    /// </summary>
    [DataField]
    public float EarCertainExcess = 100f;

    #endregion

    #region Lungs

    /// <summary>
    /// A fall of this much pressure within a second tears the lungs: the air in them can't get out fast enough.
    /// </summary>
    [DataField]
    public float LungFallThreshold = 30f;

    /// <summary>
    /// Damage to each lung per kPa of the fall beyond the threshold.
    /// </summary>
    [DataField]
    public float LungDamagePerKpa = 0.1f;

    #endregion

    #region Decompression sickness

    /// <summary>
    /// The pressure of the gas dissolved in the body, kPa. It follows the pressure around it, slowly.
    /// </summary>
    [DataField]
    public float TissuePressure = float.NaN;

    /// <summary>
    /// The dissolved gas comes halfway to the pressure around in this time.
    /// </summary>
    [DataField]
    public TimeSpan TissueHalfTime = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Gas comes out of the blood as bubbles when its pressure is this many times the pressure around.
    /// </summary>
    [DataField]
    public float BubbleRatio = 1.6f;

    /// <summary>
    /// The bubbles grow by the excess pressure (in atmospheres) every second and dissolve this fast when there is none.
    /// </summary>
    [DataField]
    public float BubbleDissolveRate = 2f;

    [DataField]
    public float Bubbles;

    /// <summary>
    /// Bubbles in the joints: they ache, the body is slow.
    /// </summary>
    [DataField]
    public float BendsThreshold = 30f;

    /// <summary>
    /// Bubbles in the blood vessels and the nerves: the body is barely moving and suffocating.
    /// </summary>
    [DataField]
    public float SevereThreshold = 90f;

    [DataField]
    public float BubblesMax = 150f;

    [DataField]
    public float BendsSpeedModifier = 0.75f;

    [DataField]
    public float SevereSpeedModifier = 0.4f;

    /// <summary>
    /// Suffocation damage per second of the severe stage: bubbles block the blood.
    /// </summary>
    [DataField]
    public float SevereDamage = 1.5f;

    [DataField, AutoNetworkedField]
    public DecompressionStage Stage;

    #endregion
}

[Serializable, NetSerializable]
public enum DecompressionStage : byte
{
    None,
    Bends,
    Severe,
}
