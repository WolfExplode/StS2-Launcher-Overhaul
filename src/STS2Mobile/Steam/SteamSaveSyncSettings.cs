using System;
using System.IO;

namespace STS2Mobile.Steam;

internal static class SteamSaveSyncSettings
{
    internal const string FileName = "steam_save_sync";

    internal static bool IsEnabled(string dataDir)
    {
        try
        {
            var path = Path.Combine(dataDir, FileName);
            return !File.Exists(path) || File.ReadAllText(path).Trim() == "enabled";
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Cloud] Could not read sync preference; sync paused: {ex.Message}");
            return false;
        }
    }

    internal static void SetEnabled(string dataDir, bool enabled)
    {
        Directory.CreateDirectory(dataDir);
        var path = Path.Combine(dataDir, FileName);
        File.WriteAllText(path + ".tmp", enabled ? "enabled" : "disabled");
        File.Move(path + ".tmp", path, overwrite: true);
    }
}
