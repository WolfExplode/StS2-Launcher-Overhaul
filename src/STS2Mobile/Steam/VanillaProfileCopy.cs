using System;
using System.Collections.Generic;
using System.IO;

namespace STS2Mobile.Steam;

// Stage complete profiles before replacing any destination. Backups are outside
// the Steam Cloud manifest and are retained even after a successful copy.
internal static class VanillaProfileCopy
{
    internal static string ResolveAndroidAccountPath(string dataRoot, bool editor)
    {
        // Steam Cloud imports can retain the pre-account profile layout.
        for (var id = 1; id <= 3; id++)
            if (File.Exists(Path.Combine(dataRoot, $"profile{id}", "saves", "progress.save")))
                return dataRoot;
        return Path.Combine(dataRoot, editor ? "editor" : "default", "0");
    }

    internal static int Copy(string dataRoot, string accountPath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot));
        var account = Path.TrimEndingDirectorySeparator(Path.GetFullPath(accountPath));
        if (!string.Equals(account, root, StringComparison.OrdinalIgnoreCase)
            && !account.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Profile account path escapes the save directory.");
        RejectLinks(root);
        var relative = Path.GetRelativePath(root, account);
        var ancestor = root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar))
        {
            ancestor = Path.Combine(ancestor, part);
            RejectLinks(ancestor);
        }
        var sources = new List<string>();
        for (var id = 1; id <= 3; id++)
        {
            var name = $"profile{id}";
            if (File.Exists(Path.Combine(account, name, "saves", "progress.save")))
                sources.Add(name);
        }
        if (sources.Count == 0)
            throw new InvalidOperationException("No vanilla profiles with saved progress were found on this device. Get your saves from Steam first if needed.");

        var work = Path.Combine(root, ".sts2-launcher", "profile-copy", Guid.NewGuid().ToString("N"));
        RejectLinks(Path.Combine(root, ".sts2-launcher"));
        RejectLinks(Path.Combine(root, ".sts2-launcher", "profile-copy"));
        var staged = Path.Combine(work, "staged");
        var backup = Path.Combine(work, "backup");
        var modded = Path.Combine(account, "modded");
        RejectLinks(modded);
        Directory.CreateDirectory(backup);
        foreach (var name in sources)
        {
            RejectLinks(Path.Combine(modded, name));
            CopyDirectory(Path.Combine(account, name), Path.Combine(staged, name));
        }
        Directory.CreateDirectory(modded);
        var replaced = new List<(string Name, bool HadOriginal)>();
        try
        {
            foreach (var name in sources)
            {
                var destination = Path.Combine(modded, name);
                var hadOriginal = Directory.Exists(destination);
                if (hadOriginal)
                    Directory.Move(destination, Path.Combine(backup, name));
                replaced.Add((name, hadOriginal));
                Directory.Move(Path.Combine(staged, name), destination);
            }
        }
        catch
        {
            for (var i = replaced.Count - 1; i >= 0; i--)
            {
                var entry = replaced[i];
                var destination = Path.Combine(modded, entry.Name);
                if (Directory.Exists(destination))
                    Directory.Move(destination, Path.Combine(work, "failed-" + entry.Name));
                if (entry.HadOriginal)
                    Directory.Move(Path.Combine(backup, entry.Name), destination);
            }
            throw;
        }
        return sources.Count;
    }

    private static void CopyDirectory(string source, string destination)
    {
        RejectLinks(source);
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            RejectLinks(file);
            var name = Path.GetFileName(file);
            if (name.StartsWith("current_run", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                continue;
            File.Copy(file, Path.Combine(destination, name));
        }
        foreach (var directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static void RejectLinks(string path)
    {
        if ((Directory.Exists(path) || File.Exists(path))
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Profile copying cannot follow symbolic links.");
    }
}
