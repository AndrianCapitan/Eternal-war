using Content.Shared.Atmos;

namespace Content.Server._EternalWar.Pressure;

/// <summary>
/// The physics of a sealed container: what its inside presses with, how fast it takes the heat around it,
/// how much a burst lets go. Real formulas and constants; one unit of solution is taken for 10 ml.
/// </summary>
public static class PressurePhysics
{
    /// <summary>
    /// m³ of one unit of solution (10 ml): a 30 u can holds a third of a litre.
    /// </summary>
    public const float UnitVolume = 1e-5f;

    /// <summary>
    /// kg of one unit of a drink (water).
    /// </summary>
    public const float UnitMass = 0.01f;

    /// <summary>
    /// J/(kg·K) of water.
    /// </summary>
    public const float WaterHeatCapacity = 4186f;

    /// <summary>
    /// How much a liquid swells per kelvin (water, averaged over 20–150 °C).
    /// </summary>
    public const float LiquidExpansion = 6e-4f;

    /// <summary>
    /// Carbon dioxide leaves the drink as it warms: its pressure grows this much per kelvin
    /// (Henry's law, van 't Hoff constant of CO₂ 2400 K at room temperature).
    /// </summary>
    public const float CarbonSolubilityFall = 0.027f;

    /// <summary>
    /// All the carbon dioxide of a drink is out by this pressure (4 volumes of gas in the headspace).
    /// </summary>
    public const float MaxCarbonPressure = 8000f;

    /// <summary>
    /// The headspace can't get smaller than this share of the container: the walls give.
    /// </summary>
    public const float MinHeadspace = 0.002f;

    /// <summary>
    /// Water above this is a gas whatever the pressure (critical point, 22.06 MPa).
    /// </summary>
    public const float WaterCriticalPressure = 22064f;

    /// <summary>
    /// Heat capacity of the walls of a small container, J/K.
    /// </summary>
    public const float ShellHeatCapacity = 150f;

    /// <summary>
    /// W/(m²·K) of still air at one atmosphere around a small container. Denser gas carries more heat, vacuum none.
    /// </summary>
    public const float ConvectionCoefficient = 10f;

    public const float Emissivity = 0.9f;

    public const float StefanBoltzmann = 5.670374e-8f;

    /// <summary>
    /// The ratio of specific heats of the gas inside (air).
    /// </summary>
    public const float GasHeatRatio = 1.4f;

    /// <summary>
    /// Pressure of water vapour at a temperature, kPa (Antoine equation, two ranges).
    /// </summary>
    public static float WaterVapour(float temperature)
    {
        var celsius = temperature - 273.15f;
        if (celsius <= 0f)
        {
            // Ice: barely any vapour; it doesn't matter for a burst.
            return 0.611f * MathF.Exp(0.08f * celsius);
        }

        var (a, b, c) = celsius <= 100f
            ? (8.07131f, 1730.63f, 233.426f)
            : (8.14019f, 1810.94f, 244.485f);

        var mmHg = MathF.Pow(10f, a - b / (c + celsius));
        return MathF.Min(mmHg * 0.133322f, WaterCriticalPressure);
    }

    /// <summary>
    /// The pressure inside a sealed container at a temperature, kPa: the gas sealed in (warmer is denser),
    /// the carbon dioxide out of a fizzy drink, the vapour of the liquid. A warm liquid swells and squeezes the gas.
    /// </summary>
    public static float InternalPressure(float gasPressure,
        float carbonPressure,
        float sealTemperature,
        float headspace,
        float liquidShare,
        float temperature)
    {
        var heat = temperature - sealTemperature;
        var space = MathF.Max(headspace - liquidShare * LiquidExpansion * heat, MinHeadspace);
        var squeeze = headspace > 0f ? headspace / space : 1f;

        var gas = gasPressure * temperature / sealTemperature * squeeze;
        var carbon = carbonPressure > 0f
            ? MathF.Min(carbonPressure * MathF.Exp(CarbonSolubilityFall * heat), MaxCarbonPressure) * squeeze
            : 0f;
        var vapour = liquidShare > 0f ? WaterVapour(temperature) : 0f;
        return gas + carbon + vapour;
    }

    /// <summary>
    /// Energy let go when gas at a pressure expands to the pressure around (adiabatic), J.
    /// </summary>
    public static float BurstEnergy(float inside, float outside, float gasVolume)
    {
        if (inside <= outside || inside <= 0f || gasVolume <= 0f)
            return 0f;

        var exponent = (GasHeatRatio - 1f) / GasHeatRatio;
        var ratio = MathF.Max(outside, 0.01f) / inside;
        return inside * 1000f * gasVolume / (GasHeatRatio - 1f) * (1f - MathF.Pow(ratio, exponent));
    }

    /// <summary>
    /// Surface of a container of a volume, m²: about a cylinder as tall as it is wide.
    /// </summary>
    public static float Surface(float volume)
    {
        return 5.5f * MathF.Pow(MathF.Max(volume, 1e-6f), 2f / 3f);
    }

    /// <summary>
    /// The temperature a container gets to after some time in a gas: convection (none in vacuum) and radiation.
    /// Never past the temperature of the gas.
    /// </summary>
    public static float Warm(float temperature, float gasTemperature, float gasPressure, float surface, float heatCapacity, float time)
    {
        var convection = ConvectionCoefficient * MathF.Sqrt(MathF.Max(gasPressure, 0f) / Atmospherics.OneAtmosphere);
        var power = surface * (convection * (gasTemperature - temperature)
                               + Emissivity * StefanBoltzmann * (Pow4(gasTemperature) - Pow4(temperature)));
        var next = temperature + power * time / heatCapacity;

        return temperature < gasTemperature
            ? MathF.Min(next, gasTemperature)
            : MathF.Max(next, gasTemperature);
    }

    private static float Pow4(float x)
    {
        var square = x * x;
        return square * square;
    }
}
