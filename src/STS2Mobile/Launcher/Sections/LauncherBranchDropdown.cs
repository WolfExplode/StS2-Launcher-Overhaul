using System;
using System.Collections.Generic;
using Godot;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher.Sections;

internal static class LauncherBranchDropdown
{
    internal static void Populate(
        OptionButton dropdown,
        List<LauncherBranchCatalog.BranchOption> branchOptions,
        string selectedBranch,
        IReadOnlyList<LauncherBranchCatalog.BranchOption> availableBranches
    )
    {
        selectedBranch = SteamGameBranch.Normalize(selectedBranch);
        branchOptions.Clear();
        dropdown.Clear();

        var selectedIndex = 0;
        foreach (var option in LauncherBranchCatalog.DropdownOptions(selectedBranch, availableBranches))
        {
            var index = branchOptions.Count;
            branchOptions.Add(option);
            dropdown.AddItem(option.Label);
            dropdown.SetItemMetadata(index, option.Branch);

            if (string.Equals(option.Branch, selectedBranch, StringComparison.OrdinalIgnoreCase))
                selectedIndex = index;
        }

        dropdown.Select(selectedIndex);
    }

    internal static bool TryGetBranch(
        List<LauncherBranchCatalog.BranchOption> branchOptions,
        long index,
        out string branch
    )
    {
        if (index < 0 || index >= branchOptions.Count)
        {
            branch = SteamGameBranch.Public;
            return false;
        }

        branch = branchOptions[(int)index].Branch;
        return true;
    }
}
