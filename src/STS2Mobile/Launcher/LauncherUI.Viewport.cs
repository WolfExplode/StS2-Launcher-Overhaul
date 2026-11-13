using Godot;

namespace STS2Mobile.Launcher;

internal sealed partial class LauncherUI
{
    // The launcher stays anchored to its parent's full rect, so a rotation or
    // window resize reaches it through layout. Writing Size here instead would
    // fix its offsets to the parent's size at that moment.
    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            _view?.UpdateViewportSize(Size);
    }

    private Vector2 GetViewportSize()
        => GetViewport()?.GetVisibleRect().Size ?? DefaultViewportSize;
}
