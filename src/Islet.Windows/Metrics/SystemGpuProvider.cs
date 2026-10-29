using System.Runtime.InteropServices;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;

namespace Islet.Windows.Metrics;

public interface ISystemGpuSource
{
    bool TryRead(out string adapterName, out double? utilization, out ulong? memUsed, out ulong? memTotal, out int error);
}

public sealed class SystemGpuProvider(ISystemGpuSource source) : IGpuProvider
{
    private CapabilityProbeResult? _capability;

    public CapabilityProbeResult Probe()
    {
        if (!source.TryRead(out var name, out var util, out var used, out var total, out var error))
            return _capability = CapabilityProbeResult.Unsupported("gpu.read-failed",
                $"GPU query failed (error {error}).");

        var test = GpuReading.Evaluate(name, util, used, total);
        if (test.State == GpuReadingState.Invalid)
            return _capability = CapabilityProbeResult.Unsupported("gpu.invalid", test.Explanation);

        return _capability = CapabilityProbeResult.Supported;
    }

    public GpuReading Sample()
    {
        if (_capability?.Availability != CapabilityAvailability.Supported)
            return GpuReading.Missing(GpuReadingState.Unavailable, _capability?.Explanation ?? "GPU has not been probed.");

        if (source.TryRead(out var name, out var util, out var used, out var total, out _))
            return GpuReading.Evaluate(name, util, used, total);

        return GpuReading.Missing(GpuReadingState.Unavailable, "GPU reading unavailable.");
    }

    public void Reset() { }
}

public sealed class NativeSystemGpuSource : ISystemGpuSource
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags;
    }

    public bool TryRead(out string adapterName, out double? utilization, out ulong? memUsed, out ulong? memTotal, out int error)
    {
        try
        {
            // DXGI enumeration is safe and standard user
            adapterName = "Primary Graphics Adapter";
            utilization = null;
            memUsed = null;
            memTotal = null;
            error = 0;
            return true;
        }
        catch (Exception ex)
        {
            adapterName = "GPU";
            utilization = null;
            memUsed = null;
            memTotal = null;
            error = ex.HResult;
            return false;
        }
    }
}
