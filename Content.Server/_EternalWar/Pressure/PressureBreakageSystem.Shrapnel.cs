using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Destructible;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._EternalWar.Pressure;

public sealed partial class PressureBreakageSystem
{
    /// <summary>
    /// Pieces with less energy than this, J, don't hurt anyone.
    /// </summary>
    private const float MinShrapnelEnergy = 5f;

    /// <summary>
    /// How far pieces are looked for, tiles.
    /// </summary>
    private const float ShrapnelRange = 6f;

    /// <summary>
    /// The side of a person the pieces fly into, m².
    /// </summary>
    private const float BodyArea = 0.7f;

    /// <summary>
    /// The pieces of a thing held in hand burst this close to the body, m.
    /// </summary>
    private const float HeldDistance = 0.3f;

    /// <summary>
    /// A person can't be in the way of more than this share of the pieces.
    /// </summary>
    private const float MaxShareHit = 0.5f;

    /// <summary>
    /// A point of damage for this many joules of sharp pieces. Pieces are many and small: cuts, not a club.
    /// </summary>
    private const float ShrapnelJoulesPerDamage = 20f;

    private const float MaxShrapnelDamage = 60f;

    /// <summary>
    /// A window pane, m², and how far it bends before it gives way, m: the gas pushes the pieces this far.
    /// </summary>
    private const float PaneArea = 1f;
    private const float PaneGive = 0.01f;

    /// <summary>
    /// A pane gives way into the side with less pressure: pieces fly only into this half of space.
    /// </summary>
    private const float PaneSpreadCos = 0.2f;

    private void InitializeShrapnel()
    {
        SubscribeLocalEvent<DeltaPressureComponent, DestructionEventArgs>(OnPaneDestroyed);
    }

    /// <summary>
    /// A window broken by pressure blows out into the side with less pressure.
    /// </summary>
    private void OnPaneDestroyed(Entity<DeltaPressureComponent> ent, ref DestructionEventArgs args)
    {
        if (!ent.Comp.IsTakingDamage)
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } grid)
            return;

        var tile = _transform.GetGridTilePositionOrDefault((ent, xform));
        var high = (Pressure: float.MinValue, Offset: Vector2i.Zero);
        var low = (Pressure: float.MaxValue, Offset: Vector2i.Zero);
        foreach (var offset in new[] { Vector2i.Up, Vector2i.Down, Vector2i.Left, Vector2i.Right })
        {
            var pressure = _atmos.GetTileMixture(grid, xform.MapUid, tile + offset)?.Pressure ?? 0f;
            if (pressure > high.Pressure)
                high = (pressure, offset);
            if (pressure < low.Pressure)
                low = (pressure, offset);
        }

        var difference = high.Pressure - low.Pressure;
        if (difference <= 0f)
            return;

        var material = _proto.Index(WindowMaterial);
        var energy = difference * 1000f * PaneArea * PaneGive * material.ShrapnelShare;
        var direction = _transform.GetWorldRotation(grid).RotateVec((Vector2) (low.Offset - high.Offset)).Normalized();
        Shrapnel(ent, _transform.GetMapCoordinates(ent, xform), energy, material.ShrapnelDamage, direction, null);
    }

    /// <summary>
    /// Pieces of a burst thing fly around. Everyone in sight takes the share of them their body covers.
    /// </summary>
    /// <param name="energy">J the pieces carry.</param>
    /// <param name="direction">Only into this half of space; everywhere if null.</param>
    /// <param name="holder">Who held it: the pieces burst right at them.</param>
    public void Shrapnel(EntityUid source,
        MapCoordinates at,
        float energy,
        ProtoId<DamageTypePrototype> damageType,
        Vector2? direction,
        EntityUid? holder)
    {
        if (energy < MinShrapnelEnergy || at.MapId == MapId.Nullspace)
            return;

        var type = _proto.Index(damageType);
        // Pieces spread over a sphere, or a half of it out of a pane.
        var spread = direction == null ? 4f * MathF.PI : 2f * MathF.PI;
        var contained = _container.IsEntityInContainer(source);

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(at, ShrapnelRange))
        {
            var position = _transform.GetMapCoordinates(mob);
            var offset = position.Position - at.Position;
            var distance = offset.Length();

            if (mob.Owner == holder)
            {
                distance = HeldDistance;
            }
            else
            {
                if (direction is { } dir && (distance < 0.01f || Vector2.Dot(offset / distance, dir) < PaneSpreadCos))
                    continue;

                if (!_interaction.InRangeUnobstructed(at,
                        position,
                        ShrapnelRange + 1f,
                        CollisionGroup.Impassable,
                        e => e == source || e == mob.Owner))
                    continue;
            }

            var share = MathF.Min(BodyArea / (spread * MathF.Max(distance * distance, HeldDistance * HeldDistance)), MaxShareHit);
            var amount = MathF.Min(energy * share / ShrapnelJoulesPerDamage, MaxShrapnelDamage);
            if (amount < 0.5f)
                continue;

            // A loose thing hits random zones; the one in hand, the whole body (nobody aims it).
            _damageable.TryChangeDamage(mob.Owner,
                new DamageSpecifier(type, amount),
                origin: contained ? null : source);
        }
    }
}
