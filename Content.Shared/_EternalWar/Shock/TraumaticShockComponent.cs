using Content.Shared.Alert;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.Shock;

/// <summary>
/// A lot of trauma in a short time puts the mob into shock: it falls, drops what it holds and for a while is slow,
/// sees blurry, shakes and stutters. Painkillers keep it out of shock, adrenaline ends it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class TraumaticShockComponent : Component
{
    /// <summary>
    /// Damage of these groups, taken within <see cref="Window"/>, adds up to the shock.
    /// Poison, suffocation, bloodloss and radiation hurt slowly from inside and don't count.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageGroupPrototype>> DamageGroups = new() { "Brute", "Burn" };

    /// <summary>
    /// This much damage within <see cref="Window"/> is a shock.
    /// </summary>
    [DataField]
    public FixedPoint2 Threshold = 35;

    /// <summary>
    /// How long a hit counts towards the shock.
    /// </summary>
    [DataField]
    public TimeSpan Window = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long the mob lies on the ground, having dropped what it held.
    /// </summary>
    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long the shock lasts, counting the time on the ground.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Walking and running speed in shock.
    /// </summary>
    [DataField]
    public float SpeedModifier = 0.6f;

    /// <summary>
    /// Blur added to the eyes in shock (6 is the most there is, a missing eye is 2).
    /// </summary>
    [DataField]
    public float Blur = 3f;

    [DataField]
    public float JitterAmplitude = 10f;

    [DataField]
    public float JitterFrequency = 4f;

    [DataField]
    public EntProtoId StutterEffect = "StatusEffectStutter";

    [DataField]
    public ProtoId<AlertPrototype> Alert = "EternalWarShock";

    /// <summary>
    /// In shock right now.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool InShock;

    /// <summary>
    /// When the shock wears off.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ShockEnd;

    /// <summary>
    /// Trauma taken lately: when, and how much.
    /// </summary>
    [ViewVariables]
    public List<(TimeSpan Time, FixedPoint2 Amount)> RecentDamage = new();
}
