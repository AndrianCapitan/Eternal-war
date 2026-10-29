using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._EternalWar.Vision;

/// <summary>
/// Draws the marks of <see cref="SoundMarkSystem"/>: a ring that spreads and fades where an unseen sound came from.
/// Drawn over the dimming of <see cref="FieldOfViewOverlay"/>.
/// </summary>
public sealed class SoundMarkOverlay : Overlay
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly SoundMarkSystem _marks;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public Color Color = Color.White;
    public TimeSpan MarkTime = TimeSpan.FromSeconds(2);

    public SoundMarkOverlay(SoundMarkSystem marks)
    {
        IoCManager.InjectDependencies(this);
        _marks = marks;
        ZIndex = 1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _marks.Marks.Count > 0 && args.Viewport.Eye == _eye.CurrentEye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var now = _timing.CurTime;

        foreach (var mark in _marks.Marks)
        {
            if (mark.Coordinates.MapId != args.MapId)
                continue;

            var age = (float) ((now - mark.Start) / MarkTime);
            if (age is < 0f or > 1f)
                continue;

            var fade = 1f - age;
            var pos = mark.Coordinates.Position;
            handle.DrawCircle(pos, 0.15f, Color.WithAlpha(0.6f * fade));
            handle.DrawCircle(pos, 0.25f + 0.35f * age, Color.WithAlpha(0.9f * fade), false);
            handle.DrawCircle(pos, 0.27f + 0.35f * age, Color.WithAlpha(0.9f * fade), false);
        }
    }
}
