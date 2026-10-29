namespace STS2Mobile.Launcher;

internal enum LauncherDownloadPhase { Idle, Progress, Failed, Completed }

internal sealed record LauncherDownloadPresentation(
    LauncherDownloadPhase Phase,
    string ButtonText,
    double Percentage,
    string ProgressText,
    bool Running
);
