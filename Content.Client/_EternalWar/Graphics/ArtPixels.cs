using Robust.Client.Graphics;

namespace Content.Client._EternalWar.Graphics;

/// <summary>
/// World textures are drawn at <see cref="EyeManager.PixelsPerMeter"/> (64), but pixel values in prototypes and code
/// (glyph widths, icon offsets, bar sizes) and UI textures drawn in the world were made for 32 px per meter art.
/// Converts those art pixels to meters.
/// </summary>
public static class ArtPixels
{
    public const int PerMeter = 32;

    /// <summary>
    /// The size of one art pixel in meters.
    /// </summary>
    public const float Size = 1f / PerMeter;
}
