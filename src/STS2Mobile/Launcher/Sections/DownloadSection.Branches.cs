using System.Collections.Generic;
using STS2Mobile.Launcher;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher.Sections;
internal sealed partial class DownloadSection
{
    internal void SetVersions(LauncherVersionsState versions)
    {
        var selectionChanged = !string.Equals(_versions.Branch, versions.Branch, System.StringComparison.OrdinalIgnoreCase);
        _versions = versions;
        LauncherBranchDropdown.Populate(_branchDropdown, _branchOptions, versions.Branch, versions.Branches);
        _branchDropdown.Disabled = versions.Busy;
        _refreshBranchesButton.Disabled = versions.Busy;
        if (_compactSelectedVersionPanel != null)
            _compactSelectedVersionPanel.Disabled = versions.Busy;
        if (selectionChanged && _compact)
            _branchDetailsExpanded = false;
        UpdateBranchHelpText();
    }

    private void ApplyBranchControlVisibility()
    {
        var controlsVisible = !_compact || _branchDetailsExpanded;
        if (_compactVersionControlsRow != null)
            _compactVersionControlsRow.Visible = controlsVisible;
        _branchDropdown.Visible = controlsVisible;
        _refreshBranchesButton.Visible = controlsVisible;
        _branchHelpLabel.Visible = controlsVisible;
    }

    private void ApplyGameBranch(long index)
    {
        if (!LauncherBranchDropdown.TryGetBranch(_branchOptions, index, out var branch))
            return;
        if (_compact)
        {
            _branchDetailsExpanded = false;
            UpdateBranchHelpText();
        }
        GameBranchChanged?.Invoke(branch);
    }

    private void ToggleBranchDetails()
    {
        _branchDetailsExpanded = !_branchDetailsExpanded;
        UpdateBranchHelpText();
    }

    private void OpenCompactBranchDetailsFromSelectedVersion()
    {
        if (!_compact)
            return;
        _branchDetailsExpanded = true;
        UpdateBranchHelpText();
    }

    private void UpdateBranchHelpText()
    {
        _branchHelpLabel.Text = _compact ? CompactInstallVersionHelpText() : SteamGameBranch.SelectorInstallSlotHelpText(_versions.Branch) + "\n" + LauncherBranchCatalog.SelectedOptionStatus(_versions.Branch, _versions.Branches) + "\n" + "Updates replace the selected Steam branch only after the new files pass checks. Recovery removes only that branch's downloaded files. Saves and other branches stay in place.";
        ApplyBranchControlVisibility();
        if (_branchDetailsToggle != null)
        {
            if (_compact)
            {
                SetCompactVersionActionButtonText(_branchDetailsToggle, _branchDetailsExpanded ? "Hide Version" : "Change Version", _branchDetailsExpanded ? "Keep selection" : "Local files only");
            }
            else
            {
                _branchDetailsToggle.Text = _branchDetailsExpanded ? "Hide Version Details" : "Show Version Details";
            }
        }

        if (_compactSelectedVersionLabel != null)
        {
            _compactSelectedVersionLabel.Text = _compact ? CompactSelectedVersionHeadline() : $"Selected version: {SteamGameBranch.CompactDisplayName(_versions.Branch, 22)}\n" + $"Install slot: {SteamGameInstallPaths.VersionSlotKind(_versions.Branch)}.";
            _compactSelectedVersionLabel.Visible = _compact;
        }

        if (_compactSelectedVersionPanel != null)
            _compactSelectedVersionPanel.Visible = _compact;
    }
}
