using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Islet.Core.Popups;
using LibreHardwareMonitor.Hardware;

namespace Islet.Windows.Metrics;

/// <summary>Lazy, bounded reader for LibreHardwareMonitor hardware sensors, temperatures, and fans.</summary>
public sealed class LibreHardwareSensorReader : IDisposable
{
    private const int MaxSensorItems = 64;
    private static readonly object Gate = new();
    private static Computer? _computer;
    private static SensorDetails? _lastReading;
    private static long _lastReadAt;
    private static int _readers;
    private bool _attached;
    private bool _disposed;

    public SensorDetails Read()
    {
        lock (Gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var elevated = IsAdministrator();
            if (!_attached)
            {
                _readers++;
                _attached = true;
            }

            if (_lastReading is { } previous && Environment.TickCount64 - _lastReadAt < 1000)
                return previous;

            try
            {
                if (_computer is null)
                {
                    _computer = new Computer
                    {
                        IsCpuEnabled = false,
                        IsGpuEnabled = true,
                        IsMotherboardEnabled = false,
                        IsControllerEnabled = false,
                        IsStorageEnabled = true,
                        IsMemoryEnabled = false,
                        IsNetworkEnabled = false,
                        IsBatteryEnabled = false,
                        IsPsuEnabled = false,
                        IsPowerMonitorEnabled = false
                    };
                    _computer.Open();
                }

                var collected = new List<SensorItem>();
                foreach (var hardware in _computer.Hardware)
                    VisitHardware(hardware, hardware.Name, collected);

                _lastReadAt = Environment.TickCount64;
                return _lastReading = Summarize(collected, elevated);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception
                or ExternalException or InvalidOperationException or NotSupportedException or TypeInitializationException
                or DllNotFoundException or BadImageFormatException or System.Security.SecurityException)
            {
                CloseLocked();
                _lastReadAt = Environment.TickCount64;
                return _lastReading = new(null, null, null, [], elevated,
                    "Hardware sensors unavailable via LibreHardwareMonitor: " + ex.Message);
            }
        }
    }

    public void Stop()
    {
        lock (Gate)
        {
            ReleaseLocked();
        }
    }

    public void Dispose()
    {
        lock (Gate)
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseLocked();
        }
    }

    internal static double? ReadCpuPackageOnly()
    {
        var computer = new Computer { IsCpuEnabled = true };
        try
        {
            computer.Open();
            var sensors = new List<SensorItem>();
            foreach (var cpu in computer.Hardware)
                if (cpu.HardwareType == HardwareType.Cpu)
                    VisitHardware(cpu, cpu.Name, sensors);

            return Summarize(sensors, elevated: true).CpuPackageCelsius;
        }
        finally { computer.Close(); }
    }

    private void ReleaseLocked()
    {
        if (!_attached) return;
        _attached = false;
        if (--_readers == 0) CloseLocked();
    }

    private static void CloseLocked()
    {
        _lastReading = null;
        if (_computer is null) return;
        try { _computer.Close(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception
            or ExternalException or InvalidOperationException or NotSupportedException) { }
        finally { _computer = null; }
    }

    private static void VisitHardware(IHardware hardware, string rootName, List<SensorItem> output)
    {
        try { hardware.Update(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception
            or ExternalException or InvalidOperationException or NotSupportedException) { return; }

        var group = string.IsNullOrWhiteSpace(hardware.Name) ? rootName : hardware.Name;
        foreach (var sensor in hardware.Sensors)
        {
            if (TryMapSensor(group, sensor, out var mapped))
                output.Add(mapped);
        }

        foreach (var sub in hardware.SubHardware)
            VisitHardware(sub, group, output);
    }

    private static bool TryMapSensor(string hardwareName, ISensor sensor, out SensorItem item)
    {
        item = default;
        if (sensor.Value is not float raw || !float.IsFinite(raw))
            return false;

        double value = raw;
        var kind = sensor.SensorType switch
        {
            SensorType.Temperature => (SensorKind?)SensorKind.Temperature,
            SensorType.Fan => SensorKind.Fan,
            SensorType.Power => SensorKind.Power,
            SensorType.Clock => SensorKind.Clock,
            SensorType.Voltage => SensorKind.Voltage,
            SensorType.Control => SensorKind.Control,
            _ => null
        };
        if (kind is not { } resolved || !IsValidValue(resolved, value))
            return false;

        var unit = resolved switch
        {
            SensorKind.Temperature => "°C",
            SensorKind.Fan => "RPM",
            SensorKind.Power => "W",
            SensorKind.Clock => "MHz",
            SensorKind.Voltage => "V",
            SensorKind.Control => "%",
            _ => ""
        };
        item = new(hardwareName, GroupLabel(sensor.Hardware?.HardwareType), sensor.Name ?? resolved.ToString(), resolved, value, unit);
        return true;
    }

    internal static bool IsValidValue(SensorKind kind, double value)
    {
        if (!double.IsFinite(value)) return false;
        return kind switch
        {
            SensorKind.Temperature => value is > 0 and <= 150,
            SensorKind.Fan => value is >= 0 and <= 25000,
            SensorKind.Power => value is > 0 and <= 5000,
            SensorKind.Clock => value is > 0 and <= 20000,
            SensorKind.Voltage => value is > 0 and <= 100,
            SensorKind.Control => value is >= 0 and <= 100,
            _ => false
        };
    }

    public static SensorDetails Summarize(IReadOnlyList<SensorItem> raw, bool elevated)
    {
        var valid = raw.Where(item => IsValidValue(item.Kind, item.Value)).ToArray();
        var items = valid
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Hardware, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxSensorItems)
            .ToArray();

        var cpuPackage = valid
            .Where(item => item.Kind == SensorKind.Temperature && item.Group == "CPU"
                && string.Equals(item.Name, "CPU Package", StringComparison.OrdinalIgnoreCase))
            .Select(item => (double?)item.Value)
            .DefaultIfEmpty()
            .Max();

        var temps = valid.Where(item => item.Kind == SensorKind.Temperature).ToArray();
        double? maxTemp = temps.Length > 0 ? temps.Max(item => item.Value) : null;

        var fans = valid.Where(item => item.Kind == SensorKind.Fan).ToArray();
        int? maxFan = fans.Length > 0 ? (int)Math.Round(fans.Max(item => item.Value)) : null;

        string explanation;
        if (items.Length == 0)
        {
            explanation = elevated
                ? "No supported temperature or fan sensors were reported by LibreHardwareMonitor on this device."
                : "No hardware temperature or fan sensors were exposed under standard user rights. CPU package and motherboard fan readings may need the optional PawnIO hardware helper.";
        }
        else if (!elevated && (cpuPackage is null || fans.Length == 0))
        {
            explanation = "Showing available user-mode sensors via LibreHardwareMonitor. CPU package and motherboard fan readings may need the optional PawnIO hardware helper.";
        }
        else
        {
            explanation = "Live hardware sensors via LibreHardwareMonitor.";
        }

        return new(cpuPackage, maxTemp, maxFan, items, elevated, explanation);
    }

    private static string GroupLabel(HardwareType? type) => type switch
    {
        HardwareType.Cpu => "CPU",
        HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => "GPU",
        HardwareType.Motherboard or HardwareType.SuperIO or HardwareType.EmbeddedController => "Motherboard",
        HardwareType.Cooler => "Cooler",
        HardwareType.Storage => "Storage",
        _ => "Device"
    };

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
