using Robust.Shared.GameStates;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// A bone organ. It sits inside a body zone and has its own health (<see cref="OrganHealthComponent"/>):
/// at <see cref="CrackRatio"/> of it the bone cracks, below <see cref="FractureRatio"/> it breaks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BoneComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Broken;

    [DataField, AutoNetworkedField]
    public bool Cracked;

    /// <summary>
    /// Set with a splint: the bone slowly grows back together.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Splinted;

    /// <summary>
    /// With 20 health a bone cracks at 15 and less.
    /// </summary>
    [DataField]
    public float CrackRatio = 0.75f;

    /// <summary>
    /// With 20 health a bone breaks below 10.
    /// </summary>
    [DataField]
    public float FractureRatio = 0.5f;

    /// <summary>
    /// Pulling it out of a living body kills (the skull, the spine).
    /// </summary>
    [DataField]
    public bool Vital;
}
