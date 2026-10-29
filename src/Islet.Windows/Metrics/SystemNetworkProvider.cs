using System.Net.NetworkInformation;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;

namespace Islet.Windows.Metrics;

public interface ISystemNetworkSource
{
    bool TryReadActiveInterface(out ulong inOctets, out ulong outOctets, out string? interfaceName, out int error);
}

public sealed class SystemNetworkProvider(ISystemNetworkSource source) : INetworkProvider
{
    private readonly NetworkDelta _delta = new();
    private CapabilityProbeResult? _capability;

    public CapabilityProbeResult Probe()
    {
        _delta.Reset();
        if (!source.TryReadActiveInterface(out var inOctets, out var outOctets, out var name, out var error))
            return _capability = CapabilityProbeResult.Unsupported("network.read-failed",
                $"Network interface query failed (error {error}).");

        var test = _delta.Update(new(inOctets, outOctets, TimeSpan.FromSeconds(1)), name);
        if (test.State == NetworkReadingState.Invalid)
            return _capability = CapabilityProbeResult.Unsupported("network.invalid", test.Explanation);

        return _capability = CapabilityProbeResult.Supported;
    }

    public NetworkReading Sample()
    {
        if (_capability?.Availability != CapabilityAvailability.Supported)
            return NetworkReading.Missing(NetworkReadingState.Unavailable, _capability?.Explanation ?? "Network has not been probed.");

        if (source.TryReadActiveInterface(out var inOctets, out var outOctets, out var name, out _))
            return _delta.Update(new(inOctets, outOctets, TimeSpan.FromTicks(DateTime.UtcNow.Ticks)), name);

        _delta.Reset();
        return NetworkReading.Missing(NetworkReadingState.Unavailable, "Network interface reading unavailable.");
    }

    public void Reset() => _delta.Reset();
}

public sealed class NativeSystemNetworkSource : ISystemNetworkSource
{
    public bool TryReadActiveInterface(out ulong inOctets, out ulong outOctets, out string? interfaceName, out int error)
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            NetworkInterface? best = null;
            ulong totalIn = 0;
            ulong totalOut = 0;

            foreach (var nic in interfaces)
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                var stats = nic.GetIPStatistics();
                totalIn += (ulong)stats.BytesReceived;
                totalOut += (ulong)stats.BytesSent;

                if (best is null && (stats.BytesReceived > 0 || stats.BytesSent > 0))
                    best = nic;
            }

            inOctets = totalIn;
            outOctets = totalOut;
            interfaceName = best?.Name ?? "Ethernet/Wi-Fi";
            error = 0;
            return true;
        }
        catch (Exception ex)
        {
            inOctets = 0;
            outOctets = 0;
            interfaceName = null;
            error = ex.HResult;
            return false;
        }
    }
}
