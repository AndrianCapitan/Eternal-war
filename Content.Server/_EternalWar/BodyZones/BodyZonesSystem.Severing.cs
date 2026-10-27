using System.Linq;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;

namespace Content.Server._EternalWar.BodyZones;

/// <summary>
/// Heavy blades cutting limbs off in one blow, severed parts being taken apart with a knife,
/// and the remains falling apart once nothing is left on the torso.
/// </summary>
public sealed partial class BodyZonesSystem
{
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>
    /// Bodies hit by a <see cref="LimbSeverComponent"/> weapon right now, until the damage of the hit lands.
    /// </summary>
    private readonly Dictionary<EntityUid, (Entity<LimbSeverComponent> Weapon, EntityUid User)> _severingHits = new();

    /// <summary>
    /// Limbs cut off by a hit, taken off in <see cref="Update"/> together with the other consequences of the damage.
    /// </summary>
    private readonly Dictionary<EntityUid, List<(BodyZone Zone, EntityUid User, EntityUid Weapon)>> _toSever = new();

    /// <summary>
    /// Bodies that lost an organ, to check whether anything is left on their torso.
    /// </summary>
    private readonly HashSet<EntityUid> _maybeEmptied = new();

    private void InitializeSevering()
    {
        SubscribeLocalEvent<LimbSeverComponent, MeleeHitEvent>(OnSeveringHit);
        SubscribeLocalEvent<SeveredPartComponent, InteractUsingEvent>(OnPartInteractUsing);
        SubscribeLocalEvent<SeveredPartComponent, PartDismantleDoAfterEvent>(OnPartDismantleDoAfter);
    }

    #region Cutting limbs off

