namespace STS2Mobile.Launcher;

internal sealed partial class LauncherLaunchCoordinator
{
    private readonly LauncherModel _model;
    private readonly LauncherView _view;
    private readonly LauncherDiagnosticsCoordinator _diagnostics;
    private bool _launchInProgress;
    internal event System.Action LaunchInProgressChanged;
    internal bool LaunchInProgress => _launchInProgress;
    private LaunchAttemptContext _activeLaunchAttempt;

    internal LauncherLaunchCoordinator(
        LauncherModel model,
        LauncherView view,
        LauncherDiagnosticsCoordinator diagnostics
    )
    {
        _model = model;
        _view = view;
        _diagnostics = diagnostics;
    }

    internal void LaunchPressed()
        => StartGame(LauncherStartGamePlan.Normal());

    internal void SafeLaunchPressed()
        => StartGame(LauncherStartGamePlan.Safe());

    internal void AutoLaunchRequested(bool safeLaunch)
        => StartGame(
            safeLaunch
                ? LauncherStartGamePlan.AutoSafe()
                : LauncherStartGamePlan.AutoNormal()
        );

}
