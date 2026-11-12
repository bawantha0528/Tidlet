using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace Islet.Windows.Metrics;

/// <summary>One explicitly requested sample. The worker receives no filenames or commands.</summary>
public sealed class CpuTemperatureHelper
{
    public const string Argument = "--read-cpu-package-temperature";
    private const int ReadingBase = 10_000;
    private const int PermissionRequired = 10;
    private const int SensorUnavailable = 11;
    private const int ReadFailed = 12;
    private const int TimedOut = 13;
    private int _tenthsCelsius;
    private long _sampledAtUtcTicks;
    private int _active;
    private long _nextAutomaticCheck;
    private readonly Lazy<bool> _automatic = new(IsElevated);
    private string? _lastStatus;
    public string? LastStatus => Volatile.Read(ref _lastStatus);

    public DateTimeOffset? SampledAt=>Volatile.Read(ref _sampledAtUtcTicks) is >0 and var ticks?new DateTimeOffset(ticks,TimeSpan.Zero):null;

    public double? LastCelsius
    {
        get
        {
            var reading = Volatile.Read(ref _tenthsCelsius);
            return reading > 0 ? reading / 10.0 : null;
        }
    }

    public static int RunWorker()
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            return PermissionRequired;

        using var watchdog = new Timer(_ => Environment.Exit(TimedOut), null, TimeSpan.FromSeconds(12), Timeout.InfiniteTimeSpan);
        try
        {
            return Encode(LibreHardwareSensorReader.ReadCpuPackageOnly());
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
        {
            return ReadFailed;
        }
    }

    internal static int Encode(double? celsius)
    {
        if (celsius is not (> 0 and <= 150)) return SensorUnavailable;
        return ReadingBase + Math.Clamp((int)Math.Round(celsius.Value * 10), 1, 1500);
    }

    internal static double? Decode(int code) => code is > ReadingBase and <= ReadingBase + 1500
        ? (code - ReadingBase) / 10.0 : null;

    public (double? Celsius, bool Automatic, bool Measuring, DateTimeOffset? SampledAt) ReadForView()
    {
        var automatic = _automatic.Value;
        if (automatic && Volatile.Read(ref _active) == 0
            && TryScheduleAutomatic(ref _nextAutomaticCheck, Environment.TickCount64))
            _ = ReadOnceAsync(requestElevation: false);

        return (LastCelsius, automatic, Volatile.Read(ref _active) != 0, SampledAt);
    }

    internal static bool TryScheduleAutomatic(ref long next, long now)
    {
        var due = Volatile.Read(ref next);
        return now >= due && Interlocked.CompareExchange(ref next, now + 15_000, due) == due;
    }

    public Task<string> ReadOnceAsync() => ReadOnceAsync(requestElevation: !_automatic.Value);

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private async Task<string> ReadOnceAsync(bool requestElevation)
    {
        if (Interlocked.Exchange(ref _active, 1) != 0)
            return "A CPU temperature check is already running.";

        try
        {
            var status = await Task.Run(() => ReadWorkerAsync(requestElevation)).ConfigureAwait(false);
            Volatile.Write(ref _lastStatus, status);
            return status;
        }
        finally { Volatile.Write(ref _active, 0); }
    }

    private async Task<string> ReadWorkerAsync(bool requestElevation)
    {
        var path = Environment.ProcessPath;
        if (path is null || !string.Equals(Path.GetFileName(path), "IsletMonitor.exe", StringComparison.OrdinalIgnoreCase))
            return "Open Tidlet from IsletMonitor.exe to read CPU temperature.";

        try
        {
            var start = new ProcessStartInfo(path)
            {
                UseShellExecute = requestElevation,
                Verb = requestElevation ? "runas" : "",
                CreateNoWindow = !requestElevation
            };
            start.ArgumentList.Add(Argument);
            using var worker = Process.Start(start);
            if (worker is null) return "Windows did not start the CPU temperature helper.";

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(16));
            await worker.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            if (Decode(worker.ExitCode).HasValue)
            {
                Volatile.Write(ref _sampledAtUtcTicks, DateTime.UtcNow.Ticks);
                Volatile.Write(ref _tenthsCelsius, worker.ExitCode - ReadingBase);
                return "CPU package temperature checked. The CPU popup and page show the last sample.";
            }

            if (!requestElevation) Volatile.Write(ref _tenthsCelsius, 0);
            return worker.ExitCode switch
            {
                SensorUnavailable => "No CPU package reading. Check that PawnIO is installed and that your CPU is supported.",
                PermissionRequired => "Windows did not grant administrator access to the helper.",
                TimedOut => "The CPU temperature helper timed out.",
                _ => "The CPU temperature helper could not read the sensor."
            };
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return "CPU temperature check canceled.";
        }
        catch (OperationCanceledException)
        {
            return "The CPU temperature helper timed out.";
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return "Windows could not start or read the CPU temperature helper.";
        }
    }
}
