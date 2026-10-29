using System;
using System.Threading.Tasks;
using STS2Mobile.Patches;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher;

internal sealed partial class LauncherDownloadCoordinator
{
    internal bool TryStartAutomaticRepair(Action afterSuccess = null)
        => TryStartAutomaticRepair(
            _selectedBranch(),
            afterSuccess
        );

    internal bool TryStartAutomaticRepair(
        string branch,
        Action afterSuccess = null
    )
    {
        var started = false;
        RouteInstalledVersionReadiness(
            LocalPckRepairOperation.ClassifyForLauncherRouting(
                _model.DataDir,
                branch
            ),
            ready: () => { },
            automaticRepair: () =>
            {
                started = true;
                ShowAutomaticRepairPresentation();
                if (_automaticRepairContinuation != null
                    && _model.SelectedVersionOperationIsRunning)
                {
                    if (afterSuccess != null)
                        _automaticRepairContinuation = afterSuccess;
                    return;
                }

                _automaticRepairContinuation = afterSuccess ?? (() => { });
                _ = RunSelectedVersionOperationAsync(
                    SteamGameBranch.Normalize(branch),
                    automaticRepairOnly: true
                );
            },
            redownloadRequired: () => { }
        );
        return started;
    }

    internal void ShowAutomaticRepairPresentation()
    {
        PresentProgress(0, LocalPckRepairOperation.ProgressMessage, showDownloadStage: true);
        DownloadViewUpdate.AutomaticRepairStarted().Apply(_view);
    }

    internal bool TryShowRedownloadRequired()
        => TryShowRedownloadRequired(_selectedBranch());

    internal bool TryShowRedownloadRequired(string branch)
    {
        if (!LauncherGameFiles.HasBranchMetadataProblem(_model.DataDir, branch))
            return false;

        _view.SetStatus(
            RedownloadRequiredStatus,
            LauncherStatusSeverity.Warning
        );
        ShowRedownloadSelectedVersionAction();
        return true;
    }

