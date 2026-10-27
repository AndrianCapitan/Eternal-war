namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// Overrides which body zones a piece of armor protects.
/// Without it the zones are taken from the inventory slot the armor is worn in,
/// see <see cref="SharedBodyZonesSystem.GetDefaultCoverage"/>.
/// </summary>
[RegisterComponent]
public sealed partial class ArmorCoverageComponent : Component
{
    [DataField(required: true)]
    public HashSet<BodyZone> Zones = new();
}
