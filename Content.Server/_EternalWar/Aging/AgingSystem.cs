using System.Linq;
using Content.Server._EternalWar.BodyZones;
using Content.Server._EternalWar.Genetics;
using Content.Shared._EternalWar.Aging;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Humanoid;
using Content.Shared.Administration.Logs;
using Content.Shared.Body;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Traits.Assorted;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._EternalWar.Aging;

/// <summary>
/// Ages bodies: the time ray adds years, the hair goes grey and the skin fades with them, natural healing slows down,
/// and once aged to <see cref="AgingComponent.DeathAge"/> the body's organs give out one by one until it is dead for good.
/// </summary>
public sealed partial class AgingSystem : SharedAgingSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private BodyZonesSystem _bodyZones = default!;
    [Dependency] private HumanoidProfileSystem _humanoid = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanAgingComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
        SubscribeLocalEvent<AgingComponent, RejuvenateEvent>(OnRejuvenate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AgingComponent>();
        while (query.MoveNext(out var uid, out var aging))
        {
            if (!aging.Dying || now < aging.NextDyingUpdate)
                continue;

            aging.NextDyingUpdate = now + aging.DyingInterval;
            Decay((uid, aging));
        }
    }

    #region Aging

    private void OnHitscanHit(Entity<HitscanAgingComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity is not { } target || !TryComp<AgingComponent>(target, out var aging))
            return;

        var years = ent.Comp.Years * GetProtection(target);
        if (years <= 0f)
            return;

        AddYears((target, aging), years, args.Data.Shooter);
    }

    /// <summary>
    /// The share of the years the time ray would take that it takes from someone wearing or holding this.
    /// </summary>
    public float GetProtection(EntityUid target)
    {
        var coefficient = 1f;
        foreach (var item in _inventory.GetHandOrInventoryEntities(target))
        {
            if (TryComp<AgingProtectionComponent>(item, out var protection))
                coefficient *= protection.Coefficient;
        }

        return coefficient;
    }

    /// <summary>
    /// Makes the body older. Parts of a year add up until they make a whole one.
    /// </summary>
    public void AddYears(Entity<AgingComponent> ent, float years, EntityUid? user = null)
    {
        if (!TryComp<HumanoidProfileComponent>(ent, out var profile))
            return;

        ent.Comp.PendingYears += years;
        var whole = (int) MathF.Floor(ent.Comp.PendingYears);
        if (whole <= 0)
            return;

        ent.Comp.PendingYears -= whole;

        // What the body was like before anything aged it: to grey the hair from and to rejuvenate back to.
        if (ent.Comp.OriginalAge == null)
        {
            ent.Comp.OriginalAge = profile.Age;
            RememberLooks(ent);
        }

        var identity = Identity.Entity(ent, EntityManager);
        _popup.PopupEntity(Loc.GetString("aging-hit-self"),
            Loc.GetString("aging-hit-others", ("target", identity)),
            ent,
            ent,
            PopupType.MediumCaution);

        _adminLogger.Add(LogType.Damaged,
            LogImpact.Medium,
            $"{ToPrettyString(user):user} aged {ToPrettyString(ent):target} by {whole} years to {profile.Age + whole}");

        _humanoid.SetAge((ent.Owner, profile), profile.Age + whole);
    }

    protected override void OnAgeChanged(Entity<AgingComponent> ent, ref AgeChangedEvent args)
    {
        base.OnAgeChanged(ent, ref args);

        UpdateHealing(ent);

        // A character made old in the editor keeps the looks the player chose for them.
        if (ent.Comp.OriginalAge == null)
            return;

        UpdateLooks(ent, args.NewAge);

        if (!ent.Comp.Dying && args.NewAge >= ent.Comp.DeathAge)
            StartDying(ent);
    }

    /// <summary>
    /// The body heals by itself slower with age: as much at a time, but less often
    /// (the amounts are too small to scale, they would be rounded).
    /// </summary>
    private void UpdateHealing(Entity<AgingComponent> ent)
    {
        if (!TryComp<PassiveDamageComponent>(ent, out var passive))
            return;

        ent.Comp.BaseRegenInterval ??= passive.Interval;
        passive.Interval = ent.Comp.BaseRegenInterval.Value / GetHealing(ent);
        Dirty(ent, passive);
    }

    #endregion

    #region Looks

    private void RememberLooks(Entity<AgingComponent> ent)
    {
        if (!_visualBody.TryGatherMarkingsData(ent.Owner, null, out var profiles, out _, out var applied))
            return;

        ent.Comp.OriginalSkin = profiles.ToDictionary(p => p.Key, p => p.Value.SkinColor);
        ent.Comp.OriginalHair = GenomeSystem.FirstMarkingColor(applied, HumanoidVisualLayers.Hair);
        ent.Comp.OriginalBeard = GenomeSystem.FirstMarkingColor(applied, HumanoidVisualLayers.FacialHair);
    }

    /// <summary>
    /// How grey the body is at this age: from 0 when it was first aged (or at <see cref="AgingComponent.GreyingAge"/>,
    /// if it was younger) to 1 at <see cref="AgingComponent.GreyAge"/>.
    /// </summary>
    private static float GetGreying(AgingComponent comp, int age)
    {
        var start = Math.Max(comp.OriginalAge ?? age, comp.GreyingAge);
        if (age <= start)
            return 0f;

        if (comp.GreyAge <= start)
            return 1f;

        return Math.Clamp((age - start) / (float) (comp.GreyAge - start), 0f, 1f);
    }

    private void UpdateLooks(Entity<AgingComponent> ent, int age)
    {
        if (ent.Comp.OriginalSkin == null
            || !_visualBody.TryGatherMarkingsData(ent.Owner, null, out var profiles, out _, out var applied))
            return;

        var greying = GetGreying(ent.Comp, age);

        var faded = new Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>();
        foreach (var (organ, profile) in profiles)
        {
            var skin = ent.Comp.OriginalSkin.GetValueOrDefault(organ, profile.SkinColor);
            faded[organ] = profile with { SkinColor = Fade(skin, ent.Comp.SkinFading * greying) };
        }

        _visualBody.ApplyProfiles(ent, faded);

        Color? hair = ent.Comp.OriginalHair is { } h ? Color.InterpolateBetween(h, ent.Comp.GreyHair, greying) : null;
        Color? beard = ent.Comp.OriginalBeard is { } b ? Color.InterpolateBetween(b, ent.Comp.GreyHair, greying) : null;

        // Markings are applied again even when the hair has no color to change: the ones that follow
        // the skin color only pick up a new skin color this way.
        var recolored = applied.ToDictionary(
            organ => organ.Key,
            organ => organ.Value.ToDictionary(
                layer => layer.Key,
                layer => layer.Key switch
                {
                    HumanoidVisualLayers.Hair when hair is { } color => layer.Value.Select(m => m.WithColor(color)).ToList(),
                    HumanoidVisualLayers.FacialHair when beard is { } color => layer.Value.Select(m => m.WithColor(color)).ToList(),
                    _ => layer.Value.ToList(),
                }));

        _visualBody.ApplyMarkings(ent, recolored);
    }

    /// <summary>
    /// Takes away a share of the color's saturation.
    /// </summary>
    private static Color Fade(Color color, float share)
    {
        var hsv = Color.ToHsv(color);
        hsv.Y *= 1f - share;
        return Color.FromHsv(hsv);
    }

    #endregion

    #region Dying of old age

    private void StartDying(Entity<AgingComponent> ent)
    {
        ent.Comp.Dying = true;
        ent.Comp.NextDyingUpdate = _timing.CurTime + ent.Comp.DyingInterval;

        _popup.PopupEntity(Loc.GetString("aging-dying-self"),
            Loc.GetString("aging-dying-others", ("target", Identity.Entity(ent, EntityManager))),
            ent,
            ent,
            PopupType.LargeCaution);

        _adminLogger.Add(LogType.Damaged, LogImpact.High, $"{ToPrettyString(ent):target} is dying of old age");
    }

    /// <summary>
    /// The first organ of <see cref="AgingComponent.FailingOrgans"/> that still works takes a bit of damage.
    /// When none is left, the body is dead for good.
    /// </summary>
    private void Decay(Entity<AgingComponent> ent)
    {
        if (TryComp<BodyZonesComponent>(ent, out var zones))
        {
            var amount = ent.Comp.OrganDecay * (float) ent.Comp.DyingInterval.TotalSeconds;
            foreach (var category in ent.Comp.FailingOrgans)
            {
                foreach (var organ in _bodyZones.GetOrgans(ent, category))
                {
                    if (!TryComp<OrganHealthComponent>(organ, out var health) || health.Failed || health.Health <= 0f)
                        continue;

                    _bodyZones.DamagePiece((ent, zones), organ, amount);
                    return;
                }
            }
        }

        ent.Comp.Dying = false;
        _mobState.ChangeMobState(ent, MobState.Dead);

        if (!HasComp<UnrevivableComponent>(ent))
        {
            AddComp<UnrevivableComponent>(ent);
            ent.Comp.MadeUnrevivable = true;
        }

        _adminLogger.Add(LogType.Damaged, LogImpact.High, $"{ToPrettyString(ent):target} died of old age");
    }

    #endregion

    private void OnRejuvenate(Entity<AgingComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.Dying = false;
        ent.Comp.PendingYears = 0f;

        if (ent.Comp.MadeUnrevivable)
        {
            RemComp<UnrevivableComponent>(ent);
            ent.Comp.MadeUnrevivable = false;
        }

        if (ent.Comp.OriginalAge is not { } original)
            return;

        // Brings back the original looks too (no greying at the original age).
        _humanoid.SetAge(ent.Owner, original);
        ent.Comp.OriginalAge = null;
        ent.Comp.OriginalSkin = null;
        ent.Comp.OriginalHair = null;
        ent.Comp.OriginalBeard = null;
    }
}
