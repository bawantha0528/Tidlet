using Islet.Core.Capabilities;
using Islet.Core.Metrics;

namespace Islet.Windows.Metrics;

public interface ISystemSensorSource
{
    bool TryRead(out double? pkgTemp, out double? zoneTemp, out int? fanRpm, out FanControlMode profile, out bool isThrottling, out string? reason);
    bool TrySetProfile(FanControlMode mode, out string explanation);
}

public sealed class SystemSensorProvider(ISystemSensorSource source) : ISensorProvider
{
    private CapabilityProbeResult? _capability;

    public CapabilityProbeResult Probe()
    {
        if (!source.TryRead(out _, out _, out _, out _, out _, out var reason))
        {
            return _capability = CapabilityProbeResult.NeedsHelper("sensors.needs-helper",
                reason ?? "Reading core temperatures and overriding firmware fan curves requires the optional elevated hardware helper (M4). Windows denies non-admin access to ACPI thermal zones and fan controllers.");
        }

        return _capability = CapabilityProbeResult.Supported;
    }

    public SensorReading Sample()
    {
        if (_capability?.Availability == CapabilityAvailability.NeedsHelper)
            return SensorReading.NeedsHelper(_capability.Explanation ?? "Sensor helper required.");

        if (source.TryRead(out var pkg, out var zone, out var fan, out var profile, out var throttling, out _))
            return SensorReading.Evaluate(pkg, zone, fan, profile, throttling);

        return SensorReading.Missing(SensorReadingState.Unavailable, _capability?.Explanation ?? "Sensor reading unavailable.");
    }

    public bool TrySetFanProfile(FanControlMode mode, out string explanation)
        => source.TrySetProfile(mode, out explanation);

    public void Reset() { }
}

public sealed class NativeSystemSensorSource : ISystemSensorSource
{
    public bool TryRead(out double? pkgTemp, out double? zoneTemp, out int? fanRpm, out FanControlMode profile, out bool isThrottling, out string? reason)
    {
        pkgTemp = null;
        zoneTemp = null;
        fanRpm = null;
        profile = FanControlMode.AutoFirmware;
        isThrottling = false;
        reason = "Reading core temperatures and overriding firmware fan curves requires the optional elevated hardware helper (M4). Windows denies non-admin access to ACPI thermal zones and fan controllers.";
        return false;
    }

    public bool TrySetProfile(FanControlMode mode, out string explanation)
    {
        explanation = "Fan control is managed by BIOS firmware. Overriding fan curves requires the M4 elevated hardware helper.";
        return false;
    }
}

public sealed class SimulatedSystemSensorSource(double packageTemp = 48.0, int fanRpm = 1850) : ISystemSensorSource
{
    private double _temp = packageTemp;
    private int _rpm = fanRpm;
    private FanControlMode _mode = FanControlMode.AutoFirmware;

    public bool TryRead(out double? pkgTemp, out double? zoneTemp, out int? fanRpmOut, out FanControlMode profile, out bool isThrottling, out string? reason)
    {
        pkgTemp = _temp;
        zoneTemp = _temp - 4.0;
        fanRpmOut = _rpm;
        profile = _mode;
        isThrottling = _temp > 95.0;
        reason = null;
        return true;
    }

    public bool TrySetProfile(FanControlMode mode, out string explanation)
    {
        _mode = mode;
        _rpm = mode switch
        {
            FanControlMode.Quiet => 1200,
            FanControlMode.Balanced => 1950,
            FanControlMode.Performance => 2800,
            FanControlMode.FullSpeed => 3600,
            _ => 1850
        };
        explanation = $"Fan profile set to {mode} (Simulated).";
        return true;
    }
}
