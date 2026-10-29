using Islet.Core.Capabilities;

namespace Islet.Core.Metrics;

public enum BatteryReadingState { Available, Unavailable, Invalid }

public enum BatteryChargingStatus { Unknown, Discharging, Charging, Full, Critical }

public sealed record BatteryReading(
    BatteryReadingState State,
    double? Percent,
    BatteryChargingStatus ChargingStatus,
    TimeSpan? EstimatedRemaining,
    double? ChargeRateMilliwatts,
    double? HealthPercent,
    string Explanation)
{
    public static BatteryReading Missing(BatteryReadingState state, string reason) =>
        new(state, null, BatteryChargingStatus.Unknown, null, null, null, reason);

    public static BatteryReading NoBattery() =>
        new(BatteryReadingState.Unavailable, null, BatteryChargingStatus.Unknown, null, null, null,
            "No battery detected on this device.");

    public static BatteryReading Evaluate(
        byte flag,
        byte percentByte,
        int lifeTimeSeconds,
        double? rateMilliwatts,
        double? healthPercent)
    {
        // 128 = No system battery
        if ((flag & 128) != 0 || percentByte == 255 && flag == 255)
            return NoBattery();

        if (percentByte > 100 && percentByte != 255)
            return Missing(BatteryReadingState.Invalid, "Windows reported invalid battery percentage.");

        double? percent = percentByte <= 100 ? Math.Clamp((double)percentByte, 0, 100) : null;

        var status = BatteryChargingStatus.Discharging;
        if ((flag & 8) != 0) status = BatteryChargingStatus.Charging;
        else if ((flag & 4) != 0) status = BatteryChargingStatus.Critical;
        else if (percent == 100) status = BatteryChargingStatus.Full;

        TimeSpan? remaining = lifeTimeSeconds > 0 && lifeTimeSeconds < 86400 * 7
            ? TimeSpan.FromSeconds(lifeTimeSeconds)
            : null;

        var text = percent.HasValue
            ? $"{percent:F0}% ({status})"
            : $"Battery status: {status}";

        if (remaining.HasValue && status == BatteryChargingStatus.Discharging)
            text += $" — ~{remaining.Value.Hours}h {remaining.Value.Minutes}m remaining";

        return new(BatteryReadingState.Available, percent, status, remaining, rateMilliwatts, healthPercent, text);
    }
}

public interface IBatteryProvider
{
    CapabilityProbeResult Probe();
    BatteryReading Sample();
    void Reset();
}
