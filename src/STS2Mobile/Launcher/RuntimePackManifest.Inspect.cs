using System;
using System.IO;
using STS2Mobile.Steam;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;

namespace STS2Mobile.Launcher;
internal sealed partial class RuntimePackManifest
{
    internal static RuntimePackManifest Inspect(string path, GameIdentity expectedGameIdentity)
    {
        if (expectedGameIdentity == null)
            throw new ArgumentNullException(nameof(expectedGameIdentity));
        var context = new RuntimePackManifestInspectionContext(path, expectedGameIdentity);
        if (!File.Exists(context.ManifestPath))
        {
            return NotInstalled(context);
        }

        try
        {
            return InspectReadable(context);
        }
        catch (Exception ex)
        {
            return Unreadable(context, ex);
        }
    }

    internal static RuntimePackManifest NotInstalled(string path, string expectedBranch) => NotInstalled(new RuntimePackManifestInspectionContext(path, expectedBranch));
    private static RuntimePackManifest NotInstalled(RuntimePackManifestInspectionContext context) => new RuntimePackManifest(context.ManifestPath, context.ExpectedBranch, context.ExpectedGameIdentity, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Array.Empty<string>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), supportAssembliesDeclared: false, supportAssemblySha256Declared: false, 0, 0, 0, generatedFromCleanDirectory: false, "not installed", exists: false, readable: false, androidAssemblyExists: false, context.AndroidAssemblyPath, "<missing>");
    private static RuntimePackManifest InspectReadable(RuntimePackManifestInspectionContext context)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(context.ManifestPath));
        var root = document.RootElement;
        var manifest = ReadManifest(context, root);
        return manifest.WithStatus(RuntimePackStatus(manifest));
    }

    private static RuntimePackManifest ReadManifest(RuntimePackManifestInspectionContext context, JsonElement root)
    {
        // The manifest is written only by RuntimePackWriter, in these names. A
        // pack from an older launcher fails the patch-set check regardless.
        return new RuntimePackManifest(context.ManifestPath, context.ExpectedBranch, context.ExpectedGameIdentity, ReadString(root, "packId"), ReadString(root, "sourceBranch"), ReadString(root, "installGeneration"), ReadString(root, "sourcePckSha256"), ReadString(root, "sourceAssemblySha256"), ReadString(root, "gameIdentityId"), ReadString(root, "androidAssemblySha256"), ReadString(root, "patchSetVersion"), ReadString(root, "patchValidationStatus"), ReadString(root, "patchValidationReport"), ReadString(root, "validationMode"), ReadString(root, "validationSurfaceVersion"), ReadStringArray(root, "supportAssemblies"), ReadStringDictionary(root, "supportAssemblySha256"), HasProperty(root, "supportAssemblies"), HasProperty(root, "supportAssemblySha256"), ReadInt(root, "checkedSymbolCount"), ReadInt(root, "presentSymbolCount"), ReadInt(root, "missingSymbolCount"), ReadBool(root, "generatedFromCleanDirectory"), "pending validation", exists: true, readable: true, context.AndroidAssemblyExists, context.AndroidAssemblyPath, context.AndroidAssemblyExists ? Sha256OrMissing(context.AndroidAssemblyPath) : "<missing>");
    }

    private static string Sha256OrMissing(string path)
    {
        try
        {
            var hashText = GameIdentityFileHasher.Instance.Sha256(path);
            return hashText;
        }
        catch
        {
            return "<hash failed>";
        }
    }

    private static RuntimePackManifest Unreadable(RuntimePackManifestInspectionContext context, Exception exception) => new RuntimePackManifest(context.ManifestPath, context.ExpectedBranch, context.ExpectedGameIdentity, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Array.Empty<string>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), supportAssembliesDeclared: false, supportAssemblySha256Declared: false, 0, 0, 0, generatedFromCleanDirectory: false, $"unreadable: {exception.GetType().Name}", exists: true, readable: false, context.AndroidAssemblyExists, context.AndroidAssemblyPath, context.AndroidAssemblyExists ? "<not inspected>" : "<missing>");
}

internal readonly struct RuntimePackManifestInspectionContext
{
    internal RuntimePackManifestInspectionContext(string manifestPath, GameIdentity expectedGameIdentity)
    {
        ManifestPath = manifestPath;
        ExpectedGameIdentity = expectedGameIdentity;
        ExpectedBranch = expectedGameIdentity?.Branch ?? SteamGameBranch.Public;
        AndroidAssemblyPath = Path.Combine(Path.GetDirectoryName(manifestPath) ?? string.Empty, RuntimePackManifest.AndroidAssemblyFileName);
        AndroidAssemblyExists = File.Exists(AndroidAssemblyPath);
    }

    internal RuntimePackManifestInspectionContext(string manifestPath, string expectedBranch) : this(manifestPath, expectedGameIdentity: null)
    {
        ExpectedBranch = SteamGameBranch.StorageIdentity(expectedBranch);
    }

    internal string ManifestPath { get; }
    internal string ExpectedBranch { get; }
    internal GameIdentity ExpectedGameIdentity { get; }
    internal string AndroidAssemblyPath { get; }
    internal bool AndroidAssemblyExists { get; }
}
