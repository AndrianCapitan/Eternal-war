using System.Linq;
using System.Numerics;
using Content.Server.Body.Systems;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared.Administration.Logs;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Throwing;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._EternalWar.BodyZones;

/// <inheritdoc/>
public sealed partial class BodyZonesSystem : SharedBodyZonesSystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private BlurryVisionSystem _blurryVision = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DetachableOrganSystem _detachableOrgan = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;
    [Dependency] private RespiratorSystem _respirator = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    [Dependency] private EntityQuery<OrganComponent> _organQuery;
    [Dependency] private EntityQuery<ChildOrganComponent> _childQuery;

    private static readonly SoundSpecifier GibSound = new SoundCollectionSpecifier("gib");

    /// <summary>
    /// Set while organs are swapped for the other sex, so that doesn't count as losing them.
    /// </summary>
    private bool _swappingOrgans;

    /// <summary>
    /// Bodies with a zone that went past its limit. Handled in <see cref="Update"/>, outside of the damage event,
    /// so tearing organs out doesn't happen in the middle of another system reacting to the same hit.
    /// </summary>
    private readonly HashSet<EntityUid> _toCheck = new();

    public override void Initialize()
    {
        base.Initialize();

#pragma warning disable CS0618 // DamageChangedEvent is the only event with the damage that was actually done.
        SubscribeLocalEvent<BodyZonesComponent, DamageChangedEvent>(OnDamageChanged);
#pragma warning restore CS0618
        SubscribeLocalEvent<BodyZonesComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<BodyZonesComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<BodyZonesComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<BodyZonesComponent, InhaledGasEvent>(OnInhaled, after: new[] { typeof(RespiratorSystem) });

        InitializeBones();
        InitializeSkin();
        InitializeSevering();
        InitializeCirculation();
        InitializeSkeleton();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // A severing blow whose damage never landed (blocked, missed) doesn't carry over to the next one.
        _severingHits.Clear();

        CheckEmptiedBodies();
        UpdateCirculation();
        UpdateSplints();
        UpdateSkeleton();

        if (_toCheck.Count == 0)
            return;

        var bodies = _toCheck.ToArray();
        _toCheck.Clear();

        foreach (var uid in bodies)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<BodyZonesComponent>(uid, out var comp))
                continue;

            // A limb cut off whole goes first, with all its layers still in it.
            CheckSevered((uid, comp));
            CheckLayers((uid, comp));
            CheckDestroyed((uid, comp));
        }

        // Cuts on bodies that were deleted in the meantime.
        _toSever.Clear();
    }

    #region Damage bookkeeping

#pragma warning disable CS0618 // DamageChangedEvent is the only event with the damage that was actually done.
    private void OnDamageChanged(Entity<BodyZonesComponent> ent, ref DamageChangedEvent args)
