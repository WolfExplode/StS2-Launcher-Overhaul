using Godot;
using STS2Mobile.Launcher.Sections;

namespace STS2Mobile.Launcher.Components;

internal static partial class LauncherButtonStyles
{
    internal static void ApplyPrimaryAction(Button button, float scale)
    {
        button.SetMeta("launcher_primary_action", true);
        button.CustomMinimumSize = new Vector2(
            button.CustomMinimumSize.X,
            LauncherViewLayoutMetrics.ScaleInt(
                LauncherSectionMetrics.PrimaryButtonHeight,
                scale
            )
        );
        Apply(
            button,
            scale,
            LauncherComponentTheme.OrangeAccent,
            LauncherComponentTheme.OrangeHot,
            LauncherComponentTheme.DarkInk,
            borderWidth: 2
        );
    }

    internal static void ApplySafeAction(Button button, float scale)
        => Apply(
            button,
            scale,
            LauncherComponentTheme.CyanDim,
            LauncherComponentTheme.CyanAccent,
            LauncherComponentTheme.CyanAccent,
            borderWidth: 2,
            filled: false
        );

    internal static void ApplySupportAction(Button button, float scale)
        => Apply(
            button,
            scale,
            LauncherComponentTheme.ButtonNormal,
            LauncherComponentTheme.ButtonHover,
            LauncherComponentTheme.TextPrimary,
            borderWidth: 1
        );
}
