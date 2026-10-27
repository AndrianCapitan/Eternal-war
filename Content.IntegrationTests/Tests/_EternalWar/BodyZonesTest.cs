#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._EternalWar.BodyZones;
using Content.Server.Mind;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Rejuvenate;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._EternalWar;

[TestOf(typeof(BodyZonesSystem))]
public sealed class BodyZonesTest : InteractionTest
{
    // A real human, so the player can aim.
    protected override string PlayerPrototype => "MobHuman";

    [SidedDependency(Side.Server)] private readonly BodyZonesSystem _zones = default!;
    [SidedDependency(Side.Server)] private readonly DamageableSystem _damageable = default!;
    [SidedDependency(Side.Server)] private readonly HumanoidProfileSystem _humanoid = default!;
    [SidedDependency(Side.Server)] private readonly InventorySystem _inventory = default!;
    [SidedDependency(Side.Server)] private readonly MindSystem _minds = default!;
    [SidedDependency(Side.Server)] private readonly MobStateSystem _mobState = default!;
    [SidedDependency(Side.Server)] private readonly SharedContainerSystem _container = default!;
    [SidedDependency(Side.Server)] private readonly SharedVisualBodySystem _visualBody = default!;

    private EntityUid _victim;

    private DamageSpecifier Damage(string type, float amount)
    {
        return new DamageSpecifier(ProtoMan.Index<DamageTypePrototype>(type), FixedPoint2.New(amount));
    }

    private BodyZonesComponent Zones => SEntMan.GetComponent<BodyZonesComponent>(_victim);

    private MovementSpeedModifierComponent Movement => SEntMan.GetComponent<MovementSpeedModifierComponent>(_victim);

    private List<EntityUid> Organs(EntityUid body, string category)
    {
        return _container.GetContainer(body, BodyComponent.ContainerID).ContainedEntities
            .Where(o => SEntMan.GetComponent<OrganComponent>(o).Category == category)
            .ToList();
    }

    /// <summary>
    /// Whether the entity is on this test's map, not a leftover of another test that used the same server.
    /// </summary>
    private bool OnTestMap(EntityUid uid)
    {
        return SEntMan.GetComponent<TransformComponent>(uid).MapID == MapId;
    }

    /// <summary>
    /// Organs of the category lying around outside of any body.
    /// </summary>
    private int Loose(string category)
    {
        return SEntMan.AllEntities<OrganComponent>()
            .Count(o => SEntMan.GetComponent<OrganComponent>(o).Category == category
                        && !_container.IsEntityInContainer(o)
                        && OnTestMap(o));
    }

    /// <summary>
    /// Torn off limb parts of the category lying around, each in a piece of its own (not in the victim or the player).
    /// </summary>
    private List<EntityUid> Severed(string category)
    {
        return SEntMan.AllEntities<OrganComponent>()
            .Where(o => SEntMan.GetComponent<OrganComponent>(o).Category == category
                        && SEntMan.GetComponent<OrganComponent>(o).Body is { } b && b != _victim && b != SPlayer
                        && OnTestMap(o))
            .Select(o => o.Owner)
            .ToList();
    }

    /// <summary>
    /// Asserts that a whole part fell off as one piece holding exactly the given organs (the part itself included).
    /// </summary>
    private void AssertPiece(string part, params string[] inside)
    {
        var severed = Severed(part);
        Assert.That(severed, Has.Count.EqualTo(1), part);
        var body = SEntMan.GetComponent<OrganComponent>(severed[0]).Body!.Value;
        var categories = _container.GetContainer(body, BodyComponent.ContainerID).ContainedEntities
            .Select(o => SEntMan.GetComponent<OrganComponent>(o).Category?.Id)
            .ToList();
        Assert.That(categories, Is.EquivalentTo(inside.Append(part)), part);
    }

    /// <summary>
    /// Asserts that nothing is left of a destroyed part: no piece of it and no empty shell on the floor.
    /// </summary>
    private void AssertGone(params string[] parts)
    {
        foreach (var part in parts)
        {
            Assert.That(Severed(part), Is.Empty, part + " leaves no piece");
            Assert.That(Loose(part), Is.Zero, part + " leaves no shell");
        }
    }

    private void AssertLoose(params string[] categories)
    {
        foreach (var category in categories)
        {
            Assert.That(Loose(category), Is.EqualTo(1), category);
        }
    }

    private string? ProtoOf(EntityUid uid)
    {
        return SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
    }

    private async Task SpawnVictim()
    {
        // Air, or barotrauma and cold slip extra Blunt and Cold damage into the zones.
        await AddAtmosphere();
        await Server.WaitPost(() =>
        {
            _victim = SEntMan.SpawnAtPosition("MobHuman", SEntMan.GetCoordinates(TargetCoords));
            // Bones are knocked out of a destroyed zone at random: never, so the tests know where they are.
            SEntMan.GetComponent<BodyZonesComponent>(_victim).BoneDropChance = 0f;
        });
        await RunTicks(1);
    }

    private async Task Hit(BodyZone aim, string type, float amount)
    {
        await Server.WaitPost(() =>
        {
            _zones.SetTarget(SPlayer, aim);
            _damageable.TryChangeDamage(_victim, Damage(type, amount), origin: SPlayer);
        });
        // Zones are torn off in the next update.
        await RunTicks(2);
    }

    /// <summary>
    /// Takes health off the bone of a zone directly: a plain crushing hit splits at random between the layers,
    /// too unpredictable to test thresholds with.
    /// </summary>
    private async Task DamageBone(BodyZone zone, float amount)
    {
        await Server.WaitPost(() => _zones.DamagePiece((_victim, Zones), _zones.GetBone((_victim, Zones), zone)!.Value, amount));
        await RunTicks(2);
    }

    /// <summary>
    /// Crushes the bone of a zone to nothing, which destroys the zone.
    /// </summary>
    private Task CrushBone(BodyZone zone)
    {
        return DamageBone(zone, 1000f);
    }

    private OrganHealthComponent HealthOf(string category)
    {
        return SEntMan.GetComponent<OrganHealthComponent>(Organs(_victim, category).Single());
    }

