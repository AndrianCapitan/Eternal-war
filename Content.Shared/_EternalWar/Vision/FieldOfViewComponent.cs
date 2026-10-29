namespace Content.Shared._EternalWar.Vision;

/// <summary>
/// The mob sees only in front of itself: outside of the cone the world is dim and other mobs and items are hidden.
/// It faces the mouse while a player controls it. Losing an eye narrows the cone on that side.
/// The cone is drawn and applied on the client of the player only.
/// </summary>
[RegisterComponent]
public sealed partial class FieldOfViewComponent : Component
{
    /// <summary>
    /// The whole cone, with both eyes.
    /// </summary>
    [DataField]
    public Angle Angle = Angle.FromDegrees(150);

    /// <summary>
    /// How much of the cone is lost on the side of a missing or crushed eye.
    /// </summary>
    [DataField]
    public Angle LostEyeNarrowing = Angle.FromDegrees(45);

    /// <summary>
    /// Everything this close is seen all around: the mob feels who stands next to it.
    /// </summary>
    [DataField]
    public float NearRadius = 1.5f;

    /// <summary>
    /// How dark the world outside the cone is, 0 to 1.
    /// </summary>
    [DataField]
    public float Darkness = 0.6f;

    /// <summary>
    /// The edge of the cone fades over this angle on each side.
    /// </summary>
    [DataField]
    public Angle EdgeSoftness = Angle.FromDegrees(4);

    /// <summary>
    /// The mob turns to the mouse once it is this far from where it faces.
    /// </summary>
    [DataField]
    public Angle MouseTolerance = Angle.FromDegrees(8);

    /// <summary>
    /// A sound the mob hears but doesn't see the source of (out of the cone or behind a wall) leaves a mark where it
    /// came from for this long.
    /// </summary>
    [DataField]
    public TimeSpan SoundMarkTime = TimeSpan.FromSeconds(2);

    [DataField]
    public Color SoundMarkColor = Color.FromHex("#f2e6c8");
}
