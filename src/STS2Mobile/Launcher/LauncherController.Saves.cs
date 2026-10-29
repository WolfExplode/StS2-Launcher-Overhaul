using System;
using System.Threading;
using System.Threading.Tasks;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher;

internal sealed partial class LauncherController
{
    private enum SaveOperation
    {
        None,
        Sync,
        ProfileCopy,
    }

    // Set and cleared on the main thread. An operation ends only once its
    // result has been applied, so the next one never starts before that.
    private SaveOperation _saveOperation;
    private bool _saveSyncConflict;
    private int _saveSyncUiAvailable = 1;
    private int _launchAfterSaveSyncPending;
    private int _automaticSaveSyncStarted;
    private CancellationTokenSource _saveSyncCancellation;
    private Task _saveSyncTask = Task.CompletedTask;

    private void LaunchPressed()
        => LaunchAfterSaveSync(() => LaunchOrRepair(_launch.LaunchPressed));

    private void SafeLaunchPressed()
        => LaunchAfterSaveSync(() => LaunchOrRepair(_launch.SafeLaunchPressed));

    private void LaunchOrRepair(Action launch)
    {
        if (_downloads.TryStartAutomaticRepair(launch)
            || _downloads.TryShowRedownloadRequired())
        {
            return;
        }

        launch();
    }

    private void SessionRetryPressed()
    {
        var fastPathReady = _session.RetryPressed();
        RefreshSaveSyncPresentation();
        if (fastPathReady)
            StartAutomaticSaveSync();
    }

    internal void CancelSaveSyncForLauncherExit()
    {
        Volatile.Write(ref _saveSyncUiAvailable, 0);
        CancelActiveSaveSync();
    }

    private void SaveSyncNowPressed()
        => StartSaveSync(SaveSyncService.SyncRequest.Reconcile, overwriteConfirmed: false);

    private void SavePullPressed()
        => StartSaveSync(SaveSyncService.SyncRequest.Pull, overwriteConfirmed: false);

    private void SavePushPressed()
        => StartSaveSync(SaveSyncService.SyncRequest.Push, overwriteConfirmed: false);

    private void StartAutomaticSaveSync()
    {
        if (!SteamSaveSyncSettings.IsEnabled(AppPaths.AppPrivateDataDir) || !CanStartSaveOperation)
            return;
        if (Interlocked.CompareExchange(ref _automaticSaveSyncStarted, 1, 0) != 0)
            return;

        StartSaveSync(
            SaveSyncService.SyncRequest.Reconcile,
            overwriteConfirmed: false
        );
    }

    // The Saves page is drawn from the sync preference, the running operation,
    // an unresolved conflict and the service's persisted status. Nothing else
    // is remembered, so every caller gets the same page for the same state.
    private void RefreshSaveSyncPresentation()
        => _view.SetSaveSyncPresentation(CurrentSaveSyncPresentation(), busy: !CanStartSaveOperation);

    // Launch cancels a running sync before it starts the game, so none may
    // start while the game is starting from the local saves.
    private bool CanStartSaveOperation
        => _saveOperation == SaveOperation.None && !_launch.LaunchInProgress;

    private LauncherSaveSyncPresentation CurrentSaveSyncPresentation()
    {
        if (!SteamSaveSyncSettings.IsEnabled(AppPaths.AppPrivateDataDir))
            return LauncherSaveSyncPresentation.Disabled();
        if (_saveOperation == SaveOperation.Sync)
            return LauncherSaveSyncPresentation.Syncing();
        if (_saveSyncConflict)
            return LauncherSaveSyncPresentation.Conflict();
        return LauncherSaveSyncPresentation.FromStatus(SaveSyncService.GetStatusSnapshot());
    }

    private void StartSaveSync(
        SaveSyncService.SyncRequest request,
        bool overwriteConfirmed
    )
    {
        if (!CanStartSaveOperation)
            return;
        if (!SteamSaveSyncSettings.IsEnabled(AppPaths.AppPrivateDataDir)
            || !SaveSyncService.TryGetActive(out var service))
        {
            RefreshSaveSyncPresentation();
            return;
        }

        var cancellation = new CancellationTokenSource();
        _saveSyncCancellation = cancellation;
        _saveOperation = SaveOperation.Sync;
        RefreshSaveSyncPresentation();
        _saveSyncTask = RunSaveSyncAsync(
            service,
            request,
            overwriteConfirmed,
            cancellation.Token
        );
    }

    private async Task RunSaveSyncAsync(
        SaveSyncService service,
        SaveSyncService.SyncRequest request,
        bool overwriteConfirmed,
        CancellationToken cancellationToken
    )
    {
        SaveSyncService.SyncResult result;
        try
        {
            result = await Task.Run(() => service.SyncAsync(
                request,
                overwriteConfirmed,
                localWritesAreStopped: true,
                cancellationToken
            )).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PatchHelper.Log(
                $"[Cloud] Manual synchronization task failed: {ex.GetType().Name}"
            );
            await SaveSyncService.ReportFailureAsync(SaveSyncService.SyncFailureKind.Other).ConfigureAwait(false);
            result = default;
        }

        DisposeSaveSyncCancellation();
        QueueSaveSyncUiCompletion(() => FinishSaveSync(request, result));
    }

