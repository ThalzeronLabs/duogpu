using System.Runtime.InteropServices;

namespace DuoGPU.Native;

// The Connecting and Configuring Displays (CCD) API: who drives which monitor, the EDID ids and the scale.
// https://learn.microsoft.com/windows-hardware/drivers/display/ccd-apis

[StructLayout(LayoutKind.Sequential)]
public struct LUID : IEquatable<LUID>
{
    public uint LowPart;
    public int HighPart;

    public bool Equals(LUID other) => LowPart == other.LowPart && HighPart == other.HighPart;
    public override bool Equals(object? obj) => obj is LUID l && Equals(l);
    public override int GetHashCode() => HashCode.Combine(LowPart, HighPart);
    public override string ToString() => $"0x{(uint)HighPart:X8}_0x{LowPart:X8}";
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_RATIONAL
{
    public uint Numerator;
    public uint Denominator;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_PATH_SOURCE_INFO
{
    public LUID adapterId;
    public uint id;
    public uint modeInfoIdx;
    public uint statusFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_PATH_TARGET_INFO
{
    public LUID adapterId;
    public uint id;
    public uint modeInfoIdx;
    public int outputTechnology;
    public int rotation;
    public int scaling;
    public DISPLAYCONFIG_RATIONAL refreshRate;
    public int scanLineOrdering;
    public int targetAvailable;
    public uint statusFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_PATH_INFO
{
    public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
    public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
    public uint flags;
}

// 64 bytes: the header and a 48-byte union (target mode, source mode or desktop image). Only the header is read here.
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct DISPLAYCONFIG_MODE_INFO
{
    [FieldOffset(0)] public int infoType;
    [FieldOffset(4)] public uint id;
    [FieldOffset(8)] public LUID adapterId;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
{
    public int type;
    public uint size;
    public LUID adapterId;
    public uint id;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public uint flags;
    public int outputTechnology;
    public ushort edidManufactureId;
    public ushort edidProductCodeId;
    public uint connectorInstance;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAYCONFIG_ADAPTER_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string adapterDevicePath;
}

// The scale ("Make everything bigger" in Settings) is not in the documented API. These two types are the ones the
// Settings app itself uses; the values are steps relative to the scale Windows recommends for that monitor.
[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public int minScaleRel;
    public int curScaleRel;
    public int maxScaleRel;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public int scaleRel;
}

public static class DisplayConfig
{
    public const uint QDC_ALL_PATHS = 0x1;
    public const uint QDC_ONLY_ACTIVE_PATHS = 0x2;
    public const uint DISPLAYCONFIG_PATH_ACTIVE = 0x1;

    const int GET_SOURCE_NAME = 1;
    const int GET_TARGET_NAME = 2;
    const int GET_ADAPTER_NAME = 4;
    const int GET_DPI_SCALE = -3;
    const int SET_DPI_SCALE = -4;

    /// <summary>The scale steps Windows offers, in percent.</summary>
    public static readonly int[] ScaleSteps = [100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500];

    [DllImport("user32.dll")]
    static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

    [DllImport("user32.dll")]
    static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

    [DllImport("user32.dll")]
    static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_ADAPTER_NAME requestPacket);

    [DllImport("user32.dll")]
    static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DPI_SCALE_GET requestPacket);

    [DllImport("user32.dll")]
    static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DPI_SCALE_SET setPacket);

    /// <summary>All the paths Windows knows (active and possible), retried if the topology changes mid-query.</summary>
    public static DISPLAYCONFIG_PATH_INFO[] QueryPaths(uint flags)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (GetDisplayConfigBufferSizes(flags, out uint np, out uint nm) != 0) return [];
            var paths = new DISPLAYCONFIG_PATH_INFO[np];
            var modes = new DISPLAYCONFIG_MODE_INFO[Math.Max(nm, 1)];
            int rc = QueryDisplayConfig(flags, ref np, paths, ref nm, modes, IntPtr.Zero);
            if (rc == 0) return paths[..(int)np];
            if (rc != 122) return []; // ERROR_INSUFFICIENT_BUFFER: the topology changed, ask again
        }
        return [];
    }

    static DISPLAYCONFIG_DEVICE_INFO_HEADER Header<T>(int type, LUID adapter, uint id) where T : struct =>
        new() { type = type, size = (uint)Marshal.SizeOf<T>(), adapterId = adapter, id = id };

    public static string? SourceGdiName(LUID adapter, uint sourceId)
    {
        var p = new DISPLAYCONFIG_SOURCE_DEVICE_NAME { header = Header<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(GET_SOURCE_NAME, adapter, sourceId) };
        return DisplayConfigGetDeviceInfo(ref p) == 0 ? p.viewGdiDeviceName : null;
    }

    public static DISPLAYCONFIG_TARGET_DEVICE_NAME? TargetName(LUID adapter, uint targetId)
    {
        var p = new DISPLAYCONFIG_TARGET_DEVICE_NAME { header = Header<DISPLAYCONFIG_TARGET_DEVICE_NAME>(GET_TARGET_NAME, adapter, targetId) };
        return DisplayConfigGetDeviceInfo(ref p) == 0 ? p : null;
    }

    public static string? AdapterPath(LUID adapter)
    {
        var p = new DISPLAYCONFIG_ADAPTER_NAME { header = Header<DISPLAYCONFIG_ADAPTER_NAME>(GET_ADAPTER_NAME, adapter, 0) };
        return DisplayConfigGetDeviceInfo(ref p) == 0 ? p.adapterDevicePath : null;
    }

    /// <summary>The current scale of an active source, in percent, or null.</summary>
    public static int? GetScale(LUID adapter, uint sourceId)
    {
        var p = new DISPLAYCONFIG_SOURCE_DPI_SCALE_GET { header = Header<DISPLAYCONFIG_SOURCE_DPI_SCALE_GET>(GET_DPI_SCALE, adapter, sourceId) };
        if (DisplayConfigGetDeviceInfo(ref p) != 0) return null;
        int recommended = Math.Abs(p.minScaleRel);
        int index = Math.Clamp(recommended + p.curScaleRel, 0, ScaleSteps.Length - 1);
        return ScaleSteps[index];
    }

    /// <summary>Sets the scale of an active source. Returns true when Windows accepted it.</summary>
    public static bool SetScale(LUID adapter, uint sourceId, int percent)
    {
        int wanted = Array.IndexOf(ScaleSteps, percent);
        if (wanted < 0) return false;
        var g = new DISPLAYCONFIG_SOURCE_DPI_SCALE_GET { header = Header<DISPLAYCONFIG_SOURCE_DPI_SCALE_GET>(GET_DPI_SCALE, adapter, sourceId) };
        if (DisplayConfigGetDeviceInfo(ref g) != 0) return false;
        int recommended = Math.Abs(g.minScaleRel);
        var s = new DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
        {
            header = Header<DISPLAYCONFIG_SOURCE_DPI_SCALE_SET>(SET_DPI_SCALE, adapter, sourceId),
            scaleRel = wanted - recommended,
        };
        return DisplayConfigSetDeviceInfo(ref s) == 0;
    }
}