    private async Task Rejuvenate()
    {
        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(_victim, new RejuvenateEvent()));
        await RunTicks(1);
    }

    [Test]
    public async Task ClientSelectsZone()
    {
        await Client.WaitPost(() => CEntMan.System<Content.Client._EternalWar.BodyZones.BodyZonesSystem>().SelectZone(BodyZone.LeftThigh));
        await RunTicks(5);

        Assert.That(SEntMan.GetComponent<TargetingComponent>(SPlayer).Selected, Is.EqualTo(BodyZone.LeftThigh));
    }

    [Test]
    public async Task ZonesAddUpToDeathThreshold()
    {
        await SpawnVictim();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.Zones.Keys, Is.EquivalentTo(BodyZoneExtensions.All));
            var total = Zones.Zones.Values.Aggregate(FixedPoint2.Zero, (sum, z) => sum + z.MaxHealth);
            Assert.That(total, Is.EqualTo(FixedPoint2.New(200)));
        });
    }

    [Test]
    public async Task AimedDamageGoesToZoneAndTearsForearmOff()
    {
        await SpawnVictim();
        await Hit(BodyZone.LeftForearm, "Blunt", 7);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.GetDamage(Zones, BodyZone.LeftForearm), Is.EqualTo(FixedPoint2.New(7)));
            Assert.That(_zones.GetDamage(Zones, BodyZone.Chest), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(Zones.Destroyed, Is.Empty);
        });

        // The forearm is destroyed when its bones are crushed.
        await CrushBone(BodyZone.LeftForearm);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsMissing(Zones, BodyZone.LeftForearm));
            Assert.That(_zones.IsMissing(Zones, BodyZone.LeftUpperArm), Is.False, "the upper arm stays");
            Assert.That(Organs(_victim, "ArmLeft"), Is.Empty);
            Assert.That(Organs(_victim, "HandLeft"), Is.Empty, "the hand goes with the forearm");
            Assert.That(Organs(_victim, "UpperArmLeft"), Has.Count.EqualTo(1));
            Assert.That(Organs(_victim, "BoneHumerusLeft"), Has.Count.EqualTo(1));
            Assert.That(HandSys.EnumerateHands(_victim), Does.Not.Contain("left"));

            // Nothing is left of the forearm, what was in it lies around but for the crushed bones.
            // The hand on it fell off whole.
            AssertGone("ArmLeft", "BoneForearmLeft");
            AssertLoose("MusclesForearmLeft", "VesselsForearmLeft", "SkinForearmLeft");
            AssertPiece("HandLeft", "BoneHandLeft", "MusclesHandLeft", "SkinHandLeft");

            // The forearm's damage stays on the body, now on the upper arm.
            Assert.That(_zones.GetDamage(Zones, BodyZone.LeftUpperArm), Is.EqualTo(FixedPoint2.New(7)));
            Assert.That(_mobState.IsAlive(_victim));
        });

        // Hitting the missing forearm hits the upper arm.
        await Hit(BodyZone.LeftForearm, "Blunt", 1);
        await Server.WaitAssertion(() =>
            Assert.That(_zones.GetDamage(Zones, BodyZone.LeftUpperArm), Is.EqualTo(FixedPoint2.New(8))));

        // Rejuvenation grows it back.
        await Rejuvenate();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.Destroyed, Is.Empty);
            Assert.That(Organs(_victim, "ArmLeft"), Has.Count.EqualTo(1));
            Assert.That(Organs(_victim, "HandLeft"), Has.Count.EqualTo(1));
            Assert.That(Organs(_victim, "BoneForearmLeft"), Has.Count.EqualTo(1));
            Assert.That(Organs(_victim, "MusclesForearmLeft"), Has.Count.EqualTo(1));
            Assert.That(HandSys.EnumerateHands(_victim), Does.Contain("left"));
        });
    }

    [Test]
    public async Task BladeCutsTheLayersAndCrushingBreaksTheBone()
    {
        await SpawnVictim();

        // 15 HP skin: a 10 slash leaves it on.
        await Hit(BodyZone.LeftShin, "Slash", 10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs(_victim, "SkinShinLeft"), Has.Count.EqualTo(1));
            Assert.That(HealthOf("SkinShinLeft").Health, Is.EqualTo(5f).Within(0.01f));
        });

        // The next slash tears it off; what's left over cuts into the muscles or vessels.
        await Hit(BodyZone.LeftShin, "Slash", 6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs(_victim, "SkinShinLeft"), Is.Empty);
            Assert.That(Loose("SkinShinLeft"), Is.EqualTo(1));
            Assert.That(Zones.Skinless, Does.Contain(BodyZone.LeftShin));
        });

        // A deep cut through the 20 HP muscles and 15 HP vessels: both torn, the zone bleeds.
        // The blade never touches the bone, and however much the zone is cut it isn't destroyed.
        await Hit(BodyZone.LeftShin, "Slash", 40);
        await Server.WaitAssertion(() =>
        {
            foreach (var organ in new[] { "MusclesShinLeft", "VesselsShinLeft" })
            {
                Assert.That(Organs(_victim, organ), Is.Empty, organ);
                Assert.That(Loose(organ), Is.EqualTo(1), organ);
            }

            Assert.That(HealthOf("BoneShinLeft").Damage, Is.Zero, "a blade doesn't cut bone");
            Assert.That(_zones.IsFractured(Zones, BodyZone.LeftShin), Is.False);
            Assert.That(_zones.IsMissing(Zones, BodyZone.LeftShin), Is.False);
            Assert.That(SEntMan.GetComponent<Content.Shared.Body.Components.BloodstreamComponent>(_victim).BleedAmount, Is.GreaterThan(0f));
        });

        // Crushing goes into what is left: only the bone (and 25% more without skin). 20 HP: cracked at 15 and less...
        await Hit(BodyZone.LeftShin, "Blunt", 6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(HealthOf("BoneShinLeft").Health, Is.EqualTo(20f - 6f * 1.25f).Within(0.01f));
            Assert.That(Zones.Cracks, Does.Contain(BodyZone.LeftShin));
            Assert.That(_zones.IsFractured(Zones, BodyZone.LeftShin), Is.False);
        });

        // ... broken below 10. With no skin and torn muscles the fracture is open.
        await Hit(BodyZone.LeftShin, "Blunt", 5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsFractured(Zones, BodyZone.LeftShin));
            Assert.That(Zones.Cracks, Does.Not.Contain(BodyZone.LeftShin));
            Assert.That(Zones.OpenFractures, Does.Contain(BodyZone.LeftShin));
        });

        // An open fracture shows on the body: a break across the bare bone.
        await RunTicks(5);
        await Client.WaitAssertion(() =>
            Assert.That(ClientExposure(HumanoidVisualLayers.LLeg, EternalWarHumanoidLayers.ExposedLShin).Overlay, Is.EqualTo("bone_open_l_shin")));

        // At 0 the bone is crushed and the shin is gone. The foot below is whole and falls off as one piece.
        await Hit(BodyZone.LeftShin, "Blunt", 9);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsMissing(Zones, BodyZone.LeftShin));
            AssertGone("LegLeft", "BoneShinLeft");
            AssertPiece("FootLeft", "BoneFootLeft", "MusclesFootLeft", "VesselsFootLeft", "SkinFootLeft");
            Assert.That(Organs(_victim, "ThighLeft"), Has.Count.EqualTo(1), "the thigh stays");
        });
    }

    [Test]
    public async Task CrushingHitsBonesAndOrgansMostly()
    {
        await SpawnVictim();

        // A club on the chest: most of it goes into the ribs and the organs, a little into the flesh and skin.
        await Hit(BodyZone.Chest, "Blunt", 20);
        await Server.WaitAssertion(() =>
        {
            var organs = HealthOf("Heart").Damage + HealthOf("LungLeft").Damage + HealthOf("LungRight").Damage;
            var soft = HealthOf("MusclesChest").Damage + HealthOf("VesselsChest").Damage;
            Assert.That(HealthOf("BoneRibs").Damage, Is.GreaterThanOrEqualTo(20f * 0.45f / 1.28f - 0.01f), "the bone takes the most");
            Assert.That(organs, Is.GreaterThanOrEqualTo(20f * 0.15f / 1.28f - 0.01f));
            Assert.That(HealthOf("SkinChest").Damage, Is.LessThanOrEqualTo(20f * 0.08f / 0.65f + 0.01f), "the skin very little");
            Assert.That(HealthOf("BoneRibs").Damage + organs + soft + HealthOf("SkinChest").Damage, Is.EqualTo(20f).Within(0.05f));
            Assert.That(Zones.Destroyed, Is.Empty);
        });

        // A blade doesn't reach the ribs, however deep it cuts.
        float ribs = 0;
        await Server.WaitPost(() => ribs = HealthOf("BoneRibs").Damage);
        await Hit(BodyZone.Chest, "Slash", 30);
        await Server.WaitAssertion(() => Assert.That(HealthOf("BoneRibs").Damage, Is.EqualTo(ribs).Within(0.01f)));
    }

    [Test]
    public async Task UpperArmTakesTheWholeArm()
    {
        await SpawnVictim();

        // Destroyed when the humerus is crushed.
        await Hit(BodyZone.RightUpperArm, "Slash", 16);
        await CrushBone(BodyZone.RightUpperArm);

        await Server.WaitAssertion(() =>
        {
            foreach (var zone in new[] { BodyZone.RightUpperArm, BodyZone.RightForearm, BodyZone.RightHand })
            {
                Assert.That(_zones.IsMissing(Zones, zone), zone.ToString());
            }

            foreach (var organ in new[] { "UpperArmRight", "ArmRight", "HandRight", "BoneHumerusRight", "BoneForearmRight",
                         "BoneHandRight", "MusclesUpperArmRight", "VesselsForearmRight", "MusclesHandRight" })
            {
                Assert.That(Organs(_victim, organ), Is.Empty, organ);
            }

            Assert.That(Zones.Fractures, Does.Not.Contain(BodyZone.RightUpperArm));
            Assert.That(HandSys.EnumerateHands(_victim), Does.Not.Contain("right"));
            Assert.That(_zones.GetDamage(Zones, BodyZone.Chest), Is.EqualTo(FixedPoint2.New(16)));

            // The upper arm is gone with its crushed bone, its other insides lie around.
            // The forearm with the hand is whole and fell off as one piece.
            AssertGone("UpperArmRight", "BoneHumerusRight");
            AssertLoose("MusclesUpperArmRight", "VesselsUpperArmRight", "SkinUpperArmRight");
            AssertPiece("ArmRight", "BoneForearmRight", "MusclesForearmRight", "VesselsForearmRight", "SkinForearmRight",
                "HandRight", "BoneHandRight", "MusclesHandRight", "SkinHandRight");
            Assert.That(Zones.Skinless, Is.Empty, "a lost arm is not a skinless one");
        });

        // A hit aimed at the missing hand lands on the chest.
        await Hit(BodyZone.RightHand, "Blunt", 2);
        await Server.WaitAssertion(() =>
            Assert.That(_zones.GetDamage(Zones, BodyZone.Chest), Is.EqualTo(FixedPoint2.New(18))));

        // Rejuvenation grows the whole arm back with its bones.
        await Rejuvenate();
        await Server.WaitAssertion(() =>
        {
            foreach (var organ in new[] { "UpperArmRight", "ArmRight", "HandRight", "BoneHumerusRight", "BoneForearmRight", "BoneHandRight" })
            {
                Assert.That(Organs(_victim, organ), Has.Count.EqualTo(1), organ);
            }

            Assert.That(HandSys.EnumerateHands(_victim), Does.Contain("right"));
        });
    }

    [Test]
    public async Task HeadBurstsAndDropsBrainAndEyes()
    {
        await SpawnVictim();

        EntityUid mind = default;
        EntityUid brain = default;
        await Server.WaitPost(() =>
        {
            mind = _minds.CreateMind(null).Owner;
            _minds.TransferTo(mind, _victim);
            brain = Organs(_victim, "Brain").Single();
        });

        // A bullet in the head isn't enough to burst it: the 30 HP skull holds.
        await Hit(BodyZone.Head, "Piercing", 47);
        await Server.WaitAssertion(() => Assert.That(Zones.Destroyed, Is.Empty));

        // The head bursts when the skull is crushed.
        await CrushBone(BodyZone.Head);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.Destroyed, Does.Contain(BodyZone.Head));
            foreach (var organ in new[] { "Head", "Brain", "EyeLeft", "EyeRight", "Tongue", "EarLeft", "EarRight", "MusclesFace", "VesselsHead" })
            {
                Assert.That(Organs(_victim, organ), Is.Empty, organ);
            }

            // The skull and the jaw stay in the body, hanging on nothing, to be pulled out.
            foreach (var organ in new[] { "BoneSkull", "BoneJaw" })
            {
                Assert.That(Organs(_victim, organ), Has.Count.EqualTo(1), organ);
                Assert.That(SEntMan.GetComponentOrNull<ChildOrganComponent>(Organs(_victim, organ)[0])?.Parent, Is.Null, organ);
                Assert.That(Loose(organ), Is.Zero, organ);
            }

            // The eyes and the mouth went with the head.
            Assert.That(_zones.IsMissing(Zones, BodyZone.LeftEye));
            Assert.That(_zones.IsMissing(Zones, BodyZone.Mouth));

            // Everything in the head fell out, the brain took the mind with it.
            Assert.That(SEntMan.Deleted(brain), Is.False);
            Assert.That(_container.IsEntityInContainer(brain), Is.False);
            Assert.That(SEntMan.GetComponent<MindContainerComponent>(brain).Mind, Is.EqualTo(mind));

            foreach (var organ in new[] { "Brain", "EarLeft", "EarRight", "EyeLeft", "EyeRight", "Tongue", "MusclesFace", "VesselsHead", "SkinFace" })
            {
                Assert.That(Loose(organ), Is.EqualTo(1), organ);
            }

            Assert.That(Loose("Head"), Is.Zero, "the head itself bursts");

            // 47 damage would leave a normal human standing, without a head they are dead for good.
            Assert.That(_mobState.IsDead(_victim));
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(_victim));
        });
    }

    [Test]
    public async Task HeterochromiaColorsTheRightEye()
    {
        await SpawnVictim();
        await Server.WaitPost(() => _visualBody.ApplyProfile(_victim, new OrganProfileData
        {
            EyeColor = Color.Red,
            RightEyeColor = Color.Green,
            SkinColor = Color.Blue,
        }));
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var left = SEntMan.GetComponent<VisualOrganComponent>(Organs(_victim, "EyeLeft").Single());
            var right = SEntMan.GetComponent<VisualOrganComponent>(Organs(_victim, "EyeRight").Single());
            Assert.That(left.Data.Color, Is.EqualTo(Color.Red));
            Assert.That(right.Data.Color, Is.EqualTo(Color.Green));
        });

        // Back to one color for both.
        await Server.WaitPost(() => _visualBody.ApplyProfile(_victim, new OrganProfileData
        {
            EyeColor = Color.Red,
            SkinColor = Color.Blue,
        }));
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            var right = SEntMan.GetComponent<VisualOrganComponent>(Organs(_victim, "EyeRight").Single());
            Assert.That(right.Data.Color, Is.EqualTo(Color.Red));
        });
    }

    [Test]
    public async Task GibbingLeavesEveryPartOfTheBody()
    {
        await SpawnVictim();
        await Server.WaitPost(() => SEntMan.System<GibbingSystem>().Gib(_victim));
        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(_victim));
            foreach (var organ in new[]
                     {
                         "Brain", "EyeLeft", "EyeRight", "Heart", "LungLeft", "KidneyLeft", "KidneyRight", "Spleen",
                         "BoneSkull", "BoneRibs", "BoneFemurLeft", "BoneHandRight", "MusclesFace", "MusclesThighLeft",
                         "VesselsChest", "VesselsShinRight", "Gonads", "Genitals", "Tongue", "EarLeft", "EarRight", "BoneJaw", "VesselsFootLeft",
                         "SkinFace", "SkinChest", "SkinAbdomen", "SkinHandLeft", "SkinFootRight",
                     })
            {
                Assert.That(Loose(organ), Is.EqualTo(1), organ);
            }

            // The head and the limbs are torn to their layers: no pieces, no empty shells.
            AssertGone("Head", "UpperArmLeft", "UpperArmRight", "ArmLeft", "ArmRight", "HandLeft", "HandRight",
                "ThighLeft", "ThighRight", "LegLeft", "LegRight", "FootLeft", "FootRight");
        });
    }

    [Test]
    public async Task SlashesAndBurnsTearTheSkinOff()
    {
        await SpawnVictim();

        // 15 HP skin: holds on until a slash takes the last of it.
        await Hit(BodyZone.Chest, "Slash", 14);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs(_victim, "SkinChest"), Has.Count.EqualTo(1));
            Assert.That(Zones.Skinless, Is.Empty);
        });

        await Hit(BodyZone.Chest, "Slash", 1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs(_victim, "SkinChest"), Is.Empty);
            Assert.That(Loose("SkinChest"), Is.EqualTo(1));
            Assert.That(Zones.Skinless, Is.EquivalentTo(new[] { BodyZone.Chest }));
            Assert.That(SEntMan.GetComponent<Content.Shared.Body.Components.BloodstreamComponent>(_victim).BleedAmount, Is.GreaterThan(0f));
        });

        // Burns wear the skin down the same way.
        await Hit(BodyZone.LeftThigh, "Heat", 14);
        await Server.WaitAssertion(() => Assert.That(Organs(_victim, "SkinThighLeft"), Has.Count.EqualTo(1)));

        await Hit(BodyZone.LeftThigh, "Heat", 1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs(_victim, "SkinThighLeft"), Is.Empty);
            Assert.That(Zones.Skinless, Does.Contain(BodyZone.LeftThigh));
        });

        await Rejuvenate();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.Skinless, Is.Empty);
            Assert.That(Organs(_victim, "SkinChest"), Has.Count.EqualTo(1));
            Assert.That(Organs(_victim, "SkinThighLeft"), Has.Count.EqualTo(1));
            Assert.That(HealthOf("SkinChest").Damage, Is.Zero);
        });
    }

    [Test]
    public async Task SkinlessZoneTakesMoreDamage()
    {
        await SpawnVictim();
        await Server.WaitPost(() => _zones.RemoveSkin((_victim, Zones), BodyZone.LeftShin));
        await RunTicks(1);

        await Hit(BodyZone.LeftShin, "Blunt", 2);
        await Hit(BodyZone.RightShin, "Blunt", 2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.GetDamage(Zones, BodyZone.LeftShin), Is.EqualTo(FixedPoint2.New(2.5f)), "no skin: x1.25");
            Assert.That(_zones.GetDamage(Zones, BodyZone.RightShin), Is.EqualTo(FixedPoint2.New(2)));
        });
    }

    [Test]
    [TestCase("KitchenKnife")]
    [TestCase("MedievalKatana")]
    public async Task SharpThingsCutTheSkinOff(string tool)
    {
        await SpawnVictim();
        Target = SEntMan.GetNetEntity(_victim);
        await Server.WaitPost(() => _zones.SetTarget(SPlayer, BodyZone.Head));
        // Let the client see the victim, the interaction is sent from there.
        await RunTicks(5);

        await InteractUsing(tool);
        await RunSeconds(6);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs(_victim, "SkinFace"), Is.Empty);
            Assert.That(Loose("SkinFace"), Is.EqualTo(1));
            Assert.That(Zones.Skinless, Is.EquivalentTo(new[] { BodyZone.Head }));
            Assert.That(Organs(_victim, "SkinChest"), Has.Count.EqualTo(1), "only the aimed zone");
        });

        // Nothing left to cut there.
        await InteractUsing(tool);
        await RunSeconds(6);
        await Server.WaitAssertion(() => Assert.That(Loose("SkinFace"), Is.EqualTo(1)));
    }

    [Test]
    public async Task LosingEyesBlursThenBlinds()
    {
        await SpawnVictim();

        // 3 HP eye: destroyed after 6 damage.
        await Hit(BodyZone.LeftEye, "Piercing", 6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsMissing(Zones, BodyZone.LeftEye));
            Assert.That(_zones.IsMissing(Zones, BodyZone.Head), Is.False);
            Assert.That(Organs(_victim, "EyeLeft"), Is.Empty);
            Assert.That(Loose("EyeLeft"), Is.EqualTo(1), "the eye is knocked out");
            Assert.That(Organs(_victim, "EyeRight"), Has.Count.EqualTo(1));
            Assert.That(SEntMan.GetComponent<BlindableComponent>(_victim).IsBlind, Is.False);
            Assert.That(SEntMan.HasComponent<BlurryVisionComponent>(_victim), "one eye sees blurry");

            // The eye's damage moves to the head.
            Assert.That(_zones.GetDamage(Zones, BodyZone.Head), Is.EqualTo(FixedPoint2.New(6)));
        });

        await Hit(BodyZone.RightEye, "Piercing", 6);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BlindableComponent>(_victim).IsBlind, "no eyes, no sight"));

        await Rejuvenate();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs(_victim, "EyeLeft"), Has.Count.EqualTo(1));
            Assert.That(Organs(_victim, "EyeRight"), Has.Count.EqualTo(1));
            Assert.That(SEntMan.GetComponent<BlindableComponent>(_victim).IsBlind, Is.False);
        });
    }

    [Test]
    public async Task EyesHaveTheirOwnLayers()
    {
        await SpawnVictim();
        await Server.WaitPost(() => _visualBody.ApplyProfile(_victim, new OrganProfileData
        {
            EyeColor = Color.Red,
            SkinColor = Color.Blue,
        }));
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var left = SEntMan.GetComponent<VisualOrganComponent>(Organs(_victim, "EyeLeft").Single());
            var right = SEntMan.GetComponent<VisualOrganComponent>(Organs(_victim, "EyeRight").Single());
            Assert.That(left.Layer, Is.EqualTo(HumanoidVisualLayers.Eyes));
            Assert.That(right.Layer, Is.EqualTo(EternalWarHumanoidLayers.RightEye));
            Assert.That(left.Data.Color, Is.EqualTo(Color.Red));
            Assert.That(right.Data.Color, Is.EqualTo(Color.Red), "the right eye takes the eye color, not the skin");
        });

        // On the client each eye is drawn on its own layer.
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CEntMan.GetEntity(SEntMan.GetNetEntity(_victim)));
            var spriteSys = CEntMan.System<SpriteSystem>();
            Assert.That(spriteSys.LayerMapTryGet((sprite.Owner, sprite), HumanoidVisualLayers.Eyes, out var leftIndex, false));
            Assert.That(spriteSys.LayerMapTryGet((sprite.Owner, sprite), EternalWarHumanoidLayers.RightEye, out var rightIndex, false));
            Assert.That(sprite[leftIndex].RsiState.Name, Is.EqualTo("eye_left"));
            Assert.That(sprite[rightIndex].RsiState.Name, Is.EqualTo("eye_right"));
        });

        // Losing the left eye leaves the right one on the face.
        await Hit(BodyZone.LeftEye, "Piercing", 6);
        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CEntMan.GetEntity(SEntMan.GetNetEntity(_victim)));
            var spriteSys = CEntMan.System<SpriteSystem>();
            Assert.That(spriteSys.LayerMapTryGet((sprite.Owner, sprite), HumanoidVisualLayers.Eyes, out var leftIndex, false));
            Assert.That(spriteSys.LayerMapTryGet((sprite.Owner, sprite), EternalWarHumanoidLayers.RightEye, out var rightIndex, false));
            Assert.That(sprite[leftIndex].RsiState.IsValid, Is.False, "the left eye is gone from the face");
            Assert.That(sprite[rightIndex].RsiState.Name, Is.EqualTo("eye_right"));
        });
    }

    [Test]
    public async Task MouthLosesJawAndTongue()
    {
        await SpawnVictim();

        // 15 HP jaw: broken below 7.5, knocked out at 0.
        await DamageBone(BodyZone.Mouth, 8);
        await Server.WaitAssertion(() => Assert.That(_zones.IsFractured(Zones, BodyZone.Mouth)));

        await CrushBone(BodyZone.Mouth);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsMissing(Zones, BodyZone.Mouth));
            Assert.That(Organs(_victim, "BoneJaw"), Has.Count.EqualTo(1), "the crushed jaw stays in the head");
            Assert.That(Organs(_victim, "Tongue"), Is.Empty);
            Assert.That(Loose("BoneJaw"), Is.Zero);
            Assert.That(Loose("Tongue"), Is.EqualTo(1), "the tongue falls out");
            Assert.That(Organs(_victim, "Head"), Has.Count.EqualTo(1));
            Assert.That(_mobState.IsAlive(_victim));
        });
    }

    /// <summary>
    /// A torn torso drops the head, the arms and the legs, each in one piece with everything in it, and kills for good.
    /// </summary>
    private void AssertTornApart()
    {
        foreach (var zone in new[] { BodyZone.Head, BodyZone.LeftUpperArm, BodyZone.RightUpperArm, BodyZone.LeftThigh, BodyZone.RightThigh })
        {
            Assert.That(_zones.IsMissing(Zones, zone), zone.ToString());
        }

        foreach (var part in new[] { "Head", "UpperArmLeft", "UpperArmRight", "ThighLeft", "ThighRight" })
        {
            Assert.That(Severed(part), Has.Count.EqualTo(1), part + " fell off whole");
        }

        // Whole pieces: the head keeps the brain and the eyes, the arm its hand.
        var head = SEntMan.GetComponent<OrganComponent>(Severed("Head")[0]).Body!.Value;
        Assert.That(Organs(head, "Brain"), Has.Count.EqualTo(1));
        Assert.That(Organs(head, "EyeLeft"), Has.Count.EqualTo(1));
        var arm = SEntMan.GetComponent<OrganComponent>(Severed("UpperArmLeft")[0]).Body!.Value;
        Assert.That(Organs(arm, "HandLeft"), Has.Count.EqualTo(1));

        Assert.That(_mobState.IsDead(_victim), "no head");
        Assert.That(SEntMan.HasComponent<UnrevivableComponent>(_victim));
        Assert.That(SEntMan.Deleted(_victim), Is.False, "the other half of the torso is still there");
    }

    [Test]
    public async Task ChestSpillsOrgansWithoutTearingOff()
    {
        await SpawnVictim();

        // Torn open when the ribs are crushed.
        await CrushBone(BodyZone.Chest);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.Destroyed, Does.Contain(BodyZone.Chest));
            Assert.That(_zones.IsMissing(Zones, BodyZone.Chest), Is.False, "a torso is torn open, not lost");
            Assert.That(Organs(_victim, "Torso"), Has.Count.EqualTo(1));
            foreach (var organ in new[] { "Heart", "LungLeft", "LungRight", "MusclesChest", "VesselsChest", "SkinChest" })
            {
                Assert.That(Organs(_victim, organ), Is.Empty, organ);
                Assert.That(Loose(organ), Is.EqualTo(1), organ);
            }

            // The bones stay in the torn open chest, laid bare.
            foreach (var organ in new[] { "BoneRibs", "BoneSpineThoracic" })
            {
                Assert.That(Organs(_victim, organ), Has.Count.EqualTo(1), organ);
                Assert.That(Loose(organ), Is.Zero, organ);
            }

            Assert.That(_zones.GetExposure(Zones, BodyZone.Chest), Is.EqualTo(2), "the ribs are seen");

            Assert.That(Organs(_victim, "Stomach"), Has.Count.EqualTo(1), "the stomach is in the abdomen");
            AssertTornApart();
        });
    }

    [Test]
    public async Task AbdomenSpillsItsOrgans()
    {
        await SpawnVictim();

        // Torn open when the pelvis is crushed.
        await CrushBone(BodyZone.Abdomen);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.Destroyed, Does.Contain(BodyZone.Abdomen));
            foreach (var organ in new[]
                     {
                         "Stomach", "Liver", "KidneyLeft", "KidneyRight", "Appendix", "Spleen", "Intestines",
                         "MusclesAbdomen", "VesselsAbdomen", "Gonads", "SkinAbdomen",
                     })
            {
                Assert.That(Organs(_victim, organ), Is.Empty, organ);
                Assert.That(Loose(organ), Is.EqualTo(1), organ);
            }

            foreach (var organ in new[] { "BonePelvis", "BoneSpineLumbar" })
            {
                Assert.That(Organs(_victim, organ), Has.Count.EqualTo(1), organ + " stays in the body");
            }

            Assert.That(Organs(_victim, "Heart"), Has.Count.EqualTo(1));
            Assert.That(Organs(_victim, "Genitals"), Has.Count.EqualTo(1), "the genitals are a zone of their own");
            AssertTornApart();
        });
    }

    [Test]
    public async Task UnaimedDamageIsSpread()
    {
        await SpawnVictim();
        await Server.WaitPost(() => _damageable.TryChangeDamage(_victim, Damage("Heat", 20)));
        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            // Split by zone size: chest 44/200, forearm 7/200, hand 5/200.
            Assert.That(_zones.GetDamage(Zones, BodyZone.Chest), Is.EqualTo(FixedPoint2.New(4.4f)));
            Assert.That(_zones.GetDamage(Zones, BodyZone.LeftForearm), Is.EqualTo(FixedPoint2.New(0.7f)));
            Assert.That(_zones.GetDamage(Zones, BodyZone.LeftHand), Is.EqualTo(FixedPoint2.New(0.5f)));
            var sum = Zones.Damage.Values.Aggregate(FixedPoint2.Zero, (a, b) => a + b);
            Assert.That(sum, Is.EqualTo(FixedPoint2.New(20)));
        });
    }

    [Test]
    public async Task AimedHealingTreatsZoneFirst()
    {
        await SpawnVictim();
        await Hit(BodyZone.LeftThigh, "Blunt", 10);
        await Hit(BodyZone.Chest, "Blunt", 10);

        await Server.WaitPost(() =>
        {
            _zones.SetTarget(SPlayer, BodyZone.LeftThigh);
            _damageable.TryChangeDamage(_victim, Damage("Blunt", -8), origin: SPlayer);
        });
        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.GetDamage(Zones, BodyZone.LeftThigh), Is.EqualTo(FixedPoint2.New(2)));
            Assert.That(_zones.GetDamage(Zones, BodyZone.Chest), Is.EqualTo(FixedPoint2.New(10)));
        });
    }

    [Test]
    public async Task ArmorProtectsOnlyCoveredZones()
    {
        await SpawnVictim();
        await Server.WaitPost(() =>
        {
            var vest = SEntMan.SpawnAtPosition("ClothingOuterArmorBasic", SEntMan.GetCoordinates(TargetCoords));
            Assert.That(_inventory.TryEquip(_victim, vest, "outerClothing", force: true));
            var helmet = SEntMan.SpawnAtPosition("ClothingHeadHelmetSwat", SEntMan.GetCoordinates(TargetCoords));
            Assert.That(_inventory.TryEquip(_victim, helmet, "head", force: true));
        });

        // Vest: 0.7 blunt on the torso only.
        await Hit(BodyZone.Chest, "Blunt", 10);
        await Hit(BodyZone.LeftThigh, "Blunt", 10);
        // Helmet: 0.8 blunt on the head, eyes and mouth.
        await Hit(BodyZone.Head, "Blunt", 10);
        await Hit(BodyZone.Mouth, "Blunt", 5);
        await Hit(BodyZone.RightUpperArm, "Blunt", 6);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.GetDamage(Zones, BodyZone.Chest), Is.EqualTo(FixedPoint2.New(7)));
            Assert.That(_zones.GetDamage(Zones, BodyZone.LeftThigh), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(_zones.GetDamage(Zones, BodyZone.Head), Is.EqualTo(FixedPoint2.New(8)));
            Assert.That(_zones.GetDamage(Zones, BodyZone.Mouth), Is.EqualTo(FixedPoint2.New(4)));
            Assert.That(_zones.GetDamage(Zones, BodyZone.RightUpperArm), Is.EqualTo(FixedPoint2.New(6)));
        });
    }

    [Test]
    public async Task RealPunchHitsAimedZone()
    {
        await SpawnVictim();
        Target = SEntMan.GetNetEntity(_victim);
        await Server.WaitPost(() => _zones.SetTarget(SPlayer, BodyZone.Head));

        await AttemptLightAttack(Target);
        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.GetDamage(Zones, BodyZone.Head), Is.GreaterThan(FixedPoint2.Zero));
            var others = Zones.Damage.Where(z => z.Key != BodyZone.Head).Sum(z => z.Value.Float());
            Assert.That(others, Is.Zero);
        });
    }

    [Test]
    public async Task RealLaserHitsAimedZone()
    {
        await SpawnVictim();
        Target = SEntMan.GetNetEntity(_victim);
        await Server.WaitPost(() => _zones.SetTarget(SPlayer, BodyZone.LeftThigh));

        // A hitscan gun reports the gun, not the shooter, as the damage origin.
        await PlaceInHands("WeaponLaserGun");
        await RunSeconds(0.5f);
        await AttemptShoot(Target);
        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            // The beam itself lands on the thigh. The heat it leaves in the body afterwards is not aimed
            // and is spread over every zone, so the other zones get a little too.
            var thigh = _zones.GetDamage(Zones, BodyZone.LeftThigh).Float();
            var otherMax = Zones.Damage.Where(z => z.Key != BodyZone.LeftThigh).Select(z => z.Value.Float()).DefaultIfEmpty(0).Max();
            Assert.That(thigh, Is.GreaterThan(otherMax * 3));
        });
    }

    [Test]
    public async Task LosingLegsSlowsDown()
    {
        await SpawnVictim();

        float baseSpeed = 0;
        await Server.WaitPost(() => baseSpeed = Movement.CurrentWalkSpeed);

        // The shin bones crushed: the leg is gone from the knee down, the thigh stays.
        await CrushBone(BodyZone.RightShin);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsMissing(Zones, BodyZone.RightShin));
            Assert.That(_zones.IsMissing(Zones, BodyZone.RightThigh), Is.False);
            Assert.That(Organs(_victim, "FootRight"), Is.Empty);
            Assert.That(Organs(_victim, "ThighRight"), Has.Count.EqualTo(1));
            Assert.That(Movement.CurrentWalkSpeed, Is.EqualTo(baseSpeed * 0.5f).Within(0.01f));
        });

        // Losing the thigh too doesn't slow down twice for the same leg.
        await CrushBone(BodyZone.RightThigh);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsMissing(Zones, BodyZone.RightThigh));
            Assert.That(Organs(_victim, "LegRight"), Is.Empty);
            Assert.That(Movement.CurrentWalkSpeed, Is.EqualTo(baseSpeed * 0.5f).Within(0.01f));
        });
    }

    [Test]
    public async Task EveryZoneHasItsContents()
    {
        await SpawnVictim();

        await Server.WaitAssertion(() =>
        {
            // Organ category -> the organ it sits in.
            var expected = new Dictionary<string, string>
            {
                ["BoneSkull"] = "Head",
                ["BoneJaw"] = "Head",
                ["MusclesFace"] = "Head",
                ["VesselsHead"] = "Head",
                ["Brain"] = "Head",
                ["EarLeft"] = "Head",
                ["EarRight"] = "Head",
                ["Tongue"] = "Head",
                ["EyeLeft"] = "Head",
                ["EyeRight"] = "Head",
                ["BoneSpineThoracic"] = "Torso",
                ["BoneRibs"] = "Torso",
                ["Heart"] = "Torso",
                ["LungLeft"] = "Torso",
                ["LungRight"] = "Torso",
                ["MusclesChest"] = "Torso",
                ["VesselsChest"] = "Torso",
                ["BoneSpineLumbar"] = "Torso",
                ["BonePelvis"] = "Torso",
                ["Stomach"] = "Torso",
                ["Spleen"] = "Torso",
                ["Liver"] = "Torso",
                ["KidneyLeft"] = "Torso",
                ["KidneyRight"] = "Torso",
                ["Appendix"] = "Torso",
                ["Intestines"] = "Torso",
                ["MusclesAbdomen"] = "Torso",
                ["VesselsAbdomen"] = "Torso",
                ["Gonads"] = "Torso",
                ["Genitals"] = "Torso",
            };

            foreach (var side in new[] { "Left", "Right" })
            {
                expected[$"UpperArm{side}"] = "Torso";
                expected[$"BoneHumerus{side}"] = $"UpperArm{side}";
                expected[$"MusclesUpperArm{side}"] = $"UpperArm{side}";
                expected[$"VesselsUpperArm{side}"] = $"UpperArm{side}";
                expected[$"Arm{side}"] = $"UpperArm{side}";
                expected[$"BoneForearm{side}"] = $"Arm{side}";
                expected[$"MusclesForearm{side}"] = $"Arm{side}";
                expected[$"VesselsForearm{side}"] = $"Arm{side}";
                expected[$"Hand{side}"] = $"Arm{side}";
                expected[$"BoneHand{side}"] = $"Hand{side}";
                expected[$"MusclesHand{side}"] = $"Hand{side}";
                expected[$"Thigh{side}"] = "Torso";
                expected[$"BoneFemur{side}"] = $"Thigh{side}";
                expected[$"MusclesThigh{side}"] = $"Thigh{side}";
                expected[$"VesselsThigh{side}"] = $"Thigh{side}";
                expected[$"Leg{side}"] = $"Thigh{side}";
                expected[$"BoneShin{side}"] = $"Leg{side}";
                expected[$"MusclesShin{side}"] = $"Leg{side}";
                expected[$"VesselsShin{side}"] = $"Leg{side}";
                expected[$"Foot{side}"] = $"Leg{side}";
                expected[$"BoneFoot{side}"] = $"Foot{side}";
                expected[$"MusclesFoot{side}"] = $"Foot{side}";
                expected[$"VesselsFoot{side}"] = $"Foot{side}";
                expected[$"SkinUpperArm{side}"] = $"UpperArm{side}";
                expected[$"SkinForearm{side}"] = $"Arm{side}";
                expected[$"SkinHand{side}"] = $"Hand{side}";
                expected[$"SkinThigh{side}"] = $"Thigh{side}";
                expected[$"SkinShin{side}"] = $"Leg{side}";
                expected[$"SkinFoot{side}"] = $"Foot{side}";
            }

            expected["SkinFace"] = "Head";
            expected["SkinChest"] = "Torso";
            expected["SkinAbdomen"] = "Torso";

            foreach (var (category, parent) in expected)
            {
                var organs = Organs(_victim, category);
                Assert.That(organs, Has.Count.EqualTo(1), category);
                var parentUid = SEntMan.GetComponent<ChildOrganComponent>(organs[0]).Parent;
                Assert.That(parentUid, Is.Not.Null, category);
                Assert.That(SEntMan.GetComponent<OrganComponent>(parentUid!.Value).Category?.Id, Is.EqualTo(parent), category);
                if (SEntMan.TryGetComponent<BoneComponent>(organs[0], out var bone))
                    Assert.That(bone.Broken, Is.False, category);
            }

            // Upstream's single eyes, lungs and kidneys are replaced by the pairs.
            Assert.That(Organs(_victim, "Eyes"), Is.Empty);
            Assert.That(Organs(_victim, "Lungs"), Is.Empty);
            Assert.That(Organs(_victim, "Kidneys"), Is.Empty);
            Assert.That(Organs(_victim, "Ears"), Is.Empty);
        });
    }

    [Test]
    public async Task SexOrgansFollowSex()
    {
        await SpawnVictim();

        await Server.WaitPost(() => _humanoid.SetSex(_victim, Sex.Male));
        await Server.WaitAssertion(() =>
        {
            Assert.That(ProtoOf(Organs(_victim, "Gonads").Single()), Is.EqualTo("OrganTestes"));
            Assert.That(ProtoOf(Organs(_victim, "Genitals").Single()), Is.EqualTo("OrganPenis"));
        });

        await Server.WaitPost(() => _humanoid.SetSex(_victim, Sex.Female));
        await Server.WaitAssertion(() =>
        {
            Assert.That(ProtoOf(Organs(_victim, "Gonads").Single()), Is.EqualTo("OrganOvaries"));
            Assert.That(ProtoOf(Organs(_victim, "Genitals").Single()), Is.EqualTo("OrganVagina"));
            Assert.That(Zones.Destroyed, Is.Empty, "swapping organs doesn't count as losing them");
        });

        // 4 HP genital zone: destroyed after 8 damage, the pain knocks the mob down.
        await Hit(BodyZone.Genitals, "Slash", 8);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsMissing(Zones, BodyZone.Genitals));
            Assert.That(Organs(_victim, "Genitals"), Is.Empty);
            Assert.That(Loose("Genitals"), Is.EqualTo(1));
            Assert.That(SEntMan.HasComponent<StunnedComponent>(_victim));
            Assert.That(_zones.GetDamage(Zones, BodyZone.Abdomen), Is.EqualTo(FixedPoint2.New(8)));
        });

        // Let the mob get up: upstream asserts when a knocked down mob is deleted at the end of the test.
        await RunSeconds(5);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<KnockedDownComponent>(_victim), Is.False));

        // A lost organ doesn't grow back with a new sex, the rest is swapped.
        await Server.WaitPost(() => _humanoid.SetSex(_victim, Sex.Male));
        await Server.WaitAssertion(() =>
        {
            Assert.That(ProtoOf(Organs(_victim, "Gonads").Single()), Is.EqualTo("OrganTestes"));
            Assert.That(Organs(_victim, "Genitals"), Is.Empty);
        });

        await Server.WaitPost(() => _humanoid.SetSex(_victim, Sex.Unsexed));
        await Server.WaitAssertion(() => Assert.That(Organs(_victim, "Gonads"), Is.Empty));
    }

    [Test]
    public async Task OneLungMakesRunningHarder()
    {
        await SpawnVictim();

        float baseWalk = 0, baseSprint = 0;
        await Server.WaitPost(() =>
        {
            baseWalk = Movement.CurrentWalkSpeed;
            baseSprint = Movement.CurrentSprintSpeed;
            var lung = Organs(_victim, "LungLeft").Single();
            _container.Remove(lung, _container.GetContainer(_victim, BodyComponent.ContainerID));
        });
        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.MissingLungs, Is.EqualTo(1));
            Assert.That(Movement.CurrentWalkSpeed, Is.EqualTo(baseWalk).Within(0.01f));
            Assert.That(Movement.CurrentSprintSpeed, Is.EqualTo(baseSprint * 0.8f).Within(0.01f));
        });

        await Rejuvenate();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.MissingLungs, Is.Zero);
            Assert.That(Movement.CurrentSprintSpeed, Is.EqualTo(baseSprint).Within(0.01f));
        });
    }

    [Test]
    public async Task HandTearsOffWithoutTheForearm()
    {
        await SpawnVictim();

        // The hand bones crushed: the hand is gone, the forearm stays.
        await Hit(BodyZone.LeftHand, "Slash", 10);
        await CrushBone(BodyZone.LeftHand);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsMissing(Zones, BodyZone.LeftHand));
            Assert.That(_zones.IsMissing(Zones, BodyZone.LeftForearm), Is.False);
            Assert.That(Organs(_victim, "HandLeft"), Is.Empty);
            Assert.That(Organs(_victim, "BoneHandLeft"), Is.Empty, "the hand bones leave with the hand");
            Assert.That(Organs(_victim, "MusclesHandLeft"), Is.Empty);
            AssertGone("HandLeft", "BoneHandLeft");
            AssertLoose("MusclesHandLeft", "SkinHandLeft");
            Assert.That(Organs(_victim, "ArmLeft"), Has.Count.EqualTo(1));
            Assert.That(HandSys.EnumerateHands(_victim), Does.Not.Contain("left"));

            // The hand's damage moves to the forearm.
            Assert.That(_zones.GetDamage(Zones, BodyZone.LeftForearm), Is.EqualTo(FixedPoint2.New(10)));
        });

        // Hits at the missing hand land on the forearm.
        await Hit(BodyZone.LeftHand, "Blunt", 2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.GetDamage(Zones, BodyZone.LeftForearm), Is.EqualTo(FixedPoint2.New(12)));
        });
    }

    [Test]
    public async Task BrokenUpperArmCantHoldAndSplintFixesIt()
    {
        await SpawnVictim();
        Target = SEntMan.GetNetEntity(_victim);

        EntityUid held = default;
        const string leftHand = "left";
        await Server.WaitPost(() =>
        {
            held = SEntMan.SpawnAtPosition("Crowbar", SEntMan.GetCoordinates(TargetCoords));
            Assert.That(HandSys.TryPickup(_victim, held, leftHand));
        });

        // Burns don't reach bones.
        await Hit(BodyZone.LeftUpperArm, "Heat", 8);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsFractured(Zones, BodyZone.LeftUpperArm), Is.False);
            Assert.That(HealthOf("BoneHumerusLeft").Damage, Is.Zero);
        });

        // 20 HP humerus: broken below 10. The skin is whole, the fracture is closed.
        await DamageBone(BodyZone.LeftUpperArm, 11);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsFractured(Zones, BodyZone.LeftUpperArm));
            Assert.That(Zones.OpenFractures, Is.Empty, "closed");
            Assert.That(SEntMan.GetComponent<BoneComponent>(Organs(_victim, "BoneHumerusLeft")[0]).Broken);
            Assert.That(_zones.IsMissing(Zones, BodyZone.LeftUpperArm), Is.False, "broken, not torn off");

            // The crowbar fell out of the hand down the arm, and the hand is filled with the broken bone.
            Assert.That(HandSys.TryGetHeldItem(_victim, leftHand, out var inHand), Is.True);
            Assert.That(inHand, Is.Not.EqualTo(held));
            Assert.That(SEntMan.HasComponent<Content.Shared.Inventory.VirtualItem.VirtualItemComponent>(inHand));
            Assert.That(HandSys.TryPickup(_victim, held, leftHand), Is.False);
        });

        // The splint sets the bone: up to a crack, and it grows back under the splint.
        await Server.WaitPost(() =>
        {
            _zones.SetTarget(SPlayer, BodyZone.LeftUpperArm);
            Zones.SplintHealRate = 5f;
        });
        await InteractUsing("Splint");
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsFractured(Zones, BodyZone.LeftUpperArm), Is.False);
            Assert.That(SEntMan.GetComponent<BoneComponent>(Organs(_victim, "BoneHumerusLeft")[0]).Broken, Is.False);
            Assert.That(HandSys.TryGetHeldItem(_victim, leftHand, out _), Is.False, "the hand is free again");
            Assert.That(HandSys.TryPickup(_victim, held, leftHand));
        });

        await RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(HealthOf("BoneHumerusLeft").Damage, Is.Zero, "grown back together");
            Assert.That(Zones.Cracks, Is.Empty);
            Assert.That(SEntMan.GetComponent<BoneComponent>(Organs(_victim, "BoneHumerusLeft")[0]).Splinted, Is.False);
        });

        // An open fracture (no skin, torn muscles) can't be set with a splint.
        await Server.WaitPost(() =>
        {
            _zones.RemoveSkin((_victim, Zones), BodyZone.LeftUpperArm);
            var body = _container.GetContainer(_victim, BodyComponent.ContainerID);
            _container.Remove(Organs(_victim, "MusclesUpperArmLeft").Single(), body);
        });
        await DamageBone(BodyZone.LeftUpperArm, 11);
        await Server.WaitAssertion(() => Assert.That(Zones.OpenFractures, Does.Contain(BodyZone.LeftUpperArm)));

        await InteractUsing("Splint");
        await RunTicks(5);
        await Server.WaitAssertion(() => Assert.That(_zones.IsFractured(Zones, BodyZone.LeftUpperArm), "still broken"));
    }

    [Test]
    public async Task BrokenLegSlowsAndRibsOnlyHurtRunning()
    {
        await SpawnVictim();

        float baseWalk = 0, baseSprint = 0;
        await Server.WaitPost(() =>
        {
            baseWalk = Movement.CurrentWalkSpeed;
            baseSprint = Movement.CurrentSprintSpeed;
        });

        // 30 HP ribs: broken below 15.
        await DamageBone(BodyZone.Chest, 16);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsFractured(Zones, BodyZone.Chest));
            Assert.That(Movement.CurrentWalkSpeed, Is.EqualTo(baseWalk).Within(0.01f));
            Assert.That(Movement.CurrentSprintSpeed, Is.LessThan(baseSprint * 0.75f));
        });

        // 25 HP femur: a crack at 18.75 and less already hurts a bit...
        await DamageBone(BodyZone.RightThigh, 7);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.Cracks, Does.Contain(BodyZone.RightThigh));
            Assert.That(Movement.CurrentWalkSpeed, Is.EqualTo(baseWalk * 0.9f).Within(0.01f));
        });

        // ... broken below 12.5 it is much worse.
        await DamageBone(BodyZone.RightThigh, 6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsFractured(Zones, BodyZone.RightThigh));
            Assert.That(Movement.CurrentWalkSpeed, Is.EqualTo(baseWalk * 0.6f).Within(0.01f));
        });

        // Rejuvenation sets every bone.
        await Rejuvenate();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.Fractures, Is.Empty);
            Assert.That(Zones.Cracks, Is.Empty);
            Assert.That(HealthOf("BoneRibs").Damage, Is.Zero);
            Assert.That(Organs(_victim, "BoneRibs").All(b => !SEntMan.GetComponent<BoneComponent>(b).Broken));
            Assert.That(Organs(_victim, "BoneFemurRight").All(b => !SEntMan.GetComponent<BoneComponent>(b).Broken));
        });
    }

    [Test]
    public async Task SeveringBladeCutsTheLimbOffWholeAndKnifeTakesItApart()
    {
        await SpawnVictim();
        Target = SEntMan.GetNetEntity(_victim);
        var blade = await PlaceInHands("KitchenKnife");
        await Server.WaitPost(() =>
        {
            _zones.SetTarget(SPlayer, BodyZone.LeftUpperArm);
            SEntMan.AddComponent<LimbSeverComponent>(SEntMan.GetEntity(blade)).Chance = 1f;
        });
        // A weapon just taken in hand can't attack right away.
        await RunSeconds(2);

        // A 10 slash on the 8 HP upper arm: without the cut it would only lose its skin and break.
        await AttemptLightAttack(Target);
        await RunTicks(2);

        EntityUid piece = default;
        await Server.WaitAssertion(() =>
        {
            foreach (var zone in new[] { BodyZone.LeftUpperArm, BodyZone.LeftForearm, BodyZone.LeftHand })
            {
                Assert.That(_zones.IsMissing(Zones, zone), zone.ToString());
            }

            // The whole arm came off in one piece, every layer still in it, the bones whole.
            AssertPiece("UpperArmLeft", "BoneHumerusLeft", "MusclesUpperArmLeft", "VesselsUpperArmLeft", "SkinUpperArmLeft",
                "ArmLeft", "BoneForearmLeft", "MusclesForearmLeft", "VesselsForearmLeft", "SkinForearmLeft",
                "HandLeft", "BoneHandLeft", "MusclesHandLeft", "SkinHandLeft");
            Assert.That(Loose("SkinUpperArmLeft"), Is.Zero);
            Assert.That(Zones.Skinless, Is.Empty);
            Assert.That(Zones.Fractures, Is.Empty);

            // The damage of a clean cut is on the torso.
            Assert.That(Zones.Damage.Where(z => z.Value > 0).Select(z => z.Key), Is.EquivalentTo(new[] { BodyZone.Chest }));
            Assert.That(_zones.GetDamage(Zones, BodyZone.Chest), Is.GreaterThan(FixedPoint2.Zero));

            piece = SEntMan.GetComponent<OrganComponent>(Severed("UpperArmLeft")[0]).Body!.Value;
            Assert.That(SEntMan.HasComponent<SeveredPartComponent>(piece));
        });

        // Bring the flung arm back and cut it up with the knife.
        await RunSeconds(2);
        await Server.WaitPost(() => Transform.SetCoordinates(piece, SEntMan.GetCoordinates(TargetCoords)));
        await RunTicks(5);
        await Interact(piece, SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(piece));
            AssertGone("UpperArmLeft", "ArmLeft", "HandLeft");
            AssertLoose("BoneHumerusLeft", "MusclesUpperArmLeft", "VesselsUpperArmLeft", "SkinUpperArmLeft",
                "BoneForearmLeft", "MusclesForearmLeft", "VesselsForearmLeft", "SkinForearmLeft",
                "BoneHandLeft", "MusclesHandLeft", "SkinHandLeft");
            var bone = SEntMan.AllEntities<BoneComponent>()
                .Single(b => SEntMan.GetComponent<OrganComponent>(b).Category == "BoneHumerusLeft"
                            && !_container.IsEntityInContainer(b) && OnTestMap(b));
            Assert.That(bone.Comp.Broken, Is.False, "a knife doesn't break the bones");
        });
    }

    [Test]
    public async Task RemainsFallApartWhenNothingIsLeftOnTheTorso()
    {
        await SpawnVictim();
        await Server.WaitPost(() =>
        {
            foreach (var zone in new[] { BodyZone.Head, BodyZone.LeftUpperArm, BodyZone.RightUpperArm, BodyZone.LeftThigh, BodyZone.RightThigh, BodyZone.Chest })
            {
                _zones.DestroyZone((_victim, Zones), zone);
            }
        });
        await RunTicks(2);

        // The belly is still closed: there is still a body.
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(_victim), Is.False));

        await Server.WaitPost(() => _zones.DestroyZone((_victim, Zones), BodyZone.Abdomen));
        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(_victim));
            Assert.That(Loose("Genitals"), Is.EqualTo(1), "what was still on the torso falls out");
            Assert.That(Loose("Torso"), Is.Zero);
        });
    }

    /// <summary>
    /// The state of a body part's exposed layer on the client, or null if it isn't shown; and whether the skin under it shows.
    /// </summary>
    private (string? Overlay, bool SkinVisible) ClientExposure(HumanoidVisualLayers layer, EternalWarHumanoidLayers overlay)
    {
        var sprite = CEntMan.GetComponent<SpriteComponent>(CEntMan.GetEntity(SEntMan.GetNetEntity(_victim)));
        var spriteSys = CEntMan.System<SpriteSystem>();
        Assert.That(spriteSys.LayerMapTryGet((sprite.Owner, sprite), layer, out var baseIndex, false));
        Assert.That(spriteSys.LayerMapTryGet((sprite.Owner, sprite), overlay, out var overlayIndex, false));
        var shown = sprite[overlayIndex].Visible ? sprite[overlayIndex].RsiState.Name : null;
        return (shown, sprite[baseIndex].Visible);
    }

    [Test]
    public async Task BareBodyShowsMusclesThenBonesZoneByZone()
    {
        await SpawnVictim();
        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            Assert.That(ClientExposure(HumanoidVisualLayers.LArm, EternalWarHumanoidLayers.ExposedLUpperArm), Is.EqualTo(((string?) null, true)));
            Assert.That(ClientExposure(HumanoidVisualLayers.LArm, EternalWarHumanoidLayers.ExposedLForearm), Is.EqualTo(((string?) null, true)));
        });

        // No skin on the forearm: the muscles show there, the upper arm keeps its skin.
        await Server.WaitPost(() => _zones.RemoveSkin((_victim, Zones), BodyZone.LeftForearm));
        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            Assert.That(ClientExposure(HumanoidVisualLayers.LArm, EternalWarHumanoidLayers.ExposedLForearm), Is.EqualTo(((string?) "flesh_l_forearm", false)));
            Assert.That(ClientExposure(HumanoidVisualLayers.LArm, EternalWarHumanoidLayers.ExposedLUpperArm), Is.EqualTo(((string?) "skin_l_upper_arm", false)));
            Assert.That(ClientExposure(HumanoidVisualLayers.RArm, EternalWarHumanoidLayers.ExposedRForearm), Is.EqualTo(((string?) null, true)), "the other arm is whole");
        });

        // No muscles on the upper arm: its bone shows, the forearm still has its muscles.
        await Server.WaitPost(() =>
        {
            var body = _container.GetContainer(_victim, BodyComponent.ContainerID);
            foreach (var organ in Organs(_victim, "MusclesUpperArmLeft").Concat(Organs(_victim, "VesselsUpperArmLeft")))
            {
                _container.Remove(organ, body);
            }
        });
        await RunTicks(5);
        await Server.WaitAssertion(() => Assert.That(Zones.TissuesTorn, Is.EquivalentTo(new[] { BodyZone.LeftUpperArm })));
        await Client.WaitAssertion(() =>
        {
            Assert.That(ClientExposure(HumanoidVisualLayers.LArm, EternalWarHumanoidLayers.ExposedLUpperArm).Overlay, Is.EqualTo("bone_l_upper_arm"));
            Assert.That(ClientExposure(HumanoidVisualLayers.LArm, EternalWarHumanoidLayers.ExposedLForearm).Overlay, Is.EqualTo("flesh_l_forearm"));
        });

        // A skinned face loses its hair.
        await Server.WaitPost(() => _zones.RemoveSkin((_victim, Zones), BodyZone.Head));
        await RunTicks(5);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<HideableHumanoidLayersComponent>(_victim).HiddenLayers.Keys, Does.Contain(HumanoidVisualLayers.Hair)));
        await Client.WaitAssertion(() =>
            Assert.That(ClientExposure(HumanoidVisualLayers.Head, EternalWarHumanoidLayers.ExposedHead).Overlay, Does.StartWith("flesh_head_")));

        // The chest without its muscles shows the ribs above a whole belly.
        await Server.WaitPost(() =>
        {
            var body = _container.GetContainer(_victim, BodyComponent.ContainerID);
            _zones.RemoveSkin((_victim, Zones), BodyZone.Chest);
            foreach (var organ in Organs(_victim, "MusclesChest").Concat(Organs(_victim, "VesselsChest")))
            {
                _container.Remove(organ, body);
            }
        });
        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            var chest = ClientExposure(HumanoidVisualLayers.Chest, EternalWarHumanoidLayers.ExposedChest);
            Assert.That(chest.Overlay, Does.StartWith("bone_chest_"));
            Assert.That(chest.SkinVisible, Is.False);
            Assert.That(ClientExposure(HumanoidVisualLayers.Chest, EternalWarHumanoidLayers.ExposedAbdomen).Overlay, Does.StartWith("skin_abdomen_"));
        });

        // Torn open, the chest spills everything else but keeps its ribs: they are still drawn.
        await Server.WaitPost(() => _zones.DestroyZone((_victim, Zones), BodyZone.Chest));
        await RunTicks(5);
        await Server.WaitAssertion(() => Assert.That(Organs(_victim, "BoneRibs"), Has.Count.EqualTo(1)));
        await Client.WaitAssertion(() =>
            Assert.That(ClientExposure(HumanoidVisualLayers.Chest, EternalWarHumanoidLayers.ExposedChest).Overlay, Does.StartWith("bone_chest_")));

        // Once the ribs are pulled out there is nothing left to draw there.
        await Server.WaitPost(() => _zones.ExtractSkeleton((_victim, Zones)));
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs(_victim, "BoneRibs"), Is.Empty);
            Assert.That(Zones.BonesLost, Does.Contain(BodyZone.Chest));
        });
        await Client.WaitAssertion(() =>
        {
            Assert.That(ClientExposure(HumanoidVisualLayers.Chest, EternalWarHumanoidLayers.ExposedChest), Is.EqualTo(((string?) null, false)),
                "no ribs drawn when they are gone");
            Assert.That(ClientExposure(HumanoidVisualLayers.Chest, EternalWarHumanoidLayers.ExposedAbdomen).Overlay, Does.StartWith("skin_abdomen_"));
        });

        // Rejuvenation: whole again.
        await Rejuvenate();
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.TissuesTorn, Is.Empty);
            Assert.That(Zones.BonesLost, Is.Empty);
            Assert.That(SEntMan.GetComponent<HideableHumanoidLayersComponent>(_victim).HiddenLayers.Keys, Does.Not.Contain(HumanoidVisualLayers.Hair));
        });
        await Client.WaitAssertion(() =>
        {
            Assert.That(ClientExposure(HumanoidVisualLayers.LArm, EternalWarHumanoidLayers.ExposedLUpperArm), Is.EqualTo(((string?) null, true)));
            Assert.That(ClientExposure(HumanoidVisualLayers.LArm, EternalWarHumanoidLayers.ExposedLForearm), Is.EqualTo(((string?) null, true)));
            Assert.That(ClientExposure(HumanoidVisualLayers.Chest, EternalWarHumanoidLayers.ExposedChest), Is.EqualTo(((string?) null, true)));
            Assert.That(ClientExposure(HumanoidVisualLayers.Chest, EternalWarHumanoidLayers.ExposedAbdomen), Is.EqualTo(((string?) null, true)));
            Assert.That(ClientExposure(HumanoidVisualLayers.Head, EternalWarHumanoidLayers.ExposedHead), Is.EqualTo(((string?) null, true)));
        });
    }

    [Test]
    public async Task CrushedOrgansStopWorking()
    {
        await SpawnVictim();

        // A crushed eye stays in its socket but sees nothing: one eye left, the world is blurry.
        await Server.WaitPost(() => _zones.DamagePiece((_victim, Zones), Organs(_victim, "EyeLeft").Single(), 100));
        await RunTicks(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.FailedOrgans, Does.Contain(new ProtoId<OrganCategoryPrototype>("EyeLeft")));
            Assert.That(Organs(_victim, "EyeLeft"), Has.Count.EqualTo(1), "still there");
            Assert.That(_zones.IsEyeBlind(Zones, BodyZone.LeftEye));
            Assert.That(_zones.IsEyeBlind(Zones, BodyZone.RightEye), Is.False);
        });

        // A crushed lung counts as a lost one.
        await Server.WaitPost(() => _zones.DamagePiece((_victim, Zones), Organs(_victim, "LungLeft").Single(), 100));
        await RunTicks(2);
        await Server.WaitAssertion(() => Assert.That(Zones.MissingLungs, Is.EqualTo(1)));

        // A crushed heart pumps nothing: the brain runs out of oxygen.
        await Server.WaitPost(() => _zones.DamagePiece((_victim, Zones), Organs(_victim, "Heart").Single(), 100));
        await RunSeconds(10);
        await Server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            Assert.That(_damageable.GetAllDamage(_victim).DamageDict.GetValueOrDefault("Asphyxiation"), Is.GreaterThan(FixedPoint2.Zero));
#pragma warning restore CS0618
        });

        // Without a brain there is nobody left.
        await Server.WaitPost(() => _zones.DamagePiece((_victim, Zones), Organs(_victim, "Brain").Single(), 1000));
        await RunTicks(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_mobState.IsDead(_victim));
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(_victim));
        });

        // Rejuvenation brings it all back.
        await Rejuvenate();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Zones.FailedOrgans, Is.Empty);
            Assert.That(Zones.MissingLungs, Is.Zero);
            Assert.That(HealthOf("Heart").Failed, Is.False);
        });
    }
}
