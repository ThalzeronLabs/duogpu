using DuoGPU.Native;

namespace DuoGPU.Core;

/// <summary>Asks the person in front of the screen whether they can see it. Returns true on "Keep", false on timeout.</summary>
public delegate Task<bool> ConfirmAsync(string gdiName, int seconds);

public record SwitchResult(bool Ok, Mode Mode, string Message);

/// <summary>
/// Moves the shared monitor from one GPU to the other. The order matters and was tested on real hardware:
/// 1. switch the monitor's input over DDC/CI through the cable it is showing now (it only answers on that one);
/// 2. turn on the other GPU's output at the same place, size and primary flag, at the tested refresh limit;
/// 3. turn off the old output, so Windows does not see the monitor twice;
/// 4. put the scale back (Windows keeps one scale per cable and resets it);
/// 5. ask "Can you see this?" and go back by itself if nobody answers.
/// </summary>
public sealed class Switcher(Config cfg)
{
    readonly Config cfg = cfg;
    static readonly SemaphoreSlim Busy = new(1, 1);

    public static bool IsBusy => Busy.CurrentCount == 0;

    public async Task<SwitchResult> SwitchAsync(Mode mode, ConfirmAsync? confirm, bool force = false)
    {
        if (!cfg.IsComplete) return new(false, Mode.Unknown, "not-configured");
        if (!await Busy.WaitAsync(0)) return new(false, Mode.Unknown, "busy");
        try
        {
            var before = Status.Read(cfg, withVram: false);
            if (before.Mode == mode && !force && !before.NeedsAttention) return new(true, mode, "already");

            var result = await Task.Run(() => Apply(mode));
            if (!result.Ok) return result;

            if (confirm != null)
            {
                var now = Topology.Read();
                var shown = now.OutputFor(cfg.SharedMonitor!, cfg.GpuFor(mode));
                bool kept = await confirm(shown?.GdiName ?? "", cfg.ConfirmSeconds);
                if (!kept)
                {
                    var back = before.Mode == Mode.Unknown ? (mode == Mode.Ai ? Mode.Game : Mode.Ai) : before.Mode;
                    Log.Write($"no confirmation for {mode}, going back to {back}");
                    var r = await Task.Run(() => Apply(back));
                    return new(false, r.Mode, "reverted");
                }
            }
            cfg.LastMode = mode;
            cfg.Save();
            return result;
        }
        finally
        {
            Busy.Release();
        }
    }

    /// <summary>Stages and applies one switch, with no confirmation. Runs on a worker thread.</summary>
    SwitchResult Apply(Mode mode)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var topo = Topology.Read();
        var monitor = cfg.SharedMonitor!;
        var targetGpu = cfg.GpuFor(mode);
        var newOut = topo.OutputFor(monitor, targetGpu);
        if (newOut == null) return new(false, Mode.Unknown, "cable-missing");

        var activeShared = topo.ActiveOutputsFor(monitor).ToList();
        var oldOut = activeShared.FirstOrDefault(o => o.GdiName != newOut.GdiName) ?? activeShared.FirstOrDefault();
        var place = oldOut?.Placement ?? newOut.Placement;
        bool primary = oldOut?.IsPrimary ?? true;
        int? scale = cfg.Scale ?? (oldOut?.Gpu is { } og ? DisplayConfig.GetScale(og.Luid, oldOut.SourceId) : null);
        var others = topo.Outputs.Where(o => o.Active && !o.MonitorIds.Contains(monitor)).ToList();
        Log.Write($"switch to {mode}: {monitor} {oldOut?.GdiName ?? "-"} -> {newOut.GdiName}");

        // 1. the monitor input, through the cable it shows now
        int? input = cfg.InputFor(targetGpu);
        if (input is int wanted && oldOut != null && oldOut.GdiName != newOut.GdiName)
        {
            var current = Ddc.GetInput(oldOut.GdiName);
            if (current != null && current != wanted)
            {
                bool ok = Ddc.SetInput(oldOut.GdiName, wanted);
                Log.Write($"  input 0x{current:X2} -> 0x{wanted:X2} through {oldOut.GdiName}: {(ok ? "ok" : "refused")}");
                Thread.Sleep(2000);
            }
        }

        // 2 and 3. the new output on, the old one off, applied together
        int width = place?.Width ?? 0, height = place?.Height ?? 0;
        // a path nobody has tested yet starts at the safe 60 Hz; calibration raises it with the person watching
        var mode2 = Refresh.Best(Gdi.Modes(newOut.GdiName), width, height, cfg.MaxHzFor(targetGpu) ?? Refresh.SafeHz);
        if (mode2 == null) return new(false, Mode.Unknown, "no-mode");
        int x = place?.X ?? 0, y = place?.Y ?? 0;
        int rc = Gdi.Stage(newOut.GdiName, x, y, mode2.Width, mode2.Height, mode2.Hz, primary);
        Log.Write($"  {newOut.GdiName}: {mode2.Width}x{mode2.Height}@{mode2.Hz} at ({x},{y}){(primary ? " primary" : "")} -> {rc}");
        foreach (var o in activeShared.Where(o => o.GdiName != newOut.GdiName))
            Log.Write($"  {o.GdiName}: off -> {Gdi.Stage(o.GdiName, 0, 0, 0, 0, 0, false)}");
        rc = Gdi.ApplyStaged();
        Log.Write($"  applied -> {rc}");
        if (rc != 0) return new(false, Mode.Unknown, $"apply-failed:{rc}");
        Thread.Sleep(2500);

