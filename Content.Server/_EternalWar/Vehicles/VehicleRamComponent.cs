using Robust.Shared.Audio;

namespace Content.Server._EternalWar.Vehicles;

/// <summary>
/// A driven vehicle that rolls into someone fast enough knocks them off their feet.
/// </summary>
[RegisterComponent, Access(typeof(VehicleRamSystem))]
public sealed partial class VehicleRamComponent : Component
{
    /// <summary>
    /// How fast the vehicle has to move towards the target, in metres per second.
    /// A wheelchair tops out at 2, so anything short of a scrape or a stuck wheel counts.
    /// </summary>
    [DataField]
    public float MinSpeed = 1.5f;

    /// <summary>
    /// How long the rammed one lies on the floor.
    /// </summary>
    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(2);

    [DataField]
    public SoundSpecifier? Sound = new SoundCollectionSpecifier("MetalThud");
}
