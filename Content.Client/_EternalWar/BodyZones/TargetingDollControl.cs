using System.Numerics;
using Content.Shared._EternalWar.BodyZones;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._EternalWar.BodyZones;

/// <summary>
/// The little body on the HUD: shows how hurt each zone is and lets the player pick the zone to aim at.
/// </summary>
public sealed class TargetingDollControl : Control
{
    private const string TexturePath = "/Textures/_EternalWar/Interface/Targeting/";
    private const float Scale = 2f;
    private static readonly Vector2 DollSize = new(40, 70);

    // Same boxes the textures were drawn from (doll pixels, inclusive), used for clicking.
    // The doll faces the viewer, so the character's right side is on the left.
    // The eyes and the mouth lie on the head, so they are checked first.
    private static readonly (BodyZone Zone, Box2i Box)[] Hitboxes =
    {
        (BodyZone.RightEye, new(15, 4, 18, 6)),
        (BodyZone.LeftEye, new(21, 4, 24, 6)),
        (BodyZone.Mouth, new(17, 9, 22, 10)),
        (BodyZone.Head, new(14, 1, 25, 12)),
        (BodyZone.Chest, new(13, 14, 26, 26)),
        (BodyZone.Abdomen, new(13, 28, 26, 36)),
        (BodyZone.Genitals, new(18, 38, 21, 41)),
        (BodyZone.RightUpperArm, new(8, 14, 11, 23)),
        (BodyZone.LeftUpperArm, new(28, 14, 31, 23)),
        (BodyZone.RightForearm, new(7, 25, 10, 34)),
        (BodyZone.LeftForearm, new(29, 25, 32, 34)),
        (BodyZone.RightHand, new(6, 36, 10, 40)),
        (BodyZone.LeftHand, new(29, 36, 33, 40)),
        (BodyZone.RightThigh, new(13, 38, 16, 50)),
        (BodyZone.LeftThigh, new(23, 38, 26, 50)),
        (BodyZone.RightShin, new(13, 52, 16, 63)),
        (BodyZone.LeftShin, new(23, 52, 26, 63)),
        (BodyZone.RightFoot, new(11, 65, 16, 68)),
        (BodyZone.LeftFoot, new(23, 65, 28, 68)),
    };

    private static readonly Color Healthy = Color.FromHex("#5ac85a");
    private static readonly Color Hurt = Color.FromHex("#e6c83c");
    private static readonly Color BadlyHurt = Color.FromHex("#e67832");
    private static readonly Color Mangled = Color.FromHex("#d23c32");
    private static readonly Color Missing = Color.FromHex("#3c3c3c80");
    private static readonly Color Unknown = Color.FromHex("#8c8c8c");
    private static readonly Color SelectedFrame = Color.FromHex("#ffe650");
    private static readonly Color HoveredFrame = Color.FromHex("#ffffff80");
    private static readonly Color FractureFrame = Color.FromHex("#c85aff");

    private readonly Dictionary<BodyZone, Texture> _fill = new();
    private readonly Dictionary<BodyZone, Texture> _frame = new();

    /// <summary>
    /// Health of each zone from 1 (unhurt) down, negative once past zero. Null for missing zones.
    /// </summary>
    public readonly Dictionary<BodyZone, float?> Health = new();

    /// <summary>
    /// Zones with a broken bone, drawn with a purple frame.
    /// </summary>
    public readonly HashSet<BodyZone> Fractures = new();

    public BodyZone Selected = BodyZone.Chest;
    public BodyZone? Hovered { get; private set; }

    public event Action<BodyZone>? OnZonePressed;
    public event Action<BodyZone?>? OnZoneHovered;

    public TargetingDollControl()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        foreach (var zone in BodyZoneExtensions.All)
        {
            var name = TextureName(zone);
            _fill[zone] = cache.GetResource<TextureResource>($"{TexturePath}{name}.png").Texture;
            _frame[zone] = cache.GetResource<TextureResource>($"{TexturePath}{name}_select.png").Texture;
        }

        MouseFilter = MouseFilterMode.Stop;
        MinSize = DollSize * Scale;
    }

    private static string TextureName(BodyZone zone)
    {
        return zone switch
        {
            BodyZone.Head => "head",
            BodyZone.LeftEye => "left_eye",
            BodyZone.RightEye => "right_eye",
            BodyZone.Mouth => "mouth",
            BodyZone.Chest => "chest",
            BodyZone.Abdomen => "abdomen",
            BodyZone.Genitals => "genitals",
            BodyZone.LeftUpperArm => "left_upper_arm",
            BodyZone.RightUpperArm => "right_upper_arm",
            BodyZone.LeftForearm => "left_forearm",
            BodyZone.RightForearm => "right_forearm",
            BodyZone.LeftHand => "left_hand",
            BodyZone.RightHand => "right_hand",
            BodyZone.LeftThigh => "left_thigh",
            BodyZone.RightThigh => "right_thigh",
            BodyZone.LeftShin => "left_shin",
            BodyZone.RightShin => "right_shin",
            BodyZone.LeftFoot => "left_foot",
            _ => "right_foot",
        };
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var pixel = Scale * UIScale;
        var origin = (PixelSize - DollSize * pixel) / 2;
        var box = UIBox2.FromDimensions(origin, DollSize * pixel);

        foreach (var zone in BodyZoneExtensions.All)
        {
            handle.DrawTextureRect(_fill[zone], box, GetColor(zone));
        }

        foreach (var zone in Fractures)
        {
            handle.DrawTextureRect(_frame[zone], box, FractureFrame);
        }

        if (Hovered is { } hovered && hovered != Selected)
            handle.DrawTextureRect(_frame[hovered], box, HoveredFrame);

        handle.DrawTextureRect(_frame[Selected], box, SelectedFrame);
    }

    private Color GetColor(BodyZone zone)
    {
        if (!Health.TryGetValue(zone, out var health))
            return Unknown;

        return health switch
        {
            null => Missing,
            > 0.66f => Healthy,
            > 0.33f => Hurt,
            > 0f => BadlyHurt,
            _ => Mangled,
        };
    }

    private BodyZone? ZoneAt(Vector2 pixelPosition)
    {
        var pixel = Scale * UIScale;
        var origin = (PixelSize - DollSize * pixel) / 2;
        var local = (pixelPosition - origin) / pixel;
        var x = (int) MathF.Floor(local.X);
        var y = (int) MathF.Floor(local.Y);

        foreach (var (zone, hitbox) in Hitboxes)
        {
            if (x >= hitbox.Left && x <= hitbox.Right && y >= hitbox.Bottom && y <= hitbox.Top)
                return zone;
        }

        return null;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || ZoneAt(args.RelativePixelPosition) is not { } zone)
            return;

        args.Handle();
        OnZonePressed?.Invoke(zone);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        SetHovered(ZoneAt(args.RelativePixelPosition));
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        SetHovered(null);
    }

    private void SetHovered(BodyZone? zone)
    {
        if (Hovered == zone)
            return;

        Hovered = zone;
        OnZoneHovered?.Invoke(zone);
    }
}
