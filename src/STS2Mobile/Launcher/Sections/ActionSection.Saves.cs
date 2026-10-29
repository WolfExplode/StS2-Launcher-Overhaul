using Godot;
using STS2Mobile.Launcher.Components;
using STS2Mobile.Steam;

namespace STS2Mobile.Launcher.Sections;

internal sealed partial class ActionSection
{
    internal event System.Action SaveSyncPreferenceChanged;
    private CheckButton _steamSaveSyncToggle;
    private bool _saveSyncControlsBusy;
    private (
        VBoxContainer Group,
        Button SyncNowButton,
        Button PullButton,
        Button PushButton,
        Label Status,
        GridContainer Details,
        Label LocalState,
        Label SteamState
    ) BuildSaveSyncControls(float scale, bool compact)
    {
        var group = BuildActionGroup(scale);
        group.Name = "SaveSyncPresentation";
        AddChild(group);

        _steamSaveSyncToggle = new CheckButton
        {
            Name = "SteamSaveSyncToggle",
            Text = "Steam save sync",
            ButtonPressed = SteamSaveSyncSettings.IsEnabled(AppPaths.AppPrivateDataDir),
            AccessibilityName = "Steam save sync",
        };
        _steamSaveSyncToggle.AddThemeFontSizeOverride("font_size",
            LauncherComponentTheme.ScaleInt(scale, compact ? 15 : 16));
        _steamSaveSyncToggle.Toggled += enabled =>
        {
            try
            {
                SteamSaveSyncSettings.SetEnabled(AppPaths.AppPrivateDataDir, enabled);
            }
            catch (System.Exception ex)
            {
                _steamSaveSyncToggle.SetPressedNoSignal(SteamSaveSyncSettings.IsEnabled(AppPaths.AppPrivateDataDir));
                PatchHelper.Log($"[Cloud] Could not save sync preference: {ex.Message}");
                return;
            }
            SetSaveSyncControlsDisabled(_saveSyncControlsBusy);
            SaveSyncPreferenceChanged?.Invoke();
        };
        group.AddChild(_steamSaveSyncToggle);

        var namespaceExplanation = new StyledLabel(
            "Vanilla and modded saves are separate. Turn sync off to use local saves and skip Steam Cloud transfers.",
            scale,
            fontSize: compact ? 12 : 13,
            align: HorizontalAlignment.Left
        )
        {
            Name = "SaveNamespaceExplanation",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        namespaceExplanation.AddThemeColorOverride(
            "font_color",
            LauncherComponentTheme.TextSecondary
        );
        group.AddChild(namespaceExplanation);

        var status = new StyledLabel(
            "Not synced yet",
            scale,
            fontSize: compact ? 15 : 16,
            align: HorizontalAlignment.Left
        )
        {
            Name = "SaveSyncHeadline",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        status.AddThemeColorOverride("font_color", LauncherComponentTheme.TextPrimary);
        group.AddChild(status);

        var details = BuildStateRows(scale);
        details.Name = "SaveSyncDetails";
        details.Visible = false;
        group.AddChild(details);
        var localState = AddStateRow(
            details,
            "This device",
            "Checking...",
            "SaveLocalState",
            scale,
            compact
        );
        var steamState = AddStateRow(
            details,
            "Steam Cloud",
            "Checking...",
            "SaveSteamState",
            scale,
            compact
        );

        var actions = BuildActionGroup(scale);
        actions.Name = "SaveSyncActions";
        group.AddChild(actions);

        var syncNow = AddActionButton(
            actions,
            "Sync now",
            scale,
            () => SaveSyncNowPressed?.Invoke()
        );
        syncNow.AccessibilityName = "Sync now";
        LauncherButtonStyles.ApplyPrimaryAction(syncNow, scale);

        var manualActions = BuildCompactActionRow(actions, scale);
        manualActions.Name = "SaveManualActions";

        var pull = AddActionButton(
            manualActions,
            "Get saves from Steam",
            scale,
            () => SavePullPressed?.Invoke()
        );
        pull.AccessibilityName = "Get saves from Steam";
        LauncherButtonStyles.ApplySupportAction(pull, scale);

        var push = AddActionButton(
            manualActions,
            "Send saves to Steam",
            scale,
            () => SavePushPressed?.Invoke()
        );
        push.AccessibilityName = "Send saves to Steam";
        LauncherButtonStyles.ApplySupportAction(push, scale);

        _copyVanillaToModdedButton = AddActionButton(
            actions,
            "Copy Vanilla to Modded",
            scale,
            () => CopyVanillaToModdedPressed?.Invoke()
        );
        _copyVanillaToModdedButton.Name = "CopyVanillaToModdedButton";
        _copyVanillaToModdedButton.AccessibilityName = "Copy Vanilla to Modded";
        LauncherButtonStyles.ApplySupportAction(_copyVanillaToModdedButton, scale);

        return (
            group,
            syncNow,
            pull,
            push,
            status,
            details,
            localState,
            steamState
        );
    }

    internal void SetSaveSyncControlsDisabled(bool disabled)
    {
        _saveSyncControlsBusy = disabled;
        var syncDisabled = disabled || !SteamSaveSyncSettings.IsEnabled(AppPaths.AppPrivateDataDir);
        _saveSyncNowButton.Disabled = syncDisabled;
        _savePullButton.Disabled = syncDisabled;
        _savePushButton.Disabled = syncDisabled;
        _copyVanillaToModdedButton.Disabled = disabled;
    }

    internal void SetSaveSyncPresentation(LauncherSaveSyncPresentation presentation)
    {
        var enabled = SteamSaveSyncSettings.IsEnabled(AppPaths.AppPrivateDataDir);
        _steamSaveSyncToggle.SetPressedNoSignal(enabled);
        if (!enabled)
            presentation = LauncherSaveSyncPresentation.Disabled();
        SetSaveSyncControlsDisabled(_saveSyncControlsBusy);
        _saveSyncStatus.Text = PresentationText(
            presentation.Summary,
            "Not synced yet"
        );
        _saveSyncStatus.SetMeta("save_sync_state", presentation.State.ToString());
        _saveSyncDetails.Visible = presentation.ShowEndpointDetails;
        _saveLocalState.Text = PresentationText(
            presentation.LocalState,
            "Saved locally"
        );
        _saveSteamState.Text = PresentationText(
            presentation.SteamState,
            "Not checked"
        );
        _homeSyncState = PresentationText(
            presentation.HomeState,
            "Not synced yet"
        );
        UpdateHomeStateLine();
    }

    private static GridContainer BuildStateRows(float scale)
    {
        var rows = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        rows.AddThemeConstantOverride(
            "h_separation",
            LauncherViewLayoutMetrics.ScaleInt(12, scale)
        );
        rows.AddThemeConstantOverride(
            "v_separation",
            LauncherViewLayoutMetrics.ScaleInt(8, scale)
        );
        return rows;
    }

    private static Label AddStateRow(
        GridContainer rows,
        string title,
        string value,
        string valueName,
        float scale,
        bool compact
    )
    {
        var titleLabel = new StyledLabel(
            title,
            scale,
            fontSize: compact ? 12 : 13,
            align: HorizontalAlignment.Left
        );
        titleLabel.AddThemeColorOverride("font_color", LauncherComponentTheme.TextSecondary);
        rows.AddChild(titleLabel);

        var valueLabel = new StyledLabel(
            value,
            scale,
            fontSize: compact ? 12 : 13,
            align: HorizontalAlignment.Left
        )
        {
            Name = valueName,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        valueLabel.AddThemeColorOverride("font_color", LauncherComponentTheme.TextPrimary);
        rows.AddChild(valueLabel);
        return valueLabel;
    }
}
