using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// Picks the body zone each damage instance goes to and tells armor whether it covers that zone.
/// The bookkeeping of zone damage and tearing limbs off is done on the server.
/// </summary>
public abstract partial class SharedBodyZonesSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    [Dependency] private EntityQuery<CirculationComponent> _circulationQuery;
    [Dependency] private EntityQuery<TargetingComponent> _targetingQuery;
    [Dependency] private EntityQuery<BodyZonesComponent> _zonesQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyZonesComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<BodyZonesComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<BodyZonesComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<BodyZonesComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BodyZonesComponent, AfterAutoHandleStateEvent>(OnZonesState);
        SubscribeLocalEvent<CirculationComponent, AfterAutoHandleStateEvent>(OnCirculationState);
        SubscribeLocalEvent<BodyZonesComponent, CanSeeAttemptEvent>(OnCanSee);
        SubscribeLocalEvent<BodyZonesComponent, GetBlurEvent>(OnGetBlur);

        SubscribeNetworkEvent<SelectTargetZoneEvent>(OnSelectTargetZone);
    }

    #region Targeting

    private void OnSelectTargetZone(SelectTargetZoneEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        SetTarget(user, ev.Zone);
    }

    public void SetTarget(Entity<TargetingComponent?> ent, BodyZone zone)
    {
        if (!_targetingQuery.Resolve(ent, ref ent.Comp, false) || ent.Comp.Selected == zone)
            return;

        ent.Comp.Selected = zone;
        Dirty(ent);
    }

    /// <summary>
    /// Finds the mob that aims the damage: the origin itself, or whoever holds it (a gun reports itself as the origin).
    /// </summary>
    private Entity<TargetingComponent>? GetAimer(EntityUid? origin)
    {
        var current = origin;
        for (var i = 0; i < 3 && current is { } uid; i++)
        {
            if (_targetingQuery.TryComp(uid, out var targeting))
                return (uid, targeting);

            current = _container.TryGetContainingContainer(uid, out var container) ? container.Owner : null;
        }

        return null;
    }

    #endregion

    #region Zone selection

    private void OnBeforeDamageChanged(Entity<BodyZonesComponent> ent, ref BeforeDamageChangedEvent args)
    {
        ent.Comp.PendingZone = PickZone(ent, args.Damage, args.Origin);
        ent.Comp.HasPendingZone = true;
    }

    private BodyZone? PickZone(Entity<BodyZonesComponent> ent, DamageSpecifier damage, EntityUid? origin)
    {
        // Suffocation, poison, radiation and the like hit the whole body.
        if (!HasTrackedDamage(ent.Comp, damage))
            return null;

        if (GetAimer(origin) is { } aimer)
            return Redirect(ent.Comp, aimer.Comp.Selected);

        // Explosions, fire, pressure: nobody aims them.
        if (origin == null)
            return null;

        // Turrets, animals and other attackers that can't aim hit a random zone, bigger zones more often.
        // The client only predicts, the actual zone is rolled on the server.
        if (_net.IsClient)
            return BodyZone.Chest;

        return Redirect(ent.Comp, PickRandomZone(ent.Comp));
    }

    private BodyZone PickRandomZone(BodyZonesComponent comp)
    {
        var total = 0f;
        foreach (var (_, data) in comp.Zones)
        {
            total += data.MaxHealth.Float();
        }

        var roll = _random.NextFloat(total);
        foreach (var (zone, data) in comp.Zones)
        {
            roll -= data.MaxHealth.Float();
            if (roll <= 0)
                return zone;
        }

        return BodyZone.Chest;
    }

    /// <summary>
    /// Hits aimed at a missing limb land on whatever it was attached to.
    /// </summary>
    public BodyZone Redirect(BodyZonesComponent comp, BodyZone zone)
    {
        while (IsMissing(comp, zone) && zone.Parent() is { } parent)
        {
            zone = parent;
        }

        return zone;
    }

    protected bool HasTrackedDamage(BodyZonesComponent comp, DamageSpecifier damage)
    {
        foreach (var type in comp.TrackedTypes)
        {
            if (damage.DamageDict.TryGetValue(type, out var value) && value != FixedPoint2.Zero)
                return true;
        }

        return false;
    }

    #endregion

    #region Skin

    /// <summary>
    /// A zone without skin takes more brute and burn damage. Body-wide damage is raised by the share of the body
    /// that has no skin.
    /// </summary>
    private void OnDamageModify(Entity<BodyZonesComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.Skinless.Count == 0 || !HasTrackedDamage(ent.Comp, args.Damage))
            return;

        float skinlessShare;
        if (ent.Comp.HasPendingZone && ent.Comp.PendingZone is { } zone)
        {
            skinlessShare = ent.Comp.Skinless.Contains(zone) ? 1f : 0f;
        }
        else
        {
            var total = 0f;
            var skinless = 0f;
            foreach (var (z, data) in ent.Comp.Zones)
            {
                if (IsMissing(ent.Comp, z))
                    continue;

                total += data.MaxHealth.Float();
                if (ent.Comp.Skinless.Contains(z))
                    skinless += data.MaxHealth.Float();
            }

            skinlessShare = total > 0 ? skinless / total : 0f;
        }

        if (skinlessShare <= 0f)
            return;

        var factor = 1f + (ent.Comp.SkinlessDamageModifier - 1f) * skinlessShare;
        var damage = new DamageSpecifier(args.Damage);
        foreach (var type in ent.Comp.TrackedTypes)
        {
            if (damage.DamageDict.TryGetValue(type, out var value) && value > 0)
                damage.DamageDict[type] = value * factor;
        }

        args.Damage = damage;
    }

    #endregion

    #region Armor

    /// <summary>
    /// How much of the armor's protection applies to the current damage instance on the wearer, from 0 to 1.
    /// </summary>
    public float GetArmorCoverage(EntityUid wearer, EntityUid armor)
    {
        if (!_zonesQuery.TryComp(wearer, out var comp))
            return 1f;

        var covered = GetCoverage(armor);
        if (covered == null)
            return 1f;

        // Aimed hit: the armor either covers the zone or does nothing.
        if (comp.HasPendingZone && comp.PendingZone is { } zone)
            return covered.Contains(zone) ? 1f : 0f;

        // Body-wide damage: protect the share of the body the armor covers.
        var total = 0f;
        var protectedPart = 0f;
        foreach (var (z, data) in comp.Zones)
        {
            if (IsMissing(comp, z))
                continue;

            total += data.MaxHealth.Float();
            if (covered.Contains(z))
                protectedPart += data.MaxHealth.Float();
        }

        return total > 0 ? protectedPart / total : 1f;
    }

    /// <summary>
    /// The zones the armor covers, or null if it isn't worn in a known slot (armor then works as before).
    /// </summary>
    public IReadOnlySet<BodyZone>? GetCoverage(EntityUid armor)
    {
        if (TryComp<ArmorCoverageComponent>(armor, out var coverage))
            return coverage.Zones;

        if (!_inventory.TryGetContainingSlot(armor, out var slot))
            return null;

        return GetDefaultCoverage(slot.SlotFlags);
    }

    private static readonly HashSet<BodyZone> HeadZones = new() { BodyZone.Head, BodyZone.LeftEye, BodyZone.RightEye, BodyZone.Mouth };
    private static readonly HashSet<BodyZone> UpperBodyZones = new()
    {
        BodyZone.Chest, BodyZone.Abdomen, BodyZone.Genitals,
        BodyZone.LeftUpperArm, BodyZone.RightUpperArm, BodyZone.LeftForearm, BodyZone.RightForearm,
    };
    private static readonly HashSet<BodyZone> BodyNoExtremitiesZones = new()
    {
        BodyZone.Chest, BodyZone.Abdomen, BodyZone.Genitals,
        BodyZone.LeftUpperArm, BodyZone.RightUpperArm, BodyZone.LeftForearm, BodyZone.RightForearm,
        BodyZone.LeftThigh, BodyZone.RightThigh, BodyZone.LeftShin, BodyZone.RightShin,
    };
    private static readonly HashSet<BodyZone> HandZones = new() { BodyZone.LeftHand, BodyZone.RightHand };
    private static readonly HashSet<BodyZone> FootZones = new() { BodyZone.LeftFoot, BodyZone.RightFoot };
    private static readonly HashSet<BodyZone> ChestZones = new() { BodyZone.Chest };
    private static readonly HashSet<BodyZone> AbdomenZones = new() { BodyZone.Abdomen };

    /// <summary>
    /// What a piece of clothing protects when it has no <see cref="ArmorCoverageComponent"/>.
    /// Headgear covers the eyes and mouth too. Outer clothing defaults to a coat: torso and arms. Vests and full suits set their coverage in YAML.
    /// Hands and feet are protected only by gloves and shoes.
    /// </summary>
    public static IReadOnlySet<BodyZone> GetDefaultCoverage(SlotFlags slot)
    {
        if ((slot & (SlotFlags.HEAD | SlotFlags.MASK | SlotFlags.EYES | SlotFlags.EARS)) != 0)
            return HeadZones;
        if ((slot & SlotFlags.OUTERCLOTHING) != 0)
            return UpperBodyZones;
        if ((slot & (SlotFlags.INNERCLOTHING | SlotFlags.LEGS)) != 0)
            return BodyNoExtremitiesZones;
        if ((slot & SlotFlags.GLOVES) != 0)
            return HandZones;
        if ((slot & SlotFlags.FEET) != 0)
            return FootZones;
        if ((slot & SlotFlags.BELT) != 0)
            return AbdomenZones;

        // Neck, back, suit storage...
        return ChestZones;
    }

    #endregion

    #region Health

    public bool IsMissing(BodyZonesComponent comp, BodyZone zone)
    {
        return comp.Destroyed.Contains(zone)
               && comp.Zones.TryGetValue(zone, out var data)
               && data.Organ != null;
    }

    /// <summary>
    /// Whether the limb this zone hangs on is gone, e.g. the forearm for a hand.
    /// </summary>
    public bool IsParentMissing(BodyZonesComponent comp, BodyZone zone)
    {
        for (var parent = zone.Parent(); parent is { } p && !p.IsTorso(); parent = p.Parent())
        {
            if (IsMissing(comp, p))
                return true;
        }

        return false;
    }

    public FixedPoint2 GetDamage(BodyZonesComponent comp, BodyZone zone)
    {
        return comp.Damage.GetValueOrDefault(zone);
    }

    public FixedPoint2 GetMaxHealth(BodyZonesComponent comp, BodyZone zone)
    {
        return comp.Zones.TryGetValue(zone, out var data) ? data.MaxHealth : FixedPoint2.Zero;
    }

    /// <summary>
    /// Health left in the zone. Goes negative before the zone is destroyed.
    /// </summary>
    public FixedPoint2 GetHealth(BodyZonesComponent comp, BodyZone zone)
    {
        return GetMaxHealth(comp, zone) - GetDamage(comp, zone);
    }

    #endregion

    public bool IsFractured(BodyZonesComponent comp, BodyZone zone)
    {
        return comp.Fractures.Contains(zone);
    }

    /// <summary>
    /// Whether the zone is out of oxygen or dead, so it doesn't work: an arm can't hold, a leg can't walk.
    /// </summary>
    public bool IsNumb(EntityUid body, BodyZone zone)
    {
        return _circulationQuery.TryComp(body, out var circulation)
               && (circulation.Numb.Contains(zone) || circulation.Necrotic.Contains(zone));
    }

    /// <summary>
    /// Whether a tourniquet on this zone or above it stops the blood, e.g. one on the upper arm for the hand.
    /// </summary>
    public bool IsTourniqueted(CirculationComponent circulation, BodyZone zone)
    {
        for (BodyZone? z = zone; z is { } current; z = current.Parent())
        {
            if (circulation.Tourniquets.Contains(current))
                return true;
        }

        return false;
    }

    private void OnCirculationState(Entity<CirculationComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnRefreshSpeed(Entity<BodyZonesComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var comp = ent.Comp;
        var walk = 1f;
        var sprint = 1f;

        foreach (var zone in BodyZoneExtensions.All)
        {
            var modifier = 1f;
            if (zone.IsThigh() || zone.IsShin() || zone.IsFoot())
            {
                // A shin or foot that went with the rest of the leg is already counted by it.
                if (IsParentMissing(comp, zone) || IsParentNumb(ent, zone))
                    continue;

                // A numb leg is as good as a missing one.
                if (IsMissing(comp, zone) || IsNumb(ent, zone))
                    modifier = zone.IsFoot() ? comp.MissingFootSpeedModifier : comp.MissingLegSpeedModifier;
                else if (IsFractured(comp, zone))
                    modifier = zone.IsThigh() ? comp.BrokenThighSpeedModifier
                        : zone.IsShin() ? comp.BrokenShinSpeedModifier
                        : comp.BrokenFootSpeedModifier;
                // A cracked bone hurts with every step.
                else if (comp.Cracks.Contains(zone))
                    modifier = comp.CrackedLegSpeedModifier;
            }
            else if (zone == BodyZone.Abdomen && IsFractured(comp, zone))
            {
                modifier = comp.BrokenPelvisSpeedModifier;
            }
            else if (zone == BodyZone.Chest && IsFractured(comp, zone))
            {
                sprint *= comp.BrokenRibsSprintModifier;
            }

            walk *= modifier;
            sprint *= modifier;
        }

        if (comp.MissingLungs == 1)
            sprint *= comp.OneLungSprintModifier;

        if (walk < 1f || sprint < 1f)
            args.ModifySpeed(walk, sprint);
    }

    private bool IsParentNumb(EntityUid body, BodyZone zone)
    {
        for (var parent = zone.Parent(); parent is { } p && !p.IsTorso(); parent = p.Parent())
        {
            if (IsNumb(body, p))
                return true;
        }

        return false;
    }

    #region Eyes

    private void OnCanSee(Entity<BodyZonesComponent> ent, ref CanSeeAttemptEvent args)
    {
        if (IsEyeBlind(ent.Comp, BodyZone.LeftEye) && IsEyeBlind(ent.Comp, BodyZone.RightEye))
            args.Cancel();
    }

    private void OnGetBlur(Entity<BodyZonesComponent> ent, ref GetBlurEvent args)
    {
        if (IsEyeBlind(ent.Comp, BodyZone.LeftEye) != IsEyeBlind(ent.Comp, BodyZone.RightEye))
            args.Blur += ent.Comp.OneEyeBlur;
    }

    /// <summary>
    /// An eye that is gone or crushed sees nothing.
    /// </summary>
    public bool IsEyeBlind(BodyZonesComponent comp, BodyZone eye)
    {
        return IsMissing(comp, eye)
               || comp.Zones.TryGetValue(eye, out var data) && data.Organ is { } organ && comp.FailedOrgans.Contains(organ);
    }

    #endregion

    private void OnZonesState(Entity<BodyZonesComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        UpdateExposure(ent);
    }

    /// <summary>
    /// The client draws the bare muscles and bones of the zones that lost their skin or muscles.
    /// </summary>
    protected virtual void UpdateExposure(Entity<BodyZonesComponent> ent)
    {
    }

    /// <summary>
    /// The outermost layer the zone still has, the one that is seen: 0 - the skin, 1 - the muscles (no skin),
    /// 2 - the bone (no muscles either), 3 - nothing (the bone is gone too, e.g. pulled out of a torn open chest).
    /// </summary>
    public int GetExposure(BodyZonesComponent comp, BodyZone zone)
    {
        if (comp.BonesLost.Contains(zone))
            return 3;

        // A torn open torso zone has spilled its skin and muscles; its bones mostly stay.
        if (comp.TissuesTorn.Contains(zone) || zone.IsTorso() && comp.Destroyed.Contains(zone))
            return 2;

        return comp.Skinless.Contains(zone) ? 1 : 0;
    }

    private void OnExamined(Entity<BodyZonesComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(BodyZonesComponent)))
        {
            var identity = Identity.Entity(ent, EntityManager);
            _circulationQuery.TryComp(ent, out var circulation);
            foreach (var zone in BodyZoneExtensions.All)
            {
                var part = Loc.GetString(zone.LocKey());

                // No need to say the hand is missing when the whole arm is.
                if (IsParentMissing(ent.Comp, zone))
                    continue;

                if (IsMissing(ent.Comp, zone))
                {
                    args.PushMarkup(Loc.GetString("body-zones-examine-missing", ("target", identity), ("part", part)));

                    if (circulation != null && circulation.Tourniquets.Contains(zone))
                        args.PushMarkup(Loc.GetString("circulation-examine-tourniquet-stump", ("part", part)));
                    else if (circulation != null && circulation.Stumps.ContainsKey(zone) && !IsTourniqueted(circulation, zone))
                        args.PushMarkup(Loc.GetString("circulation-examine-fountain", ("part", part)));

                    continue;
                }

                if (circulation != null)
                {
                    if (circulation.Tourniquets.Contains(zone))
                        args.PushMarkup(Loc.GetString("circulation-examine-tourniquet", ("part", part)));

                    if (circulation.Necrotic.Contains(zone))
                        args.PushMarkup(Loc.GetString("circulation-examine-necrotic", ("part", part)));
                    else if (circulation.Numb.Contains(zone))
                        args.PushMarkup(Loc.GetString("circulation-examine-numb", ("part", part)));
                }

                if (zone.IsTorso() && ent.Comp.Destroyed.Contains(zone))
                {
                    args.PushMarkup(Loc.GetString("body-zones-examine-torn-open", ("target", identity), ("part", part)));
                    continue;
                }

                // Only point out badly hurt parts, the regular damage examine covers the rest.
                if (GetHealth(ent.Comp, zone) <= 0)
                    args.PushMarkup(Loc.GetString("body-zones-examine-mangled", ("target", identity), ("part", part)));

                // A closed fracture and a crack aren't seen on the body, only found by examining it.
                if (ent.Comp.OpenFractures.Contains(zone))
                    args.PushMarkup(Loc.GetString("body-zones-examine-open-fracture", ("target", identity), ("part", part)));
                else if (IsFractured(ent.Comp, zone))
                    args.PushMarkup(Loc.GetString("body-zones-examine-fracture", ("target", identity), ("part", part)));
                else if (ent.Comp.Cracks.Contains(zone))
                    args.PushMarkup(Loc.GetString("body-zones-examine-crack", ("target", identity), ("part", part)));

                if (ent.Comp.Skinless.Contains(zone))
                    args.PushMarkup(Loc.GetString("body-zones-examine-skinless", ("target", identity), ("part", part)));
            }
        }
    }
}
