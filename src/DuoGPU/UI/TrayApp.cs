using System.Windows;
using System.Windows.Threading;
using DuoGPU.Core;
using Forms = System.Windows.Forms;

namespace DuoGPU.UI;

/// <summary>The tray icon, the guard that checks the displays every 5 seconds, and the windows.</summary>
public sealed class TrayApp
{
    readonly Application wpf;
    readonly Forms.NotifyIcon icon;
    readonly DispatcherTimer guard = new() { Interval = TimeSpan.FromSeconds(5) };
    readonly PanelWindow panel;
    SetupWindow? setup;
    string? lastAlert;
    bool checking;

    public Config Config { get; private set; }
    public Status Current { get; private set; } = new() { Mode = Mode.Unknown };
    public bool Busy => Switcher.IsBusy;

    TrayApp(Application wpf, Config cfg)
    {
        this.wpf = wpf;
        Config = cfg;
        S.Use(cfg.Language);
        panel = new PanelWindow(this);
        icon = new Forms.NotifyIcon { Visible = true, Text = "DuoGPU" };
        icon.MouseUp += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                if (panel.IsVisible) panel.Hide();
                else panel.ShowNearTray();
            }
        };
        icon.BalloonTipClicked += (_, _) => panel.ShowNearTray();
        BuildMenu();
        UpdateIcon();
        guard.Tick += async (_, _) => await CheckAsync();
    }

    void BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(S.OpenPanel, null, (_, _) => panel.ShowNearTray());
        var toggle = menu.Items.Add("", null, (_, _) => SwitchTo(Current.Mode == Mode.Ai ? Mode.Game : Mode.Ai));
        menu.Items.Add(S.FixNow, null, (_, _) => Fix());
        menu.Items.Add(S.RememberOthers, null, (_, _) => RememberOthers());
        menu.Items.Add(S.Settings, null, (_, _) => OpenSetup());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(S.Quit, null, (_, _) => Quit());
        menu.Opening += (_, _) => toggle.Text = S.SwitchTo(Current.Mode == Mode.Ai ? Mode.Game : Mode.Ai);
        icon.ContextMenuStrip = menu;
    }

    public void UseConfig(Config cfg)
    {
        Config = cfg;
        cfg.Save();
        S.Use(cfg.Language);
        BuildMenu();
        _ = CheckAsync();
    }

    async Task CheckAsync()
    {
        if (checking || Busy || !Config.IsComplete) return;
        checking = true;
        try
        {
            var cfg = Config;
            var st = await Task.Run(() =>
            {
                var s = Status.Read(cfg);
                if (cfg.AutoFix && s.Checks.Any(c => !c.Ok && c.SafeToFix) && new Switcher(cfg).FixSafe(s))
                    s = Status.Read(cfg);
                return s;
            });
            Current = st;
            UpdateIcon();
            if (panel.IsVisible) panel.Render(Current, Busy);
        }
        catch (Exception e)
        {
            Log.Write($"guard: {e.Message}"); // the guard never stops
        }
        finally
        {
            checking = false;
        }
    }

    void UpdateIcon()
    {
        bool attention = Current.NeedsAttention && Config.IsComplete;
        var old = icon.Icon;
        icon.Icon = TrayIcons.Icon(Current.Mode, attention, Forms.SystemInformation.SmallIconSize.Width * 2);
        old?.Dispose();
        var tip = S.Tooltip(Current.Mode, attention);
        icon.Text = tip.Length > 63 ? tip[..63] : tip;

        string? alert = attention ? string.Join("|", Current.Checks.Where(c => !c.Ok).Select(c => c.Kind)) + Current.Mode : null;
        if (alert != null && alert != lastAlert && !Busy) icon.ShowBalloonTip(5000, S.BalloonTitle, S.BalloonText, Forms.ToolTipIcon.Warning);
        lastAlert = alert;
    }

    public void SwitchTo(Mode mode) => _ = SwitchToAsync(mode);

    public async Task<SwitchResult> SwitchToAsync(Mode mode, bool force = false)
    {
        var task = new Switcher(Config).SwitchAsync(mode, ConfirmWindow.AskAsync, force);
        UpdateIcon();
        if (panel.IsVisible) panel.Render(Current, true);
        var r = await task;
        Log.Write($"switch {mode}: {r.Message}");
        if (!r.Ok && r.Message != "already") icon.ShowBalloonTip(5000, "DuoGPU", S.Result(r), Forms.ToolTipIcon.Info);
        await CheckAsync();
        return r;
    }

    /// <summary>Puts the displays back into the last confirmed mode (or AI mode the first time).</summary>
    public void Fix()
    {
        var mode = Current.Mode != Mode.Unknown ? Current.Mode : Config.LastMode != Mode.Unknown ? Config.LastMode : Mode.Ai;
        _ = SwitchToAsync(mode, force: true);
    }

    /// <summary>Saves the other monitors as they are now, after the person changed one on purpose.</summary>
    public void RememberOthers()
    {
        if (!Config.IsComplete) return;
        Config.OtherMonitors = OtherMonitors.Capture(Topology.Read(), Config.SharedMonitor!);
        Config.Save();
        icon.ShowBalloonTip(4000, "DuoGPU", S.RememberedOthers(Config.OtherMonitors.Count), Forms.ToolTipIcon.Info);
        _ = CheckAsync();
    }

    public void OpenSetup()
    {
        if (setup is { IsLoaded: true })
        {
            setup.Activate();
            return;
        }
        setup = new SetupWindow(this, Detect.Propose(), Config, firstRun: !Config.IsComplete);
        setup.Closed += (_, _) => setup = null;
        setup.Show();
        setup.Activate();
    }

    void Quit()
    {
        guard.Stop();
        icon.Visible = false;
        icon.Dispose();
        wpf.Shutdown();
    }

    static Application NewApplication() => new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };

    /// <summary>The tray app. One copy per user.</summary>
    public static int Run()
    {
        using var single = new Mutex(true, @"Local\DuoGPU.Tray", out bool first);
        if (!first) return 0;
        Log.Write("tray started");
        var wpf = NewApplication();
        wpf.DispatcherUnhandledException += (_, e) =>
        {
            Log.Write($"unhandled: {e.Exception}");
            e.Handled = true;
        };
        var tray = new TrayApp(wpf, Config.Load());
        wpf.Startup += async (_, _) =>
        {
            if (!tray.Config.IsComplete) tray.OpenSetup();
            await tray.CheckAsync();
            tray.guard.Start();
        };
        return wpf.Run();
    }

    /// <summary>One switch from the command line, with the confirmation window.</summary>
    public static SwitchResult RunWithConfirm(Config cfg, Mode mode)
    {
        S.Use(cfg.Language);
        var wpf = NewApplication();
        SwitchResult result = new(false, Mode.Unknown, "not-run");
        wpf.Startup += async (_, _) =>
        {
            try
            {
                result = await new Switcher(cfg).SwitchAsync(mode, ConfirmWindow.AskAsync);
            }
            finally
            {
                wpf.Shutdown();
            }
        };
        wpf.Run();
        return result;
    }
}
