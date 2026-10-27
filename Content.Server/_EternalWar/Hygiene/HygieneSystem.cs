using System.Linq;
using System.Numerics;
using Content.Shared.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Hygiene;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Decals;
using Content.Shared.Fluids.Components;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._EternalWar.Hygiene;

/// <summary>
/// Gets blood on the clothes of the bleeding, leaves footprints after puddles and washes it all off in the shower.
/// </summary>
public sealed partial class HygieneSystem : SharedHygieneSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedBodyZonesSystem _zones = default!;

    /// <summary>
    /// Clothes soak up this much of every unit of blood a wound bleeds, per second.
    /// With a bleeding of 5 a shirt is soaked in about 10 seconds.
    /// </summary>
    private const float StainPerBleed = 0.01f;

    /// <summary>
    /// A stump gushing blood soaks what covers it much faster than a cut.
    /// </summary>
    private const float StainPerGush = 0.03f;

    /// <summary>
    /// A zone this badly hurt (share of its health) is a bleeding wound.
    /// </summary>
    private const float WoundRatio = 0.25f;

    private static readonly TimeSpan StainInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextStain;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FootprintsComponent, MoveEvent>(OnMove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now >= _nextStain)
        {
            _nextStain = now + StainInterval;
            UpdateBleedingStains((float) StainInterval.TotalSeconds);
        }

        UpdateShowers(now);
    }

    #region Bleeding on clothes

    private void UpdateBleedingStains(float dt)
    {
        var wounds = new Dictionary<BodyZone, float>();
        var query = EntityQueryEnumerator<BloodstreamComponent, BodyZonesComponent, InventoryComponent>();
        while (query.MoveNext(out var uid, out var bloodstream, out var zones, out var inventory))
        {
            wounds.Clear();
            CollectWounds((uid, zones), bloodstream, wounds);
            if (wounds.Count == 0)
                continue;

            var color = GetBloodColor(bloodstream);
            var slots = _inventory.GetSlotEnumerator((uid, inventory));
            while (slots.NextItem(out var item))
            {
                if (_zones.GetCoverage(item) is not { } covered)
                    continue;

                var amount = 0f;
                foreach (var (zone, rate) in wounds)
                {
                    if (covered.Contains(zone))
                        amount += rate;
                }

                Stain(item, amount * dt, color);
            }
        }
    }

    /// <summary>
    /// Where the blood comes from and how fast, as clothing stain per second: gushing stumps, and the bleeding
    /// spread over the wounds (zones without skin or muscles, or badly hurt).
    /// </summary>
    private void CollectWounds(Entity<BodyZonesComponent> ent, BloodstreamComponent bloodstream, Dictionary<BodyZone, float> wounds)
    {
        if (TryComp<CirculationComponent>(ent, out var circulation) && !_mobState.IsDead(ent))
        {
            foreach (var (zone, rate) in circulation.Stumps)
            {
                if (!_zones.IsTourniqueted(circulation, zone))
                    wounds[zone] = wounds.GetValueOrDefault(zone) + rate * StainPerGush;
            }
        }

        if (bloodstream.BleedAmount <= 0f)
            return;

        var open = new List<BodyZone>();
        foreach (var (zone, data) in ent.Comp.Zones)
        {
            if (_zones.IsMissing(ent.Comp, zone))
                continue;

            if (ent.Comp.Skinless.Contains(zone)
                || ent.Comp.TissuesTorn.Contains(zone)
                || _zones.GetDamage(ent.Comp, zone) >= data.MaxHealth * WoundRatio)
                open.Add(zone);
        }

        // Bleeding from somewhere we can't tell: it runs down the chest.
        if (open.Count == 0)
            open.Add(BodyZone.Chest);

        var share = bloodstream.BleedAmount * StainPerBleed / open.Count;
        foreach (var zone in open)
        {
            wounds[zone] = wounds.GetValueOrDefault(zone) + share;
        }
    }

    private Color GetBloodColor(BloodstreamComponent bloodstream)
    {
        var reference = bloodstream.BloodReferenceSolution.Contents;
        foreach (var reagent in reference)
        {
            if (_proto.Resolve<ReagentPrototype>(reagent.Reagent.Prototype, out var proto))
                return proto.SubstanceColor;
        }

        return Color.FromHex("#800000");
    }

    #endregion

    #region Footprints

    private void OnMove(Entity<FootprintsComponent> ent, ref MoveEvent args)
    {
        var xform = args.Component;
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            ent.Comp.LastGrid = null;
            return;
        }

        var position = _transform.WithEntityId(args.NewPosition, grid);
        var tile = _map.TileIndicesFor(grid, gridComp, position);
        if (ent.Comp.LastGrid == grid && ent.Comp.LastTile == tile)
            return;

        var from = ent.Comp.LastGrid == grid ? ent.Comp.LastPosition : null;
        ent.Comp.LastGrid = grid;
        ent.Comp.LastTile = tile;
        ent.Comp.LastPosition = position;

        // Only walking leaves prints: not lying, floating or being dragged dead.
        if (_standing.IsDown(ent.Owner) || _gravity.IsWeightless(ent.Owner) || !_mobState.IsAlive(ent))
            return;

        if (TryStepIntoPuddle(ent, grid, gridComp, tile))
            return;

        if (ent.Comp.PrintsLeft <= 0 || from is not { } previous)
            return;

        var step = position.Position - previous.Position;
        if (step.LengthSquared() < 0.01f)
            return;

        LeavePrint(ent, position, Vector2.Normalize(step));
    }

    /// <summary>
    /// Stepping into a puddle wets the feet: the next tiles get prints of its color. Blood gets on the shoes too.
    /// </summary>
    private bool TryStepIntoPuddle(Entity<FootprintsComponent> ent, EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (!TryComp<PuddleComponent>(uid, out var puddle)
                || !_solutions.TryGetSolution(uid.Value, puddle.SolutionName, out _, out var solution)
                || solution.Volume < ent.Comp.MinPuddleVolume)
                continue;

            ent.Comp.PrintsLeft = ent.Comp.MaxPrints;
            ent.Comp.PrintColor = solution.GetColor(_proto);

            if (solution.Contents.Any(r => ent.Comp.BloodReagents.Contains(r.Reagent.Prototype))
                && _inventory.TryGetSlotEntity(ent, "shoes", out var shoes))
            {
                Stain(shoes.Value, ent.Comp.ShoeStain, ent.Comp.PrintColor);
            }

            return true;
        }

        return false;
    }

    private void LeavePrint(Entity<FootprintsComponent> ent, EntityCoordinates position, Vector2 direction)
    {
        // The sprite points north; turn it the way the body walks, and put it under the left or right foot.
        var rotation = new Angle(Math.Atan2(-direction.X, direction.Y));
        var side = new Vector2(-direction.Y, direction.X) * (ent.Comp.LeftFoot ? ent.Comp.FootOffset : -ent.Comp.FootOffset);
        var decal = _inventory.TryGetSlotEntity(ent, "shoes", out _) ? ent.Comp.ShoeDecal : ent.Comp.BareDecal;

        // Fresh prints are bold, the last ones barely there.
        var fade = MathF.Max(0.25f, (float) ent.Comp.PrintsLeft / ent.Comp.MaxPrints);
        var color = ent.Comp.PrintColor.WithAlpha(ent.Comp.PrintColor.A * 0.85f * fade);

        // The decal is placed by its corner; the floor is checked under the print itself, so a print near the edge
        // of a tile isn't refused for the empty space next to it.
        var center = position.Offset(side);
        var print = new Decal(center.Position - new Vector2(0.5f, 0.5f),
            $"{decal}{(ent.Comp.LeftFoot ? "Left" : "Right")}",
            color,
            rotation,
            0,
            cleanable: true);
        _decals.TryAddDecal(print, center, out _);

        ent.Comp.LeftFoot = !ent.Comp.LeftFoot;
        ent.Comp.PrintsLeft--;
    }

    #endregion

    #region Shower

    private void UpdateShowers(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ShowerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var shower, out var xform))
        {
            if (!shower.Enabled || now < shower.NextUpdate)
                continue;

            shower.NextUpdate = now + shower.UpdateInterval;
            var amount = shower.WashRate * (float) shower.UpdateInterval.TotalSeconds;

            foreach (var target in _lookup.GetEntitiesInRange(xform.Coordinates, 0.45f, LookupFlags.Dynamic | LookupFlags.Sundries))
            {
                if (target == uid)
                    continue;

                WashEntity(target, amount);
            }
        }
    }

    /// <summary>
    /// Washes everything on someone: the clothes, what they hold, their feet. Puts out fire.
    /// </summary>
    public void WashEntity(EntityUid target, float amount)
    {
        Wash(target, amount);

        if (TryComp<FootprintsComponent>(target, out var footprints))
            footprints.PrintsLeft = 0;

        if (HasComp<FlammableComponent>(target))
            _flammable.Extinguish(target);

        foreach (var item in _inventory.GetHandOrInventoryEntities(target))
        {
            Wash(item, amount);
        }
    }

    #endregion
}
