namespace STS2Mobile.Launcher.Sections;
internal sealed partial class ActionSection
{
    // Which play actions the current Home stage offers. Every other control in
    // the destinations stays visible regardless of session or download state.
    private enum PlayActions
    {
        Hidden,
        Ready,
        Retry,
    }

    private PlayActions _playActions;
    private bool _updateActionOffered;

    internal void ShowLaunch(bool showUpdate)
    {
        _updateActionOffered = showUpdate;
        ApplyContextualPlayLabel();
        SetPlayActions(PlayActions.Ready);
    }

    internal void ShowRetry() => SetPlayActions(PlayActions.Retry);
    internal void HidePlayActions() => SetPlayActions(PlayActions.Hidden);
    internal void ShowHomeHelpAction() => _homeHelpButton.Visible = true;

    private void SetPlayActions(PlayActions actions)
    {
        _playActions = actions;
        var ready = actions == PlayActions.Ready;
        _launchButton.Visible = ready;
        _safeLaunchButton.Visible = ready;
        _updateButton.Visible = ready && _updateActionOffered;
        _retryButton.Visible = actions == PlayActions.Retry;
        // A stage change clears a previous launch failure's Help shortcut.
        _homeHelpButton.Visible = false;
        ApplyLaunchControlsDisabled();
        ApplyDestinationVisibility();
    }
}
