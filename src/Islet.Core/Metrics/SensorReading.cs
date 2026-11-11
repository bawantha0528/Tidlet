using Islet.Core.Capabilities;

namespace Islet.Core.Metrics;

public enum SensorReadingState { Available, Unavailable, NeedsHelper }
public enum FanControlMode { AutoFirmware, Quiet, Balanced, Performance, FullSpeed }

public sealed record SensorReading(
    SensorReadingState State,
    double? PackageTemperatureC,
    double? ZoneTemperatureC,
    int? FanRpm,
    FanControlMode FanProfile,
    bool IsThrottling,
    string Explanation)
{
    public static SensorReading Missing(SensorReadingState state, string reason) =>
        new(state, null, null, null, FanControlMode.AutoFirmware, false, reason);

    public static SensorReading NeedsHelper(string reason) =>
        new(SensorReadingState.NeedsHelper, null, null, null, FanControlMode.AutoFirmware, false, reason);

    public static SensorReading Evaluate(
        double? packageTemp,
        double? zoneTemp,
        int? fanRpm,
        FanControlMode profile = FanControlMode.AutoFirmware,
        bool isThrottling = false)
    {
        var tempStr = packageTemp.HasValue ? $"{packageTemp.Value:F0}°C" : zoneTemp.HasValue ? $"{zoneTemp.Value:F0}°C" : "Not reported";
        var fanStr = fanRpm.HasValue ? $"{fanRpm.Value} RPM" : "Managed by BIOS";
        var expl = $"Thermal: {tempStr} · Fan: {fanStr} ({profile})";
        return new(SensorReadingState.Available, packageTemp, zoneTemp, fanRpm, profile, isThrottling, expl);
    }
}

public interface ISensorProvider
{
    CapabilityProbeResult Probe();
    SensorReading Sample();
    bool TrySetFanProfile(FanControlMode mode, out string explanation);
    void Reset();
}

