using DuoGPU.Native;

namespace DuoGPU.Core;

/// <summary>How a monitor that DuoGPU does not move was set up when the person saved it.</summary>
public record SavedOutput(int X, int Y, int Width, int Height, int Hz);

/// <summary>
/// The monitors DuoGPU does not move. Their place, size and refresh are saved once (at setup, or with "Remember the
/// other monitors"), and the guard checks them every few seconds: a switch, a driver reset or a game can change them.
/// </summary>
public static class OtherMonitors
{
    /// <summary>The key of each monitor: its Windows name ("MCR3150"), with "#2", "#3" for identical models.</summary>
    static List<(string key, OutputInfo output)> Keyed(IEnumerable<OutputInfo> outputs, string sharedMonitor)
    {
        var list = new List<(string, OutputInfo)>();
        var seen = new Dictionary<string, int>();
        foreach (var o in outputs.Where(o => o.Active && o.Placement != null && !o.MonitorIds.Contains(sharedMonitor))
                     .OrderBy(o => o.Placement!.X).ThenBy(o => o.Placement!.Y))
        {
            var id = o.MonitorIds.FirstOrDefault() ?? o.GdiName;
            int n = seen[id] = seen.GetValueOrDefault(id) + 1;
            list.Add((n == 1 ? id : $"{id}#{n}", o));
        }
        return list;
    }

    public static Dictionary<string, SavedOutput> Capture(Topology topo, string sharedMonitor) =>
        Keyed(topo.Outputs, sharedMonitor).ToDictionary(
            x => x.key,
            x => new SavedOutput(x.output.Placement!.X, x.output.Placement.Y, x.output.Placement.Width,
                x.output.Placement.Height, x.output.Placement.Hz));

    /// <summary>One check per saved monitor: still on, same size and refresh, same place.</summary>
    public static List<Check> Compare(IReadOnlyDictionary<string, SavedOutput> saved, IEnumerable<OutputInfo> outputs,
        string sharedMonitor, Func<string, string> nameOf)
    {
        var now = Keyed(outputs, sharedMonitor).ToDictionary(x => x.key, x => x.output);
        var checks = new List<Check>();
        foreach (var (key, want) in saved)
        {
            string name = nameOf(key.Split('#')[0]);
            if (!now.TryGetValue(key, out var o))
            {
                checks.Add(new Check(false, CheckKind.OtherMissing, name, false));
                continue;
            }
            var p = o.Placement!;
            bool sameMode = p.Width == want.Width && p.Height == want.Height && p.Hz == want.Hz;
            checks.Add(new Check(sameMode, CheckKind.OtherMode,
                $"{name}|{p.Width}×{p.Height}|{p.Hz}|{want.Width}×{want.Height}|{want.Hz}", true, o.GdiName, want));
            if (p.X != want.X || p.Y != want.Y)
                checks.Add(new Check(false, CheckKind.OtherPosition, name, true, o.GdiName, want));
        }
        return checks;
    }
}
