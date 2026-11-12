using System.Text.RegularExpressions;

namespace DuoGPU.Core;

/// <summary>The VESA MCCS input source values (VCP 0x60) and the monitor's capabilities string.</summary>
public static partial class Mccs
{
    static readonly Dictionary<int, string> Names = new()
    {
        [0x01] = "VGA 1", [0x02] = "VGA 2", [0x03] = "DVI 1", [0x04] = "DVI 2",
        [0x0C] = "Component 1", [0x0D] = "Component 2",
        [0x0F] = "DisplayPort 1", [0x10] = "DisplayPort 2", [0x11] = "HDMI 1", [0x12] = "HDMI 2",
        [0x13] = "HDMI 3", [0x14] = "HDMI 4", [0x1B] = "USB-C",
    };

    public static string InputName(int value) =>
        Names.TryGetValue(value, out var n) ? $"{n} (0x{value:X2})" : $"0x{value:X2}";

    [GeneratedRegex(@"vcp\s*\((.*)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex VcpSection();

    [GeneratedRegex(@"(?:^|[\s(])60\s*\(([0-9A-Fa-f\s]*)\)")]
    private static partial Regex InputList();

    /// <summary>The inputs listed under VCP 0x60 in a capabilities string such as
    /// "(prot(monitor)vcp(02 10 12 60(0F 10 11 12) 62)mccs_ver(2.2))". Empty when the monitor does not list them.</summary>
    public static List<int> Inputs(string? capabilities)
    {
        if (string.IsNullOrWhiteSpace(capabilities)) return [];
        var vcp = VcpSection().Match(capabilities);
        var m = InputList().Match(vcp.Success ? vcp.Groups[1].Value : capabilities);
        if (!m.Success) return [];
        return m.Groups[1].Value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(h => int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : -1)
            .Where(v => v > 0)
            .Distinct()
            .ToList();
    }
}
