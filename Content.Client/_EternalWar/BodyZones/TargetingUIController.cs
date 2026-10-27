using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Shared._EternalWar.BodyZones;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._EternalWar.BodyZones;

/// <summary>
/// Puts the targeting doll into the bottom right corner of the game viewport and keeps it in sync with the player.
/// </summary>
public sealed partial class TargetingUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private IPlayerManager _player = default!;

    [UISystemDependency] private readonly BodyZonesSystem? _bodyZones = default;

    private const float Margin = 10f;

    private TargetingDollWidget? _widget;

    public void OnStateEntered(GameplayState state)
    {
        UIManager.OnScreenChanged += OnScreenChanged;
        AddWidget(UIManager.ActiveScreen);
    }

    public void OnStateExited(GameplayState state)
    {
        UIManager.OnScreenChanged -= OnScreenChanged;
        RemoveWidget();
    }

    private void OnScreenChanged((UIScreen? Old, UIScreen? New) ev)
    {
        RemoveWidget();
        AddWidget(ev.New);
    }

    private void AddWidget(UIScreen? screen)
    {
        if (screen == null)
            return;

        _widget = new TargetingDollWidget();
        _widget.Doll.OnZonePressed += zone => _bodyZones?.SelectZone(zone);
        screen.AddChild(_widget);
    }

    private void RemoveWidget()
    {
        _widget?.Orphan();
        _widget = null;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_widget == null || UIManager.ActiveScreen is not { } screen)
            return;

        var player = _player.LocalEntity;
        if (_bodyZones == null
            || !EntityManager.TryGetComponent<TargetingComponent>(player, out var targeting)
            || !EntityManager.TryGetComponent<BodyZonesComponent>(player, out var zones))
        {
            _widget.Visible = false;
            return;
        }

        _widget.Visible = true;
        UpdateDoll(_widget, targeting, zones);
        PlaceWidget(_widget, screen);
    }

    private void UpdateDoll(TargetingDollWidget widget, TargetingComponent targeting, BodyZonesComponent zones)
    {
        widget.Doll.Selected = targeting.Selected;

        foreach (var zone in BodyZoneExtensions.All)
        {
            var name = Loc.GetString(zone.LocKey());
            if (_bodyZones!.IsMissing(zones, zone))
            {
                widget.Doll.Health[zone] = null;
                widget.Doll.Fractures.Remove(zone);
                widget.SetZoneText(zone, Loc.GetString("body-zones-doll-missing", ("part", name)));
                continue;
            }

            var max = _bodyZones.GetMaxHealth(zones, zone);
            var health = _bodyZones.GetHealth(zones, zone);
            widget.Doll.Health[zone] = max > 0 ? health.Float() / max.Float() : 1f;

            var text = Loc.GetString("body-zones-doll-health",
                ("part", name),
                ("health", (int) MathF.Round(health.Float())),
                ("max", max.Int()));

            if (_bodyZones.IsFractured(zones, zone))
            {
                widget.Doll.Fractures.Add(zone);
                text = Loc.GetString("body-zones-doll-fracture", ("text", text));
            }
            else
            {
                widget.Doll.Fractures.Remove(zone);
            }

            widget.SetZoneText(zone, text);
        }

        widget.UpdateLabel();
    }

    /// <summary>
    /// Bottom right of the game viewport, not of the whole window: with the separated chat the chat panel is on the right.
    /// </summary>
    private void PlaceWidget(TargetingDollWidget widget, UIScreen screen)
    {
        var viewport = FindViewport(screen) ?? (Control) screen;
        var bottomRight = (viewport.GlobalPosition - screen.GlobalPosition) + viewport.Size;
        LayoutContainer.SetPosition(widget, bottomRight - widget.Size - new Vector2(Margin, Margin));
    }

    private static MainViewport? FindViewport(Control control)
    {
        foreach (var child in control.Children)
        {
            if (child is MainViewport viewport)
                return viewport;

            if (FindViewport(child) is { } found)
                return found;
        }

        return null;
    }
}
