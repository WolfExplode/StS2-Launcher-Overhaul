using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Godot;
using STS2Mobile.Launcher;

namespace LauncherUiPreview;

public partial class PreviewRoot : Control
{
    private readonly Dictionary<string, string> _arguments = new(
        StringComparer.OrdinalIgnoreCase
    );

    private LauncherView _previewView = null!;
    private Control _previewRoot = null!;

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception ex)
        {
            GD.PushError($"Launcher UI preview failed: {ex}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        System.Environment.SetEnvironmentVariable("STS2_LAUNCHER_PREVIEW", "1");
        ParseArguments(OS.GetCmdlineUserArgs());

        if (ReadBool("asset-preload-test", false))
        {
            var context = AssemblyLoadContext.GetLoadContext(typeof(PreviewRoot).Assembly)
                ?? AssemblyLoadContext.Default;
            var directory = ReadRequiredDirectory("managed-runtime-directory");
            Assembly? Resolve(AssemblyLoadContext loadContext, AssemblyName requestedName)
                => ResolveManagedRuntimeDependency(loadContext, requestedName, directory);
            context.Resolving += Resolve;
            try { await AssetPreloadRuntimeTest.RunAsync(this, !ReadBool("unpatched", false)); }
            finally { context.Resolving -= Resolve; }
            GetTree().Quit();
            return;
        }

        if (ReadBool("mod-runtime-test", false))
        {
            var context = AssemblyLoadContext.GetLoadContext(typeof(PreviewRoot).Assembly)
                ?? AssemblyLoadContext.Default;
            var managedRuntimeDirectory = ReadRequiredDirectory(
                "managed-runtime-directory"
            );
            Assembly? ResolveFromManagedRuntime(
                AssemblyLoadContext loadContext,
                AssemblyName requestedName
            ) => ResolveManagedRuntimeDependency(
                loadContext,
                requestedName,
                managedRuntimeDirectory
            );

            context.Resolving += ResolveFromManagedRuntime;
            try
            {
                LoadManagedRuntimeDependency(
                    context,
                    ReadRequiredPath("steamworks-net-path"),
                    "Steamworks.NET"
                );
                var scenario = Read("mod-runtime-scenario", "active");
                if (scenario is "contracts" or "fault" or "native-lifecycle")
                {
                    var data = ReadRequiredPath("runtime-data-dir");
                    var assembly = ReadRequiredPath("game-assembly-path");
                    await ModFrameworkContractTest.RunAsync(data, ReadRequiredPath("base-game-pck-path"), assembly, scenario);
                    File.WriteAllText(Path.Combine(data, "framework-result.json"), System.Text.Json.JsonSerializer.Serialize(new
                    {
                        passed = true, scenario, gameAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly))),
                    }));
                }
                else await ModRuntimeActivationTest.RunAsync(
                    ReadRequiredPath("runtime-data-dir"),
                    ReadRequiredPath("base-game-pck-path"),
                    ReadRequiredPath("import-vanilla-saves-root"),
                    Read("base-lib-root", ""),
                    Read("mod-runtime-scenario", "active"),
                    ReadRequiredPath("game-assembly-path"),
                    Read("aram-root", ""),
                    Read("quick-restart-root", ""),
                    Read("additional-mod-root", "")
                );
            }
            finally
            {
                context.Resolving -= ResolveFromManagedRuntime;
            }
            GetTree().Quit();
            return;
        }

        var width = ReadInt("width", 1280);
        var height = ReadInt("height", 800);
        var previewViewport = BuildFixture(new Vector2I(width, height));
        await NextFrames(20);
        RenderingServer.ForceSync();
        RenderingServer.ForceDraw(swapBuffers: false, frameStep: 0d);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        RenderingServer.ForceSync();

        if (ReadBool("interaction-test", false))
        {
            await AssertViewportRestore(previewViewport);
            LauncherUiInteractionTest.Run(_previewView, _previewRoot);
            await LauncherUiInteractionTest.AssertModsRefreshRejectsStaleCompletionAsync(_previewView, _previewRoot);
            await LauncherUiInteractionTest.AssertModsScrollAfterListGrowthAsync(_previewView, _previewRoot);
            await LauncherUiInteractionTest.AssertTabletModsScrollingAsync(this);
            await AssertModsDragScrolling(previewViewport);
            foreach (var fixture in new[] { "ready", "error", "signed-out", "guard", "download" })
            {
                ApplyFixture(_previewView, fixture);
                _previewView.SetStatus("The selected version must be redownloaded before it can launch. " + new string('x', 180), LauncherStatusSeverity.Warning);
                _previewView.AppendLog("Long diagnostic identity: " + new string('a', 256));
                foreach (var destination in Enum.GetValues<LauncherDestination>())
                {
                    _previewView.SelectDestination(destination);
                    if (destination == LauncherDestination.Help)
                        _previewView.ShowDiagnosticsConsole();
                    await NextFrames(8);
                    AssertHorizontalFit((ScrollContainer)_previewRoot.FindChild("LauncherPrimaryScroll", true, false));
                }
            }
            GD.Print("PREVIEW_INTERACTION=passed");
        }

        var output = Read("output", "");
        if (!string.IsNullOrWhiteSpace(output))
        {
            var fullPath = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var error = previewViewport.GetTexture().GetImage().SavePng(fullPath);
            if (error != Error.Ok)
                throw new InvalidOperationException(
                    $"Failed to save preview screenshot: {error}"
                );
            GD.Print($"PREVIEW_SCREENSHOT={fullPath}");
        }

        previewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        previewViewport.QueueFree();
        await NextFrames(2);
        GetTree().Quit();
    }

    private async Task AssertModsDragScrolling(SubViewport viewport)
    {
        _previewView.SelectDestination(LauncherDestination.Mods);
        var mods = Enumerable.Range(0, 30).Select(i => new LauncherModPresentationItem(
            $"preview:{i}", $"mod{i}", $"Scroll test mod {i}", "Workshop",
            true, false, true, LauncherModLastLaunchState.NotTestedYet, false, "Touch scrolling fixture")).ToArray();
        _previewView.SetModsPresentation(new LauncherModsPresentation(
            LauncherModPlayMode.Vanilla, "scroll-test", "Vanilla saves", mods, mods.Length, 0, false));
        await NextFrames(8);
        var scroll = (ScrollContainer)_previewRoot.FindChild("LauncherPrimaryScroll", true, false);
        var row = (Control)_previewRoot.FindChild("ModRow", true, false);
        scroll.EnsureControlVisible(row);
        await NextFrames(4);
        var start = row.GetGlobalRect().Position + new Vector2(40, row.Size.Y / 2);
        var before = scroll.ScrollVertical;
        if (!DisplayServer.IsTouchscreenAvailable())
            throw new InvalidOperationException("Mods drag test requires touch emulation in the preview project.");
        viewport.PushInput(new InputEventMouseButton { Position = start, GlobalPosition = start, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        for (var i = 1; i <= 4; i++)
        {
            var position = start - new Vector2(0, i * 20);
            viewport.PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position, Relative = new Vector2(0, -20), ButtonMask = MouseButtonMask.Left }, true);
            await NextFrames(1);
        }
        viewport.PushInput(new InputEventMouseButton { Position = start - new Vector2(0, 80), ButtonIndex = MouseButton.Left, Pressed = false }, true);
        if (scroll.ScrollVertical <= before)
            throw new InvalidOperationException($"Dragging a mod row did not scroll: {before} -> {scroll.ScrollVertical}.");
        if (((CheckButton)row.FindChild("ModEnabledToggle", true, false)).ButtonPressed)
            throw new InvalidOperationException("Dragging a mod row changed its enabled state.");
        GD.Print($"PREVIEW_MODS_DRAG=passed ({before} -> {scroll.ScrollVertical})");
        _previewView.SetModsPresentation(ReadyVanillaModsPresentation());
    }

    private static void AssertHorizontalFit(ScrollContainer scroll)
    {
        var bounds = scroll.GetGlobalRect();
        AssertChildrenFit(scroll, bounds);
        scroll.ScrollHorizontal = 100;
        if (scroll.ScrollHorizontal != 0)
            throw new InvalidOperationException("Launcher still permits horizontal scrolling.");
    }

    private async Task AssertViewportRestore(SubViewport viewport)
    {
        var original = viewport.Size;
        _previewRoot.CustomMinimumSize = Vector2.Zero;
        foreach (var size in new[] { new Vector2I(1080, 2340), new Vector2I(2340, 1080), new Vector2I(1080, 2340), original })
        {
            viewport.Size = size;
            _previewRoot.Size = size;
            // On a phone the launcher's parent resizes first, and the keyboard
            // shift is reapplied every frame, including before the view sees
            // the new size.
            _previewView.UpdateKeyboardOffset();
            await NextFrames(2);
            _previewView.UpdateKeyboardOffset();
            _previewView.UpdateViewportSize(size);
            await NextFrames(8);
            _previewView.UpdateKeyboardOffset();
            await NextFrames(2);
            var panel = _previewRoot.GetChildren().OfType<STS2Mobile.Launcher.Components.StyledPanel>().Single();
            GD.Print($"VIEWPORT_RESTORE size={size} root={_previewRoot.GetRect()} panel={panel.GetRect()}");
            if (panel.Position.DistanceSquaredTo(Vector2.Zero) > 1f || panel.Size.DistanceSquaredTo(size) > 1f)
                throw new InvalidOperationException($"Launcher panel displaced after viewport restore: {panel.GetRect()} viewport={size}");
            // Rows were built for the first size; each must still fit after rotating.
            foreach (var destination in Enum.GetValues<LauncherDestination>())
            {
                _previewView.SelectDestination(destination);
                await NextFrames(4);
                AssertHorizontalFit((ScrollContainer)_previewRoot.FindChild("LauncherPrimaryScroll", true, false));
            }
            _previewView.SelectDestination(LauncherDestination.Home);
        }
    }

    private static void AssertChildrenFit(Node parent, Rect2 bounds)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is Control control && control.IsVisibleInTree())
            {
                var rect = control.GetGlobalRect();
                if (rect.Position.X < bounds.Position.X - 2 || rect.End.X > bounds.End.X + 2)
                    throw new InvalidOperationException($"Horizontal overflow: {control.GetPath()} {rect} outside {bounds}");
                AssertChildrenFit(control, bounds);
            }
        }
    }

    private SubViewport BuildFixture(Vector2I viewportSize)
    {
        var previewViewport = new SubViewport
        {
            Size = viewportSize,
            Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        AddChild(previewViewport);

        var previewRoot = new Control
        {
            CustomMinimumSize = new Vector2(viewportSize.X, viewportSize.Y),
            Size = new Vector2(viewportSize.X, viewportSize.Y),
        };
        previewViewport.AddChild(previewRoot);
        previewRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var touchOptimized = ReadBool("touch", true);
        var profile = LauncherLayoutProfile.ForViewport(
            new Vector2(viewportSize.X, viewportSize.Y),
            touchOptimized
        );
        var view = new LauncherView(previewRoot, profile);
        _previewView = view;
        _previewRoot = previewRoot;
        ApplyFixture(view, Read("fixture", "ready"));
        view.SelectDestination(ReadDestination(Read("destination", "home")));
        GD.Print($"PREVIEW_PROFILE={profile}");
        return previewViewport;
    }

    private static void ApplyFixture(LauncherView view, string fixture)
    {
        switch (fixture.Trim().ToLowerInvariant())
        {
            case "signed-out":
                view.SetStatus("Sign in to continue.", LauncherStatusSeverity.Warning);
                view.ShowLoginForm(disabled: false);
                break;
            case "guard":
                view.SetStatus("Enter your Steam Guard code.", LauncherStatusSeverity.Warning);
                view.ShowCodePrompt(wasIncorrect: false);
                break;
            case "download":
                view.SetStatus("Downloading...", LauncherStatusSeverity.Working);
                view.ShowDownloadAction("Download Game");
                view.ShowDownloadProgress("Downloading public game files...");
                view.SetDownloadProgress(42, "42% | 3.8 GB of 9.1 GB");
                break;
            case "error":
                view.SetStatus("Download required.", LauncherStatusSeverity.Warning);
                view.ShowRetry();
                break;
            case "ready":
                ApplyReadyFixture(view);
                break;
            default:
                throw new ArgumentException($"Unknown preview fixture: {fixture}");
        }
    }

    private static void ApplyReadyFixture(LauncherView view)
    {
        view.SetActionPreferences(
            new LauncherPreferences.ActionPreferences(
                gameBranch: "public",
                rendererMode: LauncherRendererMode.Auto
            ),
            new[]
            {
                new LauncherBranchCatalog.BranchOption(
                    "public",
                    source: "local install",
                    isInstalled: true
                ),
                new LauncherBranchCatalog.BranchOption(
                    "beta",
                    metadataVisible: true,
                    windowsManifestDepotCount: 1,
                    passwordRequired: "false",
                    source: "Steam app-info"
                ),
            }
        );
        view.SetSaveSyncPresentation(
            new LauncherSaveSyncPresentation(
                LauncherSaveSyncState.UpToDate,
                "Up to date · Last synced 9 Aug 2026, 10:30",
                "Synced",
                "Up to date",
                "Up to date"
            )
        );
        view.SetStatus("Ready to play.", LauncherStatusSeverity.Ready);
        view.ShowLaunchActions(showUpdate: true);
        view.SetModsPresentation(ReadyVanillaModsPresentation());
    }

    private static LauncherModsPresentation ReadyVanillaModsPresentation()
        => new(
            LauncherModPlayMode.Vanilla,
            selectionFingerprint: "preview-vanilla",
            saveNamespaceLabel: "Vanilla saves",
            mods: new[]
            {
                new LauncherModPresentationItem(
                    "workshop:3747503308",
                    "ImportVanillaSaves",
                    "Import Vanilla Saves",
                    "Workshop",
                    Installed: true,
                    Enabled: false,
                    CanChange: true,
                    LastLaunchState: LauncherModLastLaunchState.NotTestedYet,
                    IsLastLaunchStale: false,
                    Detail: "No matching result from the last launch."
                ),
            },
            installedCount: 1,
            enabledCount: 0,
            hasStaleLastLaunchResult: false
        );

    private async Task NextFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static LauncherDestination ReadDestination(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "home" => LauncherDestination.Home,
            "saves" => LauncherDestination.Saves,
            "versions" => LauncherDestination.Versions,
            "mods" => LauncherDestination.Mods,
            "help" => LauncherDestination.Help,
            _ => throw new ArgumentException($"Unknown preview destination: {value}"),
        };

    private void ParseArguments(string[] args)
    {
        foreach (var argument in args)
        {
            var normalized = argument.TrimStart('-');
            var separator = normalized.IndexOf('=');
            if (separator <= 0)
                continue;
            _arguments[normalized[..separator]] = normalized[(separator + 1)..];
        }
    }

    private string Read(string name, string fallback)
        => _arguments.TryGetValue(name, out var value) ? value : fallback;

    private int ReadInt(string name, int fallback)
        => int.TryParse(Read(name, ""), out var value) ? value : fallback;

    private bool ReadBool(string name, bool fallback)
        => bool.TryParse(Read(name, ""), out var value) ? value : fallback;

    private string ReadRequiredPath(string name)
    {
        var value = Read(name, "");
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required preview argument: --{name}=<path>");

        return Path.GetFullPath(value);
    }

    private string ReadRequiredDirectory(string name)
    {
        var path = ReadRequiredPath(name);
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException(
                $"Required preview directory was not found: {path}"
            );

        return path;
    }

    private static Assembly? ResolveManagedRuntimeDependency(
        AssemblyLoadContext context,
        AssemblyName requestedName,
        string managedRuntimeDirectory
    )
    {
        var simpleName = requestedName.Name;
        if (string.IsNullOrWhiteSpace(simpleName)
            || !string.Equals(simpleName, Path.GetFileName(simpleName), StringComparison.Ordinal))
        {
            return null;
        }

        var dependencyPath = Path.Combine(managedRuntimeDirectory, simpleName + ".dll");
        return File.Exists(dependencyPath)
            ? LoadManagedRuntimeDependency(
                context,
                dependencyPath,
                simpleName
            )
            : null;
    }

    private static Assembly LoadManagedRuntimeDependency(
        AssemblyLoadContext context,
        string path,
        string expectedAssemblyName
    )
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Required managed preview dependency was not found: {path}",
                path
            );

        var assembly = context.Assemblies.FirstOrDefault(candidate =>
            string.Equals(
                candidate.GetName().Name,
                expectedAssemblyName,
                StringComparison.Ordinal
            )
        ) ?? context.LoadFromAssemblyPath(path);
        if (!string.Equals(
                assembly.GetName().Name,
                expectedAssemblyName,
                StringComparison.Ordinal
            ))
        {
            throw new InvalidDataException(
                $"Expected managed dependency '{expectedAssemblyName}', loaded '{assembly.GetName().Name}'."
            );
        }
        return assembly;
    }
}
