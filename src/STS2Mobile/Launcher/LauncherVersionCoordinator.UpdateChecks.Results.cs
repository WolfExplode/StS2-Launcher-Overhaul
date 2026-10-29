namespace STS2Mobile.Launcher;

internal sealed partial class LauncherVersionCoordinator
{
    // Refreshing the branch options clears the previous check outcome, so a
    // result for a branch that is no longer selected leaves "Check for updates".
    internal void CompleteUpdateCheck(LauncherUpdateCheckResult result)
    {
        RefreshGameBranchOptions();
        if (!IsCurrentSelection(result.Branch))
        {
            _view.AppendLog(
                "Ignored an update result for a game version that is no longer selected."
            );
            return;
        }
        UpdateCheckViewUpdate.Completed(
            result.HasUpdate,
            STS2Mobile.Steam.SteamGameBranch.DisplayName(result.Branch)
        ).Apply(_view);
    }

    internal void FailUpdateCheck(LauncherBranchOperationFailure failure)
    {
        RefreshGameBranchOptions();
        if (!IsCurrentSelection(failure.Branch))
            return;
        UpdateCheckViewUpdate.Failed(
            LauncherBranchAvailabilityStatus.CompactFailureMessage(_model.DataDir, failure.Message),
            STS2Mobile.Steam.SteamGameBranch.DisplayName(failure.Branch)
        ).Apply(_view);
    }

    private static bool IsCurrentSelection(string branch)
        => string.Equals(
            STS2Mobile.Steam.SteamGameBranch.Normalize(branch),
            LauncherPreferences.ReadGameBranch(),
            System.StringComparison.OrdinalIgnoreCase
        );
}
