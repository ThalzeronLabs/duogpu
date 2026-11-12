using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DuoGPU.Core;
using Microsoft.Win32;

namespace DuoGPU.UI;

/// <summary>
/// The guided setup, seven short steps: welcome, what was found, the inputs, the preferences, a real test of each
/// path with its refresh limit, and a summary. Every step says what it does, why, and how long it takes.
/// </summary>
public sealed class SetupWindow : Window
{
    const int Steps = 7;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    readonly TrayApp? app;
    readonly Status preview;
    readonly Detect.Proposal proposal;
    readonly Config cfg;
    readonly List<GpuInfo> gpus;
    readonly List<int> inputs;
    int step;
    int aiIndex;
    int aiInputIndex, otherInputIndex, scaleIndex, languageIndex;
    bool autostart;
    string status = "";
    bool working;

    public SetupWindow(TrayApp? app, Detect.Proposal proposal, Config current, bool firstRun)
    {
        this.app = app;
        preview = app?.Current ?? Status.Read(current);
        this.proposal = proposal;
        cfg = current.IsComplete ? Config.FromJson(current.ToJson()) : proposal.Config;
        Title = S.SetupTitle;
        Background = Ui.Background;
        Width = 660;
        Height = 640;
        ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/duogpu.ico"));

        var monitor = cfg.SharedMonitor;
        gpus = monitor == null ? [] : proposal.Topology.Outputs
            .Where(o => o.MonitorIds.Contains(monitor) && o.Gpu != null).Select(o => o.Gpu!)
            .DistinctBy(g => g.Key).OrderByDescending(g => g.Vram).ToList();
        inputs = proposal.Inputs.Count > 0 ? proposal.Inputs : [0x0F, 0x10, 0x11, 0x12, 0x1B];
        aiIndex = Math.Max(0, gpus.FindIndex(g => g.Key == cfg.AiGpu));
        LoadInputs();
        scaleIndex = cfg.Scale is int sc ? Array.IndexOf(Native.DisplayConfig.ScaleSteps, sc) + 1 : 0;
        languageIndex = cfg.Language switch { "en" => 1, "pt-BR" => 2, _ => 0 };
        autostart = IsAutostart() || firstRun;
        step = firstRun || proposal.Problem != null ? 0 : 1;
        Render();
    }

    Status Current => app?.Current ?? preview;
    Config Saved => app?.Config ?? cfg;

    /// <summary>One step, drawn off-screen for the README.</summary>
    public FrameworkElement RenderStep(int s)
    {
        step = s;
        Render();
        var c = (FrameworkElement)Content;
        Content = null;
        return new Border { Background = Ui.Background, Width = 644, Height = 600, Child = c };
    }

    GpuInfo AiGpu => gpus[aiIndex];
    GpuInfo OtherGpu => gpus.First(g => g.Key != AiGpu.Key);

    void LoadInputs()
    {
        if (gpus.Count < 2) return;
        int? ai = cfg.InputFor(AiGpu.Key), other = cfg.InputFor(OtherGpu.Key);
        other ??= inputs.FirstOrDefault(i => i != ai);
        aiInputIndex = ai is int a && inputs.IndexOf(a) is int x and >= 0 ? x + 1 : 0;
        otherInputIndex = other is int o && inputs.IndexOf(o) is int y and >= 0 ? y + 1 : 0;
    }

    // ---------- the frame: step counter, title, body, the time it takes, navigation ----------

