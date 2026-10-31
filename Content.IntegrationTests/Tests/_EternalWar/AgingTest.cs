#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._EternalWar.Aging;
using Content.Shared._EternalWar.Aging;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared.Body;
using Content.Shared.Damage.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Preferences;
using Content.Shared.Rejuvenate;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// The time ray ages, age weakens the body, and aged to 120 it dies for good.
/// </summary>
[TestOf(typeof(AgingSystem))]
public sealed class AgingTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static readonly Color RedHair = Color.FromHex("#C0301A");
    private static readonly ProtoId<MarkingPrototype> Hair = "HumanHairAfro";

    [SidedDependency(Side.Server)] private readonly AgingSystem _aging = default!;
    [SidedDependency(Side.Server)] private readonly HumanoidProfileSystem _humanoid = default!;
    [SidedDependency(Side.Server)] private readonly InventorySystem _inventory = default!;
    [SidedDependency(Side.Server)] private readonly MobStateSystem _mobState = default!;
    [SidedDependency(Side.Server)] private readonly SharedVisualBodySystem _visualBody = default!;

    private EntityUid _victim;

    private AgingComponent Aging => SEntMan.GetComponent<AgingComponent>(_victim);
    private int Age => SEntMan.GetComponent<HumanoidProfileComponent>(_victim).Age;

    /// <summary>
    /// A 30-year-old human with red hair, right in front of the player.
    /// </summary>
    private async Task SpawnVictim()
    {
        await AddAtmosphere();
        await Server.WaitPost(() =>
        {
            _victim = SEntMan.SpawnAtPosition("MobHuman", SEntMan.GetCoordinates(TargetCoords));
            var profile = HumanoidCharacterProfile.RandomWithSpecies("Human").WithAge(30);
            _humanoid.ApplyProfileTo(_victim, profile);
            _visualBody.ApplyProfileTo(_victim, profile);

            Assert.That(_visualBody.TryGatherMarkingsData(_victim, null, out _, out var markingData, out _));
            var markings = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>();
            foreach (var (category, data) in markingData!)
            {
                if (data.Layers.Contains(HumanoidVisualLayers.Hair))
                    markings[category] = new() { [HumanoidVisualLayers.Hair] = new() { new Marking(Hair, new[] { RedHair }) } };
            }

            _visualBody.ApplyMarkings(_victim, markings);
        });
        await RunTicks(1);
        Target = SEntMan.GetNetEntity(_victim);
    }

    private Color HairColor()
    {
        Assert.That(_visualBody.TryGatherMarkingsData(_victim, null, out _, out _, out var applied));
        return applied!.Values.SelectMany(l => l.TryGetValue(HumanoidVisualLayers.Hair, out var m) ? m : new List<Marking>())
            .SelectMany(m => m.MarkingColors)
            .First();
    }

    private static float Distance(Color a, Color b)
    {
        return MathF.Abs(a.R - b.R) + MathF.Abs(a.G - b.G) + MathF.Abs(a.B - b.B);
    }

    private async Task Shoot()
    {
        // A gun fires twice a second at most.
        await RunSeconds(0.6f);
        await AttemptShoot(Target);
        await RunTicks(2);
    }

    [Test]
    public async Task TimeRayAgesTenYearsAndGreysTheHair()
    {
        await SpawnVictim();
        await PlaceInHands("EternalWarWeaponTimeRay");

        await Shoot();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Age, Is.EqualTo(40));
            // Greying starts at 40.
            Assert.That(HairColor(), Is.EqualTo(RedHair));
        });

        await Shoot();
        await Shoot();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Age, Is.EqualTo(60));
            var hair = HairColor();
            Assert.That(Distance(hair, Aging.GreyHair), Is.LessThan(Distance(RedHair, Aging.GreyHair)));
            Assert.That(Distance(hair, RedHair), Is.GreaterThan(0.1f));
        });

        // Rejuvenating gives back the years and the hair.
        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(_victim, new RejuvenateEvent()));
        await Server.WaitAssertion(() =>
        {
            Assert.That(Age, Is.EqualTo(30));
            Assert.That(Distance(HairColor(), RedHair), Is.LessThan(0.01f));
        });
    }

    [Test]
    public async Task VestTakesHalfTheYears()
    {
        await SpawnVictim();
        await Server.WaitPost(() =>
        {
            var vest = SEntMan.SpawnAtPosition("EternalWarClothingOuterChronoVest", SEntMan.GetCoordinates(TargetCoords));
            Assert.That(_inventory.TryEquip(_victim, vest, "outerClothing", force: true));
        });
        await PlaceInHands("EternalWarWeaponTimeRay");

        await Shoot();
        await Server.WaitAssertion(() => Assert.That(Age, Is.EqualTo(35)));
    }

    [Test]
    public async Task OldBodyIsSlowerWeakerAndHealsSlower()
    {
        await SpawnVictim();
        var walk = 0f;
        var stamina = 0f;
        var regen = TimeSpan.Zero;

        await Server.WaitAssertion(() =>
        {
            walk = SEntMan.GetComponent<MovementSpeedModifierComponent>(_victim).CurrentWalkSpeed;
            stamina = SEntMan.GetComponent<StaminaComponent>(_victim).CritThreshold;
            regen = SEntMan.GetComponent<PassiveDamageComponent>(_victim).Interval;
            Assert.That(_aging.GetBoneFragility(_victim), Is.EqualTo(1f));
            Assert.That(_aging.GetHealing(_victim), Is.EqualTo(1f));
        });

        void AssertStage(float speed, float staminaShare, float fragility, float healing)
        {
            Assert.That(SEntMan.GetComponent<MovementSpeedModifierComponent>(_victim).CurrentWalkSpeed,
                Is.EqualTo(walk * speed).Within(0.01f));
            Assert.That(SEntMan.GetComponent<StaminaComponent>(_victim).CritThreshold,
                Is.EqualTo(stamina * staminaShare).Within(0.01f));
            Assert.That(_aging.GetBoneFragility(_victim), Is.EqualTo(fragility));
            Assert.That(_aging.GetHealing(_victim), Is.EqualTo(healing));
            Assert.That(SEntMan.GetComponent<PassiveDamageComponent>(_victim).Interval.TotalSeconds,
                Is.EqualTo(regen.TotalSeconds / healing).Within(0.01));
        }

        await Server.WaitPost(() => _humanoid.SetAge(_victim, 65));
        await RunTicks(1);
        await Server.WaitAssertion(() =>
            AssertStage(Aging.OldSpeed, Aging.OldStamina, Aging.OldBoneFragility, Aging.OldHealing));

        await Server.WaitPost(() => _humanoid.SetAge(_victim, 85));
        await RunTicks(1);
        await Server.WaitAssertion(() =>
            AssertStage(Aging.FrailSpeed, Aging.FrailStamina, Aging.FrailBoneFragility, Aging.FrailHealing));

        // A character made 120 in the editor is frail, but doesn't die: only aging to 120 kills.
        await Server.WaitPost(() => _humanoid.SetAge(_victim, 120));
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Aging.Dying, Is.False);
            Assert.That(_mobState.IsAlive(_victim));
        });

        await Server.WaitPost(() => _humanoid.SetAge(_victim, 30));
        await RunTicks(1);
        await Server.WaitAssertion(() => AssertStage(1f, 1f, 1f, 1f));
    }

    [Test]
    public async Task AgedTo120TheHeartThenTheBrainGiveOut()
    {
        await SpawnVictim();
        await Server.WaitPost(() =>
        {
            // Ten times as fast, not to wait half a minute.
            Aging.OrganDecay = 20f;
            _aging.AddYears((_victim, Aging), 90);
        });
        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Age, Is.EqualTo(120));
            Assert.That(Aging.Dying);
            Assert.That(SEntMan.GetComponent<BodyZonesComponent>(_victim).FailedOrgans, Is.Empty);
        });

        // The heart has 25 health: it gives out in two seconds, the brain is still there.
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            var failed = SEntMan.GetComponent<BodyZonesComponent>(_victim).FailedOrgans;
            Assert.That(failed, Does.Contain(new ProtoId<OrganCategoryPrototype>("Heart")));
            Assert.That(failed, Does.Not.Contain(new ProtoId<OrganCategoryPrototype>("Brain")));
        });

        // The brain has 40: two seconds more, and the body is dead for good.
        await RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BodyZonesComponent>(_victim).FailedOrgans,
                Does.Contain(new ProtoId<OrganCategoryPrototype>("Brain")));
            Assert.That(_mobState.IsDead(_victim));
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(_victim));
            Assert.That(Aging.Dying, Is.False);
        });

        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(_victim, new RejuvenateEvent()));
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Age, Is.EqualTo(30));
            Assert.That(_mobState.IsAlive(_victim));
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(_victim), Is.False);
            Assert.That(Aging.Dying, Is.False);
        });
    }
}
