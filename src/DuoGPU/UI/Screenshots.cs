using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DuoGPU.Core;

namespace DuoGPU.UI;

/// <summary>
/// Renders the README images from the real panel code, in both languages ("DuoGPU.exe render docs/images").
/// AI mode uses the live status of this PC; Game mode and the warning use values measured on the same PC, so the
/// pictures show what the app shows instead of a mock-up.
/// </summary>
public static class Screenshots
{
    const double Dpi = 192; // 2x, sharp on high-DPI screens

    public static int Render(string folder)
    {
        Directory.CreateDirectory(folder);
        var cfg = Config.Load();
        if (!cfg.IsComplete)
        {
            Console.Error.WriteLine("Set DuoGPU up first.");
            return 2;
        }
        var live = Status.Read(cfg);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var lang in new[] { "en", "pt-BR" })
        {
            S.Use(lang);
            string suffix = lang == "en" ? "" : ".pt-BR";
            foreach (var (name, st, help) in Variants(live, cfg))
            {
                var panel = new PanelWindow(null) { ShowHelp = help };
                Save(panel.Build(st, false, cfg), Path.Combine(folder, $"panel-{name}{suffix}.png"));
            }
            Save(ConfirmWindow.BuildContent(null, 12), Path.Combine(folder, $"confirm{suffix}.png"));
            SaveIcons(Path.Combine(folder, $"icons{suffix}.png"));
            var proposal = Detect.Propose(readCapabilities: true);
            for (int step = 0; step < 7; step++)
            {
                var wizard = new SetupWindow(null, proposal, cfg, firstRun: true);
                Save(wizard.RenderStep(step), Path.Combine(folder, $"setup-{step + 1}{suffix}.png"));
            }
            Console.WriteLine($"rendered {lang}");
        }
        app.Shutdown();
        return 0;
    }

    static IEnumerable<(string, Status, bool)> Variants(Status live, Config cfg)
    {
        var ai = live.AiGpu!;
        var other = live.OtherGpu!;
        var shared = live.SharedOutput!;
        var p = shared.Placement!;
        Status With(Mode mode, GpuInfo gpu, int hz, ulong vram, List<Check> checks) => new()
        {
            Mode = mode,
            AiGpu = ai,
            OtherGpu = other,
            MonitorName = live.MonitorName,
            Scale = live.Scale,
            AiVramUsed = vram,
            SharedOutput = shared with { Gpu = gpu, Placement = p with { Hz = hz } },
            Checks = checks,
            When = live.When,
        };
        string size = $"{p.Width}×{p.Height}";
        int aiHz = cfg.MaxHzFor(other.Key) ?? p.Hz, gameHz = cfg.MaxHzFor(ai.Key) ?? p.Hz;
        string sc = $"{live.Scale}%";
        var others = live.Checks.Where(c => c.Kind == CheckKind.OtherMode && c.Ok).ToList();
        List<Check> Ok(int hz) =>
        [
            new(true, CheckKind.Scale, $"{sc}|{sc}", true),
            new(true, CheckKind.Refresh, $"{size}|{hz}|{hz}", true),
            .. others,
        ];
        const ulong gb = 1UL << 30;
        // AI mode: live from this PC when it is in AI mode, otherwise the value measured on 12/11/2026 (0.44 GB)
        ulong aiVram = live.Mode == Mode.Ai && live.AiVramUsed is ulong u ? u : (ulong)(0.44 * gb);
        yield return ("ai", With(Mode.Ai, other, aiHz, aiVram, Ok(aiHz)), false);
        // Game mode: the monitor on the big GPU (2.93 GB in use at idle, measured on the same PC)
        yield return ("game", With(Mode.Game, ai, gameHz, (ulong)(2.93 * gb), Ok(gameHz)), false);
        // the warning: Windows reset the scale to 100% after a cable change
        yield return ("attention", With(Mode.Ai, other, aiHz, aiVram,
        [
            new(false, CheckKind.Scale, $"100%|{sc}", true),
            new(true, CheckKind.Refresh, $"{size}|{aiHz}|{aiHz}", true),
            .. others,
        ]), false);
        yield return ("help", With(Mode.Ai, other, aiHz, aiVram, Ok(aiHz)), true);
    }

    static void Save(FrameworkElement element, string path)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();
        var size = element.DesiredSize;
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(size.Width * Dpi / 96), (int)Math.Ceiling(size.Height * Dpi / 96),
            Dpi, Dpi, PixelFormats.Pbgra32);
        bmp.Render(element);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var f = File.Create(path);
        enc.Save(f);
    }

    static void SaveIcons(string path)
    {
        const int size = 128, gap = 24;
        using var strip = new System.Drawing.Bitmap(size * 3 + gap * 2, size);
        using (var g = System.Drawing.Graphics.FromImage(strip))
        {
            g.Clear(System.Drawing.Color.Transparent);
            int x = 0;
            foreach (var (m, a) in new[] { (Mode.Ai, false), (Mode.Game, false), (Mode.Ai, true) })
            {
                using var icon = TrayIcons.Draw(m, a, size);
                g.DrawImage(icon, x, 0);
                x += size + gap;
            }
        }
        strip.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
