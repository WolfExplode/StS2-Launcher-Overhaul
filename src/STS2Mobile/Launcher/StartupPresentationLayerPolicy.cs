namespace STS2Mobile.Launcher;

internal static class StartupPresentationLayerPolicy
{
    // Startup status renders on the default canvas layer (0), below warmup and recovery.
    internal const int StartupStatusZIndex = 4096;
    internal const int ShaderWarmupCanvasLayer = 127;
    internal const int RecoveryCanvasLayer = 128;
}
