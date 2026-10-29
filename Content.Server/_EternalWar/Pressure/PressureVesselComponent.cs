namespace Content.Server._EternalWar.Pressure;

/// <summary>
/// A sealed container: a closed drink, a light bulb. What was sealed in and how warm it is now.
/// Added by <see cref="PressureBreakageSystem"/> the first time it sees the container closed, taken off when it opens.
/// </summary>
[RegisterComponent]
public sealed partial class PressureVesselComponent : Component
{
    /// <summary>
    /// The gas sealed in, kPa at <see cref="SealTemperature"/>, without the vapour of the liquid
    /// and the carbon dioxide of a fizzy drink.
    /// </summary>
    [DataField]
    public float GasPressure;

    /// <summary>
    /// The carbon dioxide over a fizzy drink, kPa at <see cref="SealTemperature"/>.
    /// </summary>
    [DataField]
    public float CarbonPressure;

    [DataField]
    public float SealTemperature;

    /// <summary>
    /// The share of the container that the gas had when it was sealed.
    /// </summary>
    [DataField]
    public float Headspace;

    /// <summary>
    /// The share of the container that the liquid had when it was sealed.
    /// </summary>
    [DataField]
    public float LiquidShare;

    /// <summary>
    /// How much the container holds, m³.
    /// </summary>
    [DataField]
    public float Volume;

    /// <summary>
    /// J/K of the container with what is in it.
    /// </summary>
    [DataField]
    public float HeatCapacity;

    /// <summary>
    /// The temperature of the container and what is in it, K.
    /// </summary>
    [DataField]
    public float Temperature;
}
