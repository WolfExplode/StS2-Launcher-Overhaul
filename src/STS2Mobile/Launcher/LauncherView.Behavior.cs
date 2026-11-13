using Godot;
using System;

namespace STS2Mobile.Launcher;
internal sealed partial class LauncherView
{
    private AndroidDisplayState? _androidDisplayState;
    private readonly record struct AndroidDisplayState(int KeyboardHeight, Vector2I WindowSize, Vector2 ViewportSize, Rect2I SafeArea, ulong FocusId);

    // Godot exposes keyboard height and safe area through queries. Observe those
    // values each frame, but only enter layout code when a value changes.
    internal void PollAndroidDisplayChanges()
    {
        if (!OperatingSystem.IsAndroid())
            return;
        var focus = _parent.GetViewport()?.GuiGetFocusOwner();
        var state = new AndroidDisplayState(DisplayServer.VirtualKeyboardGetHeight(),
            DisplayServer.WindowGetSize(), _parent.GetViewport()?.GetVisibleRect().Size ?? _profile.ViewportSize,
            DisplayServer.GetDisplaySafeArea(), focus?.GetInstanceId() ?? 0);
        if (_androidDisplayState == state)
            return;
        var previous = _androidDisplayState;
        _androidDisplayState = state;
        if (!previous.HasValue || previous.Value.SafeArea != state.SafeArea || previous.Value.ViewportSize != state.ViewportSize)
            UpdateSystemInsets();
        if (!previous.HasValue || previous.Value.KeyboardHeight != state.KeyboardHeight
            || previous.Value.WindowSize != state.WindowSize || previous.Value.ViewportSize != state.ViewportSize)
            UpdateKeyboardOffset(state.KeyboardHeight);
        else if (state.KeyboardHeight > 0 && previous.Value.FocusId != state.FocusId)
            ScrollFocusedInputAboveKeyboard();
    }

    internal void UpdateViewportSize(Vector2 viewportSize)
    {
        _panel.UpdateSizeFromViewport(viewportSize, _profile.PanelHeightRatio);
        UpdateDiagnosticsLogViewport(viewportSize);
        if (OperatingSystem.IsAndroid())
            PollAndroidDisplayChanges();
        else
            UpdateKeyboardOffset();
    }

    internal void ShowConfirmation(string message, Action onConfirmed, string confirmText, string cancelText)
    {
        _parent.AddChild(BuildConfirmationDialog(message, CurrentConfirmationProfile(), onConfirmed, confirmText: confirmText, cancelText: cancelText));
    }

    internal void ShowSaveSyncConflict(Action useSteamSaves, Action useDeviceSaves) => ShowConfirmation("Saves changed on this device and in Steam Cloud. Choose which copy to keep; the other copy will be replaced. Vanilla and modded saves remain separate.", useDeviceSaves, useSteamSaves, confirmText: "Use this device", cancelText: "Use Steam saves");
    internal void ShowGetSavesOverwriteConfirmation(Action onConfirmed, Action onCancelled) => ShowConfirmation("Steam saves differ from this device. Getting them will replace or delete vanilla and modded saves on this device. Continue?", onConfirmed, onCancelled, confirmText: "Get saves from Steam", cancelText: "Cancel");
    internal void ShowSendSavesOverwriteConfirmation(Action onConfirmed, Action onCancelled) => ShowConfirmation("This device differs from Steam. Sending these saves will replace or delete vanilla and modded saves in Steam Cloud. Continue?", onConfirmed, onCancelled, confirmText: "Send saves to Steam", cancelText: "Cancel");
    internal void ShowConfirmation(string message, Action onConfirmed, Action onCancelled, string confirmText, string cancelText)
    {
        _parent.AddChild(BuildConfirmationDialog(message, CurrentConfirmationProfile(), onConfirmed, onCancelled, confirmText, cancelText));
    }

    private LauncherLayoutProfile CurrentConfirmationProfile()
    {
        var viewportSize = _parent.GetViewport()?.GetVisibleRect().Size ?? _profile.ViewportSize;
        return viewportSize.X > 0f && viewportSize.Y > 0f ? LauncherLayoutProfile.ForViewport(viewportSize, _profile.TouchOptimized) : _profile;
    }

    internal void UpdateKeyboardOffset()
        => UpdateKeyboardOffset(OperatingSystem.IsAndroid() ? DisplayServer.VirtualKeyboardGetHeight() : 0);

    private void UpdateKeyboardOffset(int kbHeight)
    {
        if (kbHeight > 0)
        {
            var windowSize = DisplayServer.WindowGetSize();
            var vpSize = _parent.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            var scale = windowSize.Y > 0 ? vpSize.Y / windowSize.Y : 1f;
            var maxOffset = Math.Max(0f, vpSize.Y * 0.42f);
            _keyboardOffset = Math.Min(kbHeight * scale * 0.85f, maxOffset);
            _panel.SetVerticalShift(_keyboardOffset);
            ScrollFocusedInputAboveKeyboard();
            return;
        }

        _keyboardOffset = 0f;
        _keyboardFocusScrollTarget = null;
        _keyboardFocusScrollOffset = -1f;
        _panel.SetVerticalShift(0f);
    }

    private void ScrollFocusedInputAboveKeyboard()
    {
        var focusOwner = _parent.GetViewport()?.GuiGetFocusOwner();
        if (focusOwner == null || !PrimaryScroll.IsAncestorOf(focusOwner))
            return;
        if (focusOwner == _keyboardFocusScrollTarget && Math.Abs(_keyboardFocusScrollOffset - _keyboardOffset) < 1f)
            return;
        _keyboardFocusScrollTarget = focusOwner;
        _keyboardFocusScrollOffset = _keyboardOffset;
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(PrimaryScroll) || !GodotObject.IsInstanceValid(focusOwner) || !PrimaryScroll.IsInsideTree() || !focusOwner.IsInsideTree())
            {
                return;
            }

            PrimaryScroll.EnsureControlVisible(focusOwner);
        }).CallDeferred();
    }

    // Presses anywhere in the panel bubble here, including those on its
    // controls. The viewport focuses a focusable control before delivering
    // its press, so a press inside the focus owner was meant for it: releasing
    // focus then would close the keyboard under a text field, or cancel a
    // button's press.
    private void DismissKeyboard(InputEvent ev)
    {
        if (ev is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true })
        {
            var focusOwner = _parent.GetViewport()?.GuiGetFocusOwner();
            if (focusOwner != null && focusOwner.GetGlobalRect().HasPoint(focusOwner.GetGlobalMousePosition()))
                return;
            _parent.GetViewport()?.GuiReleaseFocus();
            if (OperatingSystem.IsAndroid())
            {
                try
                {
                    DisplayServer.VirtualKeyboardHide();
                }
                catch
                {
                // Some Android backends may already have removed the IME connection.
                }

                AndroidGodotAppBridge.NotifyLauncherTextEditingRequested(false);
            }
        }
    }
}
