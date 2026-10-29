using Islet.Core.Capabilities;

namespace Islet.Core.Metrics;

public enum CpuReadingState { Available, WarmingUp, Unavailable, Invalid }

public sealed record CpuReading(CpuReadingState State, double? Percent, string Explanation)
{
    public static CpuReading Missing(CpuReadingState state, string reason) => new(state, null, reason);
}

public readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);

public interface ICpuProvider
{
    CapabilityProbeResult Probe();
    CpuReading Sample();
    void Reset();
}

/// <summary>GetSystemTimes includes idle time in kernel time. No Windows references here.</summary>
public sealed class CpuDelta
{
    private CpuTimes? _previous;

    public void Reset() => _previous = null;

    public CpuReading Update(CpuTimes current)
    {
        var previous = _previous;
        _previous = current.Idle <= current.Kernel ? current : null;
        if (current.Idle > current.Kernel)
            return CpuReading.Missing(CpuReadingState.Invalid, "Windows returned inconsistent CPU times.");
        if (previous is not { } prior)
            return CpuReading.Missing(CpuReadingState.WarmingUp, "Measuring CPU…");
        if (current.Idle < prior.Idle || current.Kernel < prior.Kernel || current.User < prior.User)
            return CpuReading.Missing(CpuReadingState.WarmingUp, "CPU counters restarted; measuring again.");
        var idle = current.Idle - prior.Idle;
        var kernel = current.Kernel - prior.Kernel;
        var user = current.User - prior.User;
        // Convert before adding so even extreme counter deltas cannot overflow ulong.
        var total = (double)kernel + user;
        if (total <= 0 || idle > kernel)
            return CpuReading.Missing(CpuReadingState.Invalid, "CPU times have not advanced consistently.");
        var percent = Math.Clamp(100.0 * (total - idle) / total, 0, 100);
        return new(CpuReadingState.Available, percent, "CPU usage across this PC.");
    }
}
