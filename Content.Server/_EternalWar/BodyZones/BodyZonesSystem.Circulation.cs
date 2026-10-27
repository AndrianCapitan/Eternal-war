using System.Linq;
using Content.Server.Body.Components;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Fluids;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._EternalWar.BodyZones;

/// <summary>
/// The circulation: the heart pumps oxygenated blood from the lungs through the vessels of the zones,
/// every zone lives off it, the brain suffocates without it, cut arteries gush and tourniquets stop them.
/// See <see cref="CirculationComponent"/>.
/// </summary>
public sealed partial class BodyZonesSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;

    /// <summary>
    /// Vessel organ categories of each zone prototype list, resolved once.
    /// </summary>
    private readonly Dictionary<EntProtoId, bool> _isVessel = new();

    private void InitializeCirculation()
    {
        SubscribeLocalEvent<TourniquetComponent, AfterInteractEvent>(OnTourniquetAfterInteract);
        SubscribeLocalEvent<TourniquetComponent, TourniquetDoAfterEvent>(OnTourniquetDoAfter);
        SubscribeLocalEvent<CirculationComponent, GetVerbsEvent<Verb>>(OnCirculationVerbs);
    }

    #region Blood flow

    private void UpdateCirculation()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CirculationComponent, BodyZonesComponent>();
        while (query.MoveNext(out var uid, out var circulation, out var zones))
        {
            if (now < circulation.NextUpdate)
                continue;

            circulation.NextUpdate = now + circulation.UpdateInterval;

            // A dead body is left as it is: nothing pumps, nothing changes until it is revived.
            if (_mobState.IsDead(uid))
                continue;

            TickCirculation((uid, zones), circulation, (float) circulation.UpdateInterval.TotalSeconds);
        }
    }

    private void TickCirculation(Entity<BodyZonesComponent> ent, CirculationComponent circulation, float dt)
    {
        var body = ent.Owner;
        var bloodLevel = TryComp<BloodstreamComponent>(body, out var bloodstream) ? _bloodstream.GetBloodLevel((body, bloodstream)) : 1f;
        // A crushed heart doesn't pump, just like a missing one.
        var pumping = HasWorkingOrgan(ent, circulation.Heart) && bloodLevel >= circulation.MinBloodLevel;

        // The lungs fill the blood with oxygen: the respirator's saturation is the oxygen in the blood.
        TryComp<RespiratorComponent>(body, out var respirator);
        var oxygenated = respirator == null || respirator.Saturation > respirator.SuffocationThreshold;

        // Lungs that are there but crushed take in nothing: the blood gets no oxygen.
        if (ent.Comp.PairedLungs && ent.Comp.MissingLungs >= ent.Comp.Lungs.Count)
            oxygenated = false;

        var changed = false;
        var total = 0f;
        var supplied = 0f;
        var dead = 0;
        foreach (var (zone, data) in ent.Comp.Zones)
        {
            if (IsMissing(ent.Comp, zone))
                continue;

            total += data.MaxHealth.Float();
            var perfused = pumping && BloodReaches(ent, circulation, zone);
            var necrotic = circulation.Necrotic.Contains(zone);
            if (necrotic)
                dead++;
            else if (perfused)
                supplied += data.MaxHealth.Float();

            if (perfused && oxygenated)
            {
                circulation.Oxygen.Remove(zone);
                circulation.Hypoxia.Remove(zone);
                if (circulation.Numb.Remove(zone))
                {
                    changed = true;
                    OnNumbChanged(ent, zone, numb: false);
                }

                continue;
            }

            var reserve = circulation.Oxygen.TryGetValue(zone, out var left) ? left : MaxOxygen(circulation, zone);
            reserve = MathF.Max(0f, reserve - dt);
            circulation.Oxygen[zone] = reserve;

            // The head and the torso don't go numb or die on their own: the brain suffocates first.
            if (reserve > 0f || necrotic || !IsLimbZone(data))
                continue;

            if (circulation.Numb.Add(zone))
            {
                changed = true;
                OnNumbChanged(ent, zone, numb: true);
            }

            var hypoxia = circulation.Hypoxia.GetValueOrDefault(zone) + dt;
            circulation.Hypoxia[zone] = hypoxia;
            if (hypoxia < circulation.NecrosisTime)
                continue;

            circulation.Necrotic.Add(zone);
            circulation.Numb.Remove(zone);
            circulation.Hypoxia.Remove(zone);
            changed = true;
            _popup.PopupEntity(Loc.GetString("circulation-necrosis",
                    ("target", Identity.Entity(body, EntityManager)),
                    ("part", Loc.GetString(zone.LocKey()))),
                body,
                PopupType.MediumCaution);
            _adminLogger.Add(LogType.Damaged, LogImpact.Medium, $"{ToPrettyString(body):target} lost their {zone} to necrosis");
        }

        // The brain: without oxygen the body suffocates, with it the suffocation heals.
        if (!IsMissing(ent.Comp, circulation.BrainZone))
        {
            var brainStarving = circulation.Oxygen.TryGetValue(circulation.BrainZone, out var brain) && brain <= 0f;
            _damageable.ChangeDamage(body,
                brainStarving ? circulation.BrainHypoxiaDamage : circulation.BrainRecovery,
                ignoreResistances: true,
                interruptsDoAfters: false);
        }

        if (dead > 0)
            _damageable.ChangeDamage(body, circulation.NecrosisDamage * dead, ignoreResistances: true, interruptsDoAfters: false);

        // Every living tissue the blood reaches uses oxygen. The respirator takes the whole body's use off every
        // breath; give back the share of tissues that are gone or get no blood.
        if (respirator != null && total > 0f)
            _respirator.UpdateSaturation(body, (1f - supplied / total) * dt / respirator.UpdateIntervalMultiplier, respirator);

        if (pumping)
            TickFountains(ent, circulation, bloodstream, bloodLevel, dt);

        if (changed)
        {
            _movementSpeed.RefreshMovementSpeedModifiers(body);
            Dirty(body, circulation);
        }
    }

    private static float MaxOxygen(CirculationComponent circulation, BodyZone zone)
    {
        return zone == circulation.BrainZone ? circulation.BrainOxygenReserve : circulation.OxygenReserve;
    }

    private static bool IsLimbZone(BodyZoneData data)
    {
        return data is { Organ: not null, TearOff: true };
    }

    /// <summary>
    /// The zone blood flows into from: its parent, and the chest (the heart) for the abdomen.
    /// </summary>
    private static BodyZone? FlowParent(BodyZone zone)
    {
        return zone == BodyZone.Abdomen ? BodyZone.Chest : zone.Parent();
    }

    /// <summary>
    /// Whether blood from the heart gets to the zone: through the vessels of every zone on the way, the zone's own
    /// included, with no tourniquet in between. Zones without vessels of their own (a hand, an eye) pass it on.
    /// </summary>
    public bool BloodReaches(Entity<BodyZonesComponent> ent, CirculationComponent circulation, BodyZone zone)
    {
        for (BodyZone? z = zone; z is { } current; z = FlowParent(current))
        {
            if (circulation.Tourniquets.Contains(current) || !VesselsWhole(ent, current))
                return false;
        }

        return true;
    }

    private bool VesselsWhole(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (!ent.Comp.Zones.TryGetValue(zone, out var data))
            return true;

        foreach (var proto in data.Tissues)
        {
            if (IsVessel(proto) && GetCategory(proto) is { } category && GetOrgans(ent, category).Count == 0)
                return false;
        }

        return true;
    }

    private bool IsVessel(EntProtoId proto)
    {
        if (_isVessel.TryGetValue(proto, out var vessel))
            return vessel;

        vessel = ProtoMan.Resolve(proto, out var prototype) && prototype.TryComp<BloodVesselComponent>(out _, _componentFactory);
        _isVessel[proto] = vessel;
        return vessel;
    }

    /// <summary>
    /// A numb arm can't hold anything, like a broken one; the legs are handled by the movement speed.
    /// </summary>
    private void OnNumbChanged(Entity<BodyZonesComponent> ent, BodyZone zone, bool numb)
    {
        if (zone.HandOf() is { } hand)
            RefreshHandBlock(ent, hand);

        if (numb)
            _popup.PopupEntity(Loc.GetString("circulation-numb", ("part", Loc.GetString(zone.LocKey()))), ent, ent, PopupType.MediumCaution);
    }

    #endregion

    #region Stumps

    /// <summary>
    /// Blood gushes out of every stump no tourniquet holds: most of it pours down, some sprays a step away.
    /// </summary>
    private void TickFountains(Entity<BodyZonesComponent> ent, CirculationComponent circulation, BloodstreamComponent? bloodstream, float bloodLevel, float dt)
    {
        if (circulation.Stumps.Count == 0 || bloodstream == null)
            return;

        var body = ent.Owner;
        BodyZone? gushing = null;
        foreach (var (zone, rate) in circulation.Stumps)
        {
            if (IsTourniqueted(circulation, zone))
                continue;

            var amount = rate * dt * MathF.Min(1f, bloodLevel);
            var spray = amount * circulation.FountainSprayShare;
            _bloodstream.TryBleedOut((body, bloodstream), amount - spray);

            // The same blood as in the body, DNA included.
            var reference = bloodstream.BloodReferenceSolution.Contents;
            var jet = new Solution();
            foreach (var reagent in reference)
            {
                jet.AddReagent(reagent.Reagent, reagent.Quantity);
            }

            jet.ScaleTo(spray);
            if (jet.Volume > 0 && _bloodstream.TryModifyBloodLevel((body, bloodstream), -spray))
            {
                var coords = Transform(body).Coordinates.Offset(_random.NextAngle().ToVec() * _random.NextFloat(1f, 1.5f));
                _puddle.TrySpillAt(coords, jet, out _, sound: false);
            }

            gushing ??= zone;
        }

        if (gushing is not { } shown || _timing.CurTime < circulation.NextFountainPopup)
            return;

        circulation.NextFountainPopup = _timing.CurTime + circulation.FountainPopupInterval;
        _popup.PopupEntity(Loc.GetString("circulation-fountain",
                ("target", Identity.Entity(body, EntityManager)),
                ("part", Loc.GetString(shown.LocKey()))),
            body,
            PopupType.MediumCaution);
    }

    /// <summary>
    /// A limb part came off: its stump starts gushing, and tourniquets on it or below fall off with it.
    /// Called from <see cref="OnOrganRemoved"/> once the zone is marked missing.
    /// </summary>
    private void OnLimbLost(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (!TryComp<CirculationComponent>(ent, out var circulation)
            || !ent.Comp.Zones.TryGetValue(zone, out var data)
            || !IsLimbZone(data))
            return;

        foreach (var lost in circulation.Tourniquets.Where(t => IsPartOf(t, zone)).ToList())
        {
            circulation.Tourniquets.Remove(lost);
            SpawnNextToOrDrop(circulation.TourniquetPrototype, ent);
        }

        foreach (var lost in ent.Comp.Zones.Keys.Where(z => IsPartOf(z, zone)))
        {
            circulation.Stumps.Remove(lost);
            circulation.Oxygen.Remove(lost);
            circulation.Hypoxia.Remove(lost);
            circulation.Numb.Remove(lost);
            circulation.Necrotic.Remove(lost);
        }

        // Lost together with the part above it: that one's stump is the one that bleeds.
        if (zone.Parent() is not { } parent || !IsMissing(ent.Comp, parent))
        {
            if (data.ArterialBleed > 0)
                circulation.Stumps[zone] = data.ArterialBleed;
        }

        Dirty(ent, circulation);
    }

    private void OnLimbRestored(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (TryComp<CirculationComponent>(ent, out var circulation) && circulation.Stumps.Remove(zone))
            Dirty(ent, circulation);
    }

    /// <summary>
    /// Rejuvenation: blood everywhere again, nothing numb or dead, the tourniquets come off.
    /// </summary>
    private void ResetCirculation(Entity<BodyZonesComponent> ent)
    {
        if (!TryComp<CirculationComponent>(ent, out var circulation))
            return;

        foreach (var _ in circulation.Tourniquets)
        {
            SpawnNextToOrDrop(circulation.TourniquetPrototype, ent);
        }

        circulation.Tourniquets.Clear();
        circulation.Stumps.Clear();
        circulation.Oxygen.Clear();
        circulation.Hypoxia.Clear();
        circulation.Numb.Clear();
        circulation.Necrotic.Clear();
        Dirty(ent, circulation);
    }

    #endregion

    #region Tourniquet

    /// <summary>
    /// The limb zone a tourniquet aimed at the zone goes on: the zone itself, or the stump of the lost limb
    /// (the topmost missing part) when it's gone. Null if it isn't a limb.
    /// </summary>
    public BodyZone? GetTourniquetZone(BodyZonesComponent comp, BodyZone zone)
    {
        if (!comp.Zones.TryGetValue(zone, out var data) || !IsLimbZone(data))
            return null;

        while (IsMissing(comp, zone) && zone.Parent() is { } parent && IsMissing(comp, parent))
        {
            zone = parent;
        }

        return zone;
    }

    private void OnTourniquetAfterInteract(Entity<TourniquetComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target
            || !TryComp<BodyZonesComponent>(target, out var zones)
            || !TryComp<CirculationComponent>(target, out var circulation))
            return;

        args.Handled = true;

        var selected = TryComp<TargetingComponent>(args.User, out var targeting) ? targeting.Selected : BodyZone.Chest;
        if (GetTourniquetZone(zones, selected) is not { } zone)
        {
            _popup.PopupEntity(Loc.GetString("tourniquet-not-limb"), target, args.User);
            return;
        }

        var part = Loc.GetString(zone.LocKey());
        if (circulation.Tourniquets.Contains(zone))
        {
            _popup.PopupEntity(Loc.GetString("tourniquet-already", ("part", part)), target, args.User);
            return;
        }

        var delay = args.User == target ? ent.Comp.Delay * ent.Comp.SelfDelayMultiplier : ent.Comp.Delay;
        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, new TourniquetDoAfterEvent(zone), ent, target: target, used: ent)
        {
            // Like bandages and splints: the patient is usually bleeding and still being hit.
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("tourniquet-start", ("user", Identity.Entity(args.User, EntityManager)), ("part", part)),
            target,
            PopupType.Medium);
    }

    private void OnTourniquetDoAfter(Entity<TourniquetComponent> ent, ref TourniquetDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target
            || !TryComp<CirculationComponent>(target, out var circulation))
            return;

        args.Handled = true;

        if (!circulation.Tourniquets.Add(args.Zone))
            return;

        Dirty(target, circulation);
        _audio.PlayPvs(ent.Comp.Sound, target);
        _popup.PopupEntity(Loc.GetString("tourniquet-done", ("part", Loc.GetString(args.Zone.LocKey()))), target, PopupType.Medium);
        _adminLogger.Add(LogType.Healed, LogImpact.Low, $"{ToPrettyString(args.User):user} put a tourniquet on the {args.Zone} of {ToPrettyString(target):target}");

        QueueDel(ent);
    }

    private void OnCirculationVerbs(Entity<CirculationComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Tourniquets.Count == 0)
            return;

        var user = args.User;
        foreach (var zone in ent.Comp.Tourniquets)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("tourniquet-verb-remove", ("part", Loc.GetString(zone.LocKey()))),
                Act = () => RemoveTourniquet(ent, zone, user),
            });
        }
    }

    /// <summary>
    /// Takes the tourniquet off the zone and gives it to the user. Blood flows past it again.
    /// </summary>
    public bool RemoveTourniquet(Entity<CirculationComponent> ent, BodyZone zone, EntityUid? user = null)
    {
        if (!ent.Comp.Tourniquets.Remove(zone))
            return false;

        Dirty(ent);
        var tourniquet = SpawnNextToOrDrop(ent.Comp.TourniquetPrototype, ent);
        if (user is { } u)
        {
            _hands.PickupOrDrop(u, tourniquet);
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(u):user} took the tourniquet off the {zone} of {ToPrettyString(ent):target}");
        }

        return true;
    }

    #endregion
}
