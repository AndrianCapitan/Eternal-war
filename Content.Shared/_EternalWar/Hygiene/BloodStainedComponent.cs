using Robust.Shared.GameStates;

namespace Content.Shared._EternalWar.Hygiene;

/// <summary>
/// Blood on a piece of clothing (or any item). Added when blood gets on it, removed when it is washed clean.
/// Worn clothing shows the stains on the wearer's sprite.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BloodStainedComponent : Component
{
    /// <summary>
    /// How much blood is on it, from 0 to 1.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Amount;

    /// <summary>
    /// The color of the blood, mixed when different blood gets on it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#800000");

    /// <summary>
    /// From this much on it is soaked, not just stained.
    /// </summary>
    [DataField]
    public float SoakedAmount = 0.5f;

    public int Level => Amount >= SoakedAmount ? 2 : Amount > 0 ? 1 : 0;
}
