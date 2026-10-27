using Content.Shared._EternalWar.BodyZones;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._EternalWar.BodyZones;

/// <summary>
/// HUD panel in the bottom right corner with the targeting doll and the name and health of the zone under the cursor.
/// </summary>
public sealed class TargetingDollWidget : UIWidget
{
    public readonly TargetingDollControl Doll;
    private readonly Label _label;

    private readonly Dictionary<BodyZone, string> _zoneText = new();

    public TargetingDollWidget()
    {
        Doll = new TargetingDollControl { HorizontalAlignment = HAlignment.Center };
        _label = new Label
        {
            HorizontalAlignment = HAlignment.Center,
            StyleClasses = { "LabelSubText" },
        };

        AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#1b1d25d0"),
                BorderColor = Color.FromHex("#4a4e5e"),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 4,
            },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    MinWidth = 96,
                    Children = { Doll, _label },
                },
            },
        });

        Doll.OnZoneHovered += _ => UpdateLabel();
    }

    public void SetZoneText(BodyZone zone, string text)
    {
        _zoneText[zone] = text;
    }

    public void UpdateLabel()
    {
        var zone = Doll.Hovered ?? Doll.Selected;
        _label.Text = _zoneText.GetValueOrDefault(zone, Loc.GetString(zone.LocKey()));
    }
}
