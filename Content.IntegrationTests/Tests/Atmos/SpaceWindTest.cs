using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Gravity;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CCVar;
using Content.Shared.Gravity;
using Content.Shared.Stunnable;
using Content.Shared.Tests;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Atmos;

/// <summary>
/// Checks that space wind drags things toward a breach and knocks mobs down.
/// </summary>
public sealed class SpaceWindTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    // Roughly standard station pressure across the 14 tiles of the test room.
    protected override float Moles => 1460f;

    [SidedDependency(Side.Server)] private readonly EntityLookupSystem _sLookupSystem = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _sXform = default!;
    [SidedDependency(Side.Server)] private readonly IConfigurationManager _sConfig = default!;
    [SidedDependency(Side.Server)] private readonly GravitySystem _sGravity = default!;

    [Test]
    public async Task BreachPullsItemsAndKnocksDownMobs()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();

        EntityUid source, wallPos;
        source = wallPos = EntityUid.Invalid;

        Assert.Multiple(() =>
        {
            Assert.That(GetMarker(markers, "source", out source));
            Assert.That(GetMarker(markers, "wall", out wallPos));
        });

        var wall = _sLookupSystem.GetEntitiesIntersecting(wallPos).FirstOrNull();
        Assert.That(wall, Is.Not.Null);

        await Server.WaitPost(() =>
        {
            _sConfig.SetCVar(CCVars.SpaceWind, true);

            // The test room has no gravity generator, and mobs can't be knocked down while weightless.
            var gravity = SEntMan.GetComponent<GravityComponent>(MapData.Grid.Owner);
            _sGravity.EnableGravity(MapData.Grid.Owner, gravity);
            gravity.Inherent = true;
        });

        var sourceMix = SAtmos.GetTileMixture(source, true);
        Assert.That(sourceMix, Is.Not.Null);
        sourceMix.AdjustMoles(Gas.Nitrogen, Moles);

        // Let the room fill up evenly.
        await Server.WaitRunTicks(500);

        var breachPos = Vector2.Zero;
        EntityUid item = default, mob = default;
        var itemStartDist = 0f;
        var mobStartDist = 0f;

        await Server.WaitPost(() =>
        {
            breachPos = _sXform.GetWorldPosition(wallPos);
            // Both are off the breach's row, so a straight push along the flow would hit a wall.
            item = SEntMan.SpawnEntity("Wrench", new EntityCoordinates(MapData.Grid.Owner, 4.5f, -2.5f));
            mob = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(MapData.Grid.Owner, 2.5f, -2.5f));
            itemStartDist = (_sXform.GetWorldPosition(item) - breachPos).Length();
            mobStartDist = (_sXform.GetWorldPosition(mob) - breachPos).Length();
        });

        await Server.WaitRunTicks(5);

        // Space the room
        await Server.WaitPost(() => SEntMan.DeleteEntity(wall));

        // Atmos is driven manually in these tests; tick in between so physics moves things.
        var knockedDown = false;
        for (var i = 0; i < 30; i++)
        {
            await Server.WaitPost(() => SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate));
            await Server.WaitRunTicks(2);
            knockedDown |= SEntMan.HasComponent<KnockedDownComponent>(mob);
        }

        await Server.WaitAssertion(() =>
        {
            var mobCycle = SEntMan.GetComponent<MovedByPressureComponent>(mob).LastHighPressureMovementAirCycle;
            Assert.That(mobCycle, Is.GreaterThan(0), "Space wind never pushed the mob.");
            Assert.That(knockedDown, Is.True, "Mob next to the breach was never knocked down by space wind.");

            // The mob starts around a corner from the breach, so this also checks it isn't left wedged there.
            AssertPulledToBreach(mob, mobStartDist, breachPos, "Mob");
            AssertPulledToBreach(item, itemStartDist, breachPos, "Item");
        });
    }

    /// <summary>
    /// Passes if the entity was sucked out through the breach (which is in the room's west wall)
    /// or at least ended up clearly closer to it.
    /// </summary>
    private void AssertPulledToBreach(EntityUid uid, float startDist, Vector2 breachPos, string what)
    {
        if (SEntMan.Deleted(uid))
            return;

        var pos = _sXform.GetWorldPosition(uid);
        if (pos.X < breachPos.X - 0.5f)
            return;

        var endDist = (pos - breachPos).Length();
        Assert.That(endDist,
            Is.LessThan(startDist - 0.5f),
            $"{what} wasn't pulled toward the breach (start {startDist:F2}, end {endDist:F2}, pos {pos}, breach {breachPos}).");
    }
}
