#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._EternalWar.Inserter;
using Content.Server.Power.Components;
using Content.Shared.Materials;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._EternalWar;

[TestOf(typeof(InserterSystem))]
public sealed class InserterTest : InteractionTest
{
    [SidedDependency(Side.Server)] private readonly SharedContainerSystem _container = default!;
    [SidedDependency(Side.Server)] private readonly SharedMaterialStorageSystem _materials = default!;

    // A row of three tiles next to the player's tile: source, inserter, target. The inserter faces east.
    // The row has to touch the existing tile, or the grid would split it off into separate grids.
    private const int Row = 0;
    private const int FirstColumn = 1;

    private EntityCoordinates Tile(int x)
    {
        return new EntityCoordinates(MapData.Grid, FirstColumn + x + 0.5f, Row + 0.5f);
    }

    private async Task<EntityUid> Setup(bool powered = false)
    {
        for (var x = 0; x <= 2; x++)
        {
            await SetTile(Plating, SEntMan.GetNetCoordinates(Tile(x)), MapData.Grid);
        }

        EntityUid inserter = default;
        await Server.WaitPost(() =>
        {
            inserter = SEntMan.SpawnAtPosition("Inserter", Tile(1));
            Transform.SetLocalRotation(inserter, Direction.East.ToAngle());
            Transform.AnchorEntity(inserter);

            // The test map has no power grid; without a receiver the inserter counts as powered.
            if (!powered)
                SEntMan.RemoveComponent<ApcPowerReceiverComponent>(inserter);
        });
        return inserter;
    }

    private bool IsOnTile(EntityUid uid, int x)
    {
        if (_container.IsEntityInContainer(uid))
            return false;

        var pos = SEntMan.GetComponent<TransformComponent>(uid).Coordinates.Position;
        return (int) MathF.Floor(pos.X) == FirstColumn + x && (int) MathF.Floor(pos.Y) == Row;
    }

    [Test]
    public async Task MovesItemFromFloorToFloor()
    {
        await Setup();

        EntityUid crowbar = default;
        await Server.WaitPost(() => crowbar = SEntMan.SpawnAtPosition("Crowbar", Tile(0)));
        await RunSeconds(3);

        await Server.WaitAssertion(() => Assert.That(IsOnTile(crowbar, 2), "the crowbar should be moved east"));
    }

    [Test]
    public async Task PutsIntoAndTakesFromCrates()
    {
        await Setup();

        EntityUid source = default, target = default, crowbar = default;
        await Server.WaitPost(() =>
        {
            source = SEntMan.SpawnAtPosition("CrateGenericSteel", Tile(0));
            target = SEntMan.SpawnAtPosition("CrateGenericSteel", Tile(2));
            crowbar = SEntMan.SpawnAtPosition("Crowbar", Tile(0));
            var sourceStorage = SEntMan.GetComponent<EntityStorageComponent>(source);
            Assert.That(_container.Insert(crowbar, sourceStorage.Contents));
        });

        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            var targetStorage = SEntMan.GetComponent<EntityStorageComponent>(target);
            Assert.That(targetStorage.Contents.ContainedEntities, Does.Contain(crowbar));
            Assert.That(SEntMan.GetComponent<EntityStorageComponent>(source).Contents.ContainedEntities, Is.Empty);
        });
    }

    [Test]
    public async Task FeedsMaterialsIntoLathe()
    {
        await Setup();

        EntityUid lathe = default;
        var before = 0;
        await Server.WaitPost(() =>
        {
            lathe = SEntMan.SpawnAtPosition("Autolathe", Tile(2));
            SEntMan.RemoveComponent<ApcPowerReceiverComponent>(lathe);
            before = _materials.GetMaterialAmount(lathe, "Steel");
            SEntMan.SpawnAtPosition("SheetSteel1", Tile(0));
        });

        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_materials.GetMaterialAmount(lathe, "Steel"), Is.GreaterThan(before));
        });
    }

    [Test]
    public async Task DoesNothingWithoutPower()
    {
        await Setup(powered: true);

        EntityUid crowbar = default;
        await Server.WaitPost(() => crowbar = SEntMan.SpawnAtPosition("Crowbar", Tile(0)));
        await RunSeconds(3);

        await Server.WaitAssertion(() => Assert.That(IsOnTile(crowbar, 0), "an unpowered inserter must not move anything"));
    }

    [Test]
    public async Task WaitsWhenTargetIsBlocked()
    {
        var inserter = await Setup();

        EntityUid crowbar = default;
        await Server.WaitPost(() =>
        {
            SEntMan.SpawnAtPosition("WallSolid", Tile(2));
            crowbar = SEntMan.SpawnAtPosition("Crowbar", Tile(0));
        });
        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            // Picked up, but there is no room in front, so the arm keeps holding it.
            var hand = _container.GetContainer(inserter, "inserter_hand");
            Assert.That(hand.ContainedEntities, Does.Contain(crowbar));
        });
    }
}
