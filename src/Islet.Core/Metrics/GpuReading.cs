using Islet.Core.Capabilities;

namespace Islet.Core.Metrics;

public enum GpuReadingState { Available, Unavailable, Invalid }

public sealed record GpuReading(
    GpuReadingState State,
    string AdapterName,
    double? UtilizationPercent,
    ulong? DedicatedMemoryUsedBytes,
    ulong? DedicatedMemoryTotalBytes,
    string Explanation)
{
    public static GpuReading Missing(GpuReadingState state, string reason) =>
        new(state, "GPU", null, null, null, reason);

    public static GpuReading Evaluate(
        string adapterName,
        double? utilizationPercent,
        ulong? memoryUsed,
        ulong? memoryTotal)
    {
        if (string.IsNullOrWhiteSpace(adapterName))
            adapterName = "Primary GPU";

        double? clampedUtil = utilizationPercent.HasValue ? Math.Clamp(utilizationPercent.Value, 0, 100) : null;

        var text = clampedUtil.HasValue ? $"{clampedUtil.Value:F0}% load" : "GPU active";
        if (memoryUsed.HasValue && memoryTotal.HasValue && memoryTotal.Value > 0)
        {
            var usedGib = memoryUsed.Value / (1024.0 * 1024.0 * 1024.0);
            var totalGib = memoryTotal.Value / (1024.0 * 1024.0 * 1024.0);
            text += $" ({usedGib:F1} / {totalGib:F1} GiB VRAM)";
        }

        return new(GpuReadingState.Available, adapterName, clampedUtil, memoryUsed, memoryTotal,
            $"{adapterName}: {text}");
    }
}

public interface IGpuProvider
{
    CapabilityProbeResult Probe();
    GpuReading Sample();
    void Reset();
}