    private void FinishSaveSync(
        SaveSyncService.SyncRequest request,
        SaveSyncService.SyncResult result
    )
    {
        _saveOperation = SaveOperation.None;
        // A canceled sync or an unanswered overwrite question leaves the
        // previous conflict standing; any other outcome replaces it. Failures
        // need no handling here: the service records them in its status.
        if (!result.Canceled && result.Prompt != SaveSyncService.SyncPrompt.ConfirmOverwrite)
            _saveSyncConflict = result.Prompt == SaveSyncService.SyncPrompt.ChooseSource;
        RefreshSaveSyncPresentation();

        if (!SteamSaveSyncSettings.IsEnabled(AppPaths.AppPrivateDataDir))
            return;

        if (result.Prompt == SaveSyncService.SyncPrompt.ChooseSource)
        {
            _view.ShowSaveSyncConflict(
                () => StartSaveSync(
                    SaveSyncService.SyncRequest.Pull,
                    overwriteConfirmed: true
                ),
                () => StartSaveSync(
                    SaveSyncService.SyncRequest.Push,
                    overwriteConfirmed: true
                )
            );
        }
        else if (result.Prompt == SaveSyncService.SyncPrompt.ConfirmOverwrite)
        {
            Action confirm = () => StartSaveSync(request, overwriteConfirmed: true);
            if (request == SaveSyncService.SyncRequest.Pull)
                _view.ShowGetSavesOverwriteConfirmation(confirm, RefreshSaveSyncPresentation);
            else
                _view.ShowSendSavesOverwriteConfirmation(confirm, RefreshSaveSyncPresentation);
        }
    }

    private void QueueSaveSyncUiCompletion(Action completion)
    {
        if (Volatile.Read(ref _saveSyncUiAvailable) == 0)
            return;

        _runOnMainThread(() =>
        {
            if (Volatile.Read(ref _saveSyncUiAvailable) != 0)
                completion();
        });
    }

    private void SaveSyncPreferenceChanged(bool enabled)
    {
        try
        {
            SteamSaveSyncSettings.SetEnabled(AppPaths.AppPrivateDataDir, enabled);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Cloud] Could not save sync preference: {ex.Message}");
        }

        if (!SteamSaveSyncSettings.IsEnabled(AppPaths.AppPrivateDataDir))
        {
            CancelActiveSaveSync();
            if (SaveSyncService.TryGetActive(out var service))
                service.PauseAutomaticPush();
            _saveSyncConflict = false;
            RefreshSaveSyncPresentation();
        }
        else
        {
            RefreshSaveSyncPresentation();
            Interlocked.Exchange(ref _automaticSaveSyncStarted, 0);
            StartAutomaticSaveSync();
        }
    }

    private void LaunchAfterSaveSync(Action launch)
    {
        if (Interlocked.CompareExchange(ref _launchAfterSaveSyncPending, 1, 0) != 0)
            return;

        var syncTask = CancelActiveSaveSync();
        if (syncTask == null || syncTask.IsCompleted)
        {
            Interlocked.Exchange(ref _launchAfterSaveSyncPending, 0);
            launch();
            return;
        }

        _view.SetStatus("Finishing save synchronization before launch...", LauncherStatusSeverity.Working);
        _ = CompleteLaunchAfterSaveSyncAsync(syncTask, launch);
    }

    private async Task CompleteLaunchAfterSaveSyncAsync(Task syncTask, Action launch)
    {
        var cleanupFinished = true;
        try
        {
            cleanupFinished = await LauncherSaveCleanup.WaitAsync(syncTask, TimeSpan.FromSeconds(15)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Cloud] Manual synchronization cleanup failed: {ex.GetType().Name}");
        }

        _runOnMainThread(() =>
        {
            Interlocked.Exchange(ref _launchAfterSaveSyncPending, 0);
            if (Volatile.Read(ref _saveSyncUiAvailable) != 0)
            {
                if (cleanupFinished) launch();
                else _view.SetStatus("Save synchronization is still finishing. Launch was stopped to protect your saves. Wait, then press Play again.", LauncherStatusSeverity.Warning);
            }
        });
    }

    private Task CancelActiveSaveSync()
    {
        try
        {
            Volatile.Read(ref _saveSyncCancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        return Volatile.Read(ref _saveSyncTask);
    }

    private void DisposeSaveSyncCancellation()
        => Interlocked.Exchange(ref _saveSyncCancellation, null)?.Dispose();
}
