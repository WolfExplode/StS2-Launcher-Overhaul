using System;
using System.Linq;
using System.Threading.Tasks;
using STS2Mobile.Patches;
using STS2Mobile.Steam.Workshop;

namespace STS2Mobile.Launcher;

internal sealed class LauncherWorkshopCoordinator
{
    private const string WorkshopModConsentMessage =
        "Steam Workshop mods can run community code and change game content. Updating uses your subscribed Steam Workshop items only.";
    private readonly LauncherModel _model;
    private readonly LauncherView _view;
    private readonly Action _refreshModsPresentation;
    private readonly Action<Action> _runOnMainThread;
    private long _nextConnectionPoll;
    private string _connectionStatus;

    internal LauncherWorkshopCoordinator(
        LauncherModel model,
        LauncherView view,
        Action refreshModsPresentation,
        Action<Action> runOnMainThread
    )
    {
        _model = model;
        _view = view;
        _refreshModsPresentation = refreshModsPresentation ?? (() => { });
        _runOnMainThread = runOnMainThread;
    }

    internal void SyncPressed() => _ = RunSyncAsync();

    internal void PollConnection()
    {
        var now = Environment.TickCount64;
        if (now < _nextConnectionPoll)
            return;

        _nextConnectionPoll = now + 250;
        var status = _model.WorkshopConnectionStatus;
        if (status == _connectionStatus)
            return;

        _connectionStatus = status;
        _view.SetWorkshopConnectionStatus(status);
    }

    internal void ShowProgress(string message) => _view.SetWorkshopStatus(message);

    private Task<bool> ConfirmChangesAsync(SteamWorkshopUpdatePlan plan)
    {
        var choice = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _runOnMainThread(() =>
        {
            if (!_view.LaunchLifetimeHost.IsInsideTree())
            {
                choice.TrySetResult(false);
                return;
            }

            var summary = LauncherModel.WorkshopScanSummary(plan);
            ShowProgress(summary);
            var changes = plan.Items.Where(item => item.NeedsDownload || item.Revision == null).Select(item =>
                item.Revision == null ? $"Could not check · {item.Title} ({item.Error})"
                    : item.Installed == null ? $"Install · {item.Title}" : $"Update · {item.Title}");
            var removals = plan.PotentialRemovals.Select(item =>
                $"Remove if no longer needed · {item.Title}");
            var details = string.Join("\n", changes.Concat(removals));
            var action = plan.Items.Any(item => item.NeedsDownload) ? "Install changes" : "Apply changes";
            _view.ShowWorkshopConfirmation($"{summary}\n\n{details}\n\n{WorkshopModConsentMessage}", action, choice);
        });
        return choice.Task;
    }

    internal void ClearPressed()
        => ShowRemoveDownloadedModsConfirmation(_view, ClearMods);

    internal static void ShowRemoveDownloadedModsConfirmation(
        LauncherView view,
        Action onConfirmed
    )
        => view.ShowConfirmation(
            "Remove the launcher's local Workshop mod copies? Your selections and cached downloads remain, but these mods cannot load until Workshop mods are updated again.",
            onConfirmed,
            "Remove downloaded Workshop mods",
            "Cancel"
        );

    private async Task RunSyncAsync()
    {
        try
        {
            LauncherLaunchMarkers.RecordPhase("workshop sync requested");
            _view.SetWorkshopButtonsDisabled(true);
            ShowProgress("Connecting to Steam to check for Workshop updates…");
            await _model.StartWorkshopSyncAsync(ConfirmChangesAsync);
        }
        catch (Exception ex)
        {
            LauncherLaunchMarkers.RecordPhase("workshop sync failed", ex.GetBaseException().Message);
            PatchHelper.Log($"[Launcher] Workshop sync handler failed: {ex}");
            FailSync(ex.GetBaseException().Message);
        }
        finally
        {
            _view.SetWorkshopButtonsDisabled(false);
        }
    }

    internal void CompleteSync(string summary)
    {
        LauncherLaunchMarkers.RecordPhase("workshop sync completed", summary);
        var detail = string.IsNullOrWhiteSpace(summary) ? "Workshop mods updated" : summary;
        ShowProgress(detail);
        _view.AppendLog(detail);
        RefreshModsPresentation();
    }

    internal void FailSync(string message)
    {
        ShowProgress($"Workshop check or update failed · {message}");
        _view.AppendLog($"Workshop check or update failed · {message}");
        RefreshModsPresentation();
    }

    private void ClearMods()
    {
        try
        {
            LauncherLaunchMarkers.RecordPhase("workshop clear requested");
            _view.SetWorkshopButtonsDisabled(true);
            ShowProgress("Removing Workshop mods from the launcher…");
            _view.AppendLog("Removing Workshop mods from the launcher.");
            _model.ClearWorkshopMods();
        }
        catch (Exception ex)
        {
            LauncherLaunchMarkers.RecordPhase("workshop clear failed", ex.GetBaseException().Message);
            PatchHelper.Log($"[Launcher] Workshop clear handler failed: {ex}");
            FailClear(ex.GetBaseException().Message);
        }
        finally
        {
            _view.SetWorkshopButtonsDisabled(false);
        }
    }

    internal void CompleteClear(int removedCount)
    {
        LauncherLaunchMarkers.RecordPhase("workshop clear completed", $"removedCount={removedCount}");
        ShowProgress($"Removed {removedCount} Workshop mod entries. Restart the game if it was already running.");
        _view.AppendLog(
            $"Removed {removedCount} Workshop mod entries from the launcher."
        );
        RefreshModsPresentation();
    }

    internal void FailClear(string message)
    {
        ShowProgress($"Workshop mod removal failed · {message}");
        _view.AppendLog($"Workshop mod clear failed: {message}");
        RefreshModsPresentation();
    }

    private void RefreshModsPresentation()
    {
        try
        {
            _refreshModsPresentation();
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Launcher] Mods presentation refresh failed: {ex.Message}");
        }
    }
}
