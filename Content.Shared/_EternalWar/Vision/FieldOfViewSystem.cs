using System.Numerics;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared.MouseRotator;
using Content.Shared.Movement.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._EternalWar.Vision;

/// <summary>
/// The geometry of the <see cref="FieldOfViewComponent"/> cone, and turning the mob to the mouse while a player
/// controls it. Hiding and dimming what is outside the cone is done by the client.
/// </summary>
public sealed partial class FieldOfViewSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedBodyZonesSystem _bodyZones = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FieldOfViewComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<FieldOfViewComponent, PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerAttached(EntityUid uid, FieldOfViewComponent comp, PlayerAttachedEvent args)
    {
        if (_net.IsClient)
            return;

        // The mob looks where the mouse is and doesn't turn to where it walks: it can back away or look around.
        var rotator = EnsureComp<MouseRotatorComponent>(uid);
        rotator.Simple4DirMode = false;
        rotator.AngleTolerance = comp.MouseTolerance;
        Dirty(uid, rotator);
        EnsureComp<NoRotateOnMoveComponent>(uid);
    }

    private void OnPlayerDetached(EntityUid uid, FieldOfViewComponent comp, PlayerDetachedEvent args)
    {
        if (_net.IsClient || TerminatingOrDeleted(uid))
            return;

        RemComp<MouseRotatorComponent>(uid);
        RemComp<NoRotateOnMoveComponent>(uid);
    }

    /// <summary>
    /// Half of the cone to the mob's left and to its right. A missing or crushed eye takes from its own side.
    /// </summary>
    public (Angle Left, Angle Right) GetHalfAngles(Entity<FieldOfViewComponent> ent)
    {
        var half = ent.Comp.Angle.Theta / 2;
        var left = half;
        var right = half;

        if (TryComp<BodyZonesComponent>(ent, out var zones))
        {
            if (_bodyZones.IsEyeBlind(zones, BodyZone.LeftEye))
                left -= ent.Comp.LostEyeNarrowing.Theta;
            if (_bodyZones.IsEyeBlind(zones, BodyZone.RightEye))
                right -= ent.Comp.LostEyeNarrowing.Theta;
        }

        return (new Angle(Math.Max(0, left)), new Angle(Math.Max(0, right)));
    }

    /// <summary>
    /// Where the mob looks, as a unit vector in world space.
    /// </summary>
    public Vector2 GetFacing(EntityUid uid)
    {
        return _transform.GetWorldRotation(uid).ToWorldVec();
    }

    /// <summary>
    /// Whether the mob sees this point: close by, or inside the cone. Walls are not checked here.
    /// </summary>
    public bool InView(Entity<FieldOfViewComponent> ent, Vector2 eyePos, Vector2 facing, (Angle Left, Angle Right) half, Vector2 point)
    {
        var delta = point - eyePos;
        var distance = delta.Length();
        if (distance <= ent.Comp.NearRadius)
            return true;

        // Left of the facing is the facing turned a quarter counterclockwise.
        var left = new Vector2(-facing.Y, facing.X);
        var side = Vector2.Dot(delta, left);
        var angle = Math.Atan2(Math.Abs(side), Vector2.Dot(delta, facing));
        return angle <= (side >= 0 ? half.Left : half.Right).Theta;
    }

    /// <summary>
    /// Whether the mob sees the other entity, see the overload with the point.
    /// </summary>
    public bool InView(Entity<FieldOfViewComponent> ent, EntityUid target)
    {
        var eyePos = _transform.GetWorldPosition(ent);
        return InView(ent, eyePos, GetFacing(ent), GetHalfAngles(ent), _transform.GetWorldPosition(target));
    }
}
