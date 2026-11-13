using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher.Sections;

internal sealed partial class CodeSection
{
    private const int CompactCodeActionRowSeparation = 6;

    private static ResponsiveGrid BuildCompactCodeActionRow(float scale)
        => new(LauncherViewLayoutMetrics.ScaleInt(CompactCodeActionRowSeparation, scale), maxColumns: 2);
}