        // the other monitors stay where they were
        bool moved = false;
        foreach (var o in others)
        {
            var now = Gdi.Current(o.GdiName);
            if (o.Placement is { } p && now != null && (now.X != p.X || now.Y != p.Y))
            {
                Gdi.Stage(o.GdiName, p.X, p.Y, p.Width, p.Height, p.Hz, false);
                moved = true;
            }
        }
        if (moved) Log.Write($"  others put back -> {Gdi.ApplyStaged()}");

        // the input again, now through the new cable (some monitors only switch when asked on the active one)
        if (input is int target)
        {
            for (int i = 0; i < 3; i++)
            {
                var seen = Ddc.GetInput(newOut.GdiName);
                if (seen == target) break;
                if (seen != null) Ddc.SetInput(newOut.GdiName, target);
                Thread.Sleep(1500);
            }
        }

        // 4. the scale
        if (scale is int s)
        {
            var after = Topology.Read().OutputFor(monitor, targetGpu);
            if (after?.Gpu != null && DisplayConfig.GetScale(after.Gpu.Luid, after.SourceId) != s)
                Log.Write($"  scale {s}% -> {DisplayConfig.SetScale(after.Gpu.Luid, after.SourceId, s)}");
        }
        Log.Write($"  done in {clock.ElapsedMilliseconds} ms");
        return new(true, mode, "switched");
    }

    /// <summary>Raises the refresh on the current path one step at a time, from the safe 60 Hz, while the person
    /// confirms each step, and saves the last one that showed a picture. It stops at the first step nobody confirms
    /// (the screen went black) and goes back to the last good one, so the screen goes black at most once.
    /// Catches cables and ports that cannot carry a mode the driver still offers.</summary>
    public async Task<int?> CalibrateAsync(ConfirmAsync confirm, Action<int>? trying = null)
    {
        if (!await Busy.WaitAsync(0)) return null;
        try
        {
            var st = Status.Read(cfg, withVram: false);
            if (st.SharedOutput is not { Gpu: not null, Placement: not null } o) return null;
            var p = o.Placement;
            int? best = null;
            foreach (int hz in Refresh.Ascending(Gdi.Modes(o.GdiName), p.Width, p.Height))
            {
                trying?.Invoke(hz);
                await Task.Run(() =>
                {
                    Gdi.Stage(o.GdiName, p.X, p.Y, p.Width, p.Height, hz, o.IsPrimary);
                    Log.Write($"calibrate {o.Gpu.Name} at {hz} Hz -> {Gdi.ApplyStaged()}");
                    Thread.Sleep(1500);
                });
                if (!await confirm(o.GdiName, cfg.ConfirmSeconds)) break;
                best = hz;
            }
            // back to the last good step (or to where it was, if even the first one failed)
            int final = best ?? p.Hz;
            await Task.Run(() =>
            {
                Gdi.Stage(o.GdiName, p.X, p.Y, p.Width, p.Height, final, o.IsPrimary);
                Log.Write($"calibrate {o.Gpu.Name}: keeps {final} Hz -> {Gdi.ApplyStaged()}");
            });
            if (best is int b)
            {
                cfg.MaxHzByGpu[o.Gpu.Key] = b;
                cfg.Save();
            }
            return best;
        }
        finally
        {
            Busy.Release();
        }
    }

    /// <summary>The guard's repairs that never take the picture away: the scale and the best tested refresh of the
    /// shared monitor, and the saved size, refresh and place of the other monitors (a state that already showed a
    /// picture on that same cable).</summary>
    public bool FixSafe(Status st)
    {
        if (IsBusy) return false;
        bool changed = false, staged = false;
        foreach (var c in st.Checks.Where(c => !c.Ok && c.SafeToFix))
        {
            if (c.Kind == CheckKind.Scale && cfg.Scale is int s && st.SharedOutput?.Gpu is { } g)
            {
                changed |= DisplayConfig.SetScale(g.Luid, st.SharedOutput.SourceId, s);
            }
            else if (c.Kind == CheckKind.Refresh && st.BestHz is int hz && st.SharedOutput is { Placement: { } p } o)
            {
                staged |= Gdi.Stage(o.GdiName, p.X, p.Y, p.Width, p.Height, hz, o.IsPrimary) == 0;
            }
            else if (c.Kind is CheckKind.OtherMode or CheckKind.OtherPosition && c.Target != null && c.Want is { } w)
            {
                staged |= Gdi.Stage(c.Target, w.X, w.Y, w.Width, w.Height, w.Hz, false) == 0;
            }
        }
        if (staged) changed |= Gdi.ApplyStaged() == 0;
        if (changed) Log.Write("guard: put back " + string.Join(", ", st.Checks.Where(c => !c.Ok && c.SafeToFix).Select(c => c.Kind)));
        return changed;
    }
}
