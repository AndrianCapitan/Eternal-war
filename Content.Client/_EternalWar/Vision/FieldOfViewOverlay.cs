using System.Numerics;
using Content.Shared._EternalWar.Vision;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._EternalWar.Vision;

/// <summary>
/// Dims the world outside the field of view cone of the local player.
/// </summary>
public sealed class FieldOfViewOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "EternalWarFieldOfView";

    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly FieldOfViewSystem _fov;
    private readonly SharedTransformSystem _transform;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    /// <summary>
    /// Whose eyes the world is seen through.
    /// </summary>
    public EntityUid? Viewer;

    public FieldOfViewOverlay()
    {
        IoCManager.InjectDependencies(this);
        _fov = _entMan.System<FieldOfViewSystem>();
        _transform = _entMan.System<SharedTransformSystem>();
        _shader = _proto.Index(Shader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // Only the main view, not camera monitors and other viewports.
        return Viewer is { } viewer
               && _entMan.HasComponent<FieldOfViewComponent>(viewer)
               && args.Viewport.Eye == _eye.CurrentEye
               && _transform.GetMapId(viewer) == args.MapId;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (Viewer is not { } viewer || !_entMan.TryGetComponent<FieldOfViewComponent>(viewer, out var comp))
            return;

        var pos = _transform.GetWorldPosition(viewer);
        var facing = _fov.GetFacing(viewer);
        var left = new Vector2(-facing.Y, facing.X);
        var (halfLeft, halfRight) = _fov.GetHalfAngles((viewer, comp));

        var viewport = args.Viewport;
        Vector2 ToFragment(Vector2 world)
        {
            var local = viewport.WorldToLocal(world);
            local.Y = viewport.Size.Y - local.Y;
            return local;
        }

        var center = ToFragment(pos);
        _shader.SetParameter("center", center);
        _shader.SetParameter("forward", Vector2.Normalize(ToFragment(pos + facing) - center));
        _shader.SetParameter("leftDir", Vector2.Normalize(ToFragment(pos + left) - center));
        _shader.SetParameter("halfLeft", (float) halfLeft.Theta);
        _shader.SetParameter("halfRight", (float) halfRight.Theta);
        _shader.SetParameter("edge", (float) comp.EdgeSoftness.Theta);
        _shader.SetParameter("nearRadius", (ToFragment(pos + facing * comp.NearRadius) - center).Length());
        _shader.SetParameter("darkness", comp.Darkness);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }
}
