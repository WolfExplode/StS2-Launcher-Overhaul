using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher;

internal sealed partial class LauncherController
{
    private void CopyVanillaToModdedPressed()
    {
        if (Volatile.Read(ref _saveSyncRunning) != 0)
            return;
        _view.ShowConfirmation(
            "Copy all vanilla profiles on this device to their matching modded profiles? Existing matching modded profiles will be replaced and backed up. Progress and history are copied; active runs are excluded, and any active modded runs in the replaced profiles are cleared. Vanilla profiles stay unchanged. Copied saves will be included in your next Steam sync.",
            StartVanillaProfileCopy,
            confirmText: "Copy Vanilla to Modded",
            cancelText: "Cancel"
        );
    }

    private void StartVanillaProfileCopy()
    {
        if (Interlocked.CompareExchange(ref _saveSyncRunning, 1, 0) != 0)
            return;
        try
        {
            var root = OS.GetUserDataDir();
            // Android uses platform None and player 0. Resolve that namespace
            // without game methods: even explicit arguments can make Mono load
            // their desktop Steam dependencies when compiling the method.
            var account = VanillaProfileCopy.ResolveAndroidAccountPath(root, OS.HasFeature("editor"));
            _view.SetSaveSyncControlsDisabled(true);
            _view.SetStatus("Copying vanilla profiles to modded...", LauncherStatusSeverity.Working);
            var cancellation = new CancellationTokenSource();
            _saveSyncCancellation = cancellation;
            _saveSyncTask = RunVanillaProfileCopyAsync(root, account, cancellation.Token);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Save] Vanilla profile copy could not start: {ex}");
            ReleaseSaveSyncOperation();
            _view.SetSaveSyncControlsDisabled(false);
            _view.SetStatus(ex.Message, LauncherStatusSeverity.Warning);
        }
    }

    private async Task RunVanillaProfileCopyAsync(string root, string account, CancellationToken cancellationToken)
    {
        string message;
        var severity = LauncherStatusSeverity.Ready;
        try
        {
            var count = await Task.Run(() => SaveSyncService.CopyVanillaProfilesAsync(
                root, account, cancellationToken
            )).ConfigureAwait(false);
            message = $"Copied {count} vanilla profile(s) to modded. Start a new modded run to use your progress.";
        }
        catch (OperationCanceledException)
        {
            message = "Profile copy canceled.";
            severity = LauncherStatusSeverity.Information;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Save] Vanilla profile copy failed: {ex}");
            message = $"Could not copy profiles: {ex.Message}";
            severity = LauncherStatusSeverity.Warning;
        }
        ReleaseSaveSyncOperation();
        QueueSaveSyncUiCompletion(() =>
        {
            _view.SetSaveSyncControlsDisabled(false);
            RefreshSaveSyncPresentation();
            _view.SetStatus(message, severity);
        });
    }
}