    internal static void RouteInstalledVersionReadiness(
        InstalledGameVersionReadiness readiness,
        Action ready,
        Action automaticRepair,
        Action redownloadRequired
    )
    {
        if (ready == null)
            throw new ArgumentNullException(nameof(ready));
        if (automaticRepair == null)
            throw new ArgumentNullException(nameof(automaticRepair));
        if (redownloadRequired == null)
            throw new ArgumentNullException(nameof(redownloadRequired));

        switch (readiness)
        {
            case InstalledGameVersionReadiness.Ready:
                ready();
                return;
            case InstalledGameVersionReadiness.AutomaticRepair:
                automaticRepair();
                return;
            case InstalledGameVersionReadiness.RedownloadRequired:
                redownloadRequired();
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(readiness),
                    readiness,
                    "Unknown installed-version readiness."
                );
        }
    }

    private async Task DownloadAsync()
        => await RunSelectedVersionOperationAsync(
            _selectedBranch(),
            automaticRepairOnly: false
        );

    private async Task RunSelectedVersionOperationAsync(
        string branch,
        bool automaticRepairOnly
    )
    {
        try
        {
            LauncherLaunchMarkers.RecordPhase(
                automaticRepairOnly
                    ? "automatic PCK repair requested"
                    : "game download requested",
                $"branch={branch}; localRepairOnly={automaticRepairOnly}"
            );
            LauncherLaunchReadinessCache.Clear(
                automaticRepairOnly
                    ? "automatic PCK repair requested"
                    : "game download requested"
            );
            if (automaticRepairOnly)
            {
                await _model.StartAutomaticRepairAsync(branch);
            }
            else
            {
                PresentProgress(0, "Connecting to Steam...", showDownloadStage: true);
                await _model.StartDownloadAsync(branch);
            }
        }
        catch (Exception ex)
        {
            LauncherLaunchMarkers.RecordPhase(
                automaticRepairOnly
                    ? "automatic PCK repair handler failed"
                    : "game download handler failed",
                ex.GetBaseException().Message
            );
            PatchHelper.Log(
                automaticRepairOnly
                    ? $"[Launcher] Automatic PCK repair handler failed: {ex}"
                    : $"[Launcher] Download handler failed: {ex}"
            );
            FailDownload(new LauncherBranchOperationFailure(branch, ex.GetBaseException().Message));
        }
    }

    internal void UpdateDownloadProgress(DepotDownloader.DownloadProgress progress)
        => progress.ApplyTo((percentage, text) => PresentProgress(percentage, text), _view.AppendLog);

    internal void CompleteDownload(BranchInstallCompletion completion)
    {
        if (completion == null)
            throw new ArgumentNullException(nameof(completion));
        Present(_presentation with { Phase = LauncherDownloadPhase.Completed, Percentage = 0, ProgressText = "" });
        var automaticRepairContinuation = _automaticRepairContinuation;
        _automaticRepairContinuation = null;
        var branch = completion.Branch;
        LauncherLaunchMarkers.RecordPhase(
            "game download completed",
            $"branch={branch}; gameIdentity={completion.GameIdentity.Id}; transaction={completion.TransactionId:D}"
        );
        LauncherLaunchReadinessCache.Clear("game download completed");
        _refreshGameBranchOptions();
        LauncherLaunchReadiness readiness;
        try
        {
            readiness = _launch.RefreshSelectedRuntimeSlotEvidence(completion);
        }
        catch (Exception ex)
        {
            var problem = RuntimeValidationFailureMessage(branch);
            var diagnostic = RuntimeValidationFailureDiagnostic(branch, ex);
            LauncherLaunchMarkers.RecordPhase("game download runtime validation failed", diagnostic);
            PatchHelper.Log($"[Launcher] {diagnostic}");
            DownloadViewUpdate.Completed(
                filesReady: false,
                problem,
                STS2Mobile.Steam.SteamGameBranch.DisplayName(branch)
            ).Apply(_view);
            _view.ShowRetry();
            return;
        }
        var readinessProblem = readiness.Ready
            ? readiness.ReadinessProblem
            : "Download finished, but game preparation failed. Redownload the selected version. If it happens again, create a new support report.";
        DownloadViewUpdate.Completed(
            readiness.Ready,
            readinessProblem,
            STS2Mobile.Steam.SteamGameBranch.DisplayName(branch)
        ).Apply(_view);
        if (readiness.Ready)
            _launch.ShowLaunchActions(LaunchUpdateAction.Hidden);
        else
            _view.ShowRetry();
        if (!readiness.Ready && !string.IsNullOrWhiteSpace(readiness.ReadinessProblem))
            _view.AppendLog($"Preparation diagnostics: {readiness.ReadinessProblem}");
        var integritySummary = LauncherGameFiles.BranchIntegritySummary(_model.DataDir, branch);
        if (!string.IsNullOrWhiteSpace(integritySummary))
            _view.AppendLog(integritySummary);
        if (readiness.Ready)
            automaticRepairContinuation?.Invoke();
    }

    private static string RuntimeValidationFailureMessage(string branch)
        => $"Download finished for {STS2Mobile.Steam.SteamGameBranch.DisplayName(branch)}, but game preparation failed. Redownload the selected version. If it happens again, create a new support report.";

    private static string RuntimeValidationFailureDiagnostic(string branch, Exception exception)
    {
        var exceptionName = exception?.GetType().Name ?? "Exception";
        var message = string.IsNullOrWhiteSpace(exception?.Message)
            ? "No exception message was provided."
            : exception.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return $"Selected branch download completed ({STS2Mobile.Steam.SteamGameBranch.DisplayName(branch)}), but runtime validation failed ({exceptionName}: {message})";
    }

    internal void FailDownload(LauncherBranchOperationFailure failure)
    {
        _automaticRepairContinuation = null;
        var branch = failure.Branch;
        var message = LauncherBranchAvailabilityStatus.CompactFailureMessage(_model.DataDir, failure.Message);
        Present(_presentation with { Phase = LauncherDownloadPhase.Failed, ButtonText = string.IsNullOrEmpty(message) ? DownloadGameFilesButtonText : RetryDownloadButtonText, Percentage = 0, ProgressText = "" });
        LauncherLaunchMarkers.RecordPhase("game download failed", message);
        _refreshGameBranchOptions();
        DownloadViewUpdate.Failed(
            message,
            STS2Mobile.Steam.SteamGameBranch.DisplayName(branch)
        ).Apply(_view);
    }

    internal void CancelDownload(string branch)
    {
        _automaticRepairContinuation = null;
        Present(_presentation with { Phase = LauncherDownloadPhase.Idle, ButtonText = DownloadGameFilesButtonText, Percentage = 0, ProgressText = "" });
        LauncherLaunchMarkers.RecordPhase("game download cancelled", $"branch={branch}");
        DownloadViewUpdate.Cancelled().Apply(_view);
    }

    internal void ShowDownloadReadyAction()
        => PresentAction(DownloadGameFilesButtonText);

    internal void ShowRedownloadSelectedVersionAction()
        => PresentAction(RedownloadSelectedVersionButtonText);
}
