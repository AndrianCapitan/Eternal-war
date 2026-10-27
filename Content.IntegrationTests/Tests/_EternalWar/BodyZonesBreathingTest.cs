#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._EternalWar.BodyZones;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// Life with one lung: fine in good air, suffocating sooner in thin air.
/// </summary>
[TestOf(typeof(BodyZonesSystem))]
public sealed class BodyZonesBreathingTest : GameTest
{
    private static readonly ResPath BreathingMap = new("Maps/Test/Breathing/3by3-20oxy-80nit.yml");

    /// <summary>
    /// Spawns a human alone on a small room of normal air, its air scaled by <paramref name="airRatio"/>.
    /// </summary>
    private async Task<EntityUid> SpawnHuman(bool oneLung, float airRatio)
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var human = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            entMan.System<SharedMapSystem>().CreateMap(out var mapId);
            Assert.That(entMan.System<MapLoaderSystem>().TryLoadGrid(mapId, BreathingMap, out var grid));
            human = entMan.SpawnEntity("MobHuman", new EntityCoordinates(grid!.Value.Owner, new Vector2(0.5f, 0.5f)));
        });
        await server.WaitRunTicks(1);

        await server.WaitPost(() =>
        {
            var atmos = entMan.System<AtmosphereSystem>();
            foreach (var tile in atmos.GetAllMixtures(entMan.GetComponent<TransformComponent>(human).GridUid!.Value))
            {
                tile.Multiply(airRatio);
            }

            if (!oneLung)
                return;

            var containers = entMan.System<SharedContainerSystem>();
            var body = containers.GetContainer(human, BodyComponent.ContainerID);
            var lung = body.ContainedEntities.Single(o => entMan.GetComponent<OrganComponent>(o).Category == "LungLeft");
            containers.Remove(lung, body);
        });

        return human;
    }

    private async Task<int> MaxSuffocationCycles(EntityUid human, int seconds)
    {
        var server = Pair.Server;
        var max = 0;
        for (var i = 0; i < seconds; i++)
        {
            await server.WaitRunTicks(30);
            await server.WaitPost(() =>
                max = System.Math.Max(max, server.EntMan.GetComponent<RespiratorComponent>(human).SuffocationCycles));
        }

        return max;
    }

    [Test]
    public async Task OneLungIsEnoughInGoodAir()
    {
        var human = await SpawnHuman(oneLung: true, airRatio: 1f);
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Pair.Server.EntMan.GetComponent<BodyZonesComponent>(human).MissingLungs, Is.EqualTo(1)));

        Assert.That(await MaxSuffocationCycles(human, 60), Is.Zero);
    }

    [Test]
    public async Task OneLungSuffocatesSoonerInThinAir()
    {
        // At 90% pressure two lungs still get enough oxygen, one doesn't.
        var healthy = await SpawnHuman(oneLung: false, airRatio: 0.9f);
        Assert.That(await MaxSuffocationCycles(healthy, 60), Is.Zero);

        var oneLung = await SpawnHuman(oneLung: true, airRatio: 0.9f);
        Assert.That(await MaxSuffocationCycles(oneLung, 60), Is.GreaterThan(0));
    }
}
