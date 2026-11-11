using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Islet.Core.Bar;

namespace Islet.Windows.Bar;

public sealed class WindowsPowerModeControl(bool readOnly) : IPowerModeControl
{
    private static readonly Guid Efficiency = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    private static readonly Guid Performance = new("ded574b5-45a0-4f42-8737-46345c09c238");
    private const string Unsupported = "Direct power mode control is unavailable on this Windows version.";

    public PowerModeReading Read()
    {
        var source = CurrentSource();
        if (source is null) return new(null, null, "Windows did not report the current power source.");
        try
        {
            var status = source == PowerSource.PluggedIn
                ? PowerGetUserConfiguredACPowerMode(out var mode)
                : PowerGetUserConfiguredDCPowerMode(out mode);
            if (status != 0) return new(source, null, $"Windows could not read the configured mode. {Error(status)}");
            if (mode == Guid.Empty) return new(source, PowerMode.Balanced);
            if (mode == Efficiency) return new(source, PowerMode.Efficiency);
            if (mode == Performance) return new(source, PowerMode.Performance);
            return new(source, null, "Windows reported an unrecognized power mode.");
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            return new(source, null, Unsupported);
        }
    }

    public string? Apply(PowerSource expectedSource, PowerMode mode)
    {
        if (readOnly) return "Power settings are read-only during diagnostics.";
        if (CurrentSource() != expectedSource) return "The power source changed. Review the mode for the current source.";
        var id = mode switch
        {
            PowerMode.Efficiency => Efficiency,
            PowerMode.Balanced => Guid.Empty,
            PowerMode.Performance => Performance,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        try
        {
            var status = expectedSource == PowerSource.PluggedIn
                ? PowerSetUserConfiguredACPowerMode(in id)
                : PowerSetUserConfiguredDCPowerMode(in id);
            return status == 0 ? null : $"Windows did not accept the change. {Error(status)}";
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            return Unsupported;
        }
    }

    public string OpenSettings()
    {
        if (readOnly) return "Windows Settings is unavailable during diagnostics.";
        try
        {
            using var process = Process.Start(new ProcessStartInfo("ms-settings:powersleep") { UseShellExecute = true });
            return "Opened Windows power settings.";
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return "Windows could not open power settings.";
        }
    }

    private static PowerSource? CurrentSource() => BarNative.GetSystemPowerStatus(out var status)
        ? status.Ac switch { 0 => PowerSource.Battery, 1 => PowerSource.PluggedIn, _ => null }
        : null;

    private static string Error(uint status) => new Win32Exception(unchecked((int)status)).Message;

    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerGetUserConfiguredACPowerMode(out Guid mode);
    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerGetUserConfiguredDCPowerMode(out Guid mode);
    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerSetUserConfiguredACPowerMode(in Guid mode);
    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerSetUserConfiguredDCPowerMode(in Guid mode);
}
