namespace Content.Shared._EternalWar.Aging;

/// <summary>
/// A hitscan ray with this ages whoever it hits by <see cref="Years"/> (less through <see cref="AgingProtectionComponent"/>).
/// </summary>
[RegisterComponent]
public sealed partial class HitscanAgingComponent : Component
{
    [DataField]
    public float Years = 10f;
}