    private void OnSeveringHit(Entity<LimbSeverComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (HasComp<BodyZonesComponent>(hit))
                _severingHits[hit] = (ent, args.User);
        }
    }

    /// <summary>
    /// Rolls whether the damage of a severing blow cuts the limb part it landed on off.
    /// </summary>
    private void RecordSeverHit(Entity<BodyZonesComponent> ent, BodyZone? zone, DamageSpecifier delta)
    {
        if (!_severingHits.Remove(ent, out var hit) || zone is not { } target)
            return;

        var weapon = hit.Weapon.Comp;
        if (delta.DamageDict.GetValueOrDefault(weapon.DamageType) <= 0)
            return;

        var hitZone = Redirect(ent.Comp, target);
        if (!CanSever(ent.Comp, hitZone))
            return;

        var chance = weapon.Chance;
        if (IsFractured(ent.Comp, hitZone) || GetHealth(ent.Comp, hitZone) <= 0)
            chance *= weapon.DamagedMultiplier;

        if (!_random.Prob(Math.Clamp(chance, 0f, 1f)))
            return;

        if (!_toSever.TryGetValue(ent, out var zones))
            _toSever[ent] = zones = new();

        zones.Add((hitZone, hit.User, hit.Weapon));
    }

    /// <summary>
    /// Only limb parts can be cut off: an arm, a leg or a part of them.
    /// </summary>
    public bool CanSever(BodyZonesComponent comp, BodyZone zone)
    {
        return comp.Zones.TryGetValue(zone, out var data)
               && data is { Organ: not null, TearOff: true }
               && !IsMissing(comp, zone);
    }

    private void CheckSevered(Entity<BodyZonesComponent> ent)
    {
        if (!_toSever.Remove(ent, out var zones))
            return;

        foreach (var (zone, user, weapon) in zones)
        {
            if (SeverZone(ent, zone))
                _adminLogger.Add(LogType.Damaged, LogImpact.High, $"{ToPrettyString(user):user} cut off the {zone} of {ToPrettyString(ent):target} with {ToPrettyString(weapon):weapon}");
        }
    }

    /// <summary>
    /// Cuts a limb part off in one piece, together with everything inside it and hanging on it:
    /// a cut upper arm takes the forearm and the hand with it.
    /// </summary>
    /// <returns>False if the zone isn't a limb part that is there.</returns>
    public bool SeverZone(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (!CanSever(ent.Comp, zone) || ent.Comp.Zones[zone].Organ is not { } category)
            return false;

        var organs = GetOrgans(ent, category);
        if (organs.Count == 0)
            return false;

        var body = ent.Owner;
        foreach (var slot in ent.Comp.Zones[zone].DropSlots)
        {
            _inventory.TryUnequip(body, slot, silent: true, force: true);
        }

        // A clean cut: the damage of the part goes to the torso, so the stump isn't torn apart by the same blow.
        var torso = zone;
        while (!torso.IsTorso() && torso.Parent() is { } up)
        {
            torso = up;
        }

        foreach (var lost in ent.Comp.Zones.Keys)
        {
            if (IsPartOf(lost, zone) && ent.Comp.Damage.Remove(lost, out var damage))
                AddDamage(ent.Comp, torso, damage);
        }

        if (_detachableOrgan.Detach(organs[0]) is not { } piece)
            return false;

        _metaData.SetEntityName(piece, Loc.GetString($"{zone.LocKey()}-detached"));
        Fling(piece);

        _bloodstream.TryBleedOut(body, 20);
        _bloodstream.TryModifyBleedAmount(body, ent.Comp.SeverBleed);
        _audio.PlayPvs(GibSound, body);
        _popup.PopupEntity(Loc.GetString("body-zones-severed",
                ("target", Identity.Entity(body, EntityManager)),
                ("part", Loc.GetString(zone.LocKey()))),
            body,
            PopupType.LargeCaution);

        _movementSpeed.RefreshMovementSpeedModifiers(body);
        Dirty(ent);
        return true;
    }

    /// <summary>
    /// Whether the zone is the part itself or hangs on it, e.g. the hand for the upper arm.
    /// </summary>
    private static bool IsPartOf(BodyZone zone, BodyZone part)
    {
        for (BodyZone? z = zone; z is { } current; z = current.Parent())
        {
            if (current == part)
                return true;
        }

        return false;
    }

    #endregion

    #region Remains falling apart

    private void CheckEmptiedBodies()
    {
        if (_maybeEmptied.Count == 0)
            return;

        var bodies = _maybeEmptied.ToArray();
        _maybeEmptied.Clear();

        foreach (var uid in bodies)
        {
            if (!TerminatingOrDeleted(uid) && TryComp<BodyZonesComponent>(uid, out var comp))
                CheckEmptied((uid, comp));
        }
    }

    /// <summary>
    /// Once the head and all the limbs are gone and the torso is torn open, nothing is left of the body but the
    /// torso itself: it falls apart, and whatever was still in it (the genitals, the clothes) falls out.
    /// The bones still in it stay together, as a skeleton.
    /// </summary>
    private void CheckEmptied(Entity<BodyZonesComponent> ent)
    {
        foreach (var (zone, data) in ent.Comp.Zones)
        {
            if (zone.IsTorso())
            {
                if (!ent.Comp.Destroyed.Contains(zone))
                    return;

                continue;
            }

            // The head, the upper arms and the thighs: the parts attached right to the torso.
            if (data.Organ is { } organ && data.Inside == null && zone.Parent() is { } parent && parent.IsTorso()
                && GetOrgans(ent, organ).Count > 0)
                return;
        }

        _popup.PopupCoordinates(Loc.GetString("body-zones-remains-fall-apart", ("target", Identity.Entity(ent, EntityManager))),
            Transform(ent).Coordinates,
            PopupType.LargeCaution);
        _adminLogger.Add(LogType.Damaged, LogImpact.High, $"{ToPrettyString(ent):target} fell apart after losing every part of their body");
        ExtractSkeleton(ent);
        _gibbing.Gib(ent);
    }

    #endregion

    #region Taking severed parts apart

    private void OnPartInteractUsing(Entity<SeveredPartComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || !IsSharp(args.Used, ent.Comp.DismantleQuality)
            || !_container.TryGetContainer(ent, BodyComponent.ContainerID, out var container)
            || container.ContainedEntities.Count == 0)
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.DismantleDelay, new PartDismantleDoAfterEvent(), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("part-dismantle-start",
                ("user", Identity.Entity(args.User, EntityManager)),
                ("part", Name(ent))),
            ent,
            PopupType.Medium);
    }

    /// <summary>
    /// The part is cut to pieces: the bones, skin, muscles and vessels fall out one by one, and the part itself
    /// (an empty shell of a hand or a forearm) is gone. The bones come out as they were, a knife doesn't break them.
    /// </summary>
    private void OnPartDismantleDoAfter(Entity<SeveredPartComponent> ent, ref PartDismantleDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (!_container.TryGetContainer(ent, BodyComponent.ContainerID, out var container))
            return;

        var part = Name(ent);
        var bones = new List<EntityUid>();
        foreach (var organ in container.ContainedEntities.ToList())
        {
            if (_childQuery.HasComp(organ))
                _organRelation.Orphan(organ);

            if (!_container.Remove(organ, container))
                continue;

            if (IsLimbPart(organ))
            {
                QueueDel(organ);
                continue;
            }

            // The bones of a head stay together: a skull with its jaw.
            if (HasComp<BoneComponent>(organ))
            {
                _transform.SetCoordinates(organ, _transform.GetMoverCoordinates(ent));
                bones.Add(organ);
                continue;
            }

            _transform.DropNextTo(organ, ent.Owner);
            if (!_container.IsEntityInContainer(organ))
                Fling(organ);
        }

        foreach (var piece in Assemble(bones, _transform.GetMoverCoordinates(ent)))
        {
            Fling(piece);
        }

        _audio.PlayPvs(SkinningSound, ent);
        _popup.PopupEntity(Loc.GetString("part-dismantle-done", ("part", part)), ent, PopupType.Medium);
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):user} took apart {ToPrettyString(ent):part} with {ToPrettyString(args.Used):tool}");
        QueueDel(ent);
    }

    /// <summary>
    /// A limb part itself (a hand, a forearm), as opposed to what lies inside it.
    /// </summary>
    private bool IsLimbPart(EntityUid organ)
    {
        return HasComp<DetachableOrganComponent>(organ) && !HasComp<InternalChildOrganComponent>(organ);
    }

    #endregion
}
