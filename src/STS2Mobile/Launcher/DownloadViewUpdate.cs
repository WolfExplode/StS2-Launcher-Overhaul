namespace STS2Mobile.Launcher;

internal readonly struct DownloadViewUpdate
{
    private DownloadViewUpdate(
        string status = null,
        LauncherStatusSeverity? statusSeverity = null,
        string logMessage = null
    )
    {
        Status = status;
        StatusSeverity = statusSeverity;
        LogMessage = logMessage;
    }

    private string Status { get; }
    private LauncherStatusSeverity? StatusSeverity { get; }
    private string LogMessage { get; }

    internal static DownloadViewUpdate RedownloadApplied()
        => new(
            status: LauncherDownloadCoordinator.RedownloadStatusMessage,
            statusSeverity: LauncherStatusSeverity.Warning,
            logMessage: LauncherDownloadCoordinator.RedownloadLogMessage
        );

    internal static DownloadViewUpdate AutomaticRepairStarted()
        => new(
            status: LocalPckRepairOperation.ProgressMessage,
            statusSeverity: LauncherStatusSeverity.Working,
            logMessage: LocalPckRepairOperation.ProgressMessage
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
                : LauncherStatusSeverity.Error
        );

    internal static DownloadViewUpdate Failed(string message, string selectedVersion)
        => string.IsNullOrEmpty(message)
            ? new()
            : new(
                status: $"Download failed for the selected branch ({selectedVersion}). Try again. If it repeats, create a new support report.",
                statusSeverity: LauncherStatusSeverity.Error
            );

    internal static DownloadViewUpdate Cancelled()
        => new(
            status: LauncherDownloadCoordinator.DownloadCancelledStatus,
            statusSeverity: LauncherStatusSeverity.Information
        );

    internal void Apply(LauncherView view)
    {
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

    }
}
