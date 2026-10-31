#nullable enable
using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._EternalWar.Vehicles;
using Content.Server.Gravity;
using Content.Shared.Buckle;
using Content.Shared.Gravity;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// Rolling into someone in a wheelchair at speed knocks them down; walking into an empty one does not.
/// </summary>
[TestOf(typeof(VehicleRamSystem))]
public sealed class VehicleRamTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [SidedDependency(Side.Server)] private readonly GravitySystem _gravity = default!;
    [SidedDependency(Side.Server)] private readonly SharedBuckleSystem _buckle = default!;

    private EntityUid _victim;

    /// <summary>
    /// Puts a wheelchair under the player (seating them if <paramref name="seated"/>)
    /// and a human <paramref name="distance"/> metres east of it.
    /// </summary>
    private async Task Setup(float distance, bool seated = true)
    {
        await AddAtmosphere();

        // A strip of floor to drive along.
        for (var x = 1; x <= 3; x++)
        {
            var coords = SEntMan.GetNetCoordinates(MapData.GridCoords.Offset(new Vector2(x + 0.5f, 0.5f)));
            await SetTile(Plating, coords, MapData.Grid);
        }

        await Server.WaitPost(() =>
        {
            // The test grid has no gravity generator, and a wheelchair does not roll while weightless.
            var gravity = SEntMan.GetComponent<GravityComponent>(MapData.Grid.Owner);
            _gravity.EnableGravity(MapData.Grid.Owner, gravity);
            gravity.Inherent = true;

            var chair = SEntMan.SpawnAtPosition("VehicleWheelchair", SEntMan.GetCoordinates(PlayerCoords));
            if (seated)
                Assert.That(_buckle.TryBuckle(SPlayer, SPlayer, chair), "The player should sit in the wheelchair.");

            _victim = SEntMan.SpawnAtPosition("MobHuman", MapData.GridCoords.Offset(new Vector2(0.5f + distance, 0.5f)));
        });
        await RunTicks(5);
    }

    [Test]
    public async Task RammingKnocksDown()
    {
        await Setup(2f);
        await Move(DirectionFlag.East, 1.5f, run: false);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.HasComponent<KnockedDownComponent>(_victim), "A wheelchair at speed should knock the human down."));
    }

    [Test]
    public async Task WalkingIntoEmptyChairDoesNot()
    {
        // Nobody drives the chair: the player walks into it from the west.
        await Setup(2.4f, seated: false);
        await Server.WaitPost(() =>
            SEntMan.System<SharedTransformSystem>().SetCoordinates(SPlayer, MapData.GridCoords.Offset(new Vector2(-0.3f, 0.5f))));
        await Move(DirectionFlag.East, 1.5f, run: false);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.HasComponent<KnockedDownComponent>(SPlayer), Is.False, "Walking into a wheelchair should not knock you down."));
    }
}