#pragma warning restore CS0618
    {
        var zone = ent.Comp.HasPendingZone ? ent.Comp.PendingZone : null;
        ent.Comp.PendingZone = null;
        ent.Comp.HasPendingZone = false;

        if (args.DamageDelta == null)
        {
            Resync(ent, args.Damageable);
            return;
        }

        var dealt = FixedPoint2.Zero;
        var healed = FixedPoint2.Zero;
        foreach (var type in ent.Comp.TrackedTypes)
        {
            if (!args.DamageDelta.DamageDict.TryGetValue(type, out var value))
                continue;

            if (value > 0)
                dealt += value;
            else
                healed -= value;
        }

        if (dealt == FixedPoint2.Zero && healed == FixedPoint2.Zero)
            return;

        if (dealt > 0)
        {
            RecordSeverHit(ent, zone, args.DamageDelta);
            RecordLayerDamage(ent, zone, args.DamageDelta);

            if (zone is { } target)
                AddDamage(ent.Comp, Redirect(ent.Comp, target), dealt);
            else
                SpreadDamage(ent.Comp, dealt);
        }

        if (healed > 0)
            Heal(ent, zone, healed);

        // Keep the zones adding up to the real damage even if something went around this system.
        var total = GetTrackedDamage(ent, args.Damageable);
        if (FixedPoint2.Abs(total - ent.Comp.Damage.Values.Aggregate(FixedPoint2.Zero, (a, b) => a + b)) > 0.1)
            Resync(ent, args.Damageable, dirty: false);

        Dirty(ent);
        _toCheck.Add(ent);
    }

    private FixedPoint2 GetTrackedDamage(Entity<BodyZonesComponent> ent, DamageableComponent damageable)
    {
#pragma warning disable CS0618 // Zones are a numeric split of the body's damage by design.
        var damage = _damageable.GetAllDamage((ent.Owner, damageable));
#pragma warning restore CS0618
        var total = FixedPoint2.Zero;
        foreach (var type in ent.Comp.TrackedTypes)
        {
            total += damage.DamageDict.GetValueOrDefault(type);
        }

        return total;
    }

    private void AddDamage(BodyZonesComponent comp, BodyZone zone, FixedPoint2 amount)
    {
        comp.Damage[zone] = FixedPoint2.Max(FixedPoint2.Zero, GetDamage(comp, zone) + amount);
    }

    /// <summary>
    /// Body-wide damage goes to every zone that is still there, in proportion to its size.
    /// </summary>
    private void SpreadDamage(BodyZonesComponent comp, FixedPoint2 amount)
    {
        var zones = comp.Zones.Where(z => !IsMissing(comp, z.Key)).ToList();
        var total = zones.Aggregate(FixedPoint2.Zero, (sum, z) => sum + z.Value.MaxHealth);
        if (total <= 0)
            return;

        var left = amount;
        for (var i = 0; i < zones.Count; i++)
        {
            var (zone, data) = zones[i];
            var share = i == zones.Count - 1 ? left : amount * data.MaxHealth / total;
            AddDamage(comp, zone, share);
            left -= share;
        }
    }

    /// <summary>
    /// Aimed healing (a medic with a bandage) treats the chosen zone first; the rest is shared by the damaged zones.
    /// </summary>
    private void Heal(Entity<BodyZonesComponent> ent, BodyZone? zone, FixedPoint2 amount)
    {
        var comp = ent.Comp;
        if (zone is { } target)
        {
            var before = GetDamage(comp, target);
            var healed = FixedPoint2.Min(amount, before);
            AddDamage(comp, target, -healed);
            if (before > 0)
                HealLayers(ent, target, (healed / before).Float());
            amount -= healed;
        }

        var damaged = comp.Damage.Where(z => z.Value > 0).ToList();
        var total = damaged.Aggregate(FixedPoint2.Zero, (sum, z) => sum + z.Value);
        if (amount <= 0 || total <= 0)
            return;

        if (amount >= total)
        {
            foreach (var (z, _) in damaged)
            {
                comp.Damage[z] = FixedPoint2.Zero;
                HealLayers(ent, z, 1f);
            }

            return;
        }

        // Every damaged zone heals by the same share.
        var share = (amount / total).Float();
        foreach (var (z, value) in damaged)
        {
            AddDamage(comp, z, -(amount * value / total));
            HealLayers(ent, z, share);
        }
    }

    /// <summary>
    /// Fits the zones to the body's actual damage, used when damage is set directly (admin tools, rejuvenation).
    /// </summary>
    private void Resync(Entity<BodyZonesComponent> ent, DamageableComponent damageable, bool dirty = true)
    {
        var target = GetTrackedDamage(ent, damageable);
        var current = ent.Comp.Damage.Values.Aggregate(FixedPoint2.Zero, (a, b) => a + b);

        if (target <= 0)
        {
            ent.Comp.Damage.Clear();
        }
        else if (current > target)
        {
            var share = 1f - (target / current).Float();
            foreach (var zone in ent.Comp.Damage.Keys.ToList())
            {
                ent.Comp.Damage[zone] = ent.Comp.Damage[zone] * target / current;
                HealLayers(ent, zone, share);
            }
        }
        else if (current < target)
        {
            SpreadDamage(ent.Comp, target - current);
        }

        if (dirty)
            Dirty(ent);
    }

    #endregion

    #region Destruction

    /// <summary>
    /// A zone with a bone is destroyed when its bone is crushed to nothing: the skull for the head, the ribs for the
    /// chest, the pelvis for the abdomen, the bone of a limb part. Blades don't reach bones, so they don't destroy
    /// zones (heavy ones can cut a limb off, see <see cref="LimbSeverComponent"/>).
    /// Zones without a bone of their own (the eyes, the genitals) are destroyed by their health running out.
    /// </summary>
    private void CheckDestroyed(Entity<BodyZonesComponent> ent)
    {
        foreach (var (zone, data) in ent.Comp.Zones)
        {
            if (ent.Comp.Destroyed.Contains(zone) || IsMissing(ent.Comp, zone))
                continue;

            bool destroyed;
            if (data.Bone != null && GetBone(ent, zone) is { } bone && TryComp<OrganHealthComponent>(bone, out var health))
                destroyed = health.Health <= 0f;
            else
                destroyed = GetHealth(ent.Comp, zone) <= -data.MaxHealth * ent.Comp.DestroyRatio;

            if (destroyed)
                DestroyZone(ent, zone);
        }
    }

    /// <summary>
    /// Tears the zone apart: a limb falls off, a head bursts, a torso spills its organs.
    /// </summary>
    public void DestroyZone(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (!ent.Comp.Zones.TryGetValue(zone, out var data) || !ent.Comp.Destroyed.Add(zone))
            return;

        var body = ent.Owner;
        var identity = Identity.Entity(body, EntityManager);
        var part = Loc.GetString(zone.LocKey());

        foreach (var slot in data.DropSlots)
        {
            _inventory.TryUnequip(body, slot, silent: true, force: true);
        }

        // Every bone rolls once whether it is knocked out, even if it is found again below (the jaw of a head).
        var rolled = new Dictionary<EntityUid, bool>();

        foreach (var category in data.Drop)
        {
            foreach (var organ in GetOrgans(body, category))
            {
                SpillOrgan(ent, organ, rolled);
            }
        }

        if (!data.TearOff || data.Organ == null)
        {
            // A torn off limb spills these out when it is torn apart below.
            foreach (var proto in ZoneContents(data))
            {
                if (GetCategory(proto) is not { } category)
                    continue;

                foreach (var organ in GetOrgans(body, category))
                {
                    SpillOrgan(ent, organ, rolled);
                }
            }
        }

        if (data.Organ is { } anchor)
        {
            foreach (var organ in GetOrgans(body, anchor))
            {
                if (data.TearOff)
                {
                    Dismantle(ent, organ);
                    continue;
                }

                if (!data.Burst)
                {
                    SpillOrgan(ent, organ, rolled);
                    continue;
                }

                // Whatever else is still inside a burst head falls out too, but for the bones that stay.
                foreach (var child in _organRelation.AllChildren(organ).ToList())
                {
                    if (SpillOrgan(ent, child.Owner, rolled))
                        continue;

                    if (_childQuery.HasComp(child.Owner))
                        _organRelation.Orphan(child.Owner);

                    // A zone of the head (the mouth) is gone with it, even though its bone (the jaw) stayed.
                    if (GetZoneOf(ent.Comp, child.Owner) is { } inner && ent.Comp.Destroyed.Add(inner))
                    {
                        ent.Comp.Fractures.Remove(inner);
                        ent.Comp.Cracks.Remove(inner);
                        if (inner.Parent() is { } outer && ent.Comp.Damage.Remove(inner, out var damage))
                            AddDamage(ent.Comp, Redirect(ent.Comp, outer), damage);
                    }
                }

                DeleteOrgan(body, organ);
            }
        }

        _bloodstream.TryBleedOut(body, 20);
        _bloodstream.TryModifyBleedAmount(body, 5);
        _audio.PlayPvs(GibSound, body);

        var message = data.Message ?? (zone.IsTorso() ? "body-zones-torn-open" : data.TearOff ? "body-zones-shredded" : "body-zones-burst");
        _popup.PopupEntity(Loc.GetString(message, ("target", identity), ("part", part)), body, PopupType.LargeCaution);

        if (data.DestroyStun > TimeSpan.Zero)
            _stun.TryUpdateParalyzeDuration(body, data.DestroyStun);

        _adminLogger.Add(LogType.Damaged, LogImpact.High, $"{ToPrettyString(body):target} lost their {zone} to damage");

        if (data.Fatal)
            KillForGood(ent);

        // A torn torso can't hold the rest of the body: the head, the arms and the legs fall off whole.
        if (zone.IsTorso())
            TearOffFromTorso(ent);

        _movementSpeed.RefreshMovementSpeedModifiers(body);
        Dirty(ent);
    }

    /// <summary>
    /// Losing this kills for good: no defibrillator brings the body back.
    /// </summary>
    private void KillForGood(Entity<BodyZonesComponent> ent)
    {
        _mobState.ChangeMobState(ent, MobState.Dead);
        if (HasComp<UnrevivableComponent>(ent))
            return;

        AddComp<UnrevivableComponent>(ent);
        ent.Comp.MadeUnrevivable = true;
    }

    /// <summary>
    /// The parts attached right to the torso (the head, the upper arms, the thighs) come off in one piece each,
    /// with everything hanging on them and inside them: a whole arm, a whole leg, the head with the brain and eyes.
    /// Their clothes fall off.
    /// </summary>
    private void TearOffFromTorso(Entity<BodyZonesComponent> ent)
    {
        var body = ent.Owner;
        var torn = false;
        foreach (var (zone, data) in ent.Comp.Zones)
        {
            if (data.Organ is not { } category || data.Inside != null
                || zone.Parent() is not { } parent || !parent.IsTorso()
                || IsMissing(ent.Comp, zone))
                continue;

            foreach (var slot in data.DropSlots)
            {
                _inventory.TryUnequip(body, slot, silent: true, force: true);
            }

            foreach (var organ in GetOrgans(body, category))
            {
                if (_detachableOrgan.Detach(organ) is not { } piece)
                    continue;

                _metaData.SetEntityName(piece, Loc.GetString($"{zone.LocKey()}-detached"));
                Fling(piece);
                torn = true;
            }

            if (data.Fatal)
                KillForGood(ent);
        }

        if (!torn)
            return;

        _popup.PopupEntity(Loc.GetString("body-zones-body-torn-apart", ("target", Identity.Entity(body, EntityManager))),
            body,
            PopupType.LargeCaution);
        _adminLogger.Add(LogType.Damaged, LogImpact.High, $"{ToPrettyString(body):target} had their head and limbs torn off with the torso");
    }

    public List<EntityUid> GetOrgans(EntityUid body, ProtoId<OrganCategoryPrototype> category)
    {
        var result = new List<EntityUid>();
        if (!_container.TryGetContainer(body, BodyComponent.ContainerID, out var container))
            return result;

        foreach (var organ in container.ContainedEntities)
        {
            if (_organQuery.TryComp(organ, out var comp) && comp.Category == category)
                result.Add(organ);
        }

        return result;
    }

    /// <summary>
    /// An organ of a destroyed zone falls out. A bone only now and then (<see cref="BodyZonesComponent.BoneDropChance"/>):
    /// mostly it stays in the body, to be pulled out later.
    /// </summary>
    /// <returns>Whether it fell out.</returns>
    private bool SpillOrgan(Entity<BodyZonesComponent> ent, EntityUid organ, Dictionary<EntityUid, bool> rolled)
    {
        if (HasComp<BoneComponent>(organ))
        {
            if (!rolled.TryGetValue(organ, out var knockedOut))
                rolled[organ] = knockedOut = _random.Prob(ent.Comp.BoneDropChance);

            if (!knockedOut)
                return false;
        }

        DropOrgan(ent, organ);
        return true;
    }

    private void DropOrgan(EntityUid body, EntityUid organ, bool fling = true)
    {
        // Bones don't come out of a torn body in one piece.
        if (TryComp<BoneComponent>(organ, out var bone))
            SetBroken((organ, bone), true);

        if (_childQuery.HasComp(organ))
            _organRelation.Orphan(organ);

        if (!_container.TryGetContainer(body, BodyComponent.ContainerID, out var container)
            || !_container.Remove(organ, container))
            return;

        if (fling)
            Fling(organ);
    }

    private void DeleteOrgan(EntityUid body, EntityUid organ)
    {
        if (_childQuery.HasComp(organ))
            _organRelation.Orphan(organ);

        if (_container.TryGetContainer(body, BodyComponent.ContainerID, out var container))
            _container.Remove(organ, container);

        QueueDel(organ);
    }

    /// <summary>
    /// Everything the system spawns into a zone: its bone, skin, muscles, vessels and sex organs.
    /// </summary>
    private static IEnumerable<EntProtoId> ZoneContents(BodyZoneData data)
    {
        if (data.Bone is { } bone)
            yield return bone;

        if (data.Skin is { } skin)
            yield return skin;

        foreach (var proto in data.Tissues.Concat(data.Contents))
        {
            yield return proto;
        }

        foreach (var proto in data.SexContents.Values.SelectMany(p => p))
        {
            yield return proto;
        }
    }

    /// <summary>
    /// The end of a limb part that lost its layers: whatever is still inside it (layers that survived a single
    /// huge blow) falls out and the part is gone. Its bone was crushed: it is gone too, but now and then knocked out
    /// whole. The parts hanging on it are whole and fall off as one piece with everything inside them
    /// (a hand off a destroyed forearm).
    /// </summary>
    private void Dismantle(Entity<BodyZonesComponent> ent, EntityUid part)
    {
        var body = ent.Owner;
        if (TryComp<ParentOrganComponent>(part, out var parent))
        {
            foreach (var child in parent.Children.ToList())
            {
                if (HasComp<InternalChildOrganComponent>(child))
                    continue;

                if (_detachableOrgan.Detach(child) is not { } piece)
                    continue;

                if (GetZoneOf(ent.Comp, child) is { } childZone)
                    _metaData.SetEntityName(piece, Loc.GetString($"{childZone.LocKey()}-detached"));

                Fling(piece);
            }
        }

        var rolled = new Dictionary<EntityUid, bool>();
        foreach (var inner in _organRelation.AllChildren(part).ToList())
        {
            if (!SpillOrgan(ent, inner.Owner, rolled))
                DeleteOrgan(body, inner.Owner);
        }

        DeleteOrgan(body, part);
    }

    private void Fling(EntityUid uid)
    {
        var direction = _random.NextAngle().ToVec() * _random.NextFloat(0.5f, 1.5f);
        _throwing.TryThrow(uid, direction, 4f, playSound: false, recoil: false);
    }

    /// <summary>
    /// Keeps <see cref="BodyZonesComponent.Destroyed"/> right when organs are taken out or put back by other means,
    /// e.g. the admin <c>organ</c> command.
    /// </summary>
    private void OnOrganRemoved(Entity<BodyZonesComponent> ent, ref OrganRemovedFromEvent args)
    {
        if (TerminatingOrDeleted(ent) || _swappingOrgans)
            return;

        // Maybe that was the last part left on the torso.
        _maybeEmptied.Add(ent);

        RefreshFailedOrgans(ent);
        UpdateLungs(ent);

        if (GetSkinZoneOf(ent.Comp, args.Organ) is { } skinZone)
        {
            OnSkinLost(ent, skinZone);
            RefreshScalp(ent);
            return;
        }

        if (GetTissueZoneOf(ent.Comp, args.Organ) is { } tissueZone)
        {
            OnTissueChanged(ent, tissueZone);
            return;
        }

        if (GetZoneOf(ent.Comp, args.Organ) is not { } zone)
        {
            // A bone that isn't a zone of its own (the jaw is the mouth): the zone loses its last layer.
            if (GetBoneZoneOf(ent.Comp, args.Organ) is { } boneZone)
                OnBoneChanged(ent, boneZone);
            return;
        }

        // The damage on a lost limb stays on the body, on whatever the limb was attached to.
        // Mark the zone first, so a hand that leaves together with its arm passes its damage past the arm.
        ent.Comp.Destroyed.Add(zone);
        ent.Comp.Fractures.Remove(zone);
        ent.Comp.Skinless.Remove(zone);
        ent.Comp.TissuesTorn.Remove(zone);
        ent.Comp.BonesLost.Remove(zone);
        if (zone.Parent() is { } parent && ent.Comp.Damage.Remove(zone, out var damage))
            AddDamage(ent.Comp, Redirect(ent.Comp, parent), damage);

        OnLimbLost(ent, zone);

        if (zone.IsEye())
            UpdateEyes(ent);

        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        Dirty(ent);
    }

    private void OnOrganInserted(Entity<BodyZonesComponent> ent, ref OrganInsertedIntoEvent args)
    {
        if (_swappingOrgans)
            return;

        EnsureOrganHealth(ent.Comp, args.Organ);
        RefreshFailedOrgans(ent);
        UpdateLungs(ent);

        if (GetSkinZoneOf(ent.Comp, args.Organ) is { } skinZone)
        {
            if (ent.Comp.Skinless.Remove(skinZone))
                Dirty(ent);
            RefreshScalp(ent);
            return;
        }

        if (GetTissueZoneOf(ent.Comp, args.Organ) is { } tissueZone)
        {
            OnTissueChanged(ent, tissueZone);
            return;
        }

        if (GetZoneOf(ent.Comp, args.Organ) is not { } zone)
        {
            if (GetBoneZoneOf(ent.Comp, args.Organ) is { } boneZone)
                OnBoneChanged(ent, boneZone);
            return;
        }

        if (!ent.Comp.Destroyed.Remove(zone))
            return;

        OnLimbRestored(ent, zone);

        if (zone.IsEye())
            UpdateEyes(ent);

        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        Dirty(ent);
    }

    private BodyZone? GetZoneOf(BodyZonesComponent comp, EntityUid organ)
    {
        if (!_organQuery.TryComp(organ, out var organComp) || organComp.Category is not { } category)
            return null;

        foreach (var (zone, data) in comp.Zones)
        {
            if (data.Organ == category)
                return zone;
        }

        return null;
    }

    #endregion

    #region Paired organs

    private void UpdateLungs(Entity<BodyZonesComponent> ent)
    {
        var missing = 0;
        if (ent.Comp.PairedLungs)
        {
            foreach (var lung in ent.Comp.Lungs)
            {
                if (!HasWorkingOrgan(ent, lung))
                    missing++;
            }
        }

        if (ent.Comp.MissingLungs == missing)
            return;

        ent.Comp.MissingLungs = missing;
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        Dirty(ent);
    }

    /// <summary>
    /// A single lung gets less oxygen out of every breath, so the mob runs out of air sooner when the air is thin.
    /// </summary>
    private void OnInhaled(Entity<BodyZonesComponent> ent, ref InhaledGasEvent args)
    {
        if (ent.Comp.MissingLungs == 1)
            _respirator.UpdateSaturation(ent, -ent.Comp.OneLungSaturationLoss);
    }

    private void UpdateEyes(EntityUid body)
    {
        _blindable.UpdateIsBlind(body);
        _blurryVision.UpdateBlurMagnitude(body);
    }

    #endregion

    #region Rejuvenation

    /// <summary>
    /// Admin rejuvenation regrows everything that was lost.
    /// </summary>
    private void OnRejuvenate(Entity<BodyZonesComponent> ent, ref RejuvenateEvent args)
    {
        if (TryComp<InitialBodyComponent>(ent, out var initial)
            && _container.TryGetContainer(ent, BodyComponent.ContainerID, out var container))
        {
            RegrowOrgans(ent, initial, container);
        }

        ent.Comp.Destroyed.Clear();
        ent.Comp.Damage.Clear();
        ent.Comp.Skinless.Clear();
        ent.Comp.TissuesTorn.Clear();
        ent.Comp.BonesLost.Clear();
        RestoreBones(ent);
        RestoreLayers(ent);
        ResetCirculation(ent);
        RefreshScalp(ent);
        UpdateLungs(ent);
        UpdateEyes(ent);

        if (ent.Comp.MadeUnrevivable)
        {
            RemComp<UnrevivableComponent>(ent);
            ent.Comp.MadeUnrevivable = false;
        }

        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        Dirty(ent);
    }

    private void RegrowOrgans(EntityUid body, InitialBodyComponent initial, BaseContainer container)
    {
        var present = new Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>();
        foreach (var organ in container.ContainedEntities)
        {
            if (_organQuery.TryComp(organ, out var comp) && comp.Category is { } category)
                present.TryAdd(category, organ);
        }

        // Skin color etc. for the new organs is copied from the torso.
        OrganProfileData? profile = null;
        foreach (var organ in present.Values)
        {
            if (TryComp<VisualOrganComponent>(organ, out var visual))
            {
                profile = visual.Profile;
                break;
            }
        }

        var regrown = new Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>();

        // Parents have to exist before their children, so keep going while something new appears.
        var added = true;
        while (added)
        {
            added = false;
            foreach (var (category, proto) in initial.Organs)
            {
                if (present.ContainsKey(category))
                    continue;

                var parentCategory = GetParentCategory(initial, category);
                EntityUid? parent = null;
                if (parentCategory != null)
                {
                    if (!present.TryGetValue(parentCategory.Value, out var parentUid))
                        continue;

                    parent = parentUid;
                }

                var organ = SpawnAttachedTo(proto, Transform(body).Coordinates);
                if (!_container.Insert(organ, container, force: true))
                {
                    Del(organ);
                    continue;
                }

                if (parent != null)
                    _organRelation.Relate(parent.Value, organ);

                present[category] = organ;
                if (profile != null)
                    regrown[category] = profile.Value;

                added = true;
            }
        }

        if (regrown.Count > 0)
            _visualBody.ApplyProfiles(body, regrown);
    }

    private static ProtoId<OrganCategoryPrototype>? GetParentCategory(InitialBodyComponent initial, ProtoId<OrganCategoryPrototype> category)
    {
        if (initial.Relationships == null)
            return null;

        foreach (var (parent, children) in initial.Relationships)
        {
            if (children.Contains(category))
                return parent;
        }

        return null;
    }

    #endregion
}
