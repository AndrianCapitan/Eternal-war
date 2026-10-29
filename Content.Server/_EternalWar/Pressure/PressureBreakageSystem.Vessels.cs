using Content.Shared._EternalWar.Pressure;
using Content.Shared.Atmos;
using Content.Shared.Light.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server._EternalWar.Pressure;

public sealed partial class PressureBreakageSystem
{
    /// <summary>
    /// A factory-sealed drink is never filled to the brim: this share of it is gas.
    /// </summary>
    private const float FactoryHeadspace = 0.05f;

    /// <summary>
    /// Carbon dioxide over a fizzy drink at room temperature, kPa (2.5 bar, like a soda can).
    /// </summary>
    private const float FizzyCarbonPressure = 250f;

    /// <summary>
    /// The outside crushes a container only when this share of it is gas. Fuller, the liquid inside holds the walls.
    /// </summary>
    private const float CrushHeadspace = 0.5f;

    /// <summary>
    /// The gas in a light bulb (argon at 0.8 atm) and in a fluorescent tube (mercury vapour and argon, near vacuum), kPa.
    /// </summary>
    private const float BulbGasPressure = 80f;
    private const float TubeGasPressure = 0.4f;

    /// <summary>
    /// A light bulb: 100 ml of gas, 30 J/K of glass.
    /// </summary>
    private const float BulbVolume = 1e-4f;
    private const float BulbHeatCapacity = 30f;

    private void InitializeVessels()
    {
        SubscribeLocalEvent<PressureVesselComponent, OpenableOpenedEvent>(OnVesselOpened);
    }

    private void OnVesselOpened(Entity<PressureVesselComponent> ent, ref OpenableOpenedEvent args)
    {
        // Open, it holds no pressure. Closed again, it is sealed with the air around.
        RemCompDeferred<PressureVesselComponent>(ent);
    }

    private void UpdateVessels(float time)
    {
        var drinks = EntityQueryEnumerator<OpenableComponent>();
        while (drinks.MoveNext(out var uid, out var openable))
        {
            if (openable.Opened || HasComp<PressureTornComponent>(uid) || Paused(uid))
                continue;

            if (GetCompositionMaterial(uid) is not { } material)
                continue;

            UpdateVessel(uid, material, time);
        }

        var bulbMaterial = _proto.Index(BulbMaterial);
        var bulbs = EntityQueryEnumerator<LightBulbComponent>();
        while (bulbs.MoveNext(out var uid, out var bulb))
        {
            if (bulb.State == LightBulbState.Broken || Paused(uid))
                continue;

            UpdateVessel(uid, bulbMaterial, time);
        }
    }

    private void UpdateVessel(EntityUid uid, PressureMaterialPrototype material, float time)
    {
        // Off any map (nullspace) there is no air at all, not even vacuum.
        if (Transform(uid).MapUid == null || _atmos.GetContainingMixture(uid) is not { } air)
            return;

        if (!TryComp<PressureVesselComponent>(uid, out var vessel))
        {
            if (!TrySeal(uid, air.Pressure, air.Temperature, out vessel))
                return;
        }

        var outside = air.Pressure;
        vessel.Temperature = PressurePhysics.Warm(vessel.Temperature,
            air.Temperature,
            outside,
            PressurePhysics.Surface(vessel.Volume),
            vessel.HeatCapacity,
            time);

        var inside = GetInternalPressure(vessel);
        if (inside - outside > material.BurstPressure)
        {
            var energy = PressurePhysics.BurstEnergy(inside, outside, vessel.Volume * vessel.Headspace);
            Break(uid, material, crushed: false, energy);
        }
        else if (outside - inside > material.CrushPressure && vessel.Headspace >= CrushHeadspace)
        {
            // The walls fall in and the pieces fly with what the outside put into them.
            var energy = (outside - inside) * 1000f * vessel.Volume * vessel.Headspace;
            Break(uid, material, crushed: true, energy);
        }
    }

    public float GetInternalPressure(PressureVesselComponent vessel)
    {
        return PressurePhysics.InternalPressure(vessel.GasPressure,
            vessel.CarbonPressure,
            vessel.SealTemperature,
            vessel.Headspace,
            vessel.LiquidShare,
            vessel.Temperature);
    }

    /// <summary>
    /// Starts following a closed container. A factory-sealed drink was sealed with room air
    /// (and the carbon dioxide of a fizzy one), one closed by hand with the air around it now.
    /// </summary>
    public bool TrySeal(EntityUid uid, float pressure, float temperature, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PressureVesselComponent? vessel)
    {
        vessel = null;

        if (TryComp<LightBulbComponent>(uid, out var bulb))
        {
            vessel = AddComp<PressureVesselComponent>(uid);
            vessel.GasPressure = bulb.Type == LightBulbType.Tube ? TubeGasPressure : BulbGasPressure;
            vessel.SealTemperature = Atmospherics.T20C;
            vessel.Headspace = 1f;
            vessel.Volume = BulbVolume;
            vessel.HeatCapacity = BulbHeatCapacity;
            vessel.Temperature = temperature;
            return true;
        }

        var liquid = 0f;
        var capacity = 0f;
        foreach (var (_, solution) in _solutions.EnumerateSolutions(uid))
        {
            liquid += solution.Comp.Solution.Volume.Float();
            capacity += solution.Comp.Solution.MaxVolume.Float();
        }

        if (capacity <= 0f)
            return false;

        var factory = !TryComp<SealableComponent>(uid, out var sealable) || sealable.Sealed;
        var sealPressure = factory ? Atmospherics.OneAtmosphere : pressure;
        var sealTemperature = factory ? Atmospherics.T20C : temperature;

        var liquidShare = MathF.Min(liquid / capacity, 1f - FactoryHeadspace);
        var vapour = liquidShare > 0f ? PressurePhysics.WaterVapour(sealTemperature) : 0f;

        vessel = AddComp<PressureVesselComponent>(uid);
        vessel.GasPressure = MathF.Max(sealPressure - vapour, 0f);
        vessel.CarbonPressure = factory && _pressurized.CanSpray(uid) ? FizzyCarbonPressure : 0f;
        vessel.SealTemperature = sealTemperature;
        vessel.LiquidShare = liquidShare;
        vessel.Headspace = 1f - liquidShare;
        vessel.Volume = capacity * PressurePhysics.UnitVolume;
        vessel.HeatCapacity = PressurePhysics.ShellHeatCapacity
                              + liquid * PressurePhysics.UnitMass * PressurePhysics.WaterHeatCapacity;
        vessel.Temperature = temperature;
        return true;
    }
}
