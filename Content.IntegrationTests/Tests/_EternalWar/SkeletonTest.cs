#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._EternalWar.BodyZones;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Input;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// Bones staying in a destroyed zone, pulled out of a body laid bare as a skeleton, and taken apart again.
/// </summary>
[TestOf(typeof(BodyZonesSystem))]
public sealed class SkeletonTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [SidedDependency(Side.Server)] private readonly BodyZonesSystem _zones = default!;
    [SidedDependency(Side.Server)] private readonly DamageableSystem _damageable = default!;
    [SidedDependency(Side.Server)] private readonly MobStateSystem _mobState = default!;
    [SidedDependency(Side.Server)] private readonly SharedContainerSystem _container = default!;

    private EntityUid _victim;

    private BodyZonesComponent Zones => SEntMan.GetComponent<BodyZonesComponent>(_victim);

    private async Task SpawnVictim(float boneDropChance = 0f)
    {
        await AddAtmosphere();
        await Server.WaitPost(() =>
        {
            _victim = SEntMan.SpawnAtPosition("MobHuman", SEntMan.GetCoordinates(TargetCoords));
            SEntMan.GetComponent<BodyZonesComponent>(_victim).BoneDropChance = boneDropChance;
        });
        await RunTicks(1);
    }

    private List<EntityUid> Organs(EntityUid body, string category)
    {
        return _container.GetContainer(body, BodyComponent.ContainerID).ContainedEntities
            .Where(o => SEntMan.GetComponent<OrganComponent>(o).Category == category)
            .ToList();
    }

    /// <summary>
    /// Bones of the category on their own, on the floor or in a hand: not in a body, not in an assembly.
    /// </summary>
    private int Loose(string category)
    {
        return SEntMan.AllEntities<OrganComponent>()
            .Count(o => SEntMan.GetComponent<OrganComponent>(o).Category == category
                        && (!_container.TryGetContainingContainer(o.Owner, out var container)
                            || container.ID != BodyComponent.ContainerID && container.ID != BoneAssemblyComponent.ContainerId)
                        && SEntMan.GetComponent<TransformComponent>(o).MapID == MapId);
    }

    /// <summary>
    /// Assemblies of this prototype on the test map, held or on the floor.
    /// </summary>
    private List<EntityUid> Assemblies(string proto)
    {
        return SEntMan.AllEntities<BoneAssemblyComponent>()
            .Where(a => SEntMan.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == proto
                        && SEntMan.GetComponent<TransformComponent>(a).MapID == MapId)
            .Select(a => a.Owner)
            .ToList();
    }

    private List<string> BonesIn(EntityUid assembly)
    {
        return _container.GetContainer(assembly, BoneAssemblyComponent.ContainerId).ContainedEntities
            .Select(b => SEntMan.GetComponent<OrganComponent>(b).Category!.Value.Id)
            .ToList();
    }

    /// <summary>
    /// Takes the skin and the muscles off the zones, leaving their bones bare.
    /// </summary>
    private async Task LayBare(params BodyZone[] zones)
    {
        await Server.WaitPost(() =>
        {
            var body = _container.GetContainer(_victim, BodyComponent.ContainerID);
            foreach (var zone in zones)
            {
                _zones.RemoveSkin((_victim, Zones), zone);
                foreach (var proto in Zones.Zones[zone].Tissues)
                {
                    foreach (var organ in Organs(_victim, proto.Id))
                    {
                        _container.Remove(organ, body);
                    }
                }
            }
        });
        await RunTicks(2);
    }

    /// <summary>
    /// The take-apart key (middle mouse button) pressed on the entity.
    /// </summary>
    private async Task TakeApart(EntityUid target)
    {
        // Let the client learn about a piece that was just made.
        await RunTicks(5);
        var net = SEntMan.GetNetEntity(target);
        await SetKey(EternalWarKeyFunctions.TakeApart, BoundKeyState.Down, cursorEntity: net);
        await SetKey(EternalWarKeyFunctions.TakeApart, BoundKeyState.Up, cursorEntity: net);
        await RunTicks(2);
    }

    private EntityUid SpawnSpine()
    {
        var coords = SEntMan.GetCoordinates(TargetCoords);
        var bones = new List<EntityUid>
        {
            SEntMan.SpawnAtPosition("BoneSpineThoracic", coords),
            SEntMan.SpawnAtPosition("BoneSpineLumbar", coords),
        };

        return _zones.Assemble(bones, coords).Single();
    }

    [Test]
    public async Task DestroyedZoneKeepsItsBonesUnlessKnockedOut()
    {
        // Never knocked out: the ribs and spine stay in the torn open chest, the forearm bones are gone with it.
        await SpawnVictim();
        await Server.WaitPost(() =>
        {
            _zones.DestroyZone((_victim, Zones), BodyZone.Chest);
            _zones.DestroyZone((_victim, Zones), BodyZone.LeftForearm);
        });
        await RunTicks(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs(_victim, "BoneRibs"), Has.Count.EqualTo(1));
            Assert.That(Organs(_victim, "BoneSpineThoracic"), Has.Count.EqualTo(1));
            Assert.That(Loose("BoneRibs"), Is.Zero);
            Assert.That(Loose("BoneForearmLeft"), Is.Zero);
            Assert.That(Organs(_victim, "BoneForearmLeft"), Is.Empty);
        });
    }

    [Test]
    public async Task DestroyedZoneSometimesKnocksItsBonesOut()
    {
        // Always knocked out: the bones fall out broken.
        await SpawnVictim(boneDropChance: 1f);
        await Server.WaitPost(() => _zones.DestroyZone((_victim, Zones), BodyZone.Abdomen));
        await RunTicks(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Organs(_victim, "BonePelvis"), Is.Empty);
            Assert.That(Loose("BonePelvis"), Is.EqualTo(1));
            Assert.That(Loose("BoneSpineLumbar"), Is.EqualTo(1));
            Assert.That(Zones.BonesLost, Does.Contain(BodyZone.Abdomen));
        });
    }

    [Test]
    public async Task WholeSkeletonComesOutAndApart()
    {
        await SpawnVictim();

        // Nothing laid bare: nothing to pull out.
        await TakeApart(_victim);
        await RunSeconds(4);
        await Server.WaitAssertion(() => Assert.That(Assemblies("BoneAssemblySkeleton"), Is.Empty));

        await LayBare(BodyZone.Head, BodyZone.Chest, BodyZone.Abdomen);
        await TakeApart(_victim);
        await RunSeconds(4);

        EntityUid skeleton = default;
        await Server.WaitAssertion(() =>
        {
            var skeletons = Assemblies("BoneAssemblySkeleton");
            Assert.That(skeletons, Has.Count.EqualTo(1));
            skeleton = skeletons[0];
            Assert.That(BonesIn(skeleton), Is.EquivalentTo(new[]
                { "BoneSkull", "BoneJaw", "BoneSpineThoracic", "BoneSpineLumbar", "BoneRibs", "BonePelvis" }));
            Assert.That(HandSys.IsHolding(SPlayer, skeleton), "pulled out into the hand");

            foreach (var bone in new[] { "BoneSkull", "BoneJaw", "BoneRibs", "BonePelvis", "BoneSpineThoracic", "BoneSpineLumbar" })
            {
                Assert.That(Organs(_victim, bone), Is.Empty, bone);
            }

            Assert.That(_mobState.IsDead(_victim), "no skull, no spine");
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(_victim));
            Assert.That(Organs(_victim, "BoneFemurLeft"), Has.Count.EqualTo(1), "the limbs keep their bones");
        });

        // The skull with the jaw comes off the skeleton into the other hand, the rest stays in this one.
        await TakeApart(skeleton);
        EntityUid trunk = default, skull = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(skeleton));
            trunk = Assemblies("BoneAssemblyTrunk").Single();
            skull = Assemblies("BoneAssemblySkullJaw").Single();
            Assert.That(HandSys.IsHolding(SPlayer, trunk));
            Assert.That(HandSys.IsHolding(SPlayer, skull));
        });

        // The jaw off the skull.
        await TakeApart(skull);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Assemblies("BoneAssemblySkullJaw"), Is.Empty);
            Assert.That(Loose("BoneSkull"), Is.EqualTo(1));
            Assert.That(Loose("BoneJaw"), Is.EqualTo(1));
        });

        // The ribs off first, then the pelvis: a whole spine is left.
        await TakeApart(trunk);
        EntityUid pelvisSpine = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Loose("BoneRibs"), Is.EqualTo(1));
            pelvisSpine = Assemblies("BoneAssemblyPelvisSpine").Single();
        });

        await TakeApart(pelvisSpine);
        EntityUid spine = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Loose("BonePelvis"), Is.EqualTo(1));
            spine = Assemblies("BoneAssemblySpine").Single();
        });

        // A whole spine can't be pulled apart by hand.
        await TakeApart(spine);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(spine), Is.False);
            Assert.That(Loose("BoneSpineThoracic"), Is.Zero);
        });
    }

    [Test]
    public async Task SpineIsSawnInTwo()
    {
        await AddAtmosphere();
        EntityUid spine = default;
        await Server.WaitPost(() => spine = SpawnSpine());
        await PlaceInHands("Saw");

        await TakeApart(spine);
        await RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(spine));
            Assert.That(Loose("BoneSpineThoracic"), Is.EqualTo(1));
            Assert.That(Loose("BoneSpineLumbar"), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CrushingBlowsBreakTheSpine()
    {
        await AddAtmosphere();
        EntityUid spine = default;
        var blunt = new DamageSpecifier(ProtoMan.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(10));

        // A blow that cracks it.
        await Server.WaitPost(() =>
        {
            spine = SpawnSpine();
            var assembly = SEntMan.GetComponent<BoneAssemblyComponent>(spine);
            assembly.SplitChance = 0f;
            assembly.BreakChance = 1f;
            assembly.BrokenSplitChance = 0f;
            _damageable.TryChangeDamage(spine, blunt);
        });
        await RunTicks(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BoneAssemblyComponent>(spine).Broken);
            foreach (var bone in _container.GetContainer(spine, BoneAssemblyComponent.ContainerId).ContainedEntities)
            {
                Assert.That(SEntMan.GetComponent<BoneComponent>(bone).Broken);
            }
        });

        // A weak blow does nothing, a strong one breaks the cracked spine in two.
        await Server.WaitPost(() =>
        {
            SEntMan.GetComponent<BoneAssemblyComponent>(spine).BrokenSplitChance = 1f;
            _damageable.TryChangeDamage(spine, new DamageSpecifier(ProtoMan.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(2)));
        });
        await RunTicks(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(spine), Is.False));

        await Server.WaitPost(() => _damageable.TryChangeDamage(spine, blunt));
        await RunTicks(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(spine));
            Assert.That(Loose("BoneSpineThoracic"), Is.EqualTo(1));
            Assert.That(Loose("BoneSpineLumbar"), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RemainsLeaveTheirSkeleton()
    {
        await SpawnVictim();

        // The chest torn open tears the head and the limbs off; then the abdomen: nothing is left on the torso.
        await Server.WaitPost(() => _zones.DestroyZone((_victim, Zones), BodyZone.Chest));
        await RunTicks(2);
        await Server.WaitPost(() => _zones.DestroyZone((_victim, Zones), BodyZone.Abdomen));
        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(_victim), "the remains fell apart");
            var trunk = Assemblies("BoneAssemblyTrunk");
            Assert.That(trunk, Has.Count.EqualTo(1), "the bones of the torso stay together");
            Assert.That(BonesIn(trunk[0]), Is.EquivalentTo(new[] { "BoneSpineThoracic", "BoneSpineLumbar", "BoneRibs", "BonePelvis" }));
        });
    }
}
