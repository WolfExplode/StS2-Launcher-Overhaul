namespace STS2Mobile.Launcher;

internal sealed partial class LauncherController
{
    private void WireViewEvents()
    {
        _view.WireEvents(new LauncherViewHandlers
        {
            LoginRequested = _session.LoginPressed,
            CodeSubmitted = _session.CodeSubmitPressed,
            DownloadRequested = _downloads.DownloadPressed,
            GameBranchChanged = _branchSwitch.GameBranchChanged,
            RendererModeChanged = RendererModeChanged,
            LaunchPressed = LaunchPressed,
            SafeLaunchPressed = SafeLaunchPressed,
            RetryPressed = SessionRetryPressed,
            CheckForUpdatesPressed = _updates.RunUpdateCheck,
            UpdateSelectedVersionPressed = _downloads.UpdateSelectedVersionPressed,
            RefreshGameVersionsPressed = _versions.RunBranchCatalogRefresh,
            RedownloadPressed = _downloads.RedownloadPressed,
            DiagnosticsPressed = _diagnostics.DiagnosticsPressed,
            ShowLastErrorPressed = _diagnostics.ShowLastErrorPressed,
            CopyRawLogPressed = _diagnostics.CopyRawLogPressed,
            ReportBugPressed = _diagnostics.ReportBugPressed,
            CheckAppUpdatesPressed = _updates.RunAppUpdateCheck,
            SaveSyncNowPressed = SaveSyncNowPressed,
            SavePullPressed = SavePullPressed,
            SavePushPressed = SavePushPressed,
            SaveSyncPreferenceChanged = SaveSyncPreferenceChanged,
            CopyVanillaToModdedPressed = CopyVanillaToModdedPressed,
            WorkshopSyncPressed = _workshop.SyncPressed,
            WorkshopClearPressed = _workshop.ClearPressed,
            ModsSelectionChanged = RefreshModsPresentation,
        });
        _view.DestinationSelected += OnDestinationSelected;
    }
}
