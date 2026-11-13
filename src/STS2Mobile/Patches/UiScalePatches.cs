using HarmonyLib;

namespace STS2Mobile.Patches;

// Applies the UI scale percentage saved in user://ui_scale.cfg (100% when absent)
// by adjusting the window's ContentScaleSize, and intercepts window change handlers
// to keep that scale when the viewport resizes. The settings-screen scale selector
// that wrote the file is disabled: the Android runtime cannot safely Harmony-wrap
// the resolution dropdown's private-field-heavy methods.
internal static partial class UiScalePatches
{
    private const int BaseHeight = 1080;
    private const int BaseWidth = 1680;
    private const string ConfigPath = "user://ui_scale.cfg";
    private const int MaxPercent = 200;
    private const int MinPercent = 100;

    internal static int UiScalePercent { get; private set; } = 100;
    private static bool _uiScaleLoaded = false;

    internal static void Apply(Harmony harmony)
    {
        PatchWindowChangeHandlers(harmony, typeof(MegaCrit.Sts2.Core.Nodes.NGame).Assembly);
    }
}
