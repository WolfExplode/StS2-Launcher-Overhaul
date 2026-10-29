using System;
using System.IO;
using Godot;

namespace STS2Mobile.Launcher.Components;

internal static class LauncherFonts
{
    internal static Theme CreateTheme()
    {
        // Keep the launcher's existing Latin face, with explicit fallbacks for
        // Workshop titles. Some Android vendors' font configuration is not
        // discovered by Godot's automatic system-font lookup.
        var font = new FontVariation { BaseFont = ThemeDB.FallbackFont };
        if (OperatingSystem.IsAndroid())
        {
            AddAndroidFont(font, "/system/fonts/NotoSansCJK-Regular.ttc");
            AddAndroidFont(font, "/system/fonts/NotoColorEmoji.ttf");
        }
        font.Fallbacks.Add(new SystemFont
        {
            FontNames = new[] { "sans-serif" },
            AllowSystemFallback = true,
        });
        return new Theme { DefaultFont = font };
    }

    private static void AddAndroidFont(Font font, string path)
    {
        if (!File.Exists(path))
            return;

        var fallback = new FontFile();
        var error = fallback.LoadDynamicFont(path);
        if (error == Error.Ok)
        {
            font.Fallbacks.Add(fallback);
            PatchHelper.Log($"[Launcher] Font fallback loaded: {Path.GetFileName(path)}");
        }
        else
        {
            fallback.Dispose();
            PatchHelper.Log($"[Launcher] Font fallback could not load: {path} ({error})");
        }
    }
}
