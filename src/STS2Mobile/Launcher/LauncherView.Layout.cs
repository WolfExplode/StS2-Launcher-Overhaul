using System;
using Godot;
using STS2Mobile.Launcher.Components;
using STS2Mobile.Launcher.Sections;

namespace STS2Mobile.Launcher;
internal sealed partial class LauncherView
{
    private static (StyledPanel Panel, VBoxContainer Content, ColorRect AndroidCompositionRefresh) BuildShell(Control parent, LauncherLayoutProfile profile, Action<InputEvent> dismissKeyboard)
    {
        parent.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var background = new ColorRect
        {
            Color = LauncherComponentTheme.ScreenBackground,
        };
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        background.GuiInput += input => dismissKeyboard(input);
        parent.AddChild(background);
        var panel = new StyledPanel(profile.Scale, widthRatio: profile.PanelWidthRatio, compact: profile.Compact);
        panel.Theme = LauncherFonts.CreateTheme();
        panel.UpdateSizeFromViewport(parent.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080), profile.PanelHeightRatio);
        panel.OnPanelGuiInput(dismissKeyboard);
        parent.AddChild(panel);
        var content = new VBoxContainer();
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.SetSeparation(LauncherViewLayoutMetrics.ScaleInt(profile.Compact ? LauncherViewLayoutMetrics.CompactRootColumnSeparation : LauncherViewLayoutMetrics.RootColumnSeparation, profile.Scale));
        panel.AddContent(content);
        content.AddChild(BuildBrandHeader(profile));
        var androidCompositionRefresh = new ColorRect
        {
            Color = Colors.Transparent,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        androidCompositionRefresh.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        parent.AddChild(androidCompositionRefresh);
        return (panel, content, androidCompositionRefresh);
    }

