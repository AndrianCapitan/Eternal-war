using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._EternalWar.Pressure;
using Content.Shared.Administration.Logs;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Fluids;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Materials;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.PowerCell.Components;
using Content.Shared.Item;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._EternalWar.Pressure;

/// <summary>
/// Things breaking under pressure:
/// <list type="bullet">
/// <item>sealed containers (closed drinks, light bulbs) burst when the inside presses harder than the walls hold,
/// and are crushed when the outside does: the gas sealed in, the carbon dioxide of a fizzy drink and the vapour of the
/// liquid all grow as the container warms up (<see cref="PressurePhysics"/>);</item>
/// <item>electronics are crushed by a very high pressure;</item>
/// <item>things carried by space wind hit people and break against walls;</item>
/// <item>bursting containers and windows that give way under pressure throw pieces around.</item>
/// </list>
/// How much a thing holds and what is left of it comes from what it is made of (<see cref="PressureMaterialPrototype"/>).
/// </summary>
public sealed partial class PressureBreakageSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private BrokenElectronicsSystem _electronics = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private PressurizedSolutionSystem _pressurized = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedLightBulbSystem _bulbs = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPoweredLightSystem _lights = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>
    /// Containers are checked once a second.
    /// </summary>
    private const float UpdateInterval = 1f;

    private static readonly ProtoId<PressureMaterialPrototype> BulbMaterial = "ThinGlass";
    private static readonly ProtoId<PressureMaterialPrototype> ElectronicsMaterial = "Electronics";
    private static readonly ProtoId<PressureMaterialPrototype> WindowMaterial = "Glass";

    private float _accumulator;

    /// <summary>
    /// The materials by what things are made of, see <see cref="PressureMaterialPrototype.Materials"/>.
    /// </summary>
    private readonly Dictionary<ProtoId<MaterialPrototype>, List<PressureMaterialPrototype>> _byMaterial = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<PressureTornComponent, ExaminedEvent>(OnTornExamined);

        InitializeVessels();
        InitializeImpacts();
        InitializeShrapnel();

        CacheMaterials();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator -= UpdateInterval;
        UpdateVessels(UpdateInterval);
        UpdateElectronics();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<PressureMaterialPrototype>())
            CacheMaterials();
    }

    private void CacheMaterials()
    {
        _byMaterial.Clear();
        foreach (var material in _proto.EnumeratePrototypes<PressureMaterialPrototype>())
        {
            foreach (var id in material.Materials)
            {
                if (!_byMaterial.TryGetValue(id, out var list))
                    _byMaterial[id] = list = new List<PressureMaterialPrototype>();

                list.Add(material);
            }
        }
    }

    #region Materials

    /// <summary>
    /// What a thing is made of, as far as pressure is concerned: by the material there is most of in it.
    /// </summary>
    public PressureMaterialPrototype? GetCompositionMaterial(EntityUid uid)
    {
        if (!TryComp<PhysicalCompositionComponent>(uid, out var composition)
            || composition.MaterialComposition.Count == 0)
            return null;

        var (main, amount) = composition.MaterialComposition.MaxBy(pair => pair.Value);
        if (!_byMaterial.TryGetValue(main, out var candidates))
            return null;

        PressureMaterialPrototype? best = null;
        foreach (var candidate in candidates)
        {
            if (candidate.MinAmount <= amount && (best == null || candidate.MinAmount > best.MinAmount))
                best = candidate;
        }

        return best;
    }

    /// <summary>
    /// Light bulbs are thin glass, devices break by their electronics, the rest by what it is made of.
    /// </summary>
    public PressureMaterialPrototype? GetMaterial(EntityUid uid)
    {
        if (HasComp<LightBulbComponent>(uid))
            return _proto.Index(BulbMaterial);

        if (IsElectronic(uid))
            return _proto.Index(ElectronicsMaterial);

        return GetCompositionMaterial(uid);
    }

    /// <summary>
    /// A handheld device that runs on a power cell.
    /// </summary>
    public bool IsElectronic(EntityUid uid)
    {
        return HasComp<PowerCellSlotComponent>(uid) && HasComp<ItemComponent>(uid);
    }

    #endregion

    #region Breaking

    /// <summary>
    /// Breaks a thing the way its material breaks: glass and plastic smash, metal and cardboard tear,
    /// electronics stop working. What was in it spills, and the pieces fly with a share of the energy let go.
    /// </summary>
    /// <param name="crushed">Crushed from outside rather than burst from inside.</param>
    /// <param name="energy">J let go by the burst.</param>
    public void Break(EntityUid uid, PressureMaterialPrototype material, bool crushed, float energy)
    {
        if (TerminatingOrDeleted(uid))
            return;

        var coords = _transform.GetMoverCoordinates(uid);
        var holder = GetHolder(uid);

        if (material.BreakSound != null)
            _audio.PlayPvs(material.BreakSound, coords);

        var message = material.Breakage switch
        {
            PressureBreakage.Disabled => "pressure-break-electronics",
            _ when crushed => "pressure-break-crushed",
            _ => "pressure-break-burst",
        };
        var text = Loc.GetString(message, ("item", uid), ("material", Loc.GetString(material.Name)));
        if (holder != null)
            _popup.PopupEntity(text, holder.Value, PopupType.MediumCaution);
        else
            _popup.PopupCoordinates(text, coords, PopupType.MediumCaution);

        _adminLogger.Add(LogType.Damaged,
            LogImpact.Low,
            $"{ToPrettyString(uid):entity} ({material.ID}) was {(crushed ? "crushed" : "burst")} by pressure");

        if (material.Breakage != PressureBreakage.Disabled)
            Spill(uid, coords);

        Shrapnel(uid, _transform.ToMapCoordinates(coords), energy * material.ShrapnelShare, material.ShrapnelDamage, null, holder);
        RemComp<PressureVesselComponent>(uid);

        switch (material.Breakage)
        {
            case PressureBreakage.Destroyed:
                if (TryComp<LightBulbComponent>(uid, out var bulb))
                {
                    BreakBulb((uid, bulb));
                    break;
                }

                if (material.Debris is { } debris)
                    Spawn(debris, coords);

                QueueDel(uid);
                break;
            case PressureBreakage.Torn:
                _openable.SetOpen(uid);
                var torn = EnsureComp<PressureTornComponent>(uid);
                torn.Material = material.ID;
                torn.Crushed = crushed;
                break;
            case PressureBreakage.Disabled:
                _electronics.Break(uid);
                break;
        }
    }

    /// <summary>
    /// A bulb in a fixture breaks as if hit, so the fixture goes dark; a loose one just breaks.
    /// </summary>
    private void BreakBulb(Entity<LightBulbComponent> bulb)
    {
        if (_container.TryGetContainingContainer(bulb.Owner, out var container)
            && TryComp<PoweredLightComponent>(container.Owner, out var fixture)
            && _lights.TryDestroyBulb(container.Owner, fixture))
            return;

        _bulbs.SetState(bulb, LightBulbState.Broken, bulb.Comp);
    }

    /// <summary>
    /// Everything in it splashes around.
    /// </summary>
    private void Spill(EntityUid uid, EntityCoordinates coords)
    {
        foreach (var (_, solution) in _solutions.EnumerateSolutions(uid).ToList())
        {
            if (solution.Comp.Solution.Volume <= 0)
                continue;

            var spilled = _solutions.SplitSolution(solution, solution.Comp.Solution.Volume);
            _puddle.TrySplashSpillAt(uid, coords, spilled, out _, sound: false);
        }
    }

    /// <summary>
    /// The mob that holds a thing, in its hands or anywhere on it.
    /// </summary>
    private EntityUid? GetHolder(EntityUid uid)
    {
        var current = uid;
        while (_container.TryGetContainingContainer(current, out var container))
        {
            current = container.Owner;
            if (HasComp<MobStateComponent>(current))
                return current;
        }

        return null;
    }

    private void OnTornExamined(Entity<PressureTornComponent> ent, ref ExaminedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.Material, out var material))
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Crushed ? "pressure-torn-examine-crushed" : "pressure-torn-examine-burst",
            ("material", Loc.GetString(material.Name))));
    }

    #endregion

    #region Electronics

    private void UpdateElectronics()
    {
        var electronics = _proto.Index(ElectronicsMaterial);
        var query = EntityQueryEnumerator<PowerCellSlotComponent, ItemComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (HasComp<BrokenElectronicsComponent>(uid) || Paused(uid) || Transform(uid).MapUid == null)
                continue;

            if (_atmos.GetContainingMixture(uid) is not { } air)
                continue;

            if (air.Pressure - Atmospherics.OneAtmosphere >= electronics.CrushPressure)
                Break(uid, electronics, crushed: true, energy: 0f);
        }
    }

    #endregion
}
