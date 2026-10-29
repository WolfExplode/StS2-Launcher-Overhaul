using System;

namespace STS2Mobile.Launcher;

// Callbacks for the user actions LauncherView exposes. Each one is set by name
// so a missing or reordered handler cannot silently wire the wrong button.
internal sealed class LauncherViewHandlers
{
    internal Action<string, string> LoginRequested { get; init; }
    internal Action<string> CodeSubmitted { get; init; }
    internal Action DownloadRequested { get; init; }
    internal Action<string> GameBranchChanged { get; init; }
    internal Action<string> RendererModeChanged { get; init; }
    internal Action LaunchPressed { get; init; }
    internal Action SafeLaunchPressed { get; init; }
    internal Action RetryPressed { get; init; }
    internal Action CheckForUpdatesPressed { get; init; }
    internal Action UpdateSelectedVersionPressed { get; init; }
    internal Action RefreshGameVersionsPressed { get; init; }
    internal Action RedownloadPressed { get; init; }
    internal Action DiagnosticsPressed { get; init; }
    internal Action ShowLastErrorPressed { get; init; }
    internal Action CopyRawLogPressed { get; init; }
    internal Action ReportBugPressed { get; init; }
    internal Action CheckAppUpdatesPressed { get; init; }
    internal Action SaveSyncNowPressed { get; init; }
    internal Action SavePullPressed { get; init; }
    internal Action SavePushPressed { get; init; }
    internal Action SaveSyncPreferenceChanged { get; init; }
    internal Action CopyVanillaToModdedPressed { get; init; }
    internal Action WorkshopSyncPressed { get; init; }
    internal Action WorkshopClearPressed { get; init; }
    internal Action ModsSelectionChanged { get; init; }
}
