using System.Runtime.InteropServices;

namespace DuoGPU.Native;

/// <summary>The GPUs (through DXGI) and how much of their memory is in use (through the Windows performance counters).
/// Both are plain Windows APIs, so AMD, NVIDIA and Intel cards all work the same way.</summary>
public static class Gpus
{
    public record Adapter(string Name, LUID Luid, ulong DedicatedVram);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint VendorId, DeviceId, SubSysId, Revision;
        public nuint DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
        public LUID AdapterLuid;
        public uint Flags;
    }

    const uint DXGI_ADAPTER_FLAG_SOFTWARE = 2;
    const int DXGI_ERROR_NOT_FOUND = unchecked((int)0x887A0002);

    [ComImport, Guid("770aae78-f26f-4dba-a829-253c83d1b387"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDXGIFactory1
    {
        void SetPrivateData(); void SetPrivateDataInterface(); void GetPrivateData(); void GetParent();
        void EnumAdapters(); void MakeWindowAssociation(); void GetWindowAssociation(); void CreateSwapChain();
        void CreateSoftwareAdapter();
        [PreserveSig] int EnumAdapters1(uint index, out IDXGIAdapter1 adapter);
        [PreserveSig] bool IsCurrent();
    }

    [ComImport, Guid("29038f61-3839-4626-91fd-086879011a05"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDXGIAdapter1
    {
        void SetPrivateData(); void SetPrivateDataInterface(); void GetPrivateData(); void GetParent();
        void EnumOutputs(); void GetDesc(); void CheckInterfaceSupport();
        [PreserveSig] int GetDesc1(out DXGI_ADAPTER_DESC1 desc);
    }

    [DllImport("dxgi.dll")]
    static extern int CreateDXGIFactory1([In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IDXGIFactory1 factory);

    public static List<Adapter> List()
    {
        var result = new List<Adapter>();
        var iid = typeof(IDXGIFactory1).GUID;
        if (CreateDXGIFactory1(ref iid, out var factory) != 0) return result;
        try
        {
            for (uint i = 0; ; i++)
            {
                if (factory.EnumAdapters1(i, out var adapter) == DXGI_ERROR_NOT_FOUND) break;
                try
                {
                    if (adapter.GetDesc1(out var d) != 0 || (d.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) != 0) continue;
                    if (result.Any(a => a.Luid.Equals(d.AdapterLuid))) continue;
                    result.Add(new Adapter(d.Description.Trim(), d.AdapterLuid, d.DedicatedVideoMemory));
                }
                finally
                {
                    Marshal.ReleaseComObject(adapter);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
        return result;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PDH_FMT_COUNTERVALUE
    {
        public uint CStatus;
        public uint Padding;
        public long LargeValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    static extern int PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    static extern int PdhGetFormattedCounterValue(IntPtr counter, uint format, IntPtr type, out PDH_FMT_COUNTERVALUE value);

    [DllImport("pdh.dll")]
    static extern int PdhCloseQuery(IntPtr query);

    const uint PDH_FMT_LARGE = 0x400;

    /// <summary>Dedicated VRAM in use on this GPU, in bytes ("GPU Adapter Memory" in Task Manager), or null.</summary>
    public static ulong? VramInUse(LUID luid)
    {
        if (PdhOpenQuery(null, IntPtr.Zero, out var q) != 0) return null;
        try
        {
            string path = $@"\GPU Adapter Memory(luid_{luid}_phys_0)\Dedicated Usage";
            if (PdhAddEnglishCounter(q, path, IntPtr.Zero, out var c) != 0) return null;
            if (PdhCollectQueryData(q) != 0) return null;
            if (PdhGetFormattedCounterValue(c, PDH_FMT_LARGE, IntPtr.Zero, out var v) != 0) return null;
            return (ulong)Math.Max(0, v.LargeValue);
        }
        finally
        {
            PdhCloseQuery(q);
        }
    }
}
