using Content.Shared._EternalWar.Vision;
using Content.Shared.Examine;
using Robust.Client.Graphics;
using Robust.Shared.Audio.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._EternalWar.Vision;

/// <summary>
/// A sound whose source the local player doesn't see (out of its <see cref="FieldOfViewComponent"/> cone or behind
/// a wall) leaves a fading mark where it came from: a crowbar landing behind the back, footsteps behind a wall.
/// </summary>
public sealed partial class SoundMarkSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private FieldOfViewOverlaySystem _fovOverlay = default!;
    [Dependency] private FieldOfViewSystem _fov = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>
    /// A sound that started longer ago than this is old: it came into view range already playing.
    /// </summary>
    private static readonly TimeSpan FreshTime = TimeSpan.FromSeconds(1);

    private SoundMarkOverlay _overlay = default!;

    /// <summary>
    /// Sounds already checked, so each one is marked once.
    /// </summary>
    private readonly HashSet<EntityUid> _checked = new();

    public readonly List<SoundMark> Marks = new();

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new SoundMarkOverlay(this);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlays.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        _checked.RemoveWhere(uid => TerminatingOrDeleted(uid));

        if (_fovOverlay.GetViewer() is not { } viewer)
        {
            Marks.Clear();
            _overlays.RemoveOverlay(_overlay);
            // Sounds heard while not seeing through own eyes aren't marked later either.
            var all = EntityQueryEnumerator<AudioComponent>();
            while (all.MoveNext(out var uid, out _))
            {
                _checked.Add(uid);
            }
            return;
        }

        _overlay.Color = viewer.Comp.SoundMarkColor;
        _overlay.MarkTime = viewer.Comp.SoundMarkTime;
        if (!_overlays.HasOverlay<SoundMarkOverlay>())
            _overlays.AddOverlay(_overlay);

        Marks.RemoveAll(m => now - m.Start > viewer.Comp.SoundMarkTime);

        var eye = _transform.GetMapCoordinates(viewer);
        var facing = _fov.GetFacing(viewer);
        var half = _fov.GetHalfAngles(viewer);

        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            if (!_checked.Add(uid))
                continue;

            // Music, announcements and the hum of machines aren't somewhere to look at.
            if (audio.Global
                || audio.Params.Loop
                || (audio.Flags & AudioFlags.GridAudio) != 0
                || now - audio.AudioStart > FreshTime)
            {
                continue;
            }

            var source = _transform.GetMapCoordinates(uid);
            if (source.MapId != eye.MapId
                || (source.Position - eye.Position).Length() > audio.Params.MaxDistance)
            {
                continue;
            }

            if (_fov.InView(viewer, eye.Position, facing, half, source.Position)
                && _examine.InRangeUnOccluded(eye, source, 0f, null))
            {
                continue;
            }

            Marks.Add(new SoundMark(source, now));
        }
    }

    public record struct SoundMark(MapCoordinates Coordinates, TimeSpan Start);
}
