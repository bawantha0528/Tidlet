using System.Runtime.InteropServices;

namespace Islet.Windows.Popups;

internal static class GpuTemperature
{
    private const uint PhysicalAdapterCount = 30;
    private const uint AdapterPerformanceData = 62;

    [StructLayout(LayoutKind.Sequential)]
    private struct Adapter
    {
        public uint LuidLow;
        public int LuidHigh;
        public uint Handle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Query
    {
        public uint AdapterHandle;
        public uint Type;
        public nint Data;
        public uint Size;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct PerformanceData
    {
        [FieldOffset(56)] public uint Temperature;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern int D3DKMTOpenAdapterFromLuid(ref Adapter adapter);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern int D3DKMTQueryAdapterInfo(ref Query query);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern int D3DKMTCloseAdapter(in uint handle);

    internal static unsafe double? Read(ulong luid)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17134)) return null;
        var adapter = new Adapter { LuidLow = (uint)luid, LuidHigh = (int)(luid >> 32) };
        try
        {
            if (D3DKMTOpenAdapterFromLuid(ref adapter) != 0 || adapter.Handle == 0) return null;
            try
            {
                uint count = 0;
                var query = new Query { AdapterHandle = adapter.Handle, Type = PhysicalAdapterCount, Data = (nint)(&count), Size = sizeof(uint) };
                // Skip linked groups because their logical adapter has more than one sensor.
                if (D3DKMTQueryAdapterInfo(ref query) != 0 || count != 1) return null;
                var performance = new PerformanceData();
                query.Type = AdapterPerformanceData;
                query.Data = (nint)(&performance);
                query.Size = (uint)sizeof(PerformanceData);
                return D3DKMTQueryAdapterInfo(ref query) == 0 && performance.Temperature is > 0 and <= 1500
                    ? performance.Temperature / 10.0 : null;
            }
            finally { D3DKMTCloseAdapter(in adapter.Handle); }
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) { return null; }
    }
}
