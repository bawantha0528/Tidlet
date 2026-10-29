using Islet.Core.Capabilities;

namespace Islet.Core.Metrics;

public enum RamReadingState { Available, WarmingUp, Unavailable, Invalid }

public sealed record RamReading(
    RamReadingState State,
    ulong TotalBytes,
    ulong AvailableBytes,
    ulong UsedBytes,
    double? UsedPercent,
    string Explanation)
{
    public static RamReading Missing(RamReadingState state, string reason) =>
        new(state, 0, 0, 0, null, reason);

    public static RamReading Evaluate(ulong totalBytes, ulong availableBytes)
    {
        if (totalBytes == 0)
            return Missing(RamReadingState.Invalid, "Windows reported zero physical memory.");
        if (availableBytes > totalBytes)
            return Missing(RamReadingState.Invalid, "Windows reported available memory exceeding total physical memory.");

        var usedBytes = totalBytes - availableBytes;
        var percent = Math.Clamp(100.0 * usedBytes / totalBytes, 0, 100);
        var usedGib = usedBytes / (1024.0 * 1024.0 * 1024.0);
        var totalGib = totalBytes / (1024.0 * 1024.0 * 1024.0);
        return new(RamReadingState.Available, totalBytes, availableBytes, usedBytes, percent,
            $"{usedGib:F1} GiB used of {totalGib:F1} GiB physical RAM.");
    }
}

public interface IRamProvider
{
    CapabilityProbeResult Probe();
    RamReading Sample();
    void Reset();
}
