using Islet.Core.Capabilities;

namespace Islet.Core.Metrics;

public enum DiskReadingState { Available, WarmingUp, Unavailable, Invalid }

public sealed record DiskReading(
    DiskReadingState State,
    ulong ReadBytesPerSec,
    ulong WriteBytesPerSec,
    double? ActiveTimePercent,
    string Explanation)
{
    public static DiskReading Missing(DiskReadingState state, string reason) =>
        new(state, 0, 0, null, reason);

    public static string FormatThroughput(ulong bytesPerSec)
    {
        if (bytesPerSec < 1024) return $"{bytesPerSec} B/s";
        var kb = bytesPerSec / 1024.0;
        if (kb < 1024) return $"{kb:F1} KB/s";
        var mb = kb / 1024.0;
        return $"{mb:F1} MB/s";
    }
}

public readonly record struct DiskCounters(ulong ReadBytes, ulong WriteBytes, TimeSpan Timestamp);

public sealed class DiskDelta
{
    private DiskCounters? _previous;

    public void Reset() => _previous = null;

    public DiskReading Update(DiskCounters current, double? activeTimePercent)
    {
        var previous = _previous;
        _previous = current;

        if (previous is not { } prior)
            return DiskReading.Missing(DiskReadingState.WarmingUp, "Measuring disk throughput…");

        var elapsedSeconds = (current.Timestamp - prior.Timestamp).TotalSeconds;
        if (elapsedSeconds <= 0)
            return DiskReading.Missing(DiskReadingState.Invalid, "Disk measurement interval must be positive.");

        if (current.ReadBytes < prior.ReadBytes || current.WriteBytes < prior.WriteBytes)
            return DiskReading.Missing(DiskReadingState.WarmingUp, "Disk counter reset; measuring again.");

        var readDelta = current.ReadBytes - prior.ReadBytes;
        var writeDelta = current.WriteBytes - prior.WriteBytes;

        var readRate = (ulong)Math.Round(readDelta / elapsedSeconds);
        var writeRate = (ulong)Math.Round(writeDelta / elapsedSeconds);

        var activeText = activeTimePercent.HasValue ? $" ({activeTimePercent.Value:F0}% active)" : "";
        return new(DiskReadingState.Available, readRate, writeRate, activeTimePercent,
            $"R: {DiskReading.FormatThroughput(readRate)}  W: {DiskReading.FormatThroughput(writeRate)}{activeText}");
    }
}

public interface IDiskProvider
{
    CapabilityProbeResult Probe();
    DiskReading Sample();
    void Reset();
}
