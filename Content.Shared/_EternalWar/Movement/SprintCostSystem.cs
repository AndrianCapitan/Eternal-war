using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Shared._EternalWar.Movement;

/// <summary>
/// Takes stamina, food and water from running mobs and stops them running once they are out of breath.
/// Walking is the default, the run key (Shift) makes the mob run: see <see cref="InputMoverComponent.Sprinting"/>.
/// The cost is taken on the server once a second, the client only predicts the speed.
/// </summary>
public sealed partial class SprintCostSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Slower than this is not running, even with the key held: pushing into a wall, being dragged back.
    /// </summary>
    private const float MinRunSpeed = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SprintCostComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<SprintCostComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnRefreshSpeed(Entity<SprintCostComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Exhausted || !TryComp<MovementSpeedModifierComponent>(ent, out var move) || move.BaseSprintSpeed <= 0f)
            return;

        // Out of breath: running is no faster than walking.
        args.ModifySpeed(1f, move.BaseWalkSpeed / move.BaseSprintSpeed);
    }

    private void OnHandleState(Entity<SprintCostComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SprintCostComponent, InputMoverComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var cost, out var mover, out var physics))
        {
            if (IsRunning((uid, cost, mover, physics)))
                cost.RunTime += frameTime;

            if (now < cost.NextUpdate)
                continue;

            cost.NextUpdate = now + UpdateInterval;
            TakeCost((uid, cost));
        }
    }

    private bool IsRunning(Entity<SprintCostComponent, InputMoverComponent, PhysicsComponent> ent)
    {
        var (uid, cost, mover, physics) = ent;
        return !cost.Exhausted
               && mover.Sprinting
               && mover.HasDirectionalMovement
               && mover.CanMove
               && physics.LinearVelocity.LengthSquared() > MinRunSpeed * MinRunSpeed
               && _mobState.IsAlive(uid)
               && !_gravity.IsWeightless(uid);
    }

    private void TakeCost(Entity<SprintCostComponent> ent)
    {
        var time = ent.Comp.RunTime;
        ent.Comp.RunTime = 0f;

        TryComp<StaminaComponent>(ent, out var stamina);

        if (time > 0f)
        {
            if (TryComp<SatiationComponent>(ent, out var satiation))
            {
                _satiation.ModifyValue((ent, satiation), SatiationSystem.Hunger, -ent.Comp.HungerPerSecond * time);
                _satiation.ModifyValue((ent, satiation), SatiationSystem.Thirst, -ent.Comp.ThirstPerSecond * time);
            }

            if (stamina != null)
            {
                _stamina.TryTakeStamina(ent, ent.Comp.StaminaPerSecond * time, stamina, visual: false);

                if (stamina.StaminaDamage >= stamina.CritThreshold * ent.Comp.ExhaustedAt)
                {
                    SetExhausted(ent, true);
                    _popup.PopupEntity(Loc.GetString("sprint-cost-exhausted"), ent, ent, PopupType.SmallCaution);
                }
            }

            return;
        }

        if (ent.Comp.Exhausted && (stamina == null || stamina.StaminaDamage <= stamina.CritThreshold * ent.Comp.RecoveredAt))
        {
            SetExhausted(ent, false);
            _popup.PopupEntity(Loc.GetString("sprint-cost-recovered"), ent, ent);
        }
    }

    private void SetExhausted(Entity<SprintCostComponent> ent, bool exhausted)
    {
        if (ent.Comp.Exhausted == exhausted)
            return;

        ent.Comp.Exhausted = exhausted;
        Dirty(ent);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }
}
