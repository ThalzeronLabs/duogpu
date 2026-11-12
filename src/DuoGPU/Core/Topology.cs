using DuoGPU.Native;

namespace DuoGPU.Core;

public record GpuInfo(string Key, string Name, LUID Luid, ulong Vram);

public record OutputInfo(
    string GdiName,
    GpuInfo? Gpu,
    uint SourceId,
    bool Active,
    IReadOnlyList<string> MonitorIds,
    Gdi.Placement? Placement)
{
    public bool IsPrimary => Active && Placement is { X: 0, Y: 0 };
}

/// <summary>A picture of the displays right now: the GPUs, every output and which monitor sits on it.</summary>
public sealed class Topology
{
    public required IReadOnlyList<GpuInfo> Gpus { get; init; }
    public required IReadOnlyList<OutputInfo> Outputs { get; init; }
    public required IReadOnlyDictionary<string, string> MonitorNames { get; init; }

    public static Topology Read()
    {
        var adapters = Native.Gpus.List();
        var gpus = adapters
            .Select(a => new GpuInfo(DisplayConfig.AdapterPath(a.Luid) ?? a.Name, a.Name, a.Luid, a.DedicatedVram))
            .ToList();

        // Every source (a GPU's "DISPLAYn") appears in the full path list, active or not, so this maps the GDI name of
        // an output to its GPU. The targets give the monitor names from their EDID.
        var sourceOf = new Dictionary<string, (LUID luid, uint id)>(StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in DisplayConfig.QueryPaths(DisplayConfig.QDC_ALL_PATHS))
        {
            var gdi = DisplayConfig.SourceGdiName(p.sourceInfo.adapterId, p.sourceInfo.id);
            if (gdi != null) sourceOf.TryAdd(gdi, (p.sourceInfo.adapterId, p.sourceInfo.id));
            if (p.targetInfo.targetAvailable == 0) continue;
            var t = DisplayConfig.TargetName(p.targetInfo.adapterId, p.targetInfo.id);
            if (t is { } tn && tn.edidManufactureId != 0)
            {
                var id = Edid.PnpId(tn.edidManufactureId, tn.edidProductCodeId);
                if (!string.IsNullOrWhiteSpace(tn.monitorFriendlyDeviceName)) names.TryAdd(id, tn.monitorFriendlyDeviceName);
            }
        }

        var outputs = new List<OutputInfo>();
        foreach (var o in Gdi.Outputs())
        {
            GpuInfo? gpu = null;
            uint sourceId = 0;
            if (sourceOf.TryGetValue(o.GdiName, out var s))
            {
                gpu = gpus.FirstOrDefault(g => g.Luid.Equals(s.luid));
                sourceId = s.id;
            }
            gpu ??= gpus.FirstOrDefault(g => g.Name == o.AdapterName);
            outputs.Add(new OutputInfo(o.GdiName, gpu, sourceId, o.Active, o.MonitorIds, o.Active ? Gdi.Current(o.GdiName) : null));
        }
        return new Topology { Gpus = gpus, Outputs = outputs, MonitorNames = names };
    }

    public string MonitorName(string id) => MonitorNames.TryGetValue(id, out var n) ? n : id;

    /// <summary>The monitors wired to two different GPUs: the ones DuoGPU can move.</summary>
    public IEnumerable<string> DualCabledMonitors() =>
        Outputs.SelectMany(o => o.MonitorIds.Select(m => (m, o.Gpu?.Key)))
            .Where(x => x.Key != null)
            .GroupBy(x => x.m)
            .Where(g => g.Select(x => x.Key).Distinct().Count() >= 2)
            .Select(g => g.Key);

    public OutputInfo? OutputFor(string monitorId, string gpuKey) =>
        Outputs.FirstOrDefault(o => o.Gpu?.Key == gpuKey && o.MonitorIds.Contains(monitorId));

    public IEnumerable<OutputInfo> ActiveOutputsFor(string monitorId) =>
        Outputs.Where(o => o.Active && o.MonitorIds.Contains(monitorId));

    public GpuInfo? Gpu(string? key) => Gpus.FirstOrDefault(g => g.Key == key);
}

public static class Edid
{
    /// <summary>The EDID manufacturer id (three letters, 5 bits each, stored big-endian) and product code, written the
    /// way Windows names monitors: "VIE0490".</summary>
    public static string PnpId(ushort manufacturer, ushort product)
    {
        int v = (ushort)((manufacturer >> 8) | (manufacturer << 8));
        char c1 = (char)('A' - 1 + ((v >> 10) & 0x1F));
        char c2 = (char)('A' - 1 + ((v >> 5) & 0x1F));
        char c3 = (char)('A' - 1 + (v & 0x1F));
        return $"{c1}{c2}{c3}{product:X4}";
    }
}
