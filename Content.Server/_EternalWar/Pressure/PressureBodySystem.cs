using Content.Server._EternalWar.BodyZones;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Pressure;
using Content.Shared.Administration.Logs;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._EternalWar.Pressure;

/// <summary>
/// Eardrums, lungs and decompression sickness, see <see cref="PressureBodyComponent"/>.
/// </summary>
public sealed partial class PressureBodySystem : SharedPressureBodySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private BarotraumaSystem _barotrauma = default!;
    [Dependency] private BodyZonesSystem _zones = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly ProtoId<OrganCategoryPrototype>[] Ears = { "EarLeft", "EarRight" };
    private static readonly ProtoId<OrganCategoryPrototype>[] Lungs = { "LungLeft", "LungRight" };
    private static readonly ProtoId<DamageTypePrototype> BubbleDamage = "Asphyxiation";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PressureBodyComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PressureBodyComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnMapInit(Entity<PressureBodyComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.UpdateInterval;
    }

    private void OnRejuvenate(Entity<PressureBodyComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.LastPressure = float.NaN;
        ent.Comp.TissuePressure = float.NaN;
        ent.Comp.Bubbles = 0f;
        SetStage(ent, DecompressionStage.None);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<PressureBodyComponent, BodyZonesComponent>();
        while (query.MoveNext(out var uid, out var comp, out var zones))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + comp.UpdateInterval;

            if (!_mobState.IsAlive(uid) && !_mobState.IsCritical(uid))
                continue;

            if (GetFeltPressure(uid) is not { } pressure)
                continue;

            var time = (float) comp.UpdateInterval.TotalSeconds;
            var change = float.IsNaN(comp.LastPressure) ? 0f : pressure - comp.LastPressure;
            comp.LastPressure = pressure;

            UpdateEardrums((uid, comp, zones), change);
            UpdateLungs((uid, comp, zones), change);
            UpdateDecompression((uid, comp), pressure, time);
        }
    }

    /// <summary>
    /// The pressure on the body, kPa: the air around, or what a sealed suit keeps inside.
    /// </summary>
    public float? GetFeltPressure(EntityUid uid)
    {
        if (Transform(uid).MapUid == null || _atmos.GetContainingMixture(uid) is not { } air)
            return null;

        var pressure = air.Pressure;
        if (!TryComp<BarotraumaComponent>(uid, out var barotrauma))
            return pressure;

        return pressure < Atmospherics.OneAtmosphere
            ? _barotrauma.GetFeltLowPressure(uid, barotrauma, pressure)
            : _barotrauma.GetFeltHighPressure(uid, barotrauma, pressure);
    }

    #region Eardrums and lungs

    private void UpdateEardrums(Entity<PressureBodyComponent, BodyZonesComponent> ent, float change)
    {
        var threshold = change >= 0f ? ent.Comp1.EarRiseThreshold : ent.Comp1.EarFallThreshold;
        var excess = MathF.Abs(change) - threshold;
        if (excess < 0f)
            return;

        // At the threshold one eardrum in four bursts, at the threshold and the certain excess every one.
        var chance = Math.Clamp(0.25f + 0.75f * excess / ent.Comp1.EarCertainExcess, 0f, 1f);
        foreach (var category in Ears)
        {
            foreach (var ear in _zones.GetOrgans(ent, category))
            {
                if (!TryComp<OrganHealthComponent>(ear, out var health) || health.Failed || !_random.Prob(chance))
                    continue;

                _zones.DamagePiece((ent, ent.Comp2), ear, health.Health);
                _popup.PopupEntity(Loc.GetString("pressure-eardrum-burst"), ent, ent, PopupType.LargeCaution);
                _adminLogger.Add(LogType.Damaged, LogImpact.Medium,
                    $"{ToPrettyString(ent):target} had an eardrum burst by a pressure change of {change:F0} kPa");
            }
        }
    }

    private void UpdateLungs(Entity<PressureBodyComponent, BodyZonesComponent> ent, float change)
    {
        var excess = -change - ent.Comp1.LungFallThreshold;
        if (excess <= 0f)
            return;

        var damage = excess * ent.Comp1.LungDamagePerKpa;
        var hurt = false;
        foreach (var category in Lungs)
        {
            foreach (var lung in _zones.GetOrgans(ent, category))
            {
                if (!TryComp<OrganHealthComponent>(lung, out var health) || health.Failed)
                    continue;

                _zones.DamagePiece((ent, ent.Comp2), lung, damage);
                hurt = true;
            }
        }

        if (!hurt)
            return;

        _popup.PopupEntity(Loc.GetString("pressure-lungs-torn"), ent, ent, PopupType.LargeCaution);
        _adminLogger.Add(LogType.Damaged, LogImpact.Medium,
            $"{ToPrettyString(ent):target} had their lungs torn by a pressure fall of {-change:F0} kPa");
    }

    #endregion

    #region Decompression sickness

    private void UpdateDecompression(Entity<PressureBodyComponent> ent, float pressure, float time)
    {
        var comp = ent.Comp;
        if (float.IsNaN(comp.TissuePressure))
            comp.TissuePressure = pressure;

        // The dissolved gas follows the pressure around, halfway in a half-time.
        var rate = MathF.Log(2f) / (float) comp.TissueHalfTime.TotalSeconds;
        comp.TissuePressure += (pressure - comp.TissuePressure) * (1f - MathF.Exp(-rate * time));

        // Supersaturation: the gas comes out of the blood as bubbles, in atmospheres of excess.
        var excess = (comp.TissuePressure - comp.BubbleRatio * pressure) / Atmospherics.OneAtmosphere;
        comp.Bubbles = excess > 0f
            ? MathF.Min(comp.Bubbles + excess * time, comp.BubblesMax)
            : MathF.Max(comp.Bubbles - comp.BubbleDissolveRate * time, 0f);

        var stage = comp.Bubbles >= comp.SevereThreshold ? DecompressionStage.Severe
            : comp.Bubbles >= comp.BendsThreshold ? DecompressionStage.Bends
            : DecompressionStage.None;
        SetStage(ent, stage);

        if (stage == DecompressionStage.Severe)
        {
            _damageable.TryChangeDamage(ent.Owner,
                new DamageSpecifier(_proto.Index(BubbleDamage), comp.SevereDamage * time),
                ignoreResistances: true,
                interruptsDoAfters: false);
        }
    }

    private void SetStage(Entity<PressureBodyComponent> ent, DecompressionStage stage)
    {
        if (ent.Comp.Stage == stage)
            return;

        var worse = stage > ent.Comp.Stage;
        ent.Comp.Stage = stage;
        Dirty(ent);
        Speed.RefreshMovementSpeedModifiers(ent.Owner);

        var message = stage switch
        {
            DecompressionStage.Bends when worse => "pressure-decompression-bends",
            DecompressionStage.Severe => "pressure-decompression-severe",
            DecompressionStage.Bends => "pressure-decompression-better",
            _ => "pressure-decompression-gone",
        };
        _popup.PopupEntity(Loc.GetString(message), ent, ent, worse ? PopupType.LargeCaution : PopupType.Medium);

        if (worse)
            _adminLogger.Add(LogType.Damaged, LogImpact.Medium, $"{ToPrettyString(ent):target} got decompression sickness ({stage})");
    }

    #endregion
}
