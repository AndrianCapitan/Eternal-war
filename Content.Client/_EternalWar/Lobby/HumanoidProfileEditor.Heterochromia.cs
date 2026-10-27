namespace Content.Client.Lobby.UI;

/// <summary>
/// Eternal War: heterochromia, a separate color for the right eye.
/// </summary>
public sealed partial class HumanoidProfileEditor
{
    private void InitializeHeterochromia()
    {
        HeterochromiaCheckBox.OnToggled += args =>
        {
            if (Profile is null)
                return;

            // The right eye starts out as the left one, the player then picks its color.
            Profile = Profile.WithCharacterAppearance(
                Profile.Appearance.WithRightEyeColor(args.Pressed ? Profile.Appearance.EyeColor : null));
            UpdateHeterochromia();
            ReloadProfilePreview();
        };

        RightEyeColorPicker.OnEyeColorPicked += newColor =>
        {
            if (Profile?.Appearance.RightEyeColor is null)
                return;

            Profile = Profile.WithCharacterAppearance(
                Profile.Appearance.WithRightEyeColor(newColor));
            ReloadProfilePreview();
        };
    }

    private void UpdateHeterochromia()
    {
        if (Profile is null)
            return;

        var right = Profile.Appearance.RightEyeColor;
        HeterochromiaCheckBox.Pressed = right != null;
        RightEyeContainer.Visible = right != null;
        EyesLabel.Text = Loc.GetString(right != null ? "ew-profile-editor-left-eye-label" : "humanoid-profile-editor-eyes-label");

        if (right is { } color)
            RightEyeColorPicker.SetData(color);
    }
}
