using System.Linq;
using Content.Shared._EternalWar.Pressure;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._EternalWar.Pressure;

public sealed partial class PressureBreakageSystem
{
    /// <summary>
    /// The fixture a thing carried by space wind hits people with. Like a thrown item's, it doesn't stop at them.
    /// </summary>
    private const string WindFixture = "eternalwar-wind";

    /// <summary>
    /// Slower than this, a thing doesn't hurt anybody.
    /// </summary>
    private const float MinHitSpeed = 5f;

    /// <summary>
    /// A point of damage for this many joules of a blow (a 1.5 kg toolbox at 20 m/s is 15).
    /// </summary>
    private const float JoulesPerDamage = 20f;

    private const float MaxHitDamage = 40f;

    /// <summary>
    /// A thing keeps this share of its speed after it hits somebody.
    /// </summary>
    private const float SpeedKeptAfterHit = 0.5f;

    /// <summary>
    /// The weight of things by their size, kg. The game's physics masses are made for pushing, not for hitting.
    /// </summary>
    private static readonly Dictionary<ProtoId<ItemSizePrototype>, float> RealMass = new()
    {
        ["Tiny"] = 0.1f,
        ["Small"] = 0.5f,
        ["Normal"] = 1.5f,
        ["Large"] = 4f,
        ["Huge"] = 10f,
        ["Ginormous"] = 30f,
    };

    private static readonly ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype> BluntDamage = "Blunt";

    private void InitializeImpacts()
    {
        SubscribeLocalEvent<CarriedByWindComponent, ComponentStartup>(OnWindStartup);
        SubscribeLocalEvent<CarriedByWindComponent, ComponentShutdown>(OnWindShutdown);
        SubscribeLocalEvent<CarriedByWindComponent, StartCollideEvent>(OnWindCollide);
    }

    private void OnWindStartup(Entity<CarriedByWindComponent> ent, ref ComponentStartup args)
    {
        if (!HasComp<ItemComponent>(ent)
            || !TryComp<FixturesComponent>(ent, out var fixtures)
            || !TryComp<PhysicsComponent>(ent, out var body)
            || fixtures.Fixtures.Values.FirstOrDefault(f => f.Hard) is not { } fixture)
            return;

        ent.Comp.HasHitFixture = _fixtures.TryCreateFixture(ent,
            fixture.Shape,
            WindFixture,
            hard: false,
            collisionMask: (int) CollisionGroup.ThrownItem,
            manager: fixtures,
            body: body);
    }

    private void OnWindShutdown(Entity<CarriedByWindComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.HasHitFixture && !TerminatingOrDeleted(ent))
            _fixtures.DestroyFixture(ent, WindFixture);
    }

    private void OnWindCollide(Entity<CarriedByWindComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId == WindFixture)
        {
            if (args.OtherFixture.Hard && HasComp<MobStateComponent>(args.OtherEntity))
                HitPerson(ent, args.OtherEntity, args.OurBody, args.OtherBody);
            return;
        }

        if (args.OurFixture.Hard && args.OtherFixture.Hard)
            TryImpactBreak(ent, args.OurBody.LinearVelocity.Length());
    }

    /// <summary>
    /// A thing carried by the wind flies into somebody: a blow by its energy, cutting if it is sharp.
    /// </summary>
    private void HitPerson(Entity<CarriedByWindComponent> ent, EntityUid victim, PhysicsComponent body, PhysicsComponent victimBody)
    {
        if (victim == ent.Owner || !ent.Comp.Hit.Add(victim))
            return;

        var speed = (body.LinearVelocity - victimBody.LinearVelocity).Length();
        if (speed < MinHitSpeed)
            return;

        var mass = TryComp<ItemComponent>(ent, out var item) && RealMass.TryGetValue(item.Size, out var real) ? real : 1f;
        var amount = MathF.Min(0.5f * mass * speed * speed / JoulesPerDamage, MaxHitDamage);
        if (amount >= 1f)
        {
            _damageable.TryChangeDamage(victim, GetHitDamage(ent, amount), origin: ent);
            _popup.PopupEntity(Loc.GetString("pressure-wind-hit", ("item", ent.Owner)),
                Loc.GetString("pressure-wind-hit-others", ("item", ent.Owner), ("target", Identity.Entity(victim, EntityManager))),
                victim,
                victim,
                PopupType.MediumCaution);
        }

        _physics.SetLinearVelocity(ent, body.LinearVelocity * SpeedKeptAfterHit, body: body);
        TryImpactBreak(ent, speed);
    }

    /// <summary>
    /// A sharp thing cuts as it would thrown, only harder; anything else is a blunt blow.
    /// </summary>
    private DamageSpecifier GetHitDamage(EntityUid uid, float amount)
    {
        if (TryComp<DamageOtherOnHitComponent>(uid, out var onHit))
        {
            var thrown = new DamageSpecifier(onHit.Damage);
            var total = thrown.GetTotal();
            if (total > FixedPoint2.Zero)
                return thrown * (amount / total.Float());
        }

        return new DamageSpecifier(_proto.Index(BluntDamage), amount);
    }

    /// <summary>
    /// A fragile thing that hit something too fast breaks: a sealed container, a device, a glass vessel.
    /// </summary>
    public bool TryImpactBreak(EntityUid uid, float speed)
    {
        if (TerminatingOrDeleted(uid) || HasComp<StackComponent>(uid) || HasComp<PressureTornComponent>(uid))
            return false;

        if (GetMaterial(uid) is not { } material || speed < material.ImpactSpeed)
            return false;

        if (HasComp<BrokenElectronicsComponent>(uid)
            || TryComp<LightBulbComponent>(uid, out var bulb) && bulb.State == LightBulbState.Broken)
            return false;

        var sealedVessel = TryComp<OpenableComponent>(uid, out var openable) && !openable.Opened;
        var fragile = material.Breakage == PressureBreakage.Disabled
                      || bulb != null
                      || sealedVessel
                      || material.Breakage == PressureBreakage.Destroyed && _solutions.EnumerateSolutions(uid).Any();
        if (!fragile)
            return false;

        // A sealed container lets its pressure go as it breaks.
        var energy = 0f;
        if (TryComp<PressureVesselComponent>(uid, out var vessel) && _atmos.GetContainingMixture(uid) is { } air)
            energy = PressurePhysics.BurstEnergy(GetInternalPressure(vessel), air.Pressure, vessel.Volume * vessel.Headspace);

        Break(uid, material, crushed: false, energy);
        return true;
    }
}
