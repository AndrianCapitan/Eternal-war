#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._EternalWar.BodyZones;
using Content.Server._EternalWar.Pressure;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Light.EntitySystems;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Pressure;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Light.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Nutrition.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// Containers burst and get crushed by pressure and heat, electronics break and get repaired,
/// pieces fly, and sudden pressure changes hurt the body.
/// </summary>
[TestOf(typeof(PressureBreakageSystem))]
[TestOf(typeof(PressureBodySystem))]
public sealed class PressureTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [SidedDependency(Side.Server)] private readonly AtmosphereSystem _atmos = default!;
    [SidedDependency(Side.Server)] private readonly BodyZonesSystem _zones = default!;
    [SidedDependency(Side.Server)] private readonly HandheldLightSystem _lights = default!;
    [SidedDependency(Side.Server)] private readonly PressureBreakageSystem _breakage = default!;
    [SidedDependency(Side.Server)] private readonly SharedSolutionContainerSystem _solutions = default!;

    /// <summary>
    /// Fills the map with air at a pressure (kPa) and temperature (K).
    /// </summary>
    private async Task SetAir(float pressure, float temperature = Atmospherics.T20C)
    {
        await Server.WaitPost(() =>
        {
            var total = pressure * Atmospherics.CellVolume / (Atmospherics.R * temperature);
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = total * 0.21f;
            moles[(int) Gas.Nitrogen] = total * 0.79f;
            _atmos.SetMapAtmosphere(MapData.MapUid, false, new GasMixture(moles, temperature));
        });
    }

    private async Task<EntityUid> SpawnAt(string prototype, EntityCoordinates? coords = null)
    {
        var uid = EntityUid.Invalid;
        await Server.WaitPost(() => uid = SEntMan.SpawnAtPosition(prototype, coords ?? SEntMan.GetCoordinates(TargetCoords)));
        await RunTicks(1);
        return uid;
    }

    private float Liquid(EntityUid uid)
    {
        return _solutions.EnumerateSolutions(uid).Sum(s => s.Solution.Comp.Solution.Volume.Float());
    }

    [Test]
    public void Physics()
    {
        using (Assert.EnterMultipleScope())
        {
            // Water boils at 100 °C under one atmosphere.
            Assert.That(PressurePhysics.WaterVapour(373.15f), Is.EqualTo(Atmospherics.OneAtmosphere).Within(1f));
            Assert.That(PressurePhysics.WaterVapour(293.15f), Is.EqualTo(2.34f).Within(0.1f));

            // A soda can at room temperature: about 3.5 bar inside; warmed by 30 K, past what the can holds.
            var room = PressurePhysics.InternalPressure(99f, 250f, 293.15f, 0.05f, 0.95f, 293.15f);
            var warm = PressurePhysics.InternalPressure(99f, 250f, 293.15f, 0.05f, 0.95f, 323.15f);
            Assert.That(room, Is.EqualTo(351f).Within(2f));
            Assert.That(warm - Atmospherics.OneAtmosphere, Is.GreaterThan(620f));

            Assert.That(PressurePhysics.BurstEnergy(1000f, 100f, 1e-4f), Is.GreaterThan(0f));
            Assert.That(PressurePhysics.BurstEnergy(100f, 1000f, 1e-4f), Is.Zero);

            // Warming never overshoots the gas, and vacuum still warms by radiation.
            Assert.That(PressurePhysics.Warm(290f, 300f, 101f, 1f, 1f, 100f), Is.EqualTo(300f));
            Assert.That(PressurePhysics.Warm(290f, 1000f, 0f, 0.03f, 1400f, 1f), Is.GreaterThan(290f));
        }
    }

    [Test]
    public async Task VacuumTearsCartonNotCan()
    {
        await SetAir(0f);
        var can = await SpawnAt("DrinkColaCan");
        var carton = await SpawnAt("DrinkJuiceOrangeCartonXL");
        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(_breakage.GetCompositionMaterial(carton)?.ID, Is.EqualTo("Cardboard"));
                Assert.That(SEntMan.HasComponent<PressureTornComponent>(carton), "A sealed carton splits in vacuum.");
                Assert.That(SEntMan.GetComponent<OpenableComponent>(carton).Opened);
                Assert.That(Liquid(carton), Is.Zero, "The juice spilled out.");

                Assert.That(_breakage.GetCompositionMaterial(can)?.ID, Is.EqualTo("ThinMetal"));
                Assert.That(SEntMan.HasComponent<PressureTornComponent>(can), Is.False, "A can holds 3.5 bar inside.");
                Assert.That(SEntMan.HasComponent<PressureVesselComponent>(can));
            }
        });
    }

    [Test]
    public async Task FireBurstsSodaCan()
    {
        await SetAir(Atmospherics.OneAtmosphere);
        var can = await SpawnAt("DrinkColaCan");
        await RunSeconds(5);
        Assert.That(SEntMan.HasComponent<PressureTornComponent>(can), Is.False, "A can is fine in a room.");

        await SetAir(Atmospherics.OneAtmosphere, 1200f);
        await RunSeconds(30);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<PressureTornComponent>(can), "A soda can bursts in a fire.");
            Assert.That(Liquid(can), Is.Zero);
        });
    }

    [Test]
    public async Task HighPressureCrushesBulb()
    {
        await SetAir(Atmospherics.OneAtmosphere);
        var bulb = await SpawnAt("LightBulb");
        await RunSeconds(2);
        Assert.That(SEntMan.GetComponent<LightBulbComponent>(bulb).State, Is.Not.EqualTo(LightBulbState.Broken));

        await SetAir(800f);
        await RunSeconds(2);
        Assert.That(SEntMan.GetComponent<LightBulbComponent>(bulb).State, Is.EqualTo(LightBulbState.Broken),
            "80 kPa of argon inside doesn't hold 800 kPa outside.");
    }

    [Test]
    public async Task ElectronicsCrushedAndRepaired()
    {
        await SetAir(Atmospherics.OneAtmosphere);
        var lantern = await SpawnAt("FlashlightLantern");
        await RunSeconds(2);
        Assert.That(SEntMan.HasComponent<BrokenElectronicsComponent>(lantern), Is.False);

        await SetAir(2000f);
        await RunSeconds(2);
        await SetAir(Atmospherics.OneAtmosphere);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<BrokenElectronicsComponent>(lantern));
            Assert.That(_lights.TurnOn(SPlayer, (lantern, SEntMan.GetComponent<HandheldLightComponent>(lantern))), Is.False,
                "Broken, it doesn't turn on.");
        });

        Target = SEntMan.GetNetEntity(lantern);
        await InteractUsing("Screwdriver");
        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<BrokenElectronicsComponent>(lantern), Is.False, "A screwdriver fixes it.");
            Assert.That(_lights.TurnOn(SPlayer, (lantern, SEntMan.GetComponent<HandheldLightComponent>(lantern))));
        });
    }

    [Test]
    public async Task WindBreaksGlassAgainstWall()
    {
        await SetAir(Atmospherics.OneAtmosphere);
        var slow = await SpawnAt("DrinkBeerBottleFull");
        var fast = await SpawnAt("DrinkBeerBottleFull");

        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(_breakage.TryImpactBreak(slow, 5f), Is.False, "A bottle survives a soft knock.");
                Assert.That(_breakage.TryImpactBreak(fast, 10f), "A bottle smashes against a wall at 10 m/s.");
            }
        });
        await RunTicks(1);
        Assert.That(SEntMan.Deleted(fast));
    }

    [Test]
    public async Task ShrapnelFliesOneWay()
    {
        await SetAir(Atmospherics.OneAtmosphere);
        var origin = SEntMan.GetCoordinates(TargetCoords);
        var front = await SpawnAt("MobHuman", origin.Offset(new Vector2(1f, 0f)));
        var back = await SpawnAt("MobHuman", origin.Offset(new Vector2(-1f, 0f)));
        var source = await SpawnAt("DrinkBeerBottleFull", origin);

        await Server.WaitPost(() =>
        {
            var at = SEntMan.System<SharedTransformSystem>().ToMapCoordinates(origin);
            _breakage.Shrapnel(source, at, 2000f, "Slash", new Vector2(1f, 0f), null);
        });

        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(front).Float(), Is.GreaterThan(5f));
                Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(back).Float(), Is.Zero,
                    "A pane blows out into one side only.");
            }
        });
    }

    [Test]
    public async Task SuddenPressureHurtsEarsAndLungs()
    {
        await SetAir(Atmospherics.OneAtmosphere);
        var victim = await SpawnAt("MobHuman");
        await RunSeconds(2);

        // A blast of 200 kPa in a second: the eardrums burst.
        await SetAir(300f);
        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            foreach (var category in new[] { "EarLeft", "EarRight" })
            {
                var ear = _zones.GetOrgans(victim, category).Single();
                Assert.That(SEntMan.GetComponent<OrganHealthComponent>(ear).Failed, $"{category} should burst.");
            }
        });

        // A fall of 150 kPa in a second: the air tears the lungs on its way out.
        await SetAir(150f);
        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            foreach (var category in new[] { "LungLeft", "LungRight" })
            {
                var lung = _zones.GetOrgans(victim, category).Single();
                Assert.That(SEntMan.GetComponent<OrganHealthComponent>(lung).Damage, Is.EqualTo(12f).Within(0.5f));
            }
        });
    }

    [Test]
    public async Task DecompressionSickness()
    {
        await SetAir(Atmospherics.OneAtmosphere);
        var victim = await SpawnAt("MobHuman");
        var body = SEntMan.GetComponent<PressureBodyComponent>(victim);
        var move = SEntMan.GetComponent<MovementSpeedModifierComponent>(victim);

        // As if after a long stay at 6 atmospheres.
        await Server.WaitPost(() => body.TissuePressure = 600f);
        await RunSeconds(10);

        Assert.That(body.Stage, Is.EqualTo(DecompressionStage.Bends));
        Assert.That(move.CurrentSprintSpeed, Is.LessThan(move.BaseSprintSpeed));

        await RunSeconds(15);
        Assert.That(body.Stage, Is.EqualTo(DecompressionStage.Severe));

        // Back under pressure the bubbles dissolve.
        await SetAir(400f);
        await RunSeconds(60);
        Assert.That(body.Stage, Is.EqualTo(DecompressionStage.None));
    }
}
