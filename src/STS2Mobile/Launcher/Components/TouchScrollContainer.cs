using System;
using Godot;

namespace STS2Mobile.Launcher.Components;

// A scroll area that a finger drag can scroll from anywhere inside it.
//
// Godot only drag-scrolls a ScrollContainer when the input bubbles up to it,
// and a control with MouseFilter.Stop ends the bubbling. Buttons and panels
// default to Stop, so on a touchscreen most of a page could not start a
// scroll. Every descendant that stops input is switched to Pass as it enters
// the tree: taps still reach the control under the finger, and once a drag
// passes the deadzone the container sends NOTIFICATION_SCROLL_BEGIN, which
// cancels the pending button press. Controls that give a drag its own meaning
// keep Stop.
internal sealed partial class TouchScrollContainer : ScrollContainer
{
    private const int ScrollBarWidth = 6;
    private const int MinimumGrabberLength = 24;

    internal TouchScrollContainer(float scale)
        => StyleVerticalScrollBar(GetVScrollBar(), scale);

    // A node enters the tree after its parent, so subscribing here also
    // covers content that was built before this container was added.
    public override void _EnterTree()
        => GetTree().NodeAdded += OnNodeAdded;

    public override void _ExitTree()
        => GetTree().NodeAdded -= OnNodeAdded;

    private void OnNodeAdded(Node node)
    {
        if (node is Control { MouseFilter: MouseFilterEnum.Stop } control && !OwnsDragGesture(control) && IsAncestorOf(control))
            control.MouseFilter = MouseFilterEnum.Pass;
    }

    // A scrollbar drag moves its grabber and a text-field drag selects text.
    private static bool OwnsDragGesture(Control control) => control is ScrollBar or LineEdit or TextEdit;

    // The default theme's scrollbar is a few unscaled pixels wide, which is
    // nearly invisible on a high-density phone screen.
    private static void StyleVerticalScrollBar(ScrollBar bar, float scale)
    {
        var width = LauncherComponentTheme.ScaleInt(scale, ScrollBarWidth);
        var radius = Math.Max(1, width / 2);
        var track = LauncherStyleBoxes.MakeFilled(LauncherComponentTheme.ScrollTrack, radius);
        track.ContentMarginLeft = width / 2f;
        track.ContentMarginRight = width - width / 2f;
        bar.AddThemeStyleboxOverride("scroll", track);
        bar.AddThemeStyleboxOverride("scroll_focus", track);
        bar.AddThemeStyleboxOverride("grabber", Grabber(LauncherComponentTheme.CyanDim, radius, scale));
        bar.AddThemeStyleboxOverride("grabber_highlight", Grabber(LauncherComponentTheme.CyanAccent.Darkened(0.25f), radius, scale));
        bar.AddThemeStyleboxOverride("grabber_pressed", Grabber(LauncherComponentTheme.CyanAccent, radius, scale));
    }

    private static StyleBoxFlat Grabber(Color color, int radius, float scale)
    {
        var grabber = LauncherStyleBoxes.MakeFilled(color, radius);
        var halfLength = LauncherComponentTheme.ScaleInt(scale, MinimumGrabberLength) / 2f;
        grabber.ContentMarginTop = halfLength;
        grabber.ContentMarginBottom = halfLength;
        return grabber;
    }
}
