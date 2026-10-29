#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._EternalWar.BodyZones;
using Content.Server._EternalWar.Vision;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Vision;
using Content.Shared.Body;
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.MouseRotator;
using Content.Shared.Movement.Components;
using Content.Client._EternalWar.Vision;
using Robust.Client.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// A human sees in a 150 degree cone where the mouse is, and everything next to it; mobs and items outside the cone
/// are hidden on its client. Losing an eye narrows the cone on that side.
/// </summary>
[TestOf(typeof(FieldOfViewSystem))]
public sealed class FieldOfViewTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [SidedDependency(Side.Server)] private readonly FieldOfViewSystem _fov = default!;
    [SidedDependency(Side.Server)] private readonly BodyZonesSystem _zones = default!;
    [SidedDependency(Side.Server)] private readonly SharedCombatModeSystem _combat = default!;
    [SidedDependency(Side.Server)] private readonly SharedContainerSystem _container = default!;
    [SidedDependency(Side.Server)] private readonly SharedHandsSystem _hands = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly SharedAudioSystem _audio = default!;
    [SidedDependency(Side.Client)] private readonly SoundMarkSystem _marks = default!;

    private Entity<FieldOfViewComponent> Viewer => (SPlayer, SEntMan.GetComponent<FieldOfViewComponent>(SPlayer));

    private void Face(Vector2 direction)
    {
        _transform.SetWorldRotation(SPlayer, direction.ToWorldAngle());
    }

    /// <summary>
    /// A point this far from the player, turned this many degrees to its left from east (to the right if negative).
    /// </summary>
    private Vector2 Point(float distance, double degrees)
    {
        return _transform.GetWorldPosition(SPlayer) + Angle.FromDegrees(degrees).RotateVec(new Vector2(distance, 0));
    }

    private bool Sees(Vector2 point)
    {
        var viewer = Viewer;
        return _fov.InView(viewer, _transform.GetWorldPosition(SPlayer), _fov.GetFacing(SPlayer), _fov.GetHalfAngles(viewer), point);
    }

    [Test]
    public async Task HumanFacesTheMouseEvenOutOfCombat()
    {
        await Server.WaitAssertion(() =>
        {
            _combat.SetInCombatMode(SPlayer, true);
            _combat.SetInCombatMode(SPlayer, false);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(SEntMan.TryGetComponent<MouseRotatorComponent>(SPlayer, out var rotator));
                Assert.That(rotator!.Simple4DirMode, Is.False, "It should face the mouse, not the nearest side.");
                Assert.That(SEntMan.HasComponent<NoRotateOnMoveComponent>(SPlayer), "Walking should not turn it.");
                Assert.That(SEntMan.GetComponent<CombatModeComponent>(SPlayer).CanDisarm, Is.True,
                    "The human's combat mode should keep what it inherits.");
            }
        });
    }

    [Test]
    public async Task SeesIn150DegreesAndAllAroundUpClose()
    {
        await Server.WaitAssertion(() =>
        {
            Face(new Vector2(1, 0));
            Assert.That(_fov.GetFacing(SPlayer).X, Is.EqualTo(1f).Within(0.01f));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Sees(Point(5, 0)), "Straight ahead.");
                Assert.That(Sees(Point(5, 70)), "70 degrees to the left.");
                Assert.That(Sees(Point(5, -70)), "70 degrees to the right.");
                Assert.That(Sees(Point(5, 80)), Is.False, "80 degrees to the left is out of the cone.");
                Assert.That(Sees(Point(5, -80)), Is.False, "80 degrees to the right is out of the cone.");
                Assert.That(Sees(Point(5, 180)), Is.False, "Behind.");
                Assert.That(Sees(Point(1, 180)), "Right behind the back is felt.");
            }
        });
    }

    [Test]
    public async Task LostEyeNarrowsItsSide()
    {
        await Server.WaitAssertion(() =>
        {
            var body = _container.GetContainer(SPlayer, BodyComponent.ContainerID);
            var eye = body.ContainedEntities.Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == "EyeLeft");
            _container.Remove(eye, body);
        });
        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_zones.IsEyeBlind(SEntMan.GetComponent<BodyZonesComponent>(SPlayer), BodyZone.LeftEye));
            Face(new Vector2(1, 0));

            var (left, right) = _fov.GetHalfAngles(Viewer);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(left.Degrees, Is.EqualTo(30).Within(0.1));
                Assert.That(right.Degrees, Is.EqualTo(75).Within(0.1));
                Assert.That(Sees(Point(5, 50)), Is.False, "50 degrees to the blind side is out of the cone.");
                Assert.That(Sees(Point(5, -50)), "50 degrees to the seeing side.");
            }
        });
    }

    [Test]
    public async Task ItemsAndMobsBehindAreHiddenOnTheClient()
    {
        await AddAtmosphere();

        EntityUid ahead = default, behind = default, close = default, mob = default;
        await Server.WaitPost(() =>
        {
            Face(new Vector2(1, 0));
            var map = _transform.GetMapId(SPlayer);
            ahead = SEntMan.SpawnEntity("Wrench", new MapCoordinates(Point(3, 0), map));
            behind = SEntMan.SpawnEntity("Wrench", new MapCoordinates(Point(3, 180), map));
            close = SEntMan.SpawnEntity("Wrench", new MapCoordinates(Point(1, 180), map));
            mob = SEntMan.SpawnEntity("MobHuman", new MapCoordinates(Point(3, 170), map));
        });
        await RunTicks(5);

        bool Visible(EntityUid uid) => CEntMan.GetComponent<SpriteComponent>(CEntMan.GetEntity(SEntMan.GetNetEntity(uid))).Visible;

        await Client.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Visible(ahead));
                Assert.That(Visible(behind), Is.False);
                Assert.That(Visible(close), "Right behind the back is felt.");
                Assert.That(Visible(mob), Is.False);
            }
        });

        // Turning around shows what was behind and hides what was ahead.
        await Server.WaitPost(() => Face(new Vector2(-1, 0)));
        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Visible(ahead), Is.False);
                Assert.That(Visible(behind));
                Assert.That(Visible(mob));
            }
        });
    }

    private static readonly SoundSpecifier Clank = new SoundPathSpecifier("/Audio/Items/wrench_drop.ogg");

    private void Sound(Vector2 position, AudioParams? audioParams = null)
    {
        var map = _transform.GetMapId(SPlayer);
        _audio.PlayPvs(Clank, _transform.ToCoordinates(new MapCoordinates(position, map)), audioParams);
    }

    private bool MarkedNear(Vector2 position)
    {
        return _marks.Marks.Any(m => (m.Coordinates.Position - position).Length() < 0.1f);
    }

    [Test]
    public async Task UnseenSoundLeavesAMark()
    {
        await AddAtmosphere();

        Vector2 behind = default, ahead = default, wall = default, looped = default;
        await Server.WaitPost(() =>
        {
            Face(new Vector2(1, 0));
            behind = Point(4, 180);
            ahead = Point(4, 0);
            wall = Point(4, 30);
            looped = Point(4, 160);

            // A wall between the player and a sound in front of it.
            var map = _transform.GetMapId(SPlayer);
            SEntMan.SpawnEntity("WallSolid", new MapCoordinates(Point(2.5f, 30), map));

            Sound(behind);
            Sound(ahead);
            Sound(wall);
            Sound(looped, AudioParams.Default.WithLoop(true));
        });
        await RunTicks(5);

        await Client.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(MarkedNear(behind), "A sound behind the back should leave a mark.");
                Assert.That(MarkedNear(wall), "A sound behind a wall should leave a mark.");
                Assert.That(MarkedNear(ahead), Is.False, "A sound in sight needs no mark.");
                Assert.That(MarkedNear(looped), Is.False, "The hum of a machine is not marked.");
            }
        });

        await RunSeconds(2.5f);
        await Client.WaitAssertion(() => Assert.That(_marks.Marks, Is.Empty, "Marks fade in a couple of seconds."));
    }

    [Test]
    public async Task DroppedItemIsHeard()
    {
        await AddAtmosphere();
        await PlaceInHands("Wrench");

        // The wrench lands with /Audio/Items/wrench_drop.ogg.
        bool Heard() => SEntMan.EntityQuery<AudioComponent>().Select(a => a.FileName).Any(f => f.EndsWith("wrench_drop.ogg"));

        await Server.WaitAssertion(() => Assert.That(Heard(), Is.False));
        await Server.WaitPost(() => Assert.That(_hands.TryDrop(SPlayer)));
        await RunTicks(1);
        await Server.WaitAssertion(() => Assert.That(Heard(), "Letting go of an item should make a sound."));
    }
}
