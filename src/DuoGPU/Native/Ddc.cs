using System.Runtime.InteropServices;
using System.Text;

namespace DuoGPU.Native;

/// <summary>
/// DDC/CI: the small control channel inside the video cable. VCP code 0x60 is the monitor's input source.
/// A monitor usually answers only on the cable of the input it is showing, so reads and writes go through the
/// Windows output that is driving the picture right now.
/// </summary>
public static class Ddc
{
    public const byte VcpInputSource = 0x60;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MONITORINFOEX
    {
        public int cbSize;
        public Gdi.RECT rcMonitor;
        public Gdi.RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }

    delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("dxva2.dll")]
    static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);

    [DllImport("dxva2.dll")]
    static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll")]
    static extern bool DestroyPhysicalMonitors(uint count, PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll")]
    static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte code, IntPtr type, out uint current, out uint maximum);

    [DllImport("dxva2.dll")]
    static extern bool SetVCPFeature(IntPtr hMonitor, byte code, uint value);

    [DllImport("dxva2.dll")]
    static extern bool GetCapabilitiesStringLength(IntPtr hMonitor, out uint length);

    [DllImport("dxva2.dll", CharSet = CharSet.Ansi)]
    static extern bool CapabilitiesRequestAndCapabilitiesReply(IntPtr hMonitor, StringBuilder caps, uint length);

    static T? WithMonitor<T>(string gdiName, Func<IntPtr, T?> action)
    {
        IntPtr found = IntPtr.Zero;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (h, _, _, _) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(h, ref mi) && string.Equals(mi.szDevice, gdiName, StringComparison.OrdinalIgnoreCase))
            {
                found = h;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        if (found == IntPtr.Zero || !GetNumberOfPhysicalMonitorsFromHMONITOR(found, out uint n) || n == 0) return default;
        var arr = new PHYSICAL_MONITOR[n];
        if (!GetPhysicalMonitorsFromHMONITOR(found, n, arr)) return default;
        try
        {
            return action(arr[0].hPhysicalMonitor);
        }
        finally
        {
            DestroyPhysicalMonitors(n, arr);
        }
    }

    /// <summary>The input the monitor reports through this output, or null when it does not answer.</summary>
    public static int? GetInput(string gdiName) =>
        WithMonitor<int?>(gdiName, h => GetVCPFeatureAndVCPFeatureReply(h, VcpInputSource, IntPtr.Zero, out uint cur, out _)
            ? (int)(cur & 0xFF) : null);

    public static bool SetInput(string gdiName, int input) =>
        WithMonitor<bool?>(gdiName, h => SetVCPFeature(h, VcpInputSource, (uint)input)) ?? false;

    /// <summary>The MCCS capabilities string (slow: one to two seconds on most monitors).</summary>
    public static string? GetCapabilities(string gdiName) =>
        WithMonitor(gdiName, h =>
        {
            if (!GetCapabilitiesStringLength(h, out uint len) || len == 0) return null;
            var sb = new StringBuilder((int)len);
            return CapabilitiesRequestAndCapabilitiesReply(h, sb, len) ? sb.ToString() : null;
        });
}
