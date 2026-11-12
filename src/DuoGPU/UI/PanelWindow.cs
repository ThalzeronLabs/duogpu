using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DuoGPU.Core;

namespace DuoGPU.UI;

/// <summary>The small panel above the tray: the mode, the path, the free VRAM, the checks and one button.</summary>
public sealed class PanelWindow : Window
{
    readonly TrayApp? app;
    bool showHelp;

    /// <summary>Opens the offline help under the button (also used for the README screenshot).</summary>
    public bool ShowHelp { get => showHelp; set => showHelp = value; }

    public PanelWindow(TrayApp? app)
    {
        this.app = app;
        Title = "DuoGPU";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        Left = -10000;
        Deactivated += (_, _) => Hide();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Hide(); };
    }

    public void ShowNearTray()
    {
        Render(app!.Current, app.Busy);
        Show();
        UpdateLayout();
        var wa = Ui.Bounds(null, workArea: true);
        Ui.PlaceInPixels(this, (w, h) => (wa.Right - w - 16, wa.Bottom - h - 16));
        Activate();
    }

    public void Render(Status st, bool busy) => Content = Build(st, busy, app!.Config);

    /// <summary>Builds the panel. Also used to render the screenshots in the README from a given status.</summary>
    public FrameworkElement Build(Status st, bool busy, Config cfg)
    {
        var mode = st.Mode;
        bool attention = st.NeedsAttention;
        var color = attention ? Ui.Amber : mode == Mode.Ai ? Ui.Purple : Ui.Green;
        string aiName = st.AiGpu?.Name ?? "GPU";
        string monitor = string.IsNullOrEmpty(st.MonitorName) ? "monitor" : st.MonitorName;

        var root = new StackPanel { Width = 380 };

        // header: the mode pill and the close button
        var header = new DockPanel();
        var close = Ui.Label("✕", 15, Ui.Muted);
        close.Cursor = Cursors.Hand;
        close.MouseLeftButtonUp += (_, _) => Hide();
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(new Border
        {
            Background = color,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 4, 12, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = Ui.Label(S.ModeName(mode), 18, Brushes.White, FontWeights.Bold),
        });
        root.Children.Add(header);

        var sub = Ui.Label(S.Subtitle(mode, aiName, monitor), 13, Ui.Muted);
        sub.Margin = new Thickness(0, 10, 0, 0);
        root.Children.Add(sub);

        // the card: where the monitor is plugged and how much VRAM the AI GPU has left
        var card = new StackPanel();
        if (st.SharedOutput is { Placement: { } p, Gpu: { } g })
        {
            string input = cfg.InputFor(g.Key) is int i ? $" · {Mccs.InputName(i).Split(" (")[0]}" : "";
            card.Children.Add(Ui.Label(S.Path(monitor, g.Name + input), 14, Ui.Text, FontWeights.SemiBold));
            var sig = Ui.Label(S.Signal(p.Width, p.Height, p.Hz, st.Scale), 12.5, Ui.Muted);
            sig.Margin = new Thickness(0, 3, 0, 0);
            card.Children.Add(sig);
        }
        if (st.AiVramUsed is ulong used && st.AiGpu is { Vram: > 0 } ai)
        {
            double usedGb = used / (double)(1UL << 30), totalGb = ai.Vram / (double)(1UL << 30);
            var v = Ui.Label(S.FreeVram(ai.Name, usedGb, Math.Max(0, totalGb - usedGb)), 12.5, Ui.Muted);
            v.Margin = new Thickness(0, 10, 0, 6);
            card.Children.Add(v);
            card.Children.Add(Bar(Math.Clamp(usedGb / totalGb, 0, 1), mode == Mode.Ai ? Ui.Purple : Ui.Green));
        }
        if (card.Children.Count > 0)
            root.Children.Add(new Border
            {
                Background = Ui.Card, CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 11, 14, 12),
                Margin = new Thickness(0, 14, 0, 0), Child = card,
            });

        // the checks
        var checks = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        if (st.SharedOutput != null) checks.Children.Add(CheckLine(true, S.OnceOnly(monitor)));
        foreach (var c in st.Checks) checks.Children.Add(CheckLine(c.Ok, S.CheckText(c, monitor)));
        root.Children.Add(checks);

        // the one button
        Border button;
        if (busy) button = Ui.Button(S.Switching, Ui.Line, null);
        else if (attention) button = Ui.Button(S.FixNow, Ui.Amber, () => { Hide(); app?.Fix(); });
        else
        {
            var target = mode == Mode.Ai ? Mode.Game : Mode.Ai;
            button = Ui.Button(S.SwitchTo(target), target == Mode.Game ? Ui.Green : Ui.Purple, () => { Hide(); app?.SwitchTo(target); });
        }
        button.Margin = new Thickness(0, 16, 0, 0);
        root.Children.Add(button);

        // the footer
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var when = Ui.Label(S.CheckedAt(st.When), 11.5, Ui.Muted);
        DockPanel.SetDock(when, Dock.Right);
        footer.Children.Add(when);
        var links = new StackPanel { Orientation = Orientation.Horizontal };
        links.Children.Add(Ui.Link(showHelp ? S.HideHelp : S.Help, () => { showHelp = !showHelp; Render(app!.Current, app.Busy); }));
        var setup = Ui.Link(S.Settings, () => { Hide(); app?.OpenSetup(); });
        setup.Margin = new Thickness(14, 0, 0, 0);
        links.Children.Add(setup);
        footer.Children.Add(links);
        root.Children.Add(footer);

        if (showHelp)
        {
            string? Input(string? gpu) => gpu != null && cfg.InputFor(gpu) is int i ? Mccs.InputName(i) : null;
            var help = Ui.Label(S.HelpText(monitor, aiName, st.OtherGpu?.Name ?? "GPU", Input(cfg.AiGpu), Input(cfg.OtherGpu), cfg.ConfirmSeconds), 11.5);
            root.Children.Add(new Border
            {
                Background = Ui.Card, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 10, 0, 0), Child = help,
            });
        }

        return new Border
        {
            Background = Ui.Background,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(22, 18, 22, 18),
            Child = root,
        };
    }

    static FrameworkElement CheckLine(bool ok, string text)
    {
        var line = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var mark = Ui.Label(ok ? "✓" : "!", 13, ok ? Ui.OkText : Ui.Amber, FontWeights.Bold);
        mark.Width = 20;
        line.Children.Add(mark);
        line.Children.Add(Ui.Label(text, 13, ok ? Ui.OkText : Ui.Amber));
        return line;
    }

    static FrameworkElement Bar(double fraction, Brush color)
    {
        var grid = new Grid { Height = 6 };
        grid.Children.Add(new Border { Background = Ui.Line, CornerRadius = new CornerRadius(3) });
        var fill = new Border { Background = color, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left };
        grid.SizeChanged += (_, e) => fill.Width = Math.Max(6, e.NewSize.Width * fraction);
        grid.Children.Add(fill);
        return grid;
    }
}
