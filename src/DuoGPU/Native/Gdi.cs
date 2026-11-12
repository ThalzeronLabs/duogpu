using System.Runtime.InteropServices;

namespace DuoGPU.Native;

/// <summary>
/// The classic display API: each GPU output is a "\\.\DISPLAYn" device. It turns outputs on and off, moves them and
/// picks the refresh rate. Changes are staged with CDS_NORESET and applied together by <see cref="ApplyStaged"/>.
/// </summary>
public static class Gdi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    public const uint DISPLAY_DEVICE_ACTIVE = 0x1;
    const uint DM_POSITION = 0x20, DM_PELSWIDTH = 0x80000, DM_PELSHEIGHT = 0x100000, DM_DISPLAYFREQUENCY = 0x400000;
    const uint CDS_UPDATEREGISTRY = 0x1, CDS_SET_PRIMARY = 0x10, CDS_NORESET = 0x10000000;
    const int ENUM_CURRENT_SETTINGS = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

    public record Output(string GdiName, string AdapterName, bool Active, IReadOnlyList<string> MonitorIds);

    /// <summary>Every GPU output with at least one monitor plugged in, active or not.</summary>
    public static List<Output> Outputs()
    {
        var list = new List<Output>();
        for (uint i = 0; ; i++)
        {
            var d = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (!EnumDisplayDevices(null, i, ref d, 0)) break;
            var monitors = new List<string>();
            for (uint j = 0; ; j++)
            {
                var m = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                if (!EnumDisplayDevices(d.DeviceName, j, ref m, 0)) break;
                // "MONITOR\VIE0490\{4d36e96e-...}\0003" -> "VIE0490" (the EDID manufacturer and product code)
                var parts = m.DeviceID.Split('\\');
                monitors.Add(parts.Length > 1 ? parts[1] : m.DeviceString);
            }
            if (monitors.Count > 0)
                list.Add(new Output(d.DeviceName, d.DeviceString, (d.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0, monitors));
        }
        return list;
    }

    static DEVMODE NewDevMode() => new() { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };

    public record Mode(int Width, int Height, int Hz);

    public record Placement(int X, int Y, int Width, int Height, int Hz);

    public static Placement? Current(string gdiName)
    {
        var dm = NewDevMode();
        if (!EnumDisplaySettings(gdiName, ENUM_CURRENT_SETTINGS, ref dm) || dm.dmPelsWidth == 0) return null;
        return new Placement(dm.dmPositionX, dm.dmPositionY, (int)dm.dmPelsWidth, (int)dm.dmPelsHeight, (int)dm.dmDisplayFrequency);
    }

    /// <summary>The modes the driver offers on this output. A mode in this list can still show a black screen when the
    /// cable or the port cannot carry it, which is why DuoGPU keeps a tested refresh limit for each path.</summary>
    public static HashSet<Mode> Modes(string gdiName)
    {
        var set = new HashSet<Mode>();
        for (int k = 0; ; k++)
        {
            var dm = NewDevMode();
            if (!EnumDisplaySettings(gdiName, k, ref dm)) break;
            set.Add(new Mode((int)dm.dmPelsWidth, (int)dm.dmPelsHeight, (int)dm.dmDisplayFrequency));
        }
        return set;
    }

    /// <summary>Stages an output at a position, size and refresh (0 width = turn the output off).</summary>
    public static int Stage(string gdiName, int x, int y, int width, int height, int hz, bool primary)
    {
        var dm = NewDevMode();
        EnumDisplaySettings(gdiName, ENUM_CURRENT_SETTINGS, ref dm);
        dm.dmSize = (ushort)Marshal.SizeOf<DEVMODE>();
        dm.dmPositionX = x;
        dm.dmPositionY = y;
        dm.dmPelsWidth = (uint)width;
        dm.dmPelsHeight = (uint)height;
        dm.dmFields = DM_POSITION | DM_PELSWIDTH | DM_PELSHEIGHT;
        if (width > 0 && hz > 0)
        {
            dm.dmDisplayFrequency = (uint)hz;
            dm.dmFields |= DM_DISPLAYFREQUENCY;
        }
        uint flags = CDS_UPDATEREGISTRY | CDS_NORESET | (primary ? CDS_SET_PRIMARY : 0);
        return ChangeDisplaySettingsEx(gdiName, ref dm, IntPtr.Zero, flags, IntPtr.Zero);
    }

    /// <summary>Applies everything staged so far, in one go. 0 means success.</summary>
    public static int ApplyStaged() => ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
}
