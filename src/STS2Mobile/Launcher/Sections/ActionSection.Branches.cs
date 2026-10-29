using System.Collections.Generic;
using STS2Mobile.Steam;
using Godot;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher.Sections;
internal sealed partial class ActionSection
{
    // Draws the version selector, its state line, the update button and the
    // list and redownload tools from one state.
    internal void SetVersions(LauncherVersionsState versions)
    {
        _versions = versions;
        UpdateHomeStateLine();
        LauncherBranchDropdown.Populate(_branchDropdown, _branchOptions, versions.Branch, versions.Branches);
        _branchDropdown.Disabled = versions.Busy;

        var state = LauncherBranchCatalog.SelectedVersionState(versions.Branch, versions.Branches);
        _branchHelpLabel.Text = versions.CheckOutcome switch
        {
            LauncherVersionCheckOutcome.UpToDate => state + " · Up to date",
            LauncherVersionCheckOutcome.UpdateAvailable => state + " · Update available",
            _ => state,
        };
        _branchHelpLabel.TooltipText = LauncherBranchCatalog.SelectedOptionStatus(versions.Branch, versions.Branches);
        _branchHelpLabel.AccessibilityDescription = _branchHelpLabel.TooltipText;

        SetCompactActionButtonText(_updateButton, versions.CheckingForUpdates ? "Checking..."
            : UpdateAvailable ? "Update selected Steam branch" : "Check for updates");
        _updateButton.Disabled = versions.Busy;
        _refreshVersionsButton.Disabled = versions.Busy;
        UpdateVersionActionAvailability();
    }

    private bool UpdateAvailable => _versions.CheckOutcome == LauncherVersionCheckOutcome.UpdateAvailable;

    // The update button's action follows the last check result for the
    // selected version, so the label, the state line and the press agree.
    private void InvokeVersionPrimaryAction()
    {
        if (UpdateAvailable)
            UpdateSelectedVersionPressed?.Invoke();
        else
            CheckForUpdatesPressed?.Invoke();
    }

    // The selection is applied only once the controller confirms the switch
    // and draws the new state; a declined switch draws the previous one.
    private void ApplyGameBranch(long index)
    {
        if (LauncherBranchDropdown.TryGetBranch(_branchOptions, index, out var branch))
            GameBranchChanged?.Invoke(branch);
    }

    private void UpdateVersionActionAvailability()
    {
        _redownloadButton.Visible = LauncherBranchCatalog.SelectedVersionInstalled(_versions.Branch, _versions.Branches);
        // Redownload may interrupt a stuck download on purpose, but must not
        // delete the files a starting game is loading.
        _redownloadButton.Disabled = _versions.LaunchInProgress;
        if (_versionMaintenanceGroup != null)
            _versionMaintenanceGroup.Visible = _redownloadButton.Visible;
    }

    private (VBoxContainer Group, OptionButton Dropdown, Label StateLabel) BuildBranchControls(float scale, bool compact)
    {
        var group = new VBoxContainer
        {
            Name = "GameVersionSelection",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        group.SetSeparation(LauncherViewLayoutMetrics.ScaleInt(6, scale));
        var title = new StyledLabel("Game version", scale, fontSize: compact ? LauncherSectionMetrics.CompactVersionSummaryFontSize : LauncherSectionMetrics.ProgressFontSize, align: HorizontalAlignment.Left);
        title.Name = "GameVersionLabel";
        title.AddThemeColorOverride(LauncherViewLayoutMetrics.ThemeFontColor, LauncherComponentTheme.TextPrimary);
        group.AddChild(title);
        var branchDropdown = new OptionButton
        {
            FitToLongestItem = !compact,
        };
        branchDropdown.AccessibilityName = "Game version";
        branchDropdown.AccessibilityDescription = "Select the game version Play will use.";
        branchDropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        branchDropdown.CustomMinimumSize = new Vector2(0, LauncherViewLayoutMetrics.ScaleInt(LauncherSectionMetrics.SecondaryButtonHeight, scale));
        LauncherButtonStyles.ApplyDropdownAction(branchDropdown, scale, compact ? LauncherSectionMetrics.PrimaryButtonFontSize : LauncherSectionMetrics.SecondaryButtonFontSize, compact);
        branchDropdown.ItemSelected += ApplyGameBranch;
        group.AddChild(branchDropdown);
        var branchHelpLabel = new StyledLabel("", scale, fontSize: compact ? LauncherSectionMetrics.CompactDetailLabelFontSize : LauncherSectionMetrics.ProgressFontSize, align: HorizontalAlignment.Left);
        branchHelpLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        branchHelpLabel.Name = "SelectedGameVersionState";
        branchHelpLabel.VerticalAlignment = VerticalAlignment.Top;
        branchHelpLabel.MouseFilter = MouseFilterEnum.Ignore;
        branchHelpLabel.AddThemeColorOverride(LauncherViewLayoutMetrics.ThemeFontColor, LauncherViewLayoutMetrics.LogTitleColor);
        group.AddChild(branchHelpLabel);
        AddChild(group);
        return (group, branchDropdown, branchHelpLabel);
    }
}
