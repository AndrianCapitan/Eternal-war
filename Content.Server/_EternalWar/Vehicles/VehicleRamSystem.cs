using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Server._EternalWar.Vehicles;

/// <summary>
/// Knocks down whoever a driven vehicle rolls into at speed.
/// </summary>
public sealed partial class VehicleRamSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleRamComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<VehicleRamComponent> ent, ref StartCollideEvent args)
    {
        var target = args.OtherEntity;

        // Only someone driving it rams; an empty chair rolling on its own does not.
        if (!TryComp<VehicleComponent>(ent, out var vehicle) || vehicle.Operator is not { } driver || target == driver)
            return;

        if (!HasComp<StandingStateComponent>(target) || _standing.IsDown(target))
            return;

        // Someone sitting in another chair or bed is not standing to be knocked over.
        if (TryComp<BuckleComponent>(target, out var buckle) && buckle.Buckled)
            return;

        // Only the speed towards the target counts: being walked into, or scraping past, is not a ram.
        var toTarget = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(ent);
        if (toTarget.LengthSquared() < 0.0001f)
            return;

        var speed = Vector2.Dot(args.OurBody.LinearVelocity, Vector2.Normalize(toTarget));
        if (speed < ent.Comp.MinSpeed)
            return;

        if (!_stun.TryKnockdown(target, ent.Comp.KnockdownTime, refresh: true, autoStand: true, drop: true))
            return;

        _audio.PlayPvs(ent.Comp.Sound, ent);

        var name = Identity.Entity(target, EntityManager);
        _popup.PopupEntity(Loc.GetString("vehicle-ram-self"), target, target, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("vehicle-ram-others", ("target", name)), target, Filter.PvsExcept(target), true, PopupType.Medium);
    }
}
