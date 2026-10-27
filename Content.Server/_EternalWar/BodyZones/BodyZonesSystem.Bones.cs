using System.Linq;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Humanoid;
using Content.Shared.Body;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._EternalWar.BodyZones;

/// <summary>
/// What lies inside the body zones (bones, muscles, vessels, sex organs), bones breaking and being set with a splint.
/// </summary>
public sealed partial class BodyZonesSystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;

    private static readonly SoundSpecifier FractureSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");

    private static readonly ProtoId<OrganCategoryPrototype> TorsoCategory = "Torso";

    private void InitializeBones()
    {
        // Bones go into the organs InitialBody spawns, so they have to come after it.
        SubscribeLocalEvent<BodyZonesComponent, MapInitEvent>(OnMapInit, after: new[] { typeof(InitialBodySystem) });
        SubscribeLocalEvent<BodyZonesComponent, SexChangedEvent>(OnSexChanged);

        SubscribeLocalEvent<SplintComponent, AfterInteractEvent>(OnSplintAfterInteract);
        SubscribeLocalEvent<SplintComponent, SplintDoAfterEvent>(OnSplintDoAfter);
    }

    private void OnMapInit(Entity<BodyZonesComponent> ent, ref MapInitEvent args)
    {
        EnsureContents(ent);
        EnsureOrganHealth(ent);

        ent.Comp.PairedLungs = ent.Comp.Lungs.All(lung => GetOrgans(ent, lung).Count > 0);
    }

    private void OnSexChanged(Entity<BodyZonesComponent> ent, ref SexChangedEvent args)
    {
        SwapSexOrgans(ent, args.NewSex);
    }

    #region Bones

    /// <summary>
    /// The organ category of an organ prototype, e.g. of a zone's bone.
    /// </summary>
    private ProtoId<OrganCategoryPrototype>? GetCategory(EntProtoId? proto)
    {
        if (proto is not { } id
            || !ProtoMan.Resolve(id, out var prototype)
            || !prototype.TryComp<OrganComponent>(out var organ, _componentFactory))
            return null;

        return organ.Category;
    }

    public Entity<BoneComponent>? GetBone(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (!ent.Comp.Zones.TryGetValue(zone, out var data) || GetCategory(data.Bone) is not { } category)
            return null;

        foreach (var organ in GetOrgans(ent, category))
        {
            if (TryComp<BoneComponent>(organ, out var bone))
                return (organ, bone);
        }

        return null;
    }

    /// <summary>
    /// Puts the bone, skin, tissues, other contents and sex organs into every zone that is there and lacks them.
    /// </summary>
    private void EnsureContents(Entity<BodyZonesComponent> ent)
    {
        var sex = GetSex(ent);
        foreach (var (_, data) in ent.Comp.Zones)
        {
            if (data.Bone is { } bone)
                EnsureOrgan(ent, data, bone);

            if (data.Skin is { } skin)
                EnsureOrgan(ent, data, skin);

            foreach (var proto in data.Tissues.Concat(data.Contents))
            {
                EnsureOrgan(ent, data, proto);
            }

            if (sex is { } s && data.SexContents.TryGetValue(s, out var sexOrgans))
            {
                foreach (var proto in sexOrgans)
                {
                    EnsureOrgan(ent, data, proto);
                }
            }
        }
    }

    private void EnsureOrgan(Entity<BodyZonesComponent> ent, BodyZoneData data, EntProtoId proto)
    {
        if (GetCategory(proto) is not { } category || GetOrgans(ent, category).Count > 0)
            return;

        SpawnInZone(ent, data, proto);
    }

    /// <summary>
    /// Spawns an organ inside the zone, so it leaves together with a torn off limb.
    /// </summary>
    private void SpawnInZone(Entity<BodyZonesComponent> ent, BodyZoneData data, EntProtoId proto)
    {
        if (!_container.TryGetContainer(ent, BodyComponent.ContainerID, out var container))
            return;

        var parentCategory = data.Inside ?? data.Organ ?? TorsoCategory;
        var parents = GetOrgans(ent, parentCategory);
        if (parents.Count == 0)
            return;

        var organ = SpawnAttachedTo(proto, Transform(ent).Coordinates);
        if (!_container.Insert(organ, container, force: true))
        {
            Del(organ);
            return;
        }

        // Hands and feet normally have no organs inside them.
        EnsureComp<ParentOrganComponent>(parents[0]);
        _organRelation.Relate(parents[0], organ);
    }

    private Sex? GetSex(EntityUid body)
    {
        return TryComp<HumanoidProfileComponent>(body, out var profile) ? profile.Sex : null;
    }

    /// <summary>
    /// Replaces the organs of the old sex with those of the new one. Organs that were already lost stay lost.
    /// </summary>
    private void SwapSexOrgans(Entity<BodyZonesComponent> ent, Sex sex)
    {
        _swappingOrgans = true;
        try
        {
            foreach (var (_, data) in ent.Comp.Zones)
            {
                if (data.SexContents.Count == 0)
                    continue;

                var wanted = data.SexContents.GetValueOrDefault(sex) ?? new List<EntProtoId>();
                var removed = false;
                foreach (var (otherSex, protos) in data.SexContents)
                {
                    if (otherSex == sex)
                        continue;

                    foreach (var proto in protos)
                    {
                        if (wanted.Contains(proto) || GetCategory(proto) is not { } category)
                            continue;

                        foreach (var organ in GetOrgans(ent, category))
                        {
                            if (MetaData(organ).EntityPrototype?.ID != proto.Id)
                                continue;

                            DeleteOrgan(ent, organ);
                            removed = true;
                        }
                    }
                }

                if (!removed)
                    continue;

                foreach (var proto in wanted)
                {
                    EnsureOrgan(ent, data, proto);
                }
            }
        }
        finally
        {
            _swappingOrgans = false;
        }
    }

    private void SetBroken(Entity<BoneComponent> bone, bool broken)
    {
        if (bone.Comp.Broken == broken)
            return;

        bone.Comp.Broken = broken;
        Dirty(bone);
    }

    /// <summary>
    /// Breaks the bone of a zone: its health drops below the fracture threshold if it wasn't already.
    /// A broken arm or hand can't hold anything, a broken leg or foot slows down, a broken skull knocks out,
    /// broken ribs make running hard and a broken pelvis slows everything.
    /// </summary>
    public bool BreakBone(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (IsMissing(ent.Comp, zone) || GetBone(ent, zone) is not { } bone)
            return false;

        if (TryComp<OrganHealthComponent>(bone, out var health))
        {
            var below = health.MaxHealth * bone.Comp.FractureRatio - 1f;
            if (health.Health > below)
            {
                health.Damage = health.MaxHealth - below;
                Dirty(bone, health);
            }
        }

        return ApplyFracture(ent, zone, bone);
    }

    private bool ApplyFracture(Entity<BodyZonesComponent> ent, BodyZone zone, Entity<BoneComponent> bone)
    {
        if (!ent.Comp.Fractures.Add(zone))
            return false;

        SetBroken(bone, true);
        ent.Comp.Cracks.Remove(zone);

        // The blow knocks the splint off.
        if (bone.Comp.Splinted)
        {
            bone.Comp.Splinted = false;
            Dirty(bone);
        }

        var part = Loc.GetString(zone.LocKey());
        _popup.PopupEntity(Loc.GetString("body-zones-fracture", ("target", Identity.Entity(ent, EntityManager)), ("part", part)),
            ent,
            PopupType.MediumCaution);
        _audio.PlayPvs(FractureSound, ent);
        _adminLogger.Add(LogType.Damaged, LogImpact.Medium, $"{ToPrettyString(ent):target} broke the bone in their {zone}");

        if (zone == BodyZone.Head)
            _stun.TryUpdateParalyzeDuration(ent, ent.Comp.SkullFractureStun);

        if (zone.HandOf() is { } hand)
            RefreshHandBlock(ent, hand);

        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        Dirty(ent);
        return true;
    }

    private void UnbreakBone(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (!ent.Comp.Fractures.Remove(zone))
            return;

        if (GetBone(ent, zone) is { } bone)
            SetBroken(bone, false);

        ent.Comp.OpenFractures.Remove(zone);
        if (zone.HandOf() is { } hand)
            RefreshHandBlock(ent, hand);

        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        Dirty(ent);
    }

    /// <summary>
    /// Sets a cracked or closed broken bone with a splint: the bone is brought up to a crack and slowly grows
    /// back together under the splint. An open fracture can't be set like this.
    /// </summary>
    public bool HealFracture(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (ent.Comp.OpenFractures.Contains(zone)
            || !IsFractured(ent.Comp, zone) && !ent.Comp.Cracks.Contains(zone)
            || GetBone(ent, zone) is not { } bone)
            return false;

        if (TryComp<OrganHealthComponent>(bone, out var health))
        {
            var cracked = health.MaxHealth * bone.Comp.CrackRatio;
            if (health.Health < cracked)
            {
                health.Damage = health.MaxHealth - cracked;
                Dirty(bone, health);
            }
        }

        bone.Comp.Splinted = true;
        Dirty(bone);
        UpdateBone(ent, zone);
        return true;
    }

    /// <summary>
    /// A hand is useless while a bone anywhere in that arm is broken, or a part of the arm is numb without blood:
    /// it gets filled with the bone, which can't be let go of.
    /// </summary>
    private void RefreshHandBlock(Entity<BodyZonesComponent> ent, BodyZone handZone)
    {
        if (!ent.Comp.Zones.TryGetValue(handZone, out var handData) || handData.Organ is not { } handCategory)
            return;

        string? handId = null;
        foreach (var organ in GetOrgans(ent, handCategory))
        {
            if (TryComp<HandOrganComponent>(organ, out var handOrgan))
                handId = handOrgan.HandID;
        }

        if (handId == null)
            return;

        // A broken arm holds its bone; a numb one (no blood) can't hold anything either.
        Entity<BoneComponent>? broken = null;
        for (BodyZone? zone = handZone; zone is { } z && z.IsArm(); zone = z.Parent())
        {
            if (!IsFractured(ent.Comp, z) && !IsNumb(ent, z))
                continue;

            broken = GetBone(ent, z);
            if (broken != null)
                break;
        }

        if (_hands.TryGetHeldItem(ent.Owner, handId, out var held)
            && TryComp<VirtualItemComponent>(held, out var virtualItem)
            && HasComp<BoneComponent>(virtualItem.BlockingEntity))
        {
            // Still blocked: keep the blocker there is. Replacing it in the same tick loses the new one.
            if (broken != null)
                return;

            // The bone has healed or the blood is back.
            _virtualItem.DeleteVirtualItem((held.Value, virtualItem), ent);
        }

        if (broken is not { } bone)
            return;

        _hands.TryDrop(ent.Owner, handId, checkActionBlocker: false);
        _virtualItem.TrySpawnUnremoveableVirtualItemInHand(bone, ent, out _, empty: handId, silent: true);
    }

    /// <summary>
    /// Rejuvenation: every zone gets its bone, muscles and vessels back, the bones whole.
    /// </summary>
    private void RestoreBones(Entity<BodyZonesComponent> ent)
    {
        EnsureContents(ent);
        RelateLooseBones(ent);

        var fractured = ent.Comp.Fractures.ToArray();
        ent.Comp.Fractures.Clear();

        foreach (var zone in ent.Comp.Zones.Keys)
        {
            if (GetBone(ent, zone) is { } bone)
                SetBroken(bone, false);
        }

        foreach (var zone in fractured)
        {
            if (zone.HandOf() is { } hand)
                RefreshHandBlock(ent, hand);
        }
    }

    /// <summary>
    /// A bone that stayed in the body when its zone was destroyed (the skull of a burst head) hangs on nothing.
    /// Once the zone is back it goes back into it, so it leaves together with it again.
    /// </summary>
    private void RelateLooseBones(Entity<BodyZonesComponent> ent)
    {
        foreach (var (_, data) in ent.Comp.Zones)
        {
            var parentCategory = data.Inside ?? data.Organ ?? TorsoCategory;
            foreach (var proto in data.Contents.Select(p => (EntProtoId?) p).Prepend(data.Bone))
            {
                if (GetCategory(proto) is not { } category || category == parentCategory)
                    continue;

                foreach (var organ in GetOrgans(ent, category))
                {
                    if (!HasComp<BoneComponent>(organ) || _childQuery.CompOrNull(organ)?.Parent != null)
                        continue;

                    var parents = GetOrgans(ent, parentCategory);
                    if (parents.Count == 0)
                        continue;

                    EnsureComp<ParentOrganComponent>(parents[0]);
                    _organRelation.Relate(parents[0], organ);
                }
            }
        }
    }

    #endregion

    #region Splint

    private void OnSplintAfterInteract(Entity<SplintComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target
            || !TryComp<BodyZonesComponent>(target, out var zones))
            return;

        args.Handled = true;

        var selected = TryComp<TargetingComponent>(args.User, out var targeting) ? targeting.Selected : BodyZone.Chest;
        var zone = Redirect(zones, selected);
        var part = Loc.GetString(zone.LocKey());

        if (zones.OpenFractures.Contains(zone))
        {
            _popup.PopupEntity(Loc.GetString("splint-open-fracture", ("part", part)), target, args.User);
            return;
        }

        if (!IsFractured(zones, zone) && !zones.Cracks.Contains(zone))
        {
            _popup.PopupEntity(Loc.GetString("splint-no-fracture", ("part", part)), target, args.User);
            return;
        }

        var delay = args.User == target ? ent.Comp.Delay * ent.Comp.SelfDelayMultiplier : ent.Comp.Delay;
        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, new SplintDoAfterEvent(zone), ent, target: target, used: ent)
        {
            // Like bandages: don't break on damage, the patient is usually still bleeding.
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("splint-start", ("user", Identity.Entity(args.User, EntityManager)), ("part", part)),
            target,
            PopupType.Medium);
    }

    private void OnSplintDoAfter(Entity<SplintComponent> ent, ref SplintDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target
            || !TryComp<BodyZonesComponent>(target, out var zones))
            return;

        args.Handled = true;

        if (!HealFracture((target, zones), args.Zone))
            return;

        _audio.PlayPvs(ent.Comp.Sound, target);
        _popup.PopupEntity(Loc.GetString("splint-done", ("part", Loc.GetString(args.Zone.LocKey()))), target, PopupType.Medium);
        _adminLogger.Add(LogType.Healed, LogImpact.Low, $"{ToPrettyString(args.User):user} set the broken {args.Zone} of {ToPrettyString(target):target} with {ToPrettyString(ent):splint}");

        QueueDel(ent);
    }

    #endregion
}
