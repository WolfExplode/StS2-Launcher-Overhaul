using System;
using Godot;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;

namespace MobileConsole;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    public static void Initialize()
    {
        if (OperatingSystem.IsAndroid())
            Callable.From(Attach).CallDeferred();
    }

    private static void Attach()
    {
        if (NGame.Instance is not { } game || !game.IsInsideTree())
            return;

        var layer = new CanvasLayer { Name = "MobileConsole", Layer = 100 };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);

        var open = MakeButton("Console");
        open.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        open.OffsetLeft = -168;
        open.OffsetRight = -16;
        open.OffsetTop = 105;
        open.OffsetBottom = 161;
        root.AddChild(open);

        var modal = new ColorRect { Color = new Color(0, 0, 0, 0.55f), Visible = false };
        modal.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(modal);

        var panel = new PanelContainer();
        modal.AddChild(panel);
        panel.AnchorLeft = 0.08f;
        panel.AnchorRight = 0.92f;
        panel.AnchorTop = 0.025f;
        panel.AnchorBottom = 0.42f;
        var fields = new VBoxContainer();
        fields.AddThemeConstantOverride("separation", 8);
        panel.AddChild(fields);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        fields.AddChild(row);
        var command = new LineEdit
        {
            PlaceholderText = "Command",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VirtualKeyboardEnabled = true,
            ClearButtonEnabled = true
        };
        command.AddThemeFontSizeOverride("font_size", 27);
        row.AddChild(command);
        var run = MakeButton("Run");
        var close = MakeButton("Close");
        row.AddChild(run);
        row.AddChild(close);
        var suggestionScroll = new ScrollContainer
        {
            Visible = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            ScrollDeadzone = 12
        };
        var suggestionRows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        suggestionScroll.AddChild(suggestionRows);
        fields.AddChild(suggestionScroll);
        var output = new RichTextLabel
        {
            Text = "Enter help to list commands.",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectionEnabled = true
        };
        output.AddThemeFontSizeOverride("normal_font_size", 24);
        fields.AddChild(output);

        DevConsole? console = null;

        void ClearSuggestions()
        {
            foreach (var child in suggestionRows.GetChildren())
            {
                suggestionRows.RemoveChild(child);
                child.QueueFree();
            }
            suggestionScroll.Hide();
            output.Show();
        }

        void RefreshSuggestions(string text)
        {
            ClearSuggestions();
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                console ??= new DevConsole(shouldAllowDebugCommands: true);
                foreach (var option in ConsoleSuggestions.Get(console, text.TrimStart()))
                {
                    var choice = MakeButton(option.Label);
                    choice.Alignment = HorizontalAlignment.Left;
                    choice.Pressed += () =>
                    {
                        command.Text = option.Text;
                        command.CaretColumn = option.Text.Length;
                        if (option.Text.EndsWith(' '))
                            RefreshSuggestions(option.Text);
                        else
                        {
                            command.ReleaseFocus();
                            DisplayServer.VirtualKeyboardHide();
                            ClearSuggestions();
                        }
                    };
                    suggestionRows.AddChild(choice);
                }
            }
            catch (Exception error)
            {
                ClearSuggestions();
                GD.PushWarning($"[MobileConsole] Suggestions unavailable: {error.Message}");
            }

            if (suggestionRows.GetChildCount() > 0)
            {
                output.Hide();
                suggestionScroll.Show();
                suggestionScroll.ScrollVertical = 0;
            }
        }

        void Close()
        {
            command.ReleaseFocus();
            DisplayServer.VirtualKeyboardHide();
            modal.Hide();
            open.Show();
        }

        void Submit(string text)
        {
            text = text.Trim();
            if (text.Length == 0)
                return;
            command.ReleaseFocus();
            DisplayServer.VirtualKeyboardHide();
            command.Clear();
            ClearSuggestions();
            switch (text)
            {
                case "exit":
                    Close();
                    return;
                case "clear":
                    output.Clear();
                    return;
            }

            try
            {
                console ??= new DevConsole(shouldAllowDebugCommands: true);
                var result = console.ProcessCommand(text);
                output.Text = $"> {text}\n{result.msg}";
            }
            catch (Exception error)
            {
                output.Text = $"Command failed: {error.Message}";
                GD.PushWarning($"[MobileConsole] {error}");
            }
        }

        command.TextChanged += RefreshSuggestions;
        command.TextSubmitted += Submit;
        run.Pressed += () => Submit(command.Text);
        close.Pressed += Close;
        open.Pressed += () =>
        {
            open.Hide();
            modal.Show();
            RefreshSuggestions(command.Text);
        };
        game.AddChild(layer);
        GD.Print("[MobileConsole] Touch controls attached");
    }

    private static Button MakeButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(96, 56),
            FocusMode = Control.FocusModeEnum.None
        };
        button.AddThemeFontSizeOverride("font_size", 25);
        return button;
    }
}
