using Content.Server.Power.EntitySystems;
using Content.Shared._EternalWar.Inserter;
using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Materials;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._EternalWar.Inserter;

/// <summary>
/// Moves items one at a time from the tile behind the inserter to the tile in front of it.
/// </summary>
public sealed partial class InserterSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    [Dependency] private EntityQuery<ItemComponent> _itemQuery;

    private readonly HashSet<EntityUid> _onTile = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InserterComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<InserterComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInit(Entity<InserterComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.HandContainerId);
    }

    private void OnExamined(Entity<InserterComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (GetHeld(ent) is { } held)
            args.PushMarkup(Loc.GetString("inserter-examine-holding", ("item", held)));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<InserterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (curTime < comp.NextAction)
                continue;

            // Unpowered or unanchored inserters freeze mid-swing and carry on when they get power back.
            if (!xform.Anchored || !this.IsPowered(uid, EntityManager))
                continue;

            Step((uid, comp, xform), curTime);
        }
    }

    private void Step(Entity<InserterComponent, TransformComponent> ent, TimeSpan curTime)
    {
        var comp = ent.Comp1;
        var held = GetHeld((ent, comp));

        switch (comp.State)
        {
            // The arm has finished swinging.
            case InserterState.ToTarget:
                SetState(ent, InserterState.AtTarget, curTime);
                return;
            case InserterState.ToSource:
                SetState(ent, InserterState.AtSource, curTime);
                return;

            case InserterState.AtSource:
                if (held != null
                    || TryGetTile(ent, -1, out var sourceGrid, out var sourceTile) && TryTake(ent, sourceGrid, sourceTile))
                    SetState(ent, InserterState.ToTarget, curTime);
                else
                    comp.NextAction = curTime + comp.RetryTime;
                return;

            case InserterState.AtTarget:
                if (held == null
                    || TryGetTile(ent, 1, out var targetGrid, out var targetTile) && TryPut(ent, held.Value, targetGrid, targetTile))
                    SetState(ent, InserterState.ToSource, curTime);
                else
                    comp.NextAction = curTime + comp.RetryTime;
                return;
        }
    }

    private void SetState(Entity<InserterComponent, TransformComponent> ent, InserterState state, TimeSpan curTime)
    {
        ent.Comp1.State = state;
        ent.Comp1.NextAction = state is InserterState.ToTarget or InserterState.ToSource
            ? curTime + ent.Comp1.SwingTime
            : curTime;
        _appearance.SetData(ent, InserterVisuals.State, state);
    }

    private EntityUid? GetHeld(Entity<InserterComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.HandContainerId, out var hand) || hand.ContainedEntities.Count == 0)
            return null;

        return hand.ContainedEntities[0];
    }

    /// <summary>
    /// The tile in front of the inserter (direction 1) or behind it (direction -1).
    /// Front is the way the inserter faces, the same as a conveyor with the same rotation moves items.
    /// </summary>
    private bool TryGetTile(Entity<InserterComponent, TransformComponent> ent, int direction, out Entity<MapGridComponent> grid, out Vector2i tile)
    {
        grid = default;
        tile = default;

        if (ent.Comp2.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return false;

        grid = (gridUid, gridComp);
        var own = _map.TileIndicesFor(grid, ent.Comp2.Coordinates);
        tile = own + ent.Comp2.LocalRotation.GetCardinalDir().ToIntVec() * direction;
        return true;
    }

    private HashSet<EntityUid> EntitiesOn(Entity<MapGridComponent> grid, Vector2i tile)
    {
        _onTile.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid, tile, _onTile, gridComp: grid.Comp);
        return _onTile;
    }

    #region Taking

    private bool TryTake(Entity<InserterComponent, TransformComponent> ent, Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (!_container.TryGetContainer(ent, ent.Comp1.HandContainerId, out var hand))
            return false;

        var floorItem = (EntityUid?) null;
        foreach (var uid in EntitiesOn(grid, tile))
        {
            if (uid == ent.Owner)
                continue;

            // Containers first: a closed crate or locker, then a bag or a box.
            if (TryComp<EntityStorageComponent>(uid, out var entityStorage) && !entityStorage.Open)
            {
                foreach (var inside in entityStorage.Contents.ContainedEntities)
                {
                    if (!_itemQuery.HasComp(inside))
                        continue;

                    if (_entityStorage.Remove(inside, uid, entityStorage) && _container.Insert(inside, hand))
                        return true;
                }
            }

            if (TryComp<StorageComponent>(uid, out var storage) && storage.Container.ContainedEntities.Count > 0)
            {
                foreach (var inside in storage.Container.ContainedEntities)
                {
                    if (_container.Insert(inside, hand))
                        return true;
                }
            }

            if (floorItem == null && CanTakeFromFloor(uid))
                floorItem = uid;
        }

        return floorItem != null && _container.Insert(floorItem.Value, hand);
    }

    private bool CanTakeFromFloor(EntityUid uid)
    {
        return _itemQuery.HasComp(uid)
               && !Transform(uid).Anchored
               && !_container.IsEntityInContainer(uid);
    }

    #endregion

    #region Putting

    private bool TryPut(Entity<InserterComponent, TransformComponent> ent, EntityUid item, Entity<MapGridComponent> grid, Vector2i tile)
    {
        foreach (var uid in EntitiesOn(grid, tile))
        {
            if (uid == ent.Owner || uid == item)
                continue;

            // Materials go straight into lathes, ore processors and the like.
            if (HasComp<MaterialStorageComponent>(uid) && HasComp<MaterialComponent>(item)
                && _materialStorage.TryInsertMaterialEntity(ent, item, uid))
                return true;

            if (TryComp<EntityStorageComponent>(uid, out var entityStorage) && !entityStorage.Open
                && _entityStorage.CanInsert(item, uid, entityStorage)
                && _entityStorage.Insert(item, uid, entityStorage))
                return true;

            if (TryComp<StorageComponent>(uid, out var storage)
                && _storage.CanInsert(uid, item, out _, storage)
                && _storage.Insert(uid, item, out _, storageComp: storage, playSound: false))
            {
                // A stack may have gone in only partly. Then the rest stays in the arm for the next try.
                return GetHeld((ent, ent.Comp1)) == null;
            }
        }

        // Nothing to put it into: drop it on the floor, unless a wall or the like is in the way.
        if (_turf.IsTileBlocked(grid, tile, CollisionGroup.Impassable))
            return false;

        if (!_container.TryGetContainer(ent, ent.Comp1.HandContainerId, out var hand))
            return false;

        var coords = _map.GridTileToLocal(grid, grid.Comp, tile);
        return _container.Remove(item, hand, destination: coords);
    }

    #endregion
}
