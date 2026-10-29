using Islet.Core.Capabilities;

namespace Islet.Core.Metrics;

public enum NetworkReadingState { Available, WarmingUp, Unavailable, Invalid }

public sealed record NetworkReading(
    NetworkReadingState State,
    ulong BytesReceivedPerSec,
    ulong BytesSentPerSec,
    string? InterfaceName,
    string Explanation)
{
    public static NetworkReading Missing(NetworkReadingState state, string reason) =>
        new(state, 0, 0, null, reason);

    public static string FormatRate(ulong bytesPerSec)
    {
        if (bytesPerSec < 1024) return $"{bytesPerSec} B/s";
        var kb = bytesPerSec / 1024.0;
        if (kb < 1024) return $"{kb:F1} KB/s";
        var mb = kb / 1024.0;
        return $"{mb:F1} MB/s";
    }
}

public readonly record struct NetworkCounters(ulong InOctets, ulong OutOctets, TimeSpan Timestamp);

public sealed class NetworkDelta
{
    private NetworkCounters? _previous;

    public void Reset() => _previous = null;

    public NetworkReading Update(NetworkCounters current, string? interfaceName)
    {
        var previous = _previous;
        _previous = current;

        if (previous is not { } prior)
            return NetworkReading.Missing(NetworkReadingState.WarmingUp, "Measuring network throughput…");

        var elapsedSeconds = (current.Timestamp - prior.Timestamp).TotalSeconds;
        if (elapsedSeconds <= 0)
            return NetworkReading.Missing(NetworkReadingState.Invalid, "Network measurement interval must be positive.");

        if (current.InOctets < prior.InOctets || current.OutOctets < prior.OutOctets)
            return NetworkReading.Missing(NetworkReadingState.WarmingUp, "Network adapter reset or counter restarted; re-measuring.");

        var deltaIn = current.InOctets - prior.InOctets;
        var deltaOut = current.OutOctets - prior.OutOctets;

        var inRate = (ulong)Math.Round(deltaIn / elapsedSeconds);
        var outRate = (ulong)Math.Round(deltaOut / elapsedSeconds);

        var name = string.IsNullOrWhiteSpace(interfaceName) ? "Active connection" : interfaceName;
        return new(NetworkReadingState.Available, inRate, outRate, interfaceName,
            $"↓ {NetworkReading.FormatRate(inRate)}  ↑ {NetworkReading.FormatRate(outRate)} ({name})");
    }
}

public interface INetworkProvider
{
    CapabilityProbeResult Probe();
    NetworkReading Sample();
    void Reset();
}