    void Render()
    {
        var body = new StackPanel();
        string title;
        string? time = S.NoScreenChange;
        if (proposal.Problem != null)
        {
            title = S.SetupTitle;
            body.Children.Add(Text(proposal.Problem == "one-gpu" ? S.OneGpu : S.NoDualCable, Ui.Amber));
            time = null;
        }
        else
        {
            (title, time) = step switch
            {
                0 => (S.WelcomeTitle, Welcome(body)),
                1 => (S.FoundTitle, Found(body)),
                2 => (S.InputsTitle, Inputs(body)),
                3 => (S.PrefsTitle, Prefs(body)),
                4 => (S.PathTitle(Mode.Ai, OtherGpu.Name), PathStep(body, Mode.Ai)),
                5 => (S.PathTitle(Mode.Game, AiGpu.Name), PathStep(body, Mode.Game)),
                _ => (S.DoneTitle, Done(body)),
            };
        }

        var root = new DockPanel { Margin = new Thickness(30, 22, 30, 22) };

        var top = new StackPanel();
        top.Children.Add(Dots());
        var t = Ui.Label(title, 24, Ui.Text, FontWeights.SemiBold);
        t.Margin = new Thickness(0, 14, 0, 0);
        top.Children.Add(t);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        var nav = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        nav.ColumnDefinitions.Add(new ColumnDefinition());
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (step > 0 && proposal.Problem == null)
        {
            var back = Ui.Button(S.Back, Ui.Line, working ? null : () => Go(step - 1));
            back.MinWidth = 120;
            nav.Children.Add(back);
        }
        bool last = step == Steps - 1 || proposal.Problem != null;
        var next = Ui.Button(last ? (proposal.Problem != null ? S.Close : S.Finish) : S.Next, Ui.Purple,
            working ? null : () => { if (last) Close(); else Go(step + 1); });
        next.MinWidth = 160;
        Grid.SetColumn(next, 2);
        nav.Children.Add(next);
        DockPanel.SetDock(nav, Dock.Bottom);
        root.Children.Add(nav);

        if (time != null)
        {
            var chip = new Border
            {
                Background = Ui.Card, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 7, 12, 8),
                Margin = new Thickness(0, 14, 0, 0), Child = Ui.Label("⏱  " + time, 12.5, Ui.Muted),
            };
            DockPanel.SetDock(chip, Dock.Bottom);
            root.Children.Add(chip);
        }

