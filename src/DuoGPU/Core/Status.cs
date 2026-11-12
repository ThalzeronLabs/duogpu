using DuoGPU.Native;

namespace DuoGPU.Core;

public enum CheckKind { DuplicateMonitor, NoOutput, Scale, Refresh, Primary }

public record Check(bool Ok, CheckKind Kind, string Detail, bool SafeToFix);

/// <summary>The current mode and everything the guard checks, read every few seconds.</summary>
public sealed class Status
{
    public Mode Mode { get; init; }
    public OutputInfo? SharedOutput { get; init; }
    public GpuInfo? AiGpu { get; init; }
    public GpuInfo? OtherGpu { get; init; }
    public string MonitorName { get; init; } = "";
    public int? Scale { get; init; }
    public int? BestHz { get; init; }
    public ulong? AiVramUsed { get; init; }
    public List<Check> Checks { get; init; } = [];
    public DateTime When { get; init; } = DateTime.Now;

    public bool NeedsAttention => Mode == Mode.Unknown || Checks.Any(c => !c.Ok);

    public GpuInfo? ActiveGpu => SharedOutput?.Gpu;

    public static Status Read(Config cfg, Topology? topo = null, bool withVram = true)
    {
        if (!cfg.IsComplete) return new Status { Mode = Mode.Unknown };
        topo ??= Topology.Read();
        var ai = topo.Gpu(cfg.AiGpu);
        var other = topo.Gpu(cfg.OtherGpu);
        var monitor = cfg.SharedMonitor!;
        var active = topo.ActiveOutputsFor(monitor).ToList();
        var checks = new List<Check>();
        OutputInfo? shared = null;
        Mode mode = Mode.Unknown;
        int? scale = null, best = null;

        if (active.Count > 1)
        {
            checks.Add(new Check(false, CheckKind.DuplicateMonitor, "", false));
        }
        else if (active.Count == 0)
        {
            checks.Add(new Check(false, CheckKind.NoOutput, "", false));
        }
        else
        {
            shared = active[0];
            mode = shared.Gpu?.Key == cfg.AiGpu ? Mode.Game : shared.Gpu?.Key == cfg.OtherGpu ? Mode.Ai : Mode.Unknown;
            var p = shared.Placement!;
            if (shared.Gpu != null) scale = DisplayConfig.GetScale(shared.Gpu.Luid, shared.SourceId);
            if (cfg.Scale is int want && scale is int have)
                checks.Add(new Check(have == want, CheckKind.Scale, $"{have}%|{want}%", true));
            // Only a path whose limit was tested with someone watching is checked: the driver can offer a refresh that
            // the cable cannot carry, and raising it there would turn the screen black.
            if (shared.Gpu != null && cfg.MaxHzFor(shared.Gpu.Key) is int limit)
            {
                best = Refresh.Best(Gdi.Modes(shared.GdiName), p.Width, p.Height, limit)?.Hz;
                if (best is int b)
                    checks.Add(new Check(p.Hz == b, CheckKind.Refresh, $"{p.Width}×{p.Height}|{p.Hz}|{b}", true));
            }
        }

        ulong? used = null;
        if (withVram && ai != null) used = Gpus.VramInUse(ai.Luid);

        return new Status
        {
            Mode = mode,
            SharedOutput = shared,
            AiGpu = ai,
            OtherGpu = other,
            MonitorName = topo.MonitorName(monitor),
            Scale = scale,
            BestHz = best,
            AiVramUsed = used,
            Checks = checks,
        };
    }
}

public static class Refresh
{
    /// <summary>The mode to use on a path: the requested size when the output offers it, at the highest refresh up to the
    /// tested limit of that path (null = no limit known). Falls back to the largest size the output offers.</summary>
    public static Gdi.Mode? Best(IEnumerable<Gdi.Mode> modes, int width, int height, int? maxHz)
    {
        var allowed = modes.Where(m => m.Width > 0 && m.Hz > 1 && (maxHz is null || m.Hz <= maxHz)).ToList();
        var same = allowed.Where(m => m.Width == width && m.Height == height).ToList();
        if (same.Count > 0) return same.MaxBy(m => m.Hz);
        return allowed.OrderByDescending(m => (long)m.Width * m.Height).ThenByDescending(m => m.Hz).FirstOrDefault();
    }

    /// <summary>A refresh rate every cable and port carries at the monitor's native size.</summary>
    public const int SafeHz = 60;

    /// <summary>The refresh rates to try during calibration, lowest first, from the safe 60 Hz up.</summary>
    public static List<int> Ascending(IEnumerable<Gdi.Mode> modes, int width, int height)
    {
        var all = modes.Where(m => m.Width == width && m.Height == height && m.Hz > 1)
            .Select(m => m.Hz).Distinct().OrderBy(h => h).ToList();
        var fromSafe = all.Where(h => h >= SafeHz).ToList();
        return fromSafe.Count > 0 ? fromSafe : all;
    }
}
