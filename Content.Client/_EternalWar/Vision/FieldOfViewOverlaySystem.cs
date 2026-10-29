using Content.Shared._EternalWar.Vision;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Containers;

namespace Content.Client._EternalWar.Vision;

/// <summary>
/// Shows the world through the field of view of the local player (<see cref="FieldOfViewComponent"/>):
/// dims what is outside the cone and hides the mobs and items there. Hidden sprites can't be clicked either.
/// </summary>
public sealed partial class FieldOfViewOverlaySystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FieldOfViewSystem _fov = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    [Dependency] private EntityQuery<ItemComponent> _itemQuery;
    [Dependency] private EntityQuery<MobStateComponent> _mobQuery;

    /// <summary>
    /// Mobs and items further than this are off the screen anyway.
    /// </summary>
    private const float Range = 20f;

    private FieldOfViewOverlay _overlay = default!;

    /// <summary>
    /// Sprites this system hid. Only these are shown again, so sprites hidden by others stay hidden.
    /// </summary>
    private readonly HashSet<EntityUid> _hidden = new();

    private readonly HashSet<Entity<SpriteComponent>> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new FieldOfViewOverlay();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlays.RemoveOverlay(_overlay);
        ShowAll();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (GetViewer() is not { } viewer)
        {
            _overlay.Viewer = null;
            _overlays.RemoveOverlay(_overlay);
            ShowAll();
            return;
        }

        _overlay.Viewer = viewer;
        if (!_overlays.HasOverlay<FieldOfViewOverlay>())
            _overlays.AddOverlay(_overlay);

        var xform = Transform(viewer);
        var eyePos = _transform.GetWorldPosition(xform);
        var facing = _fov.GetFacing(viewer);
        var half = _fov.GetHalfAngles(viewer);

        // Hidden ones that came into view or were picked up. A container hides its contents by itself, and nothing
        // shows a sprite again when it is dropped from the hand, so it is shown now.
        _hidden.RemoveWhere(uid =>
        {
            if (TerminatingOrDeleted(uid) || !TryComp<SpriteComponent>(uid, out var sprite))
                return true;

            if (!_container.IsEntityOrParentInContainer(uid)
                && !_fov.InView(viewer, eyePos, facing, half, _transform.GetWorldPosition(uid)))
            {
                return false;
            }

            _sprite.SetVisible((uid, sprite), true);
            return true;
        });

        _nearby.Clear();
        _lookup.GetEntitiesInRange(xform.MapID, eyePos, Range, _nearby, LookupFlags.Dynamic | LookupFlags.Sundries);
        foreach (var (uid, sprite) in _nearby)
        {
            if (uid == viewer.Owner
                || !sprite.Visible
                || !_mobQuery.HasComp(uid) && !_itemQuery.HasComp(uid)
                || _container.IsEntityOrParentInContainer(uid)
                || _fov.InView(viewer, eyePos, facing, half, _transform.GetWorldPosition(uid)))
            {
                continue;
            }

            _sprite.SetVisible((uid, sprite), false);
            _hidden.Add(uid);
        }
    }

    /// <summary>
    /// The local player's mob, if it sees through its own eyes: alive, not inside something, not looking through
    /// a camera.
    /// </summary>
    public Entity<FieldOfViewComponent>? GetViewer()
    {
        if (_player.LocalEntity is not { } player
            || !TryComp<FieldOfViewComponent>(player, out var fov)
            || !_mobState.IsAlive(player)
            || _container.IsEntityOrParentInContainer(player)
            || !TryComp<EyeComponent>(player, out var eye)
            || eye.Eye != _eye.CurrentEye)
        {
            return null;
        }

        return (player, fov);
    }

    private void ShowAll()
    {
        foreach (var uid in _hidden)
        {
            if (!TerminatingOrDeleted(uid) && TryComp<SpriteComponent>(uid, out var sprite))
                _sprite.SetVisible((uid, sprite), true);
        }

        _hidden.Clear();
    }
}
