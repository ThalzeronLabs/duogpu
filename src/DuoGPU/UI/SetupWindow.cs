using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DuoGPU.Core;
using Microsoft.Win32;

namespace DuoGPU.UI;

/// <summary>The first-run setup: what DuoGPU detected, the two inputs, the scale, and a test switch.</summary>
public sealed class SetupWindow : Window
{
    readonly TrayApp app;
    readonly Detect.Proposal proposal;
    readonly Config cfg;
    readonly ComboBox aiGpu = Combo();
    readonly ComboBox aiInput = Combo();
    readonly ComboBox otherInput = Combo();
    readonly ComboBox scale = Combo();
    readonly ComboBox language = Combo();
    readonly CheckBox autostart = new() { Foreground = Ui.Text, FontFamily = Ui.Font, FontSize = 13 };
    readonly TextBlock message = Ui.Label("", 13, Ui.OkText);
    List<GpuInfo> gpus = [];

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public SetupWindow(TrayApp app, Detect.Proposal proposal, Config current)
    {
        this.app = app;
        this.proposal = proposal;
        cfg = current.IsComplete ? Config.FromJson(current.ToJson()) : proposal.Config;
        Title = S.SetupTitle;
        Background = Ui.Background;
        SizeToContent = SizeToContent.Height;
        Width = 560;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/duogpu.ico"));
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = Build() };
    }

    static ComboBox Combo() => new() { FontFamily = Ui.Font, FontSize = 13, Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(8, 5, 8, 5) };

    FrameworkElement Build()
    {
        var root = new StackPanel { Margin = new Thickness(26, 22, 26, 22) };
        root.Children.Add(Ui.Label(S.SetupTitle, 22, Ui.Text, FontWeights.SemiBold));
        var t = proposal.Topology;

        if (proposal.Problem != null)
        {
            var why = Ui.Label(proposal.Problem == "one-gpu" ? S.OneGpu : S.NoDualCable, 14, Ui.Amber);
            why.Margin = new Thickness(0, 14, 0, 0);
            root.Children.Add(why);
            return root;
        }

        var intro = Ui.Label(S.SetupIntro, 13, Ui.Muted);
        intro.Margin = new Thickness(0, 8, 0, 6);
        root.Children.Add(intro);

        var monitor = cfg.SharedMonitor!;
        Field(root, S.SharedMonitorLabel, Ui.Label($"{t.MonitorName(monitor)}  ({monitor})", 15, Ui.Text, FontWeights.SemiBold));

        gpus = t.Outputs.Where(o => o.MonitorIds.Contains(monitor) && o.Gpu != null).Select(o => o.Gpu!)
            .DistinctBy(g => g.Key).OrderByDescending(g => g.Vram).ToList();
        foreach (var g in gpus) aiGpu.Items.Add($"{g.Name} · {S.Gb(g.Vram)}");
        aiGpu.SelectedIndex = Math.Max(0, gpus.FindIndex(g => g.Key == cfg.AiGpu));
        Field(root, S.AiGpuLabel, aiGpu);

        // the inputs: what the monitor lists, or the common ones when it lists nothing
        var inputs = proposal.Inputs.Count > 0 ? proposal.Inputs : [0x0F, 0x10, 0x11, 0x12, 0x1B];
        foreach (var box in new[] { aiInput, otherInput })
        {
            box.Items.Add(S.InputManual);
            foreach (var i in inputs) box.Items.Add(Mccs.InputName(i));
        }
        var hint = Ui.Label(S.InputHint(proposal.CurrentInput is int c ? Mccs.InputName(c) : null), 12, Ui.Muted);
        hint.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(hint);
        var aiLabel = Field(root, "", aiInput);
        var otherLabel = Field(root, "", otherInput);
        void Relabel()
        {
            var (a, o) = Pair();
            aiLabel.Text = S.InputFor(a.Name);
            otherLabel.Text = S.InputFor(o.Name);
            Select(aiInput, cfg.InputFor(a.Key), inputs);
            Select(otherInput, cfg.InputFor(o.Key) ?? inputs.FirstOrDefault(i => i != cfg.InputFor(a.Key)), inputs);
        }
        aiGpu.SelectionChanged += (_, _) => Relabel();
        Relabel();

        scale.Items.Add(S.KeepAsIs);
        foreach (var s in Native.DisplayConfig.ScaleSteps) scale.Items.Add($"{s}%");
        scale.SelectedIndex = cfg.Scale is int sc ? Array.IndexOf(Native.DisplayConfig.ScaleSteps, sc) + 1 : 0;
        Field(root, S.ScaleLabel, scale);

        language.Items.Add("Windows");
        language.Items.Add("English");
        language.Items.Add("Português (Brasil)");
        language.SelectedIndex = cfg.Language switch { "en" => 1, "pt-BR" => 2, _ => 0 };
        Field(root, S.Language, language);

        autostart.Content = S.StartWithWindows;
        autostart.Margin = new Thickness(0, 16, 0, 0);
        autostart.IsChecked = IsAutostart() || !app.Config.IsComplete;
        root.Children.Add(autostart);

        var buttons = new Grid { Margin = new Thickness(0, 22, 0, 0) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        var save = Ui.Button(S.Save, Ui.Line, () => { Apply(); Close(); });
        var test = Ui.Button(S.TestSwitch, Ui.Purple, async () =>
        {
            Apply();
            var r = await app.SwitchToAsync(app.Current.Mode == Mode.Ai ? Mode.Game : Mode.Ai);
            message.Foreground = r.Ok ? Ui.OkText : Ui.Amber;
            message.Text = S.Result(r);
        });
        Grid.SetColumn(test, 2);
        buttons.Children.Add(save);
        buttons.Children.Add(test);
        root.Children.Add(buttons);

        var calibrate = Ui.Link(S.Calibrate, async () =>
        {
            Apply();
            var hz = await new Switcher(app.Config).CalibrateAsync(ConfirmWindow.AskAsync);
            message.Foreground = hz != null ? Ui.OkText : Ui.Amber;
            message.Text = hz is int h ? S.CalibrateDone(h) : S.CalibrateFailed;
        });
        calibrate.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(calibrate);
        message.Margin = new Thickness(0, 12, 0, 0);
        root.Children.Add(message);
        return root;
    }

    (GpuInfo ai, GpuInfo other) Pair()
    {
        var a = gpus[Math.Max(0, aiGpu.SelectedIndex)];
        return (a, gpus.First(g => g.Key != a.Key));
    }

    static void Select(ComboBox box, int? value, List<int> inputs) =>
        box.SelectedIndex = value is int v && inputs.IndexOf(v) is int i and >= 0 ? i + 1 : 0;

    static TextBlock Field(Panel parent, string label, UIElement control)
    {
        var l = Ui.Label(label, 12.5, Ui.Muted);
        l.Margin = new Thickness(0, 12, 0, 0);
        parent.Children.Add(l);
        parent.Children.Add(control);
        return l;
    }

    void Apply()
    {
        var (a, o) = Pair();
        var inputs = proposal.Inputs.Count > 0 ? proposal.Inputs : [0x0F, 0x10, 0x11, 0x12, 0x1B];
        cfg.AiGpu = a.Key;
        cfg.OtherGpu = o.Key;
        cfg.InputByGpu.Clear();
        if (aiInput.SelectedIndex > 0) cfg.InputByGpu[a.Key] = inputs[aiInput.SelectedIndex - 1];
        if (otherInput.SelectedIndex > 0) cfg.InputByGpu[o.Key] = inputs[otherInput.SelectedIndex - 1];
        cfg.Scale = scale.SelectedIndex > 0 ? Native.DisplayConfig.ScaleSteps[scale.SelectedIndex - 1] : null;
        cfg.Language = language.SelectedIndex switch { 1 => "en", 2 => "pt-BR", _ => null };
        // keep what calibration already learned
        foreach (var kv in app.Config.MaxHzByGpu) cfg.MaxHzByGpu.TryAdd(kv.Key, kv.Value);
        SetAutostart(autostart.IsChecked == true);
        app.UseConfig(cfg);
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