        body.Margin = new Thickness(0, 12, 6, 0);
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body });
        Content = root;
    }

    FrameworkElement Dots()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < Steps; i++)
        {
            row.Children.Add(new Border
            {
                Width = i == step ? 26 : 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 6, 0),
                Background = i <= step ? Ui.Purple : Ui.Line,
            });
        }
        var label = Ui.Label(S.Step(step + 1, Steps), 12, Ui.Muted);
        label.Margin = new Thickness(8, -3, 0, 0);
        row.Children.Add(label);
        return row;
    }

    void Go(int to)
    {
        if (step == 3 && to == 4) Apply(); // "Next" on the preferences saves them
        step = Math.Clamp(to, 0, Steps - 1);
        status = "";
        Render();
    }

    static TextBlock Text(string s, Brush? color = null, double size = 13.5)
    {
        var t = Ui.Label(s, size, color ?? Ui.Text);
        t.Margin = new Thickness(0, 0, 0, 12);
        t.LineHeight = size * 1.45;
        return t;
    }

    static Border Box(UIElement child, Brush? border = null) => new()
    {
        Background = Ui.Card, CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 12, 14, 12),
        BorderBrush = border ?? Ui.Card, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 12), Child = child,
    };

    static ComboBox Combo(IEnumerable<string> items, int selected, Action<int> changed)
    {
        var c = new ComboBox { FontFamily = Ui.Font, FontSize = 13, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 4, 0, 12) };
        foreach (var i in items) c.Items.Add(i);
        c.SelectedIndex = Math.Clamp(selected, 0, c.Items.Count - 1);
        c.SelectionChanged += (_, _) => changed(c.SelectedIndex);
        return c;
    }

    static TextBlock Caption(string s)
    {
        var t = Ui.Label(s, 12.5, Ui.Muted);
        t.Margin = new Thickness(0, 4, 0, 0);
        return t;
    }

    // ---------- the steps ----------

    string? Welcome(StackPanel body)
    {
        body.Children.Add(Text(S.WelcomeBody));
        body.Children.Add(Box(Ui.Label("🛟  " + S.WelcomeSafety, 13, Ui.OkText), Ui.Green));
        return S.WelcomeTime;
    }

    string? Found(StackPanel body)
    {
        var t = proposal.Topology;
        var list = new StackPanel();
        list.Children.Add(Ui.Label(S.FoundGpus, 12.5, Ui.Muted));
        foreach (var g in t.Gpus) list.Children.Add(Ui.Label($"•  {g.Name}  ·  {S.Gb(g.Vram)} VRAM", 14, Ui.Text, FontWeights.SemiBold));
        var m = Ui.Label(S.FoundMonitor, 12.5, Ui.Muted);
        m.Margin = new Thickness(0, 10, 0, 0);
        list.Children.Add(m);
        list.Children.Add(Ui.Label($"•  {t.MonitorName(cfg.SharedMonitor!)}", 14, Ui.Text, FontWeights.SemiBold));
        var others = t.Outputs.Where(o => o.Active && !o.MonitorIds.Contains(cfg.SharedMonitor!))
            .SelectMany(o => o.MonitorIds).Distinct().Select(t.MonitorName).ToList();
        if (others.Count > 0)
        {
            var o = Ui.Label(S.FoundOthers, 12.5, Ui.Muted);
            o.Margin = new Thickness(0, 10, 0, 0);
            list.Children.Add(o);
            foreach (var name in others) list.Children.Add(Ui.Label($"•  {name}", 13.5));
        }
        body.Children.Add(Box(list));
        body.Children.Add(Text(S.FoundWhy, Ui.Muted));
        body.Children.Add(Caption(S.AiGpuLabel));
        body.Children.Add(Combo(gpus.Select(g => $"{g.Name} · {S.Gb(g.Vram)}"), aiIndex, i => { aiIndex = i; LoadInputs(); }));
        return S.NoScreenChange;
    }

    string? Inputs(StackPanel body)
    {
        body.Children.Add(Text(S.InputsWhy, Ui.Muted));
        var active = proposal.Topology.ActiveOutputsFor(cfg.SharedMonitor!).FirstOrDefault();
        body.Children.Add(Box(Ui.Label(proposal.CurrentInput is int c && active?.Gpu != null
            ? "🔌  " + S.InputsRead(Mccs.InputName(c), active.Gpu.Name)
            : "⚠  " + S.InputsNoAnswer, 13, proposal.CurrentInput != null ? Ui.OkText : Ui.Amber)));
        var names = new[] { S.InputManual }.Concat(inputs.Select(Mccs.InputName)).ToList();
        body.Children.Add(Caption(S.InputFor(AiGpu.Name)));
        body.Children.Add(Combo(names, aiInputIndex, i => aiInputIndex = i));
        body.Children.Add(Caption(S.InputFor(OtherGpu.Name)));
        body.Children.Add(Combo(names, otherInputIndex, i => otherInputIndex = i));
        body.Children.Add(Text(S.InputsTip, Ui.Muted, 12.5));
        return S.NoScreenChange;
    }

    string? Prefs(StackPanel body)
    {
        body.Children.Add(Caption(S.ScaleLabel));
        body.Children.Add(Combo(new[] { S.KeepAsIs }.Concat(Native.DisplayConfig.ScaleSteps.Select(s => $"{s}%")), scaleIndex, i => scaleIndex = i));
        body.Children.Add(Text(S.PrefsScaleWhy, Ui.Muted, 12.5));
        var check = new CheckBox { Content = S.StartWithWindows, IsChecked = autostart, Foreground = Ui.Text, FontFamily = Ui.Font, FontSize = 13.5, Margin = new Thickness(0, 4, 0, 4) };
        check.Checked += (_, _) => autostart = true;
        check.Unchecked += (_, _) => autostart = false;
        body.Children.Add(check);
        body.Children.Add(Text(S.PrefsStartWhy, Ui.Muted, 12.5));
        body.Children.Add(Caption(S.Language));
        body.Children.Add(Combo(["Windows", "English", "Português (Brasil)"], languageIndex, i =>
        {
            languageIndex = i;
            S.Use(i switch { 1 => "en", 2 => "pt-BR", _ => System.Globalization.CultureInfo.CurrentUICulture.Name });
            Title = S.SetupTitle;
            Dispatcher.BeginInvoke(Render);
        }));
        body.Children.Add(Text(S.PrefsSaves, Ui.Muted, 12.5));
        return S.NoScreenChange;
    }

    string? PathStep(StackPanel body, Mode mode)
    {
        body.Children.Add(Text(S.PathWhat));
        body.Children.Add(Text(S.PathHzWhy, Ui.Muted, 12.5));

        var gpu = cfg.GpuFor(mode);
        bool inMode = Current.Mode == mode && !Current.NeedsAttention;
        var switchButton = Ui.Button(inMode ? S.AlreadyIn(mode) : S.SwitchNow(mode), mode == Mode.Ai ? Ui.Purple : Ui.Green,
            inMode || working ? null : async () =>
            {
                working = true;
                status = "";
                Render();
                var r = await app!.SwitchToAsync(mode);
                working = false;
                status = S.Result(r);
                Render();
            });
        switchButton.Margin = new Thickness(0, 4, 0, 10);
        body.Children.Add(switchButton);

        int? tested = Saved.MaxHzFor(gpu);
        var hzButton = Ui.Button(tested is int hz ? S.FoundHz(hz) : S.FindHz, Ui.Line, !inMode || working ? null : async () =>
        {
            working = true;
            Render();
            var found = await new Switcher(Saved).CalibrateAsync(ConfirmWindow.AskAsync, h =>
                Dispatcher.Invoke(() => { status = S.Trying(h); Render(); }));
            working = false;
            status = found is int f ? S.CalibrateDone(f) : S.CalibrateFailed;
            Render();
        });
        if (tested != null && inMode && !working) hzButton.Opacity = 0.9;
        body.Children.Add(hzButton);

        var note = Ui.Label(status != "" ? status : inMode ? S.SkipHint : S.SwitchFirst(mode), 13,
            status != "" ? Ui.OkText : Ui.Muted);
        note.Margin = new Thickness(0, 12, 0, 0);
        body.Children.Add(note);
        return S.PathTime;
    }

    string? Done(StackPanel body)
    {
        body.Children.Add(Text(S.DoneHow));
        var c = Saved;
        var t = proposal.Topology;
        string Input(string key) => c.InputFor(key) is int i ? Mccs.InputName(i) : S.ByHand;
        string Hz(string key) => c.MaxHzFor(key) is int h ? $"{h} Hz" : S.NotTested;
        var sum = new StackPanel();
        sum.Children.Add(Ui.Label(S.Summary, 12.5, Ui.Muted));
        sum.Children.Add(Ui.Label($"{t.MonitorName(c.SharedMonitor!)}", 14, Ui.Text, FontWeights.SemiBold));
        sum.Children.Add(Ui.Label($"{S.ModeShort(Mode.Ai)}: {OtherGpu.Name} · {Input(OtherGpu.Key)} · {Hz(OtherGpu.Key)}", 13, Ui.Purple));
        sum.Children.Add(Ui.Label($"{S.ModeShort(Mode.Game)}: {AiGpu.Name} · {Input(AiGpu.Key)} · {Hz(AiGpu.Key)}", 13, Ui.Green));
        if (c.Scale is int s) sum.Children.Add(Ui.Label($"{S.ScaleLabel}: {s}%", 13, Ui.Muted));
        body.Children.Add(Box(sum));
        return null;
    }

    // ---------- saving ----------

    void Apply()
    {
        cfg.AiGpu = AiGpu.Key;
        cfg.OtherGpu = OtherGpu.Key;
        cfg.InputByGpu.Clear();
        if (aiInputIndex > 0) cfg.InputByGpu[AiGpu.Key] = inputs[aiInputIndex - 1];
        if (otherInputIndex > 0) cfg.InputByGpu[OtherGpu.Key] = inputs[otherInputIndex - 1];
        cfg.Scale = scaleIndex > 0 ? Native.DisplayConfig.ScaleSteps[scaleIndex - 1] : null;
        cfg.Language = languageIndex switch { 1 => "en", 2 => "pt-BR", _ => null };
        foreach (var kv in Saved.MaxHzByGpu) cfg.MaxHzByGpu.TryAdd(kv.Key, kv.Value);
        SetAutostart(autostart);
        app?.UseConfig(cfg);
    }

    static bool IsAutostart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue("DuoGPU") != null;
    }

    static void SetAutostart(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue("DuoGPU", $"\"{Environment.ProcessPath}\" --tray");
        else k.DeleteValue("DuoGPU", false);
    }
}
