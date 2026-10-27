#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._EternalWar.BodyZones;
using Content.Server._EternalWar.Organs;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Organs;
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// The organs at work: the tongue speaks, the ears hear, the liver and kidneys take the poison, the stomach sorts
/// what was eaten, the appendix clogs up, the spleen makes the blood.
/// </summary>
[TestOf(typeof(OrganFunctionsSystem))]
public sealed class OrganFunctionsTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [SidedDependency(Side.Server)] private readonly OrganFunctionsSystem _organs = default!;
    [SidedDependency(Side.Server)] private readonly BodyZonesSystem _zones = default!;
    [SidedDependency(Side.Server)] private readonly DamageableSystem _damageable = default!;
    [SidedDependency(Side.Server)] private readonly ActionBlockerSystem _blocker = default!;
    [SidedDependency(Side.Server)] private readonly AlertsSystem _alerts = default!;
    [SidedDependency(Side.Server)] private readonly BloodstreamSystem _bloodstream = default!;
    [SidedDependency(Side.Server)] private readonly SharedSolutionContainerSystem _solutions = default!;
    [SidedDependency(Side.Server)] private readonly SharedContainerSystem _container = default!;

    private EntityUid _victim;

    private OrganFunctionsComponent Organs => SEntMan.GetComponent<OrganFunctionsComponent>(_victim);

    private async Task SpawnVictim()
    {
        await AddAtmosphere();
        await Server.WaitPost(() => _victim = SEntMan.SpawnAtPosition("MobHuman", SEntMan.GetCoordinates(TargetCoords)));
        await RunTicks(1);
    }

    private EntityUid Organ(string category)
    {
        var body = _container.GetContainer(_victim, BodyComponent.ContainerID);
        return body.ContainedEntities.Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    private void RemoveOrgan(string category)
    {
        _container.Remove(Organ(category), _container.GetContainer(_victim, BodyComponent.ContainerID));
    }

    private float OrganDamage(string category)
    {
        return SEntMan.GetComponent<OrganHealthComponent>(Organ(category)).Damage;
    }

    private float BodyPoison()
    {
#pragma warning disable CS0618
        return _damageable.GetAllDamage(_victim).DamageDict.GetValueOrDefault("Poison").Float();
#pragma warning restore CS0618
    }

    private void Poison(float amount)
    {
        _damageable.TryChangeDamage(_victim, new DamageSpecifier { DamageDict = { ["Poison"] = amount } }, ignoreResistances: true);
    }

    private Entity<SolutionComponent> BodySolution(string name)
    {
        Assert.That(_solutions.TryGetSolution(_victim, name, out var solution), $"the body has the {name} solution");
        return solution!.Value;
    }

    private Entity<SolutionComponent> Stomach()
    {
        Assert.That(_solutions.TryGetSolution(Organ("Stomach"), StomachSystem.DefaultSolutionName, out var solution));
        return solution!.Value;
    }

    private FixedPoint2 Quantity(Entity<SolutionComponent> solution, string reagent)
    {
        return solution.Comp.Solution.GetTotalPrototypeQuantity(reagent);
    }

    [Test]
    public async Task WithoutATongueTheBodyCantSpeak()
    {
        await SpawnVictim();
        await Server.WaitAssertion(() =>
        {
            Assert.That(_blocker.CanSpeak(_victim));
            RemoveOrgan("Tongue");
            Assert.That(_blocker.CanSpeak(_victim), Is.False, "no tongue, no speech");
            Assert.That(_blocker.CanEmote(_victim), "emotes still work");
        });
    }

    [Test]
    public async Task EachInnerEarHearsHalf()
    {
        await SpawnVictim();
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs.Hearing, Is.EqualTo(1f));
            Assert.That(_organs.HeardDistance(_victim, 3f), Is.EqualTo(3f));
        });

        await Server.WaitPost(() => RemoveOrgan("EarLeft"));
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs.Hearing, Is.EqualTo(0.5f));
            Assert.That(_organs.HeardDistance(_victim, 3f), Is.EqualTo(6f), "with one ear speech is heard at half the range");
        });

        await Server.WaitPost(() => RemoveOrgan("EarRight"));
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs.Hearing, Is.EqualTo(0f));
            Assert.That(_organs.HeardDistance(_victim, 0.5f), Is.EqualTo(float.PositiveInfinity), "the deaf hear nothing");
        });
    }

    [Test]
    public async Task LiverAndKidneysMakeALittlePoisonHarmless()
    {
        await SpawnVictim();
        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs.DetoxBudget, Is.EqualTo(4f).Within(0.01f), "1 + 0.5 + 0.5 a second, up to two seconds worth");
            Poison(1.5f);
            Assert.That(BodyPoison(), Is.EqualTo(0f), "within what they process: harmless");
            Assert.That(OrganDamage("Liver"), Is.EqualTo(0f));
        });
    }

    [Test]
    public async Task PoisonHitsTheLiverAndKidneysFirstThenTheBody()
    {
        await SpawnVictim();
        await Server.WaitAssertion(() =>
        {
            Organs.DetoxBudget = 0f;

            // 20 + 15 + 15 health between them.
            Poison(30f);
            Assert.That(BodyPoison(), Is.EqualTo(0f), "the organs take it on themselves");
            Assert.That(OrganDamage("Liver") + OrganDamage("KidneyLeft") + OrganDamage("KidneyRight"), Is.EqualTo(30f).Within(0.01f));
            Assert.That(OrganDamage("Liver"), Is.EqualTo(10f).Within(0.01f), "shared out evenly");

            Poison(30f);
            Assert.That(BodyPoison(), Is.EqualTo(10f).Within(0.05f), "what they can't take reaches the body");
        });

        await RunTicks(2);
        await Server.WaitAssertion(() =>
        {
            var zones = SEntMan.GetComponent<BodyZonesComponent>(_victim);
            Assert.That(zones.FailedOrgans, Does.Contain(new Robust.Shared.Prototypes.ProtoId<OrganCategoryPrototype>("Liver")));
            Assert.That(_zones.HasWorkingOrgan((_victim, zones), "Liver"), Is.False);

            // Failed, they protect no more.
            Organs.DetoxBudget = 0f;
            Poison(5f);
            Assert.That(BodyPoison(), Is.EqualTo(15f).Within(0.05f));
        });
    }

    [Test]
    public async Task HealingPoisonBeyondTheBodysHealsTheOrgans()
    {
        await SpawnVictim();
        await Server.WaitAssertion(() =>
        {
            Organs.DetoxBudget = 0f;
            Poison(9f);
            Assert.That(OrganDamage("Liver"), Is.EqualTo(3f).Within(0.01f));

            Poison(-9f);
            Assert.That(OrganDamage("Liver") + OrganDamage("KidneyLeft") + OrganDamage("KidneyRight"), Is.EqualTo(0f).Within(0.01f));
        });
    }

    [Test]
    public async Task StomachSendsPoisonToTheLiverAndFoodToTheIntestines()
    {
        await SpawnVictim();
        await Server.WaitPost(() =>
        {
            _solutions.TryAddReagent(Stomach(), "Toxin", 4);
            _solutions.TryAddReagent(Stomach(), "Nutriment", 8);
        });

        // 2 units a second leave the stomach: 12 units in 6 seconds.
        await RunSeconds(7);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Quantity(Stomach(), "Toxin"), Is.EqualTo(FixedPoint2.Zero), "the stomach passed it all on");
            Assert.That(Quantity(BodySolution("liver"), "Toxin"), Is.GreaterThan(FixedPoint2.Zero), "the liver holds the poison back");
            Assert.That(Quantity(BodySolution("bloodstream"), "Toxin"), Is.EqualTo(FixedPoint2.Zero), "none of it reached the blood");
            Assert.That(BodyPoison(), Is.EqualTo(0f));
        });

        // The intestines digest the food: none of it is left after a while.
        await RunSeconds(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Quantity(Stomach(), "Nutriment"), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(Quantity(BodySolution("intestines"), "Nutriment"), Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task WithoutALiverEatenPoisonGoesIntoTheBlood()
    {
        await SpawnVictim();
        await Server.WaitPost(() =>
        {
            RemoveOrgan("Liver");
            _solutions.TryAddReagent(Stomach(), "Toxin", 10);
        });

        await RunSeconds(2);
        await Server.WaitAssertion(() =>
            Assert.That(Quantity(BodySolution("bloodstream"), "Toxin"), Is.GreaterThan(FixedPoint2.Zero)));
    }

    [Test]
    public async Task FullAppendixHurtsUntilItEmptiesByHalf()
    {
        await SpawnVictim();
        await Server.WaitPost(() =>
        {
            Organs.AppendixChance = 1f;
            Organs.AppendixFirstVomit = TimeSpan.FromHours(1);
            _solutions.TryAddReagent(Stomach(), "Nutriment", 30);
        });

        await RunSeconds(8);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Quantity(BodySolution("appendix"), "Nutriment"), Is.GreaterThan(FixedPoint2.New(9.9)));
            Assert.That(Organs.Appendicitis);
            Assert.That(_alerts.IsShowingAlert(_victim, Organs.AppendixAlert));
        });

        await Server.WaitPost(() =>
        {
            Organs.AppendixChance = 0f;
            var appendix = BodySolution("appendix");
            _solutions.RemoveAllSolution(appendix);
            _solutions.TryAddReagent(appendix, "Nutriment", 4);
        });

        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs.Appendicitis, Is.False, "below half full the pain passes");
            Assert.That(_alerts.IsShowingAlert(_victim, Organs.AppendixAlert), Is.False);
        });
    }

    [Test]
    public async Task TheSpleenMakesTheBlood()
    {
        await SpawnVictim();
        float Blood()
        {
            var name = SEntMan.GetComponent<BloodstreamComponent>(_victim).BloodSolutionName;
            return BodySolution(name).Comp.Solution.Volume.Float();
        }

        var before = 0f;
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<BloodstreamComponent>(_victim).BloodRefreshAmount, Is.EqualTo(FixedPoint2.Zero),
                "the body doesn't make blood by itself");
            _bloodstream.TryModifyBloodLevel(_victim, -30);
            before = Blood();
        });

        await RunSeconds(7);
        var after = 0f;
        await Server.WaitPost(() => after = Blood());
        Assert.That(after, Is.GreaterThan(before), "the spleen made some");

        await Server.WaitPost(() =>
        {
            RemoveOrgan("Spleen");
            before = Blood();
        });

        await RunSeconds(7);
        await Server.WaitPost(() => after = Blood());
        Assert.That(after, Is.EqualTo(before).Within(0.01f), "without a spleen no new blood");
    }
}
