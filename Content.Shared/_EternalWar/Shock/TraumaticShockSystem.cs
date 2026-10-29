using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using OldStatusEffectsSystem = Content.Shared.StatusEffect.StatusEffectsSystem;

namespace Content.Shared._EternalWar.Shock;

/// <summary>
/// Puts mobs that took a lot of trauma at once into shock, see <see cref="TraumaticShockComponent"/>.
/// The shock starts and ends on the server, the client only predicts the speed.
/// </summary>
public sealed partial class TraumaticShockSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private BlurryVisionSystem _blurryVision = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private OldStatusEffectsSystem _oldStatusEffects = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

#pragma warning disable CS0618 // DamageChangedEvent is the only event with the damage that was actually done.
        SubscribeLocalEvent<TraumaticShockComponent, DamageChangedEvent>(OnDamageChanged);
#pragma warning restore CS0618
        SubscribeLocalEvent<TraumaticShockComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<TraumaticShockComponent, GetBlurEvent>(OnGetBlur);
        SubscribeLocalEvent<TraumaticShockComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<TraumaticShockComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<TraumaticShockComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

#pragma warning disable CS0618 // DamageChangedEvent is the only event with the damage that was actually done.
    private void OnDamageChanged(Entity<TraumaticShockComponent> ent, ref DamageChangedEvent args)
#pragma warning restore CS0618
    {
        // Hits taken in shock don't count towards the next one.
        if (_net.IsClient || ent.Comp.InShock || !args.DamageIncreased || args.DamageDelta == null)
            return;

        var amount = GetTrauma(ent.Comp, args.DamageDelta);
        if (amount <= FixedPoint2.Zero)
            return;

        var now = _timing.CurTime;
        ent.Comp.RecentDamage.Add((now, amount));
        ent.Comp.RecentDamage.RemoveAll(hit => hit.Time + ent.Comp.Window < now);

        var total = FixedPoint2.Zero;
        foreach (var hit in ent.Comp.RecentDamage)
        {
            total += hit.Amount;
        }

        if (total >= ent.Comp.Threshold)
            TryStartShock(ent);
    }

    private FixedPoint2 GetTrauma(TraumaticShockComponent comp, DamageSpecifier damage)
    {
        var amount = FixedPoint2.Zero;
        foreach (var groupId in comp.DamageGroups)
        {
            if (!_proto.TryIndex(groupId, out var group))
                continue;

            foreach (var type in group.DamageTypes)
            {
                if (damage.DamageDict.TryGetValue(type, out var value) && value > 0)
                    amount += value;
            }
        }

        return amount;
    }

    /// <summary>
    /// Painkillers (and the pain numbness trait) keep the mob from feeling the pain, adrenaline keeps it going.
    /// </summary>
    public bool IsProtected(EntityUid uid)
    {
        return _statusEffects.HasEffectComp<PainNumbnessStatusEffectComponent>(uid)
               || _statusEffects.HasEffectComp<IgnoreSlowOnDamageComponent>(uid);
    }

    public bool TryStartShock(Entity<TraumaticShockComponent> ent)
    {
        if (ent.Comp.InShock || !_mobState.IsAlive(ent) || IsProtected(ent))
            return false;

        ent.Comp.InShock = true;
        ent.Comp.ShockEnd = _timing.CurTime + ent.Comp.Duration;
        ent.Comp.RecentDamage.Clear();
        Dirty(ent);

        _speed.RefreshMovementSpeedModifiers(ent.Owner);
        _blurryVision.UpdateBlurMagnitude(ent.Owner);
        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert);
        _stun.TryKnockdown(ent.Owner, ent.Comp.KnockdownTime, drop: true);
        _jitter.DoJitter(ent, ent.Comp.Duration, true, ent.Comp.JitterAmplitude, ent.Comp.JitterFrequency);
        _statusEffects.TryUpdateStatusEffectDuration(ent, ent.Comp.StutterEffect, ent.Comp.Duration);

        _popup.PopupEntity(Loc.GetString("traumatic-shock-start"),
            Loc.GetString("traumatic-shock-start-others", ("name", Identity.Entity(ent, EntityManager))),
            ent,
            ent,
            PopupType.LargeCaution);
        _adminLogger.Add(LogType.Damaged, LogImpact.Low, $"{ToPrettyString(ent):target} went into traumatic shock");
        return true;
    }

    /// <summary>
    /// Ends the shock. Cut short (adrenaline, painkillers, death), the shaking and stutter go with it.
    /// </summary>
    public void EndShock(Entity<TraumaticShockComponent> ent, bool early)
    {
        if (!ent.Comp.InShock)
            return;

        ent.Comp.InShock = false;
        Dirty(ent);

        _speed.RefreshMovementSpeedModifiers(ent.Owner);
        _blurryVision.UpdateBlurMagnitude(ent.Owner);
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);

        if (early)
        {
#pragma warning disable CS0618 // Jitter is still on the old status effects.
            _oldStatusEffects.TryRemoveStatusEffect(ent, "Jitter");
#pragma warning restore CS0618
            _statusEffects.TryRemoveStatusEffect(ent, ent.Comp.StutterEffect);
        }

        if (_mobState.IsAlive(ent))
            _popup.PopupEntity(Loc.GetString(early ? "traumatic-shock-end-early" : "traumatic-shock-end"), ent, ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TraumaticShockComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.InShock)
                continue;

            if (now >= comp.ShockEnd)
                EndShock((uid, comp), early: false);
            else if (!_mobState.IsAlive(uid) || IsProtected(uid))
                EndShock((uid, comp), early: true);
        }
    }

    private void OnRefreshSpeed(Entity<TraumaticShockComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.InShock)
            args.ModifySpeed(ent.Comp.SpeedModifier, ent.Comp.SpeedModifier);
    }

    private void OnGetBlur(Entity<TraumaticShockComponent> ent, ref GetBlurEvent args)
    {
        if (ent.Comp.InShock)
            args.Blur += ent.Comp.Blur;
    }

    private void OnExamined(Entity<TraumaticShockComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.InShock && _mobState.IsAlive(ent))
            args.PushMarkup(Loc.GetString("traumatic-shock-examine", ("target", Identity.Entity(ent, EntityManager))));
    }

    private void OnRejuvenate(Entity<TraumaticShockComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.RecentDamage.Clear();
        EndShock(ent, early: true);
    }

    private void OnHandleState(Entity<TraumaticShockComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }
}