    private static (RichTextLabel Log, VBoxContainer Drawer, Button Toggle) BuildLogColumn(LauncherLayoutProfile profile, VBoxContainer root)
    {
        var scale = profile.Scale;
        var drawer = new VBoxContainer
        {
            Name = "TechnicalDetailsDrawer",
            Visible = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        drawer.SetSeparation(LauncherViewLayoutMetrics.ScaleInt(6, scale));
        var toggle = new StyledButton(profile.Compact ? "" : DiagnosticsToggleText(visible: false), scale, fontSize: profile.Compact ? LauncherSectionMetrics.CompactDetailButtonFontSize : 14, height: profile.Compact ? LauncherSectionMetrics.CompactDrawerToggleHeight : 48);
        toggle.Name = "TechnicalDetailsToggle";
        LauncherButtonStyles.ApplySupportAction(toggle, scale);
        SetDiagnosticsToggleText(toggle, profile, visible: false);
        toggle.Pressed += () =>
        {
            drawer.Visible = !drawer.Visible;
            SetDiagnosticsToggleText(toggle, profile, drawer.Visible);
        };
        root.AddChild(toggle);
        var title = new StyledLabel("Technical details", scale, fontSize: profile.Compact ? LauncherSectionMetrics.PromptFontSize : LauncherViewLayoutMetrics.LogTitleFontSize, align: HorizontalAlignment.Left);
        title.Name = "TechnicalDetailsTitle";
        title.AddThemeColorOverride(LauncherViewLayoutMetrics.ThemeFontColor, LauncherComponentTheme.TextSecondary);
        drawer.AddChild(title);
        var log = BuildLogView(profile);
        log.CustomMinimumSize = new Vector2(0, DiagnosticsLogHeight(profile));
        drawer.AddChild(log);
        root.AddChild(drawer);
        return (log, drawer, toggle);
    }

    private const float CompactDiagnosticsLogViewportHeightRatio = 0.28f;
    private const int CompactDiagnosticsLogMinHeight = 220;
    private const int CompactDiagnosticsLogMaxHeight = 340;
    private static int DiagnosticsLogHeight(LauncherLayoutProfile profile)
    {
        if (!profile.Compact)
            return LauncherViewLayoutMetrics.ScaleInt(180, profile.Scale);
        var viewportHeight = (int)MathF.Round(profile.ViewportSize.Y * CompactDiagnosticsLogViewportHeightRatio, MidpointRounding.AwayFromZero);
        var minHeight = LauncherViewLayoutMetrics.ScaleInt(CompactDiagnosticsLogMinHeight, profile.Scale);
        var maxHeight = LauncherViewLayoutMetrics.ScaleInt(CompactDiagnosticsLogMaxHeight, profile.Scale);
        return Math.Clamp(viewportHeight, minHeight, maxHeight);
    }

    private void UpdateDiagnosticsLogViewport(Vector2 viewportSize)
    {
        if (!GodotObject.IsInstanceValid(Log))
            return;
        var profile = viewportSize.X > 0f && viewportSize.Y > 0f ? LauncherLayoutProfile.ForViewport(viewportSize, _profile.TouchOptimized) : _profile;
        Log.CustomMinimumSize = new Vector2(0, DiagnosticsLogHeight(profile));
    }

    private const string CompactDiagnosticsToggleBodyName = "CompactDiagnosticsToggleBody";
    private const string CompactDiagnosticsToggleTitleName = "CompactDiagnosticsToggleTitle";
    private const string CompactDiagnosticsToggleDetailName = "CompactDiagnosticsToggleDetail";
    private static readonly CompactButtonDetailLabelSpec CompactDiagnosticsToggleLabels = CompactButtonDetailLabelSpec.Default(CompactDiagnosticsToggleBodyName, CompactDiagnosticsToggleTitleName, CompactDiagnosticsToggleDetailName);
    private static void SetDiagnosticsToggleText(Button toggle, LauncherLayoutProfile profile, bool visible)
    {
        if (!profile.Compact)
        {
            CompactButtonDetailLabels.Apply(toggle, DiagnosticsToggleText(visible), profile.Scale, enabled: false, CompactDiagnosticsToggleLabels);
            return;
        }

        CompactButtonDetailLabels.Apply(toggle, visible ? "Hide technical details" : "Show technical details", profile.Scale, enabled: true, CompactDiagnosticsToggleLabels);
    }

    private static string DiagnosticsToggleText(bool visible) => visible ? "Hide technical details" : "Show technical details";
    private readonly record struct DestinationNavigation(MarginContainer Root, Button[] Buttons);
    private static DestinationNavigation BuildDestinationNavigation(LauncherLayoutProfile profile)
    {
        var frame = new MarginContainer
        {
            Name = "DestinationNavigation",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        var navigation = new GridContainer
        {
            Columns = 5,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        navigation.SetColumnSeparation(LauncherViewLayoutMetrics.ScaleInt(profile.Compact ? 3 : 6, profile.Scale));
        frame.AddChild(navigation);
        string[] labels = ["Home", "Saves", "Versions", "Mods", "Help"];
        var buttons = new Button[labels.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            var button = new StyledButton(labels[i], profile.Scale, fontSize: profile.Compact ? 12 : 14, height: 66)
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                FocusMode = Control.FocusModeEnum.All,
                ToggleMode = true,
                Icon = LauncherIcons.Create(i, profile.Scale * (profile.Compact ? 0.83f : 0.75f)),
                IconAlignment = HorizontalAlignment.Center,
                VerticalIconAlignment = VerticalAlignment.Top,
                TooltipText = labels[i],
            };
            LauncherButtonStyles.ApplySupportAction(button, profile.Scale);
            navigation.AddChild(button);
            buttons[i] = button;
        }

        return new DestinationNavigation(frame, buttons);
    }

    internal void UpdateSystemInsets()
    {
        if (!_profile.Compact || !OperatingSystem.IsAndroid())
            return;
        var safeArea = DisplayServer.GetDisplaySafeArea();
        if (safeArea.Size.X <= 0 || safeArea.Size.Y <= 0)
            return;
        var viewportSize = _parent.GetViewport()?.GetVisibleRect().Size ?? _profile.ViewportSize;
        var obscuredLeft = Math.Max(0, (int)MathF.Ceiling(safeArea.Position.X));
        var obscuredTop = Math.Max(0, (int)MathF.Ceiling(safeArea.Position.Y));
        var obscuredRight = Math.Max(0, (int)MathF.Ceiling(viewportSize.X - safeArea.Position.X - safeArea.Size.X));
        var obscuredBottom = Math.Max(0, (int)MathF.Ceiling(viewportSize.Y - safeArea.Position.Y - safeArea.Size.Y));
        var insets = new Vector4(obscuredLeft, obscuredTop, obscuredRight, obscuredBottom);
        if (insets == _systemSafeAreaInsets)
            return;
        _systemSafeAreaInsets = insets;
        _panel.UpdateSafeAreaContentInsets(obscuredLeft, obscuredTop, obscuredRight);
        var bottomInset = obscuredBottom + LauncherViewLayoutMetrics.ScaleInt(4, _profile.Scale);
        _destinationSafeAreaSpacer.CustomMinimumSize = new Vector2(0, bottomInset);
        RequestAndroidCompositionRefresh();
        GD.Print($"Launcher safe area: viewport={viewportSize} safe={safeArea} " + $"insets=({obscuredLeft},{obscuredTop},{obscuredRight},{obscuredBottom})");
    }

    private static LauncherViewPrimaryBody BuildPrimaryColumnBody(LauncherLayoutProfile profile, VBoxContainer root)
    {
        var scale = profile.Scale;
        var leftScroll = new TouchScrollContainer(scale)
        {
            Name = "LauncherPrimaryScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        leftScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        leftScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        leftScroll.FollowFocus = true;
        root.AddChild(leftScroll);
        var leftFrame = new MarginContainer();
        leftFrame.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        leftFrame.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        leftScroll.AddChild(leftFrame);
        var left = new VBoxContainer();
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        left.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        left.SetSeparation(LauncherViewLayoutMetrics.ScaleInt(profile.Compact ? LauncherViewLayoutMetrics.CompactPrimaryColumnSeparation : LauncherViewLayoutMetrics.PrimaryColumnSeparation, scale));
        leftFrame.AddChild(left);
        return new LauncherViewPrimaryBody(leftScroll, left);
    }

    private static LauncherViewPrimaryColumn BuildPrimaryColumn(LauncherLayoutProfile profile, VBoxContainer root)
    {
        var scale = profile.Scale;
        var primaryBody = BuildPrimaryColumnBody(profile, root);
        var left = primaryBody.Body;
        var status = BuildPrimaryStatus(profile);
        left.AddChild(status.Capsule);
        var secondaryStatus = BuildSecondaryStatusBanner(profile);
        left.AddChild(secondaryStatus.Banner);
        var firstRunGuide = BuildFirstRunGuide(scale, profile.Compact);
        var homeSections = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        homeSections.SetSeparation(LauncherViewLayoutMetrics.ScaleInt(profile.Compact ? LauncherViewLayoutMetrics.CompactPrimaryColumnSeparation : LauncherViewLayoutMetrics.PrimaryColumnSeparation, scale));
        left.AddChild(homeSections);
        homeSections.AddChild(firstRunGuide);
        var login = new LoginSection(scale, profile.Compact);
        homeSections.AddChild(login);
        var code = new CodeSection(scale, profile.Compact);
        homeSections.AddChild(code);
        var download = new DownloadSection(scale, profile.Compact, profile.CompactStackedActionRows);
        homeSections.AddChild(download);
        var actions = new ActionSection(scale, profile.Compact);
        left.AddChild(actions);
        return new LauncherViewPrimaryColumn(
            Status: status,
            SecondaryStatus: secondaryStatus,
            PrimaryScroll: primaryBody.PrimaryScroll,
            HomeSections: homeSections,
            FirstRunGuide: firstRunGuide,
            Login: login,
            Code: code,
            Download: download,
            Actions: actions
        );
    }

    private static LauncherViewPrimaryStatus BuildPrimaryStatus(LauncherLayoutProfile profile)
    {
        var scale = profile.Scale;
        const LauncherStatusSeverity initialSeverity = LauncherStatusSeverity.Working;
        var initialPhase = LauncherPortalStatusFormatter.LabelFor(initialSeverity);
        var statusPhaseLabel = new StyledLabel(initialPhase, scale, fontSize: profile.Compact ? 13 : 11, align: HorizontalAlignment.Center);
        statusPhaseLabel.AddThemeColorOverride(LauncherViewLayoutMetrics.ThemeFontColor, LauncherPortalStatusFormatter.ColorFor(initialSeverity));
        statusPhaseLabel.Name = "GlobalStatusSeverity";
        var statusLabel = new StyledLabel(LauncherPortalStatusFormatter.MessageFor("Starting launcher..."), scale, fontSize: profile.Compact ? 15 : 14, align: HorizontalAlignment.Left);
        statusLabel.Name = "GlobalStatusMessage";
        statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        statusLabel.AddThemeColorOverride(LauncherViewLayoutMetrics.ThemeFontColor, LauncherComponentTheme.TextSecondary);
        var statusAccent = new ColorRect();
        statusAccent.Color = LauncherPortalStatusFormatter.ColorFor(initialSeverity);
        var statusCapsule = BuildStatusCapsule(statusPhaseLabel, statusLabel, statusAccent, profile);
        return new LauncherViewPrimaryStatus(statusPhaseLabel, statusLabel, statusAccent, statusCapsule.Capsule, statusCapsule.CompactDetailButton);
    }
}

internal readonly struct LauncherViewPrimaryBody
{
    internal LauncherViewPrimaryBody(ScrollContainer primaryScroll, VBoxContainer body)
    {
        PrimaryScroll = primaryScroll;
        Body = body;
    }

    internal ScrollContainer PrimaryScroll { get; }
    internal VBoxContainer Body { get; }
}

internal readonly record struct LauncherViewPrimaryColumn(
    LauncherViewPrimaryStatus Status,
    LauncherViewSecondaryStatus SecondaryStatus,
    ScrollContainer PrimaryScroll,
    VBoxContainer HomeSections,
    Control FirstRunGuide,
    LoginSection Login,
    CodeSection Code,
    DownloadSection Download,
    ActionSection Actions
);

// CompactDetailButton is null outside the compact layout.
internal readonly record struct LauncherViewPrimaryStatus(
    StyledLabel Phase,
    StyledLabel Message,
    ColorRect Accent,
    Control Capsule,
    Button CompactDetailButton
);
