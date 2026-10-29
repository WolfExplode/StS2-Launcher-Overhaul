using System;
using System.Collections.Generic;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher;

// Everything the version controls on Home and the Versions page show. The
// view keeps one copy and both sections draw from it, so they cannot disagree
// about the selected version or about whether it can be changed.
internal readonly record struct LauncherVersionsState(
    string Branch,
    IReadOnlyList<LauncherBranchCatalog.BranchOption> Branches,
    LauncherVersionCheckOutcome CheckOutcome,
    bool CheckingForUpdates,
    bool RefreshingList,
    bool SelectedVersionOperationRunning,
    bool LaunchInProgress
)
{
    internal static LauncherVersionsState Initial
        => new(SteamGameBranch.Public, Array.Empty<LauncherBranchCatalog.BranchOption>(),
            LauncherVersionCheckOutcome.None, false, false, false, false);

    // Changing the version, checking it or refreshing the list while another
    // of these runs, or while the game starts from the selected files, would
    // act on files or results the running one depends on.
    internal bool Busy => CheckingForUpdates || RefreshingList || SelectedVersionOperationRunning || LaunchInProgress;

    // A different selection or branch list makes the last check describe
    // something that is no longer shown.
    internal LauncherVersionsState WithSelection(string branch, IReadOnlyList<LauncherBranchCatalog.BranchOption> branches)
        => this with
        {
            Branch = SteamGameBranch.Normalize(branch),
            Branches = branches ?? Array.Empty<LauncherBranchCatalog.BranchOption>(),
            CheckOutcome = LauncherVersionCheckOutcome.None,
        };
}
