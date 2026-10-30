using System.Diagnostics;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;

namespace Islet.Windows.Metrics;

public interface ISystemDiskSource
{
    bool TryRead(out ulong readBytes, out ulong writeBytes, out double? activePercent, out int error);
}

public sealed class SystemDiskProvider(ISystemDiskSource source) : IDiskProvider
{
    private readonly DiskDelta _delta = new();
    private CapabilityProbeResult? _capability;

    public CapabilityProbeResult Probe()
    {
        _delta.Reset();
        if (!source.TryRead(out var readBytes, out var writeBytes, out var active, out var error))
            return _capability = CapabilityProbeResult.Unsupported("disk.read-failed",
                $"Disk activity read failed (error {error}).");

        var test = _delta.Update(new(readBytes, writeBytes, TimeSpan.FromSeconds(1)), active);
        if (test.State == DiskReadingState.Invalid)
            return _capability = CapabilityProbeResult.Unsupported("disk.invalid", test.Explanation);

        return _capability = CapabilityProbeResult.Supported;
    }

    public DiskReading Sample()
    {
        if (_capability?.Availability != CapabilityAvailability.Supported)
            return DiskReading.Missing(DiskReadingState.Unavailable, _capability?.Explanation ?? "Disk has not been probed.");

        if (source.TryRead(out var readBytes, out var writeBytes, out var active, out _))
            return _delta.Update(new(readBytes, writeBytes, TimeSpan.FromTicks(DateTime.UtcNow.Ticks)), active);

        _delta.Reset();
        return DiskReading.Missing(DiskReadingState.Unavailable, "Disk read failed.");
    }

    public void Reset() => _delta.Reset();
}

// Legacy source contract supplies cumulative bytes, while real PDH supplies already formatted rates.
// Use PdhDiskProvider for production. Never substitute made-up activity for missing counters.
public sealed class NativeSystemDiskSource : ISystemDiskSource
{
    public bool TryRead(out ulong readBytes, out ulong writeBytes, out double? activePercent, out int error)
    { readBytes = writeBytes = 0; activePercent = null; error = 50; return false; }
}