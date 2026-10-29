using System.Net.NetworkInformation;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;

namespace Islet.Windows.Metrics;

public sealed class SystemPingProvider(string targetHost = "1.1.1.1") : IPingProvider
{
    private readonly JitterCalculator _jitter = new();
    private CapabilityProbeResult? _capability;

    public CapabilityProbeResult Probe()
    {
        _jitter.Reset();
        return _capability = CapabilityProbeResult.Supported;
    }

    public async Task<PingReading> SampleAsync(CancellationToken cancellationToken)
    {
        if (_capability?.Availability != CapabilityAvailability.Supported)
            return PingReading.Missing(PingReadingState.Unavailable, _capability?.Explanation ?? "Ping has not been probed.");

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(targetHost, 1000).ConfigureAwait(false);

            if (reply.Status == IPStatus.Success)
            {
                var rtt = (double)reply.RoundtripTime;
                var jitter = _jitter.Record(rtt);
                var jitterText = jitter.HasValue ? $"  ±{jitter.Value:F1}ms jitter" : "";
                return new(PingReadingState.Available, rtt, jitter, targetHost,
                    $"{rtt:F0} ms to {targetHost}{jitterText}");
            }

            return PingReading.Missing(PingReadingState.Unavailable,
                $"Ping status: {reply.Status} to {targetHost}");
        }
        catch (Exception ex)
        {
            return PingReading.Missing(PingReadingState.Unavailable,
                $"Ping error: {ex.Message}");
        }
    }

    public void Reset() => _jitter.Reset();
}
