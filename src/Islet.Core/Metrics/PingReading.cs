using Islet.Core.Capabilities;

namespace Islet.Core.Metrics;

public enum PingReadingState { Available, Unavailable, Invalid }

public sealed record PingReading(
    PingReadingState State,
    double? RttMs,
    double? JitterMs,
    string? TargetHost,
    string Explanation)
{
    public static PingReading Missing(PingReadingState state, string reason) =>
        new(state, null, null, null, reason);
}

public sealed class JitterCalculator
{
    private readonly Queue<double> _rttHistory = new();
    private const int MaxHistory = 10;

    public void Reset() => _rttHistory.Clear();

    public double? Record(double rttMs)
    {
        if (rttMs < 0 || double.IsNaN(rttMs) || double.IsInfinity(rttMs))
            return null;

        _rttHistory.Enqueue(rttMs);
        while (_rttHistory.Count > MaxHistory) _rttHistory.Dequeue();

        if (_rttHistory.Count < 2) return null;

        double sumDiff = 0;
        double? prev = null;
        var diffCount = 0;
        foreach (var rtt in _rttHistory)
        {
            if (prev.HasValue)
            {
                sumDiff += Math.Abs(rtt - prev.Value);
                diffCount++;
            }
            prev = rtt;
        }

        return diffCount > 0 ? sumDiff / diffCount : null;
    }
}

public interface IPingProvider
{
    CapabilityProbeResult Probe();
    Task<PingReading> SampleAsync(CancellationToken cancellationToken);
    void Reset();
}
