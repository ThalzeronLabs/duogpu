using System.Text;
using DuoGPU.Native;

namespace DuoGPU.Core;

/// <summary>Reads the machine and proposes a configuration. Nothing here changes the displays.</summary>
public static class Detect
{
    public record Proposal(Config Config, Topology Topology, string? Capabilities, List<int> Inputs, int? CurrentInput,
        string? Problem);

    public static Proposal Propose(bool readCapabilities = true)
    {
        var topo = Topology.Read();
        var cfg = new Config();
        var shared = topo.DualCabledMonitors().ToList();
        if (topo.Gpus.Count < 2) return new(cfg, topo, null, [], null, "one-gpu");
        if (shared.Count == 0) return new(cfg, topo, null, [], null, "no-dual-cable");

        var monitor = shared[0];
        var gpusOfMonitor = topo.Outputs.Where(o => o.MonitorIds.Contains(monitor) && o.Gpu != null)
            .Select(o => o.Gpu!).DistinctBy(g => g.Key).OrderByDescending(g => g.Vram).ToList();
        cfg.SharedMonitor = monitor;
        cfg.AiGpu = gpusOfMonitor[0].Key;
        cfg.OtherGpu = gpusOfMonitor[1].Key;

        // the input of the cable on screen now, and the list the monitor declares
        string? caps = null;
        int? current = null;
        var active = topo.ActiveOutputsFor(monitor).FirstOrDefault();
        if (active != null)
        {
            current = Ddc.GetInput(active.GdiName);
            if (readCapabilities) caps = Ddc.GetCapabilities(active.GdiName);
            if (current is int c && active.Gpu != null) cfg.InputByGpu[active.Gpu.Key] = c;
            if (active.Gpu != null) cfg.Scale = DisplayConfig.GetScale(active.Gpu.Luid, active.SourceId);
        }
        return new(cfg, topo, caps, Mccs.Inputs(caps), current, null);
    }

    public static string Report(Proposal p, Config? saved = null)
    {
        var sb = new StringBuilder();
        var t = p.Topology;
        sb.AppendLine("GPUs:");
        foreach (var g in t.Gpus) sb.AppendLine($"  {g.Name} · {g.Vram / (1UL << 20)} MB · LUID {g.Luid}");
        sb.AppendLine("Outputs:");
        foreach (var o in t.Outputs)
        {
            var place = o.Placement is { } pl ? $"{pl.Width}x{pl.Height}@{pl.Hz} at ({pl.X},{pl.Y})" : "off";
            var mons = string.Join(", ", o.MonitorIds.Select(m => $"{t.MonitorName(m)} [{m}]"));
            sb.AppendLine($"  {o.GdiName} · {o.Gpu?.Name ?? "?"} · {place} · {mons}");
        }
        sb.AppendLine($"Dual-cabled monitor: {(p.Config.SharedMonitor is { } m ? $"{t.MonitorName(m)} [{m}]" : "none")}");
        if (p.Problem != null) sb.AppendLine($"Problem: {p.Problem}");
        if (p.Config.IsComplete)
        {
            sb.AppendLine($"AI GPU (most VRAM): {t.Gpu(p.Config.AiGpu)?.Name}");
            sb.AppendLine($"Other GPU: {t.Gpu(p.Config.OtherGpu)?.Name}");
            sb.AppendLine($"Input on screen now (DDC/CI): {(p.CurrentInput is int c ? Mccs.InputName(c) : "no answer")}");
            sb.AppendLine($"Inputs the monitor lists: {(p.Inputs.Count > 0 ? string.Join(", ", p.Inputs.Select(Mccs.InputName)) : "none")}");
            sb.AppendLine($"Scale now: {p.Config.Scale?.ToString() ?? "?"}%");
        }
        if (saved is { IsComplete: true })
        {
            var st = Status.Read(saved, t);
            sb.AppendLine($"Mode now: {st.Mode}");
            if (st.AiVramUsed is ulong u && st.AiGpu != null)
                sb.AppendLine($"VRAM in use on {st.AiGpu.Name}: {u / (double)(1UL << 30):F2} GB of {st.AiGpu.Vram / (double)(1UL << 30):F1} GB");
            foreach (var c in st.Checks) sb.AppendLine($"  {(c.Ok ? "ok" : "!!")} {c.Kind} {c.Detail}");
        }
        return sb.ToString();
    }
}
