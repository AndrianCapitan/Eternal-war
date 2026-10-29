#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server._EternalWar.Underwear;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// A player that joins the round spawns in underwear, under the jumpsuit of the job.
/// </summary>
[TestOf(typeof(UnderwearDressingSystem))]
public sealed class UnderwearRoundStartTest : GameTest
{
    private const string Map = "UnderwearTestMap";

    [TestPrototypes]
    private static readonly string Prototypes = @$"
- type: gameMap
  id: {Map}
  mapName: {Map}
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Empty:
      stationProto: StandardNanotrasenStation
      components:
        - type: StationNameSetup
          mapNameTemplate: ""Empty""
        - type: StationJobs
          availableJobs:
            Passenger: [ -1, -1 ]
";

    public override PoolSettings PoolSettings => new()
    {
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    [Test]
    public async Task PlayerSpawnsInUnderwear()
    {
        var pair = Pair;
        pair.Server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = pair.Server.System<ServerGameTicker>();
        var inventory = pair.Server.System<InventorySystem>();

        ticker.ToggleReadyAll(true);
        await pair.Server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            var mob = pair.Server.PlayerMan.SessionsDict[pair.Client.User!.Value].AttachedEntity!.Value;
            var sex = pair.Server.EntMan.GetComponent<HumanoidProfileComponent>(mob).Sex;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(inventory.TryGetSlotEntity(mob, "jumpsuit", out _), "The passenger should wear the job's jumpsuit.");
                Assert.That(inventory.TryGetSlotEntity(mob, "underwearBottom", out _), "Underpants should be on under it.");
                if (sex == Sex.Female)
                    Assert.That(inventory.TryGetSlotEntity(mob, "underwearTop", out _), "A woman should wear a top.");
            }
        });

        await pair.Server.WaitPost(() => ticker.RestartRound());
    }
}
