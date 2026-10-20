using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<MovedByPressureComponent> _movedByPressureQuery = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly ProtoId<SoundCollectionPrototype> DefaultSpaceWindSounds = "SpaceWind";

    private const int SpaceWindSoundCooldownCycles = 75;

    /// <summary>
    /// Within this many tiles of a breach, space wind aims straight at the hole instead of following the tile flow.
    /// </summary>
    private const float SpaceWindDirectAimRange = 6f;

    /// <summary>
    /// How many tiles along the airflow we look ahead when the breach itself isn't in a straight line.
    /// </summary>
    private const int SpaceWindFlowLookahead = 8;

    /// <summary>
    /// How far, in tiles, an intermediate flow target is shifted toward the breach.
    /// Big enough to start the turn, small enough not to clip the corner being rounded.
    /// </summary>
    private const float SpaceWindCornerLean = 0.15f;

    private int _spaceWindSoundCooldown = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? SpaceWindSound { get; private set; } = new SoundCollectionSpecifier(DefaultSpaceWindSounds, AudioParams.Default.WithVariation(0.125f));

    private readonly HashSet<Entity<MovedByPressureComponent>> _activePressures = new(8);

    private void UpdateHighPressure(float frameTime)
    {
        var toRemove = new RemQueue<Entity<MovedByPressureComponent>>();

        foreach (var ent in _activePressures)
        {
            var (uid, comp) = ent;
            MetaDataComponent? metadata = null;

            if (Deleted(uid, metadata))
            {
                toRemove.Add((uid, comp));
                continue;
            }

            if (Paused(uid, metadata))
                continue;

            comp.Accumulator += frameTime;

            if (comp.Accumulator < 2f)
                continue;

            // Reset it just for VV reasons even though it doesn't matter
            comp.Accumulator = 0f;
            toRemove.Add(ent);

            if (comp.LiftedByPressure &&
                _physicsQuery.TryGetComponent(uid, out var body))
            {
                _physics.SetBodyStatus(uid, body, BodyStatus.OnGround);
            }

            comp.LiftedByPressure = false;

            if ((comp.TableLayerRemoved.Count > 0 || comp.RemovedRestitution.Count > 0)
                && TryComp<FixturesComponent>(uid, out var fixtures))
            {
                foreach (var (id, fixture) in fixtures.Fixtures)
                {
                    if (comp.TableLayerRemoved.Contains(id))
                    {
                        _physics.AddCollisionMask(uid, id, fixture, (int)CollisionGroup.TableLayer, manager: fixtures);
                    }

                    if (comp.RemovedRestitution.TryGetValue(id, out var restitution))
                        _physics.SetRestitution(uid, fixture, restitution, manager: fixtures);
                }

                comp.TableLayerRemoved.Clear();
                comp.RemovedRestitution.Clear();
            }
        }

        foreach (var comp in toRemove)
        {
            _activePressures.Remove(comp);
        }
    }

    /// <summary>
    /// Lifts an entity caught by space wind off the ground so tile friction doesn't stop it,
    /// and lets it fly over tables. Undone in <see cref="UpdateHighPressure"/>.
    /// </summary>
    private void AddMovedByPressure(EntityUid uid, MovedByPressureComponent component, PhysicsComponent body)
    {
        // Don't take over bodies that are already airborne for another reason (e.g. thrown).
        if (body.BodyStatus == BodyStatus.OnGround)
        {
            _physics.SetBodyStatus(uid, body, BodyStatus.InAir);
            component.LiftedByPressure = true;
        }

        if (TryComp<FixturesComponent>(uid, out var fixtures))
        {
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                // Mark fixtures that have TableLayer removed
                if ((fixture.CollisionMask & (int)CollisionGroup.TableLayer) != 0)
                {
                    component.TableLayerRemoved.Add(id);
                    _physics.RemoveCollisionMask(uid, id, fixture, (int)CollisionGroup.TableLayer, manager: fixtures);
                }

                // Things carried by the wind bounce off walls like rubber balls and never make it to the breach.
                // Contacts use the bouncier of the two fixtures, so zeroing ours is enough against unbouncy walls.
                if (fixture.Restitution > 0f && component.RemovedRestitution.TryAdd(id, fixture.Restitution))
                    _physics.SetRestitution(uid, fixture, 0f, manager: fixtures);
            }
        }
        // TODO: Make them dynamic type? Ehh but they still want movement so uhh make it non-predicted like weightless?
        // idk it's hard.

        component.Accumulator = 0f;
        _activePressures.Add((uid, component));
    }

    private void HighPressureMovements(Entity<GridAtmosphereComponent> gridAtmosphere, TileAtmosphere tile)
    {
        // TODO ATMOS finish this

        // Don't play the space wind sound on tiles that are on fire...
        if (tile.PressureDifference > 15 && !tile.Hotspot.Valid)
        {
            if (_spaceWindSoundCooldown == 0 && SpaceWindSound != null)
            {
                var coordinates = _mapSystem.ToCenterCoordinates(tile.GridIndex, tile.GridIndices);
                _audio.PlayPvs(SpaceWindSound, coordinates, SpaceWindSound.Params.WithVolume(MathHelper.Clamp(tile.PressureDifference / 10, 10, 100)));
            }
        }


        if (tile.PressureDifference > 100)
        {
            // TODO ATMOS Do space wind graphics here!
        }

        if (_spaceWindSoundCooldown++ > SpaceWindSoundCooldownCycles)
            _spaceWindSoundCooldown = 0;

        // No atmos yeets, return early.
        if (!SpaceWind)
            return;

        _entSet.Clear();
        _lookup.GetLocalEntitiesIntersecting(tile.GridIndex, tile.GridIndices, _entSet, 0f);

        // Most tiles in a breach are empty, so skip the direction and rotation work for them.
        if (_entSet.Count == 0)
            return;

        // Used by ExperiencePressureDifference to correct push/throw directions from tile-relative to physics world.
        var gridWorldRotation = XformSystem.GetWorldRotation(gridAtmosphere);

        // Explosive depressurization leaves the space tile this air is escaping into here.
        var breach = tile.PressureSpecificTarget is { Space: true } spaceTile ? spaceTile : null;
        var aimAtBreach = false;
        var cornerLean = Vector2.Zero;

        // Close to the breach, head straight for the hole. Following the per-tile flow directions
        // here is a grid staircase that slams things into the walls on either side of it.
        if (breach != null
            && (breach.GridIndices - tile.GridIndices).Length <= SpaceWindDirectAimRange
            && HasStraightAirPath(tile, breach))
        {
            aimAtBreach = true;
        }
        // Around a corner from the breach: follow the flow to the furthest point we can fly to
        // in a straight line, so we round the corner instead of getting wedged in it.
        else if (breach != null)
        {
            var curTile = tile;
            for (var i = 0; i < SpaceWindFlowLookahead; i++)
            {
                if (curTile.Space
                    || curTile.PressureDirection == AtmosDirection.Invalid
                    || !curTile.AdjacentBits.IsFlagSet(curTile.PressureDirection))
                    break;

                var next = curTile.AdjacentTiles[curTile.PressureDirection.ToIndex()]!;
                if (!HasStraightAirPath(tile, next))
                    break;

                curTile = next;
            }

            if (curTile != tile)
            {
                tile.PressureSpecificTarget = curTile;
                aimAtBreach = true;
                // Lean a little toward the breach so we start turning the corner instead of overshooting it.
                cornerLean = (Vector2) (breach.GridIndices - curTile.GridIndices);
                cornerLean = cornerLean.LengthSquared() > 0f ? cornerLean.Normalized() * SpaceWindCornerLean : Vector2.Zero;
            }
        }
        // If we're using monstermos, smooth out the yeet direction to follow the flow
        else if (MonstermosEqualization)
        {
            // We step through tiles according to the pressure direction on the current tile.
            // The goal is to get a general direction of the airflow in the area.
            // 3 is the magic number - enough to go around corners, but not U-turns.
            var curTile = tile;
            for (var i = 0; i < 3; i++)
            {
                if (curTile.Space
                    || curTile.PressureDirection == AtmosDirection.Invalid
                    || !curTile.AdjacentBits.IsFlagSet(curTile.PressureDirection))
                    break;
                curTile = curTile.AdjacentTiles[curTile.PressureDirection.ToIndex()]!;
            }

            if (curTile != tile)
                tile.PressureSpecificTarget = curTile;
        }

        // Same for every entity on the tile, so work it out once.
        var throwTarget = tile.PressureSpecificTarget != null
            ? _mapSystem.ToCenterCoordinates(tile.GridIndex, tile.PressureSpecificTarget.GridIndices).Offset(cornerLean)
            : EntityCoordinates.Invalid;
        var cycle = gridAtmosphere.Comp.UpdateCounter;

        foreach (var entity in _entSet)
        {
            // Ideally containers would have their own EntityQuery internally or something given recursively it may need to slam GetComp<T> anyway.
            // Also, don't care about static bodies (but also due to collisionwakestate can't query dynamic directly atm).
            if (!_movedByPressureQuery.TryGetComponent(entity, out var pressure) ||
                !pressure.Enabled ||
                pressure.LastHighPressureMovementAirCycle >= cycle ||
                !_physicsQuery.TryGetComponent(entity, out var body))
                continue;

            if (_containers.IsEntityInContainer(entity))
                continue;

            // tl;dr YEET
            ExperiencePressureDifference(
                (entity, pressure),
                cycle,
                tile.PressureDifference,
                tile.PressureDirection, 0,
                throwTarget,
                gridWorldRotation,
                Transform(entity),
                body,
                aimAtBreach,
                breach != null);
        }
    }

    /// <summary>
    /// Walks the tiles on the straight line between two tiles and checks air can pass between each pair,
    /// i.e. nothing like a wall or a corner is in the way.
    /// </summary>
    private static bool HasStraightAirPath(TileAtmosphere from, TileAtmosphere to)
    {
        var delta = to.GridIndices - from.GridIndices;
        var stepsX = Math.Abs(delta.X);
        var stepsY = Math.Abs(delta.Y);
        var dirX = delta.X > 0 ? AtmosDirection.East : AtmosDirection.West;
        var dirY = delta.Y > 0 ? AtmosDirection.North : AtmosDirection.South;

        var cur = from;
        var x = 0;
        var y = 0;
        while (x < stepsX || y < stepsY)
        {
            // Step along whichever axis the line crosses into next.
            var nextX = (0.5f + x) * stepsY;
            var nextY = (0.5f + y) * stepsX;

            TileAtmosphere? next;
            if (MathHelper.CloseTo(nextX, nextY))
            {
                // The line passes exactly through a tile corner. Anything solid on either side
                // would catch a body, so both ways around the corner have to be open.
                var viaX = Step(cur, dirX) is { } a ? Step(a, dirY) : null;
                var viaY = Step(cur, dirY) is { } b ? Step(b, dirX) : null;
                if (viaX == null || viaX != viaY)
                    return false;

                next = viaX;
                x++;
                y++;
            }
            else if (nextX < nextY)
            {
                next = Step(cur, dirX);
                x++;
            }
            else
            {
                next = Step(cur, dirY);
                y++;
            }

            if (next == null)
                return false;

            cur = next;
        }

        return cur == to;
    }

    private static TileAtmosphere? Step(TileAtmosphere tile, AtmosDirection dir)
    {
        return tile.AdjacentBits.IsFlagSet(dir) ? tile.AdjacentTiles[dir.ToIndex()] : null;
    }

    // Called from AtmosphereSystem.LINDA.cs with SpaceWind CVar check handled there.
    private void ConsiderPressureDifference(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile, AtmosDirection differenceDirection, float difference)
    {
        gridAtmosphere.HighPressureDelta.Add(tile);

        if (difference <= tile.PressureDifference)
            return;

        tile.PressureDifference = difference;
        tile.PressureDirection = differenceDirection;
    }

    public void ExperiencePressureDifference(
        Entity<MovedByPressureComponent> ent,
        int cycle,
        float pressureDifference,
        AtmosDirection direction,
        float pressureResistanceProbDelta,
        EntityCoordinates throwTarget,
        Angle gridWorldRotation,
        TransformComponent? xform = null,
        PhysicsComponent? physics = null,
        bool aimAtTarget = false,
        bool explosiveDepressurization = false)
    {
        var (uid, component) = ent;
        if (!Resolve(uid, ref physics, false))
            return;

        if (!Resolve(uid, ref xform))
            return;

        var maxForce = MathF.Sqrt(pressureDifference) * 2.25f;
        // Air rushing out to space grabs everything in the room, not just what's next to the hole.
        // This only decides whether the wind grabs the object; the push itself still scales with maxForce,
        // so things further from the breach are dragged gently rather than launched.
        var gripForce = explosiveDepressurization ? maxForce * SpaceWindForceMultiplier : maxForce;
        var moveProb = 100f;

        // Tiles at the edge of a breach only carry their own trickle of air, which would otherwise
        // fail this roll almost every time.
        if (component.PressureResistance > 0 && !explosiveDepressurization)
            moveProb = MathF.Abs((pressureDifference / component.PressureResistance * MovedByPressureComponent.ProbabilityBasePercent) -
                                 MovedByPressureComponent.ProbabilityOffset);

        // Can we yeet the thing (due to probability, strength, etc.)
        if (moveProb > MovedByPressureComponent.ProbabilityOffset && _random.Prob(MathF.Min(moveProb / 100f, 1f))
                                                                  && !float.IsPositiveInfinity(component.MoveResist)
                                                                  && (physics.BodyType != BodyType.Static
                                                                      && (gripForce >= (component.MoveResist * MovedByPressureComponent.MoveForcePushRatio)))
            || (physics.BodyType == BodyType.Static && (gripForce >= (component.MoveResist * MovedByPressureComponent.MoveForceForcePushRatio))))
        {
            if (gripForce > MovedByPressureComponent.ThrowForce)
            {
                AddMovedByPressure(uid, component, physics);

                if (SpaceWindKnockdownTime > 0f && HasComp<MobStateComponent>(uid))
                    _stun.TryKnockdown(uid, TimeSpan.FromSeconds(SpaceWindKnockdownTime));

                var moveForce = maxForce;
                moveForce /= (throwTarget != EntityCoordinates.Invalid) ? SpaceWindPressureForceDivisorThrow : SpaceWindPressureForceDivisorPush;
                moveForce *= MathHelper.Clamp(moveProb, 0, 100);

                // The base push is tuned for gentle drafts and barely shifts a person, even when
                // the room is emptying into space.
                if (explosiveDepressurization)
                    moveForce *= SpaceWindDepressurizationPushMultiplier;

                // Apply a sanity clamp to prevent being thrown through objects.
                var maxSafeForceForObject = SpaceWindMaxVelocity * physics.Mass;
                moveForce = MathF.Min(moveForce, maxSafeForceForObject);

                // Grid-rotation adjusted direction
                var dirVec = (direction.ToAngle() + gridWorldRotation).ToWorldVec();

                // TODO: Technically these directions won't be correct but uhh I'm just here for optimisations buddy not to fix my old bugs.
                if (throwTarget != EntityCoordinates.Invalid)
                {
                    var toTarget = (XformSystem.ToMapCoordinates(throwTarget).Position - XformSystem.GetWorldPosition(xform)).Normalized();
                    // Mixing in the tile's cardinal flow direction bends the path into nearby walls,
                    // so skip it when we're aiming straight at the breach.
                    var pos = aimAtTarget ? toTarget : (toTarget + dirVec).Normalized();
                    _physics.ApplyLinearImpulse(uid, pos * moveForce, body: physics);
                }
                else
                {
                    moveForce = MathF.Min(moveForce, SpaceWindMaxPushForce);
                    _physics.ApplyLinearImpulse(uid, dirVec * moveForce, body: physics);
                }

                component.LastHighPressureMovementAirCycle = cycle;
            }
        }
    }
}
