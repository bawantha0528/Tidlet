namespace Islet.Core.Bar;

public enum ThresholdLevel { Normal, Warning, Critical }

public enum FrameRateLevel { Unknown, Good, Fair, Low }

/// <summary>Evaluates color-coding levels for strip utilization thresholds and FPS targets.</summary>
public static class BarThresholds
{
    public const int CriticalPercent = 95;

    public static ThresholdLevel UsageLevel(BarModule module, double? percent, int thresholdPercent)
    {
        if (module is not (BarModule.Cpu or BarModule.Gpu or BarModule.Ram or BarModule.Disk))
            return ThresholdLevel.Normal;
        if (percent is not { } value || !double.IsFinite(value) || value is < 0 or > 100)
            return ThresholdLevel.Normal;
        if (value >= CriticalPercent)
            return ThresholdLevel.Critical;
        return value >= thresholdPercent ? ThresholdLevel.Warning : ThresholdLevel.Normal;
    }

    public static ThresholdLevel StorageLevel(double? usedPercent) => UsageLevel(BarModule.Disk, usedPercent, 90);

    public static ThresholdLevel BatteryChargeLevel(double? percent) => LowLevel(percent, 25, 15);
    public static ThresholdLevel BatteryHealthLevel(double? percent) => LowLevel(percent, 80, 60);

    private static ThresholdLevel LowLevel(double? percent, int warning, int critical) =>
        percent is >= 0 and <= 100
            ? percent <= critical ? ThresholdLevel.Critical : percent <= warning ? ThresholdLevel.Warning : ThresholdLevel.Normal
            : ThresholdLevel.Normal;

    public static ThresholdLevel TemperatureLevel(double? celsius) =>
        celsius is >= 90 and <= 150 ? ThresholdLevel.Critical
        : celsius is >= 80 and < 90 ? ThresholdLevel.Warning : ThresholdLevel.Normal;

    public static double? ComputeFps(double? milliseconds) =>
        milliseconds is { } ms && double.IsFinite(ms) && ms > 0 ? 1000.0 / ms : null;

    public static FrameRateLevel FrameLevel(double? milliseconds, int fpsTarget)
    {
        if (ComputeFps(milliseconds) is not { } fps || fpsTarget <= 0)
            return FrameRateLevel.Unknown;
        var displayed = Math.Round(fps);
        if (displayed >= fpsTarget)
            return FrameRateLevel.Good;
        return displayed >= fpsTarget / 2.0 ? FrameRateLevel.Fair : FrameRateLevel.Low;
    }
}
