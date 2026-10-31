namespace Content.Shared._EternalWar.Aging;

/// <summary>
/// Worn or held, this lets the time ray take fewer years: they are multiplied by <see cref="Coefficient"/>.
/// Several such things multiply together.
/// </summary>
[RegisterComponent]
public sealed partial class AgingProtectionComponent : Component
{
    [DataField]
    public float Coefficient = 0.5f;
}
