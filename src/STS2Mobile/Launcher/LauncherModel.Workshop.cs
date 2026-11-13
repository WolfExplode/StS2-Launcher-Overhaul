using System;
using System.Linq;
using System.Threading.Tasks;
using STS2Mobile.Steam;
using STS2Mobile.Steam.Workshop;

namespace STS2Mobile.Launcher;

internal partial class LauncherModel
{
    internal async Task StartWorkshopSyncAsync(Func<SteamWorkshopUpdatePlan, Task<bool>> confirm)
    {
        var run = DownloadRunGuard.TryAcquire(this);
        if (!run.Acquired)
        {
            RaiseWorkshopSyncFailed("Another Steam file operation is already running");
            return;
        }

        try
        {
            var branch = LauncherPreferences.ReadGameBranch();
            var connection = await GetDepotConnectionAsync().ConfigureAwait(false);
            if (connection == null)
            {
                RaiseWorkshopSyncFailed("Steam is disconnected. Sign in on Home and try again.");
                return;
            }

            await Task.Run(() => ScanAndSyncWorkshopAsync(connection, branch, confirm)).ConfigureAwait(false);
        }
        finally
        {
            run.Release();
        }
    }

    internal void ClearWorkshopMods()
    {
        var run = DownloadRunGuard.TryAcquire(this);
        if (!run.Acquired)
        {
            RaiseWorkshopClearFailed("Another Steam file operation is already running");
            return;
        }

        try
        {
            var stager = new SteamWorkshopStager();
            var removed = stager.ClearStagedMods();
            RaiseWorkshopSyncLogReceived(
                $"Workshop mods cleared: retired {removed} installations; file cleanup will run after restart."
            );
            RaiseWorkshopClearCompleted(removed);
        }
        catch (System.Exception ex)
        {
            RaiseWorkshopClearFailed(ex.GetBaseException().Message);
        }
        finally
        {
            run.Release();
        }
    }

    internal string WorkshopConnectionStatus
        => _steamSession.TryGetConnection(out var connection) && connection.IsConnected
            ? "Steam connected"
            : IsConnectionAttemptState(CurrentSessionState) || connection?.IsConnecting == true
                ? "Connecting to Steam…"
                : "Steam disconnected";

    private async Task ScanAndSyncWorkshopAsync(SteamConnection connection, string branch,
        Func<SteamWorkshopUpdatePlan, Task<bool>> confirm)
    {
        var service = new SteamWorkshopSyncService(connection);
        service.LogMessage += RaiseWorkshopSyncLogReceived;
        service.StatusChanged += message => Raise(WorkshopSyncStatusChanged, message, nameof(WorkshopSyncStatusChanged));
        var plan = await service.ScanAsync(branch).ConfigureAwait(false);
        if (plan.NeedsConfirmation && !await confirm(plan).ConfigureAwait(false))
        {
            Raise(WorkshopSyncStatusChanged, $"Workshop changes skipped · {WorkshopScanSummary(plan)}",
                nameof(WorkshopSyncStatusChanged));
            return;
        }

        if (!string.Equals(branch, LauncherPreferences.ReadGameBranch(), StringComparison.Ordinal))
            throw new InvalidOperationException("The game branch changed. Check Workshop updates again.");

        var manifest = await service.ApplyAsync(plan).ConfigureAwait(false);
        var installed = manifest.Items.Where(item => item.Status == "staged").ToDictionary(item => item.PublishedFileId);
        var completed = plan.Items.Where(item => item.NeedsDownload && installed.ContainsKey(item.Id)).ToArray();
        var failed = manifest.Items
            .Where(item => item.Status == "unsupported" || item.Status?.EndsWith("failed", StringComparison.Ordinal) == true)
            .ToDictionary(item => item.PublishedFileId);
        var issues = plan.Items.Where(item => failed.ContainsKey(item.Id)).ToArray();
        var remaining = manifest.Items.Select(item => item.PublishedFileId).ToHashSet();
        var removed = plan.PotentialRemovals.Count(item => !remaining.Contains(item.PublishedFileId));
        var summary = WorkshopScanSummary(plan);
        if (plan.NeedsConfirmation)
        {
            var added = completed.Count(item => item.Installed == null);
            summary = $"Workshop finished · {completed.Length - added} updated · {added} installed · {removed} removed";
            if (issues.Length > 0)
                summary += $" · {issues.Length} need attention";
            summary += WorkshopDiscoveryNotice(plan);
            if (completed.Length > 0 || removed > 0)
                summary += ". Restart the game if it was already running.";
        }

        foreach (var item in issues)
            RaiseWorkshopSyncLogReceived($"{item.Title} ({item.Id}) · {failed[item.Id].Error}");
        if (issues.Length > 0)
            summary += $"\n{string.Join(", ", issues.Take(3).Select(item => item.Title))}";
        RaiseWorkshopSyncCompleted(summary);
    }

    internal static string WorkshopScanSummary(SteamWorkshopUpdatePlan plan)
    {
        var downloads = plan.Items.Count(item => item.NeedsDownload);
        var newItems = plan.Items.Count(item => item.NeedsDownload && item.Installed == null);
        var blocked = plan.Items.Count(item => item.Revision == null);
        var result = downloads > 0
            ? $"{downloads - newItems} Workshop updates · {newItems} new installs"
            : plan.Metadata.SyncStatus == "complete" && blocked == 0
                ? plan.Items.Count == 0 ? "No Workshop subscriptions found" : "Workshop mods are up to date"
                : "No Workshop downloads found";
        if (plan.PotentialRemovals.Count > 0)
            result += $" · {plan.PotentialRemovals.Count} possible removals";
        if (blocked > 0)
            result += $" · {blocked} could not be checked";
        return result + WorkshopDiscoveryNotice(plan);
    }

    private static string WorkshopDiscoveryNotice(SteamWorkshopUpdatePlan plan)
    {
        var notice = plan.Metadata.MissingDependencyItemCount > 0
            ? $" · {plan.Metadata.MissingDependencyItemCount} dependencies unavailable" : "";
        if (plan.Metadata.SyncStatus == "discovery-unverified")
            notice += " · Steam subscription list could be incomplete";
        return notice;
    }
}
