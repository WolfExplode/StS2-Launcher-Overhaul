using System;
using System.Threading.Tasks;
using STS2Mobile.Patches;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher;

internal sealed partial class LauncherSteamSession
{
    private static async Task<string?> SessionFailureAsync(
        string logContext,
        Exception ex,
        string? userPrefix = null
    )
    {
        await SaveSyncService.ReportFailureAsync(SaveSyncService.FailureKindFor(ex));
        var authFailure = SteamAuthFailureReport.From(ex);
        LauncherLaunchMarkers.RecordSteamAuthFailure(authFailure, logContext);
        PatchHelper.Log($"[Launcher] {logContext}: {ex}");
        var message = authFailure.UserMessage;
        return userPrefix == null ? message : $"{userPrefix}: {message}";
    }
}
