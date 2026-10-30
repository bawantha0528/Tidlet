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
                error == 50 ? "GPU load is not connected in this build. No adapter or utilization value is assumed." : $"Windows could not read GPU activity (error {error}).");

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

// GPU PDH/DXGI enumeration is unfinished. Report unsupported instead of inventing an adapter.
public sealed class NativeSystemGpuSource : ISystemGpuSource
{
    public bool TryRead(out string adapterName, out double? utilization, out ulong? memUsed, out ulong? memTotal, out int error)
    { adapterName = "GPU"; utilization = null; memUsed = memTotal = null; error = 50; return false; }
}