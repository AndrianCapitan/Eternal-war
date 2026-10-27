#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._EternalWar.BodyZones;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Organs;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory.VirtualItem;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// The circulation: blood reaches the zones through their vessels, tourniquets and cut vessels starve the limbs,
/// stumps gush, the brain suffocates without blood.
/// </summary>
[TestOf(typeof(BodyZonesSystem))]
public sealed class CirculationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [SidedDependency(Side.Server)] private readonly BodyZonesSystem _zones = default!;
    [SidedDependency(Side.Server)] private readonly DamageableSystem _damageable = default!;
    [SidedDependency(Side.Server)] private readonly BloodstreamSystem _bloodstream = default!;
    [SidedDependency(Side.Server)] private readonly SharedContainerSystem _container = default!;

    private EntityUid _victim;

    private CirculationComponent Circulation => SEntMan.GetComponent<CirculationComponent>(_victim);

    private BodyZonesComponent Zones => SEntMan.GetComponent<BodyZonesComponent>(_victim);

    private async Task SpawnVictim(bool air = true)
    {
        if (air)
            await AddAtmosphere();

        await Server.WaitPost(() =>
        {
            _victim = SEntMan.SpawnAtPosition("MobHuman", SEntMan.GetCoordinates(TargetCoords));
            // Fast limbs, so the tests don't wait minutes.
            var circulation = SEntMan.GetComponent<CirculationComponent>(_victim);
            circulation.OxygenReserve = 2f;
            circulation.NecrosisTime = 3f;
        });
        await RunTicks(1);
    }

    private FixedPoint2 DamageOf(string type)
    {
#pragma warning disable CS0618
        return _damageable.GetAllDamage(_victim).DamageDict.GetValueOrDefault(type);
#pragma warning restore CS0618
    }

    private void RemoveOrgan(string category)
    {
        var body = _container.GetContainer(_victim, BodyComponent.ContainerID);
        var organ = body.ContainedEntities.Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
        _container.Remove(organ, body);
    }

    /// <summary>
    /// Units of blood in the body. The blood level is rounded to hundredths, too coarse for a few seconds of bleeding.
    /// </summary>
    private float BloodVolume()
    {
        var name = SEntMan.GetComponent<BloodstreamComponent>(_victim).BloodSolutionName;
        Assert.That(SEntMan.System<SharedSolutionContainerSystem>().TryGetSolution(_victim, name, out _, out var blood));
        return blood!.Volume.Float();
    }

    [Test]
    public async Task BloodReachesEveryZoneOfAHealthyBody()
    {
        await SpawnVictim();
        await RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Circulation.Numb, Is.Empty);
            Assert.That(Circulation.Necrotic, Is.Empty);
            Assert.That(Circulation.Oxygen, Is.Empty, "every zone gets blood");
            Assert.That(DamageOf("Asphyxiation"), Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task TourniquetStarvesTheLimbBelowUntilTakenOff()
    {
        await SpawnVictim();
        Target = SEntMan.GetNetEntity(_victim);
        await Server.WaitPost(() => _zones.SetTarget(SPlayer, BodyZone.LeftForearm));
        await RunTicks(5);

        await InteractUsing("Tourniquet");
        await RunTicks(2);
        await Server.WaitAssertion(() =>
            Assert.That(Circulation.Tourniquets, Is.EquivalentTo(new[] { BodyZone.LeftForearm })));

        // The 2 second reserve runs out: the forearm and the hand below it go numb, the upper arm is fine.
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Circulation.Numb, Is.EquivalentTo(new[] { BodyZone.LeftForearm, BodyZone.LeftHand }));
            Assert.That(HandSys.TryGetHeldItem(_victim, "left", out var held), "a numb arm can't hold anything");
            Assert.That(SEntMan.HasComponent<VirtualItemComponent>(held));
        });

        await Server.WaitPost(() => Assert.That(_zones.RemoveTourniquet((_victim, Circulation), BodyZone.LeftForearm)));
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Circulation.Numb, Is.Empty, "blood is back");
            Assert.That(HandSys.TryGetHeldItem(_victim, "left", out _), Is.False);
        });
    }

    [Test]
    public async Task LimbWithoutBloodDiesAndPoisons()
    {
        await SpawnVictim();
        await Server.WaitPost(() =>
        {
            // The liver and kidneys would make this little poison harmless (OrganFunctionsTest).
            SEntMan.GetComponent<OrganFunctionsComponent>(_victim).Detox.Clear();
            Circulation.Tourniquets.Add(BodyZone.RightShin);
        });

        // 2 seconds of reserve, then 3 seconds to die.
        await RunSeconds(7);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Circulation.Necrotic, Is.EquivalentTo(new[] { BodyZone.RightShin, BodyZone.RightFoot }));
            Assert.That(DamageOf("Poison"), Is.GreaterThan(FixedPoint2.Zero));
        });

        // Dead tissue stays dead with the blood back: it can only be cut off.
        await Server.WaitPost(() => _zones.RemoveTourniquet((_victim, Circulation), BodyZone.RightShin));
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
            Assert.That(Circulation.Necrotic, Is.EquivalentTo(new[] { BodyZone.RightShin, BodyZone.RightFoot })));

        await Server.WaitPost(() => _zones.SeverZone((_victim, Zones), BodyZone.RightShin));
        await RunTicks(2);
        await Server.WaitAssertion(() => Assert.That(Circulation.Necrotic, Is.Empty));
    }

    [Test]
    public async Task TornVesselsStarveTheZonesBelow()
    {
        await SpawnVictim();
        await Server.WaitPost(() => RemoveOrgan("VesselsUpperArmLeft"));
        await RunSeconds(4);

        await Server.WaitAssertion(() =>
            Assert.That(Circulation.Numb, Is.EquivalentTo(new[] { BodyZone.LeftUpperArm, BodyZone.LeftForearm, BodyZone.LeftHand })));
    }

    [Test]
    public async Task StumpGushesUntilATourniquetHoldsIt()
    {
        await SpawnVictim();
        await Server.WaitPost(() => _zones.SeverZone((_victim, Zones), BodyZone.LeftUpperArm));
        await RunTicks(1);

        await Server.WaitAssertion(() =>
            Assert.That(Circulation.Stumps.Keys, Is.EquivalentTo(new[] { BodyZone.LeftUpperArm }), "one stump, at the shoulder"));

        // Only the fountain bleeds from now on.
        await Server.WaitPost(() => _bloodstream.TryModifyBleedAmount(_victim, -100));
        var before = 0f;
        await Server.WaitPost(() => before = BloodVolume());
        await RunSeconds(3);
        var after = 0f;
        await Server.WaitPost(() => after = BloodVolume());
        Assert.That(after, Is.LessThan(before - 5f), "the stump gushes: about 4 units a second");

        // A tourniquet aimed at the missing hand goes on the stump of the arm.
        Target = SEntMan.GetNetEntity(_victim);
        await Server.WaitPost(() => _zones.SetTarget(SPlayer, BodyZone.LeftHand));
        await RunTicks(5);
        await InteractUsing("Tourniquet");
        await RunTicks(2);
        await Server.WaitAssertion(() =>
            Assert.That(Circulation.Tourniquets, Is.EquivalentTo(new[] { BodyZone.LeftUpperArm })));

        await Server.WaitPost(() => _bloodstream.TryModifyBleedAmount(_victim, -100));
        await Server.WaitPost(() => before = BloodVolume());
        await RunSeconds(3);
        await Server.WaitPost(() => after = BloodVolume());
        Assert.That(after, Is.GreaterThanOrEqualTo(before), "the tourniquet holds the blood");
    }

    [Test]
    public async Task WithoutHeartTheBrainSuffocatesInGoodAir()
    {
        await SpawnVictim();
        await Server.WaitPost(() => RemoveOrgan("Heart"));

        // The brain lasts 5 seconds on what it has.
        await RunSeconds(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(DamageOf("Asphyxiation"), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(_zones.IsNumb(_victim, BodyZone.LeftHand), "no zone gets blood");
        });
    }

    [Test]
    public async Task NoAirMeansNoOxygenInTheBlood()
    {
        // The test map is a vacuum.
        await SpawnVictim(air: false);
        await RunSeconds(15);

        await Server.WaitAssertion(() => Assert.That(DamageOf("Asphyxiation"), Is.GreaterThan(FixedPoint2.Zero)));
    }
}
