namespace STS2Mobile.Launcher;

internal readonly struct DownloadViewUpdate
{
    private DownloadViewUpdate(
        string status = null,
        LauncherStatusSeverity? statusSeverity = null,
        string logMessage = null,
        string downloadAction = null,
        string downloadProgress = null,
        string resetDownloadButton = null,
        LaunchUpdateAction? launchAction = null,
        bool? downloadButtonDisabled = null,
        bool showRetry = false,
        bool resetDownload = false
    )
    {
        Status = status;
        StatusSeverity = statusSeverity;
        LogMessage = logMessage;
        DownloadAction = downloadAction;
        DownloadProgress = downloadProgress;
        ResetDownloadButton = resetDownloadButton;
        LaunchAction = launchAction;
        DownloadButtonDisabled = downloadButtonDisabled;
        ShowRetry = showRetry;
        ResetDownload = resetDownload;
    }

    private string Status { get; }
    private LauncherStatusSeverity? StatusSeverity { get; }
    private string LogMessage { get; }
    private string DownloadAction { get; }
    private string DownloadProgress { get; }
    private string ResetDownloadButton { get; }
    private LaunchUpdateAction? LaunchAction { get; }
    private bool? DownloadButtonDisabled { get; }
    private bool ShowRetry { get; }
    private bool ResetDownload { get; }

    internal static DownloadViewUpdate Ready(
        string buttonText = LauncherDownloadCoordinator.DownloadGameFilesButtonText
    )
        => new(
            downloadAction: buttonText,
            downloadButtonDisabled: false
        );

    internal static DownloadViewUpdate RedownloadApplied()
        => new(
            status: LauncherDownloadCoordinator.RedownloadStatusMessage,
            statusSeverity: LauncherStatusSeverity.Warning,
            logMessage: LauncherDownloadCoordinator.RedownloadLogMessage,
            downloadAction: LauncherDownloadCoordinator.DownloadGameFilesButtonText,
            downloadButtonDisabled: false
        );

    internal static DownloadViewUpdate AutomaticRepairStarted()
        => new(
            status: LocalPckRepairOperation.ProgressMessage,
            statusSeverity: LauncherStatusSeverity.Working,
            logMessage: LocalPckRepairOperation.ProgressMessage,
            downloadAction: LocalPckRepairOperation.ProgressMessage,
            downloadProgress: LocalPckRepairOperation.ProgressMessage
        );

    internal static DownloadViewUpdate Completed(
        bool filesReady,
        string readinessProblem,
        string selectedVersion
    )
        => new(
            status: filesReady
                ? $"Selected version prepared and checked ({selectedVersion}). Runtime preparation is checked again before launch."
                : readinessProblem,
            statusSeverity: filesReady
                ? LauncherStatusSeverity.Ready
                : LauncherStatusSeverity.Error,
            launchAction: filesReady
                ? LaunchUpdateAction.Hidden
                : (LaunchUpdateAction?)null,
            showRetry: !filesReady
        );

    internal static DownloadViewUpdate Failed(string message, string selectedVersion)
        => string.IsNullOrEmpty(message)
            ? new(resetDownload: true)
            : new(
                status: $"Download failed for the selected branch ({selectedVersion}). Try again. If it repeats, create a new support report.",
                statusSeverity: LauncherStatusSeverity.Error,
                resetDownloadButton: LauncherDownloadCoordinator.RetryDownloadButtonText
            );

    internal static DownloadViewUpdate Cancelled()
        => new(
            status: LauncherDownloadCoordinator.DownloadCancelledStatus,
            statusSeverity: LauncherStatusSeverity.Information,
            downloadButtonDisabled: false
        );

    // Stage changes come first (download action, then progress) so the
    // status, log, and button state below apply to the stage being shown.
    internal void Apply(LauncherView view, LauncherLaunchCoordinator launch)
    {
        if (DownloadAction != null)
            view.ShowDownloadAction(DownloadAction);

        if (DownloadProgress != null)
            view.ShowDownloadProgress(DownloadProgress);

        if (DownloadButtonDisabled.HasValue)
            view.SetDownloadButtonDisabled(DownloadButtonDisabled.Value);

        if (Status != null)
            view.SetStatus(
                Status,
                StatusSeverity
                    ?? throw new System.InvalidOperationException(
                        "A download status message requires an explicit severity."
                    )
            );

        if (LogMessage != null)
            view.AppendLog(LogMessage);

        if (ResetDownload)
            view.ResetDownload();
        else if (ResetDownloadButton != null)
            view.ResetDownload(ResetDownloadButton);

        if (LaunchAction.HasValue)
            launch.ShowLaunchActions(LaunchAction.Value);

        if (ShowRetry)
            view.ShowRetry();
    }
}
