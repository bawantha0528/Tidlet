namespace Islet.Core.Bar;

public enum PowerMode { Efficiency, Balanced, Performance }
public enum PowerSource { PluggedIn, Battery }

public sealed record PowerModeReading(PowerSource? Source, PowerMode? Mode, string? Reason = null);

public interface IPowerModeControl
{
    PowerModeReading Read();
    string? Apply(PowerSource expectedSource, PowerMode mode);
    string OpenSettings();
}
