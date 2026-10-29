using System;
using Godot;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher;
internal sealed partial class LauncherView
{
    private static ColorRect BuildConfirmationDialog(string message, LauncherLayoutProfile profile, Action onConfirmed, Action onCancelled = null, string confirmText = null, string cancelText = null)
    {
        var scale = profile.Scale;
        var dialog = new ColorRect
        {
            Color = LauncherComponentTheme.DialogOverlay,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        dialog.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var center = BuildDialogCenter();
        var dialogBox = BuildDialogBox(scale);
        var vbox = BuildDialogContentBox(scale);
        dialogBox.AddChild(vbox);
        var messageArea = BuildDialogMessageArea(message, profile);
        vbox.AddChild(messageArea);
        vbox.AddChild(BuildDialogButtons(dialog, profile, onConfirmed, onCancelled, confirmText, cancelText));
        center.AddChild(dialogBox);
        dialog.AddChild(center);
        void UpdateDialogSize()
        {
            var viewport = dialog.Size;
            if (viewport.X <= 0 || viewport.Y <= 0)
                return;
            var current = LauncherLayoutProfile.ForViewport(viewport, profile.TouchOptimized);
            var width = Math.Min(DialogMessageWidth(current), Math.Max(1, (int)(viewport.X * CompactDialogWidthRatio) - LauncherComponentTheme.ScaleInt(scale, LauncherComponentTheme.DialogPanelMargin * 2)));
            messageArea.CustomMinimumSize = new Vector2(width, Math.Min(DialogMessageScrollHeight(message, current), Math.Max(1, (int)(viewport.Y * CompactDialogMaxMessageHeightRatio))));
        }
        dialog.Resized += UpdateDialogSize;
        return dialog;
    }

    private static CenterContainer BuildDialogCenter()
    {
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Pass;
        return center;
    }

    private static PanelContainer BuildDialogBox(float scale)
    {
        var dialogBox = new PanelContainer();
        dialogBox.MouseFilter = Control.MouseFilterEnum.Pass;
        var boxStyle = new StyleBoxFlat();
        boxStyle.BgColor = LauncherComponentTheme.DialogPanelBackground;
        boxStyle.SetCornerRadiusAll(LauncherComponentTheme.ScaleInt(scale, LauncherComponentTheme.DialogPanelRadius));
        boxStyle.SetContentMarginAll(LauncherComponentTheme.ScaleInt(scale, LauncherComponentTheme.DialogPanelMargin));
        dialogBox.AddThemeStyleboxOverride(LauncherComponentTheme.Panel, boxStyle);
        return dialogBox;
    }

    private static VBoxContainer BuildDialogContentBox(float scale)
    {
        var vbox = new VBoxContainer();
        vbox.MouseFilter = Control.MouseFilterEnum.Pass;
        vbox.SetSeparation(LauncherComponentTheme.ScaleInt(scale, LauncherComponentTheme.DialogContentSeparation));
        return vbox;
    }

    private static Button BuildDialogButton(string text, float scale, Action callback, Action<Button, float> applyStyle)
    {
        var button = new StyledButton(text, scale, LauncherComponentTheme.DialogButtonFontSize, LauncherComponentTheme.DialogButtonHeight);
        button.MouseFilter = Control.MouseFilterEnum.Stop;
        button.CustomMinimumSize = new Vector2(LauncherComponentTheme.ScaleInt(scale, LauncherComponentTheme.DialogButtonWidth), button.CustomMinimumSize.Y);
        applyStyle(button, scale);
        button.Pressed += callback;
        return button;
    }

    private static Container BuildDialogButtons(ColorRect dialog, LauncherLayoutProfile profile, Action onConfirmed, Action onCancelled, string confirmText, string cancelText)
    {
        var scale = profile.Scale;
        var buttonRow = new ResponsiveGrid(LauncherComponentTheme.ScaleInt(scale, LauncherComponentTheme.DialogButtonSeparation), 2);
        buttonRow.Name = "ConfirmationActions";
        buttonRow.MouseFilter = Control.MouseFilterEnum.Pass;

        void Dismiss(Action callback)
        {
            dialog.QueueFree();
            callback?.Invoke();
        }

        var cancel = BuildDialogButton(DialogButtonText(cancelText, "Cancel"), scale, () => Dismiss(onCancelled), LauncherButtonStyles.ApplySupportAction);
        var ok = BuildDialogButton(DialogButtonText(confirmText, "OK"), scale, () => Dismiss(onConfirmed), LauncherButtonStyles.ApplyPrimaryAction);
        buttonRow.AddChild(cancel);
        buttonRow.AddChild(ok);
        return buttonRow;
    }

    private static string DialogButtonText(string text, string fallback) => string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
    private const float CompactDialogWidthRatio = 0.9f;
    private const float CompactDialogMaxMessageHeightRatio = 0.44f;
    private const int CompactDialogMessageMinWidth = 280;
    private const int CompactDialogMessageMinScrollHeight = 96;
    private const int CompactDialogMessageLineHeight = 24;
    private const int CompactDialogLongMessageWidthCharacters = 34;
    private static Control BuildDialogMessageArea(string message, LauncherLayoutProfile profile)
    {
        var label = BuildDialogMessage(message, profile);
        var scroll = new TouchScrollContainer(profile.Scale)
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(1, 1),
        };
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(label);
        return scroll;
    }

    private static Label BuildDialogMessage(string message, LauncherLayoutProfile profile)
    {
        var scale = profile.Scale;
        var label = new StyledLabel(message, scale, fontSize: LauncherComponentTheme.DialogMessageFontSize);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = Vector2.Zero;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    private static int DialogMessageWidth(LauncherLayoutProfile profile)
    {
        var scaledDefault = LauncherComponentTheme.ScaleInt(profile.Scale, LauncherComponentTheme.DialogMessageWidth);
        if (!profile.Compact)
            return scaledDefault;
        var compactCap = Math.Max(CompactDialogMessageMinWidth, (int)(profile.ViewportSize.X * CompactDialogWidthRatio));
        return Math.Min(scaledDefault, compactCap);
    }

    private static int DialogMessageScrollHeight(string message, LauncherLayoutProfile profile)
    {
        var wrappedLines = Math.Max(3, (int)Math.Ceiling((message?.Length ?? 0) / (double)CompactDialogLongMessageWidthCharacters));
        if (!string.IsNullOrEmpty(message))
            wrappedLines += Math.Max(0, message.Split('\n').Length - 1);
        var estimatedHeight = LauncherComponentTheme.ScaleInt(profile.Scale, Math.Max(CompactDialogMessageMinScrollHeight, wrappedLines * CompactDialogMessageLineHeight));
        var maxHeight = Math.Max(LauncherComponentTheme.ScaleInt(profile.Scale, CompactDialogMessageMinScrollHeight), (int)(profile.ViewportSize.Y * CompactDialogMaxMessageHeightRatio));
        return Math.Min(estimatedHeight, maxHeight);
    }

}
