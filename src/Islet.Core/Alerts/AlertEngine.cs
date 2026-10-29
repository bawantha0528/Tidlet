using Islet.Core.Metrics;
using Islet.Core.Settings;

namespace Islet.Core.Alerts;

public enum AlertSeverity { Info, Warning, Critical }

public sealed record AlertNotice(string Title, string Message, DateTime Timestamp, AlertSeverity Severity);

public sealed class AlertEngine(AlertSettings settings)
{
    private readonly Dictionary<string, DateTime> _lastTriggered = new(StringComparer.OrdinalIgnoreCase);
    private bool _batteryWasLow;

    public IReadOnlyList<AlertNotice> Evaluate(
        BatteryReading? battery,
        PingReading? ping,
        bool isDoNotDisturb,
        bool isSuppressed,
        DateTime now)
    {
        if (!settings.Enabled || isDoNotDisturb || isSuppressed)
            return Array.Empty<AlertNotice>();

        var notices = new List<AlertNotice>();

        // 1. Battery Low Alert with Hysteresis
        if (battery is { State: BatteryReadingState.Available, Percent: { } percent, ChargingStatus: BatteryChargingStatus.Discharging })
        {
            if (percent <= settings.BatteryLowThresholdPercent)
            {
                if (!_batteryWasLow && CanTrigger("battery.low", now))
                {
                    _batteryWasLow = true;
                    RecordTrigger("battery.low", now);
                    notices.Add(new AlertNotice("Battery Low", $"Battery is at {percent:F0}%. Connect charger.", now, AlertSeverity.Warning));
                }
            }
            else if (percent >= settings.BatteryLowThresholdPercent + 5.0)
            {
                _batteryWasLow = false;
            }
        }
        else if (battery is { ChargingStatus: BatteryChargingStatus.Charging })
        {
            _batteryWasLow = false;
        }

        // 2. Ping Spike Alert
        if (ping is { State: PingReadingState.Available, RttMs: { } rtt })
        {
            if (rtt >= settings.PingSpikeThresholdMs && CanTrigger("ping.spike", now))
            {
                RecordTrigger("ping.spike", now);
                notices.Add(new AlertNotice("High Latency", $"Ping latency reached {rtt:F0} ms.", now, AlertSeverity.Info));
            }
        }

        return notices;
    }

    private bool CanTrigger(string ruleKey, DateTime now)
    {
        if (!_lastTriggered.TryGetValue(ruleKey, out var last))
            return true;
        return (now - last).TotalSeconds >= settings.CooldownSeconds;
    }

    private void RecordTrigger(string ruleKey, DateTime now)
    {
        _lastTriggered[ruleKey] = now;
    }
}
