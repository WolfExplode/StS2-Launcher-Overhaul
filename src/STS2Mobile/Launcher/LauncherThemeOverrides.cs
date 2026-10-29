using Godot;

namespace STS2Mobile.Launcher;

// Typed theme constant overrides. Each container type reads different
// constant names, and Godot silently ignores a name a control does not use:
// "separation" on a GridContainer did nothing. These helpers only accept the
// container types that read the constant, so a mismatch fails to compile.
internal static class LauncherThemeOverrides
{
    internal static void SetSeparation(this BoxContainer box, int separation)
        => box.AddThemeConstantOverride("separation", separation);

    internal static void SetSeparation(this GridContainer grid, int horizontal, int vertical)
    {
        grid.AddThemeConstantOverride("h_separation", horizontal);
        grid.AddThemeConstantOverride("v_separation", vertical);
    }

    internal static void SetColumnSeparation(this GridContainer grid, int separation)
        => grid.AddThemeConstantOverride("h_separation", separation);

    internal static void SetMargins(this MarginContainer margin, int left, int top, int right, int bottom)
    {
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
    }
}
