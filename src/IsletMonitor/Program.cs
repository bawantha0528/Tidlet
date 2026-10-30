using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Islet.Shell.Island;
using Islet.Windows.Metrics;
using Islet.Windows.Native;
using Islet.Core.Island;

namespace IsletMonitor;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--media-worker") return RunMediaWorker();
        if (args.Length > 0 && args[0] is "--island-probe" or "--scaffold") return RunLegacy();
        var fixture = args.Length > 0 && args[0] == "--fullscreen-fixture";
        if (fixture) args = args[1..];
        if (!IslandLaunchArguments.TryParse(args, out var options, out var report)) return 2;
        if (report is not null)
        {
            try { report = Path.GetFullPath(report); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return 2; }
        }
        if (fixture) return report is not null && options.ExitAfterSeconds is { } duration ? RunFixture(duration, report) : 2;
        return RunNative(options, report);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunMediaWorker()
    {
        var code = 1;
        try
        {
            code = Islet.Windows.Media.MediaWorkerServer.RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput(),
                new Islet.Windows.Media.SystemMediaProvider()).GetAwaiter().GetResult();
        }
        finally
        {
            try
            {
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetName().Name).ToArray();
                var mediaAssemblies = assemblies.Where(n=>n is "Microsoft.Windows.SDK.NET" or "WinRT.Runtime").Select(n=>n!).ToArray();
                var wpfAssemblies = assemblies.Where(n=>n is "PresentationCore" or "PresentationFramework" or "WindowsBase").Select(n=>n!).ToArray();
                // Warm this packet's serializer metadata before recording the worker's exit peak.
                JsonSerializer.Serialize(new Islet.Windows.Media.MediaWorkerExit(0,0,code,mediaAssemblies,wpfAssemblies),Islet.Windows.Media.MediaWorkerJson.Default.MediaWorkerExit);
                process.Refresh();
                Console.Error.Write(JsonSerializer.Serialize(new Islet.Windows.Media.MediaWorkerExit(
                    process.TotalProcessorTime.TotalSeconds,process.PeakWorkingSet64,code,mediaAssemblies,wpfAssemblies),Islet.Windows.Media.MediaWorkerJson.Default.MediaWorkerExit));
            }
            catch (IOException) { }
        }
        return code;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunLegacy()
    {
        var application = new App();
        application.InitializeComponent();
        return application.Run();
    }

    private static int RunFixture(int seconds, string report)
    {
        using var desktop = new NativeIslandDesktop();
        var code = 1;
        string? failure = null;
        try { code = desktop.RunFullscreenFixture(seconds); failure = desktop.Failure; }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ExternalException) { failure = ex.Message; }
        try
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { Mode = "Owned borderless fullscreen test fixture",
                desktop.FixtureForegroundConfirmed, Failure = failure, NaturalExit = true, ExitCode = code },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 1; }
        return code;
    }

    private static int RunNative(NativeIslandOptions options, string? reportPath)
    {
        var exitCode = 1;
        string? failure = null;
        using var desktop = new NativeIslandDesktop();
        var cpu = new SystemCpuProvider(new NativeSystemCpuSource());
        var ram = new SystemRamProvider(new NativeSystemRamSource());
        var net = new SystemNetworkProvider(new NativeSystemNetworkSource());
        var battery = new SystemBatteryProvider(new NativeSystemBatterySource());
        using var disk = new PdhDiskProvider(new NativeDiskRateQuery());
        var gpu = new SystemGpuProvider(new NativeSystemGpuSource());
        var processes = new SystemProcessProvider();
        var preferences = reportPath is null && !options.Minimal ? new Islet.Core.Settings.FileSettingsStore().Load() : null;
        var journalLock = new object();
        using var media = new Islet.Windows.Media.WorkerMediaProvider(
            () => new Islet.Windows.Media.MediaWorkerProcessSession(Environment.ProcessPath!),
            reportPath is null ? null : record => { lock(journalLock) File.AppendAllText(Path.ChangeExtension(reportPath,".workers.jsonl"),JsonSerializer.Serialize(record,Islet.Windows.Media.MediaWorkerJson.Default.MediaWorkerLifetime) + "\n"); });
        using var host = new NativeIslandHost(desktop, cpu, ram, net, battery, disk: disk, gpu: gpu, processes: processes, settings: preferences, media: media);
        var frames = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            if ((options.Exercise || options.ExerciseDashboard) && reportPath is not null)
            {
                var directory = Path.Combine(Path.GetDirectoryName(reportPath)!, "frames");
                Directory.CreateDirectory(directory);
                host.CaptureFrame += key => { desktop.SaveBitmap(Path.Combine(directory, key + ".bmp")); frames.Add(key); };
            }
            host.Start(options);
            exitCode = desktop.Run();
            media.SetVisible(false);
            media.WaitForExitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            failure = desktop.Failure ?? media.JournalFailure;
            if (reportPath is not null && desktop.RenderCount > 0)
                desktop.SaveBitmap(Path.ChangeExtension(reportPath, ".bmp"));
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ExternalException
            or DllNotFoundException or EntryPointNotFoundException or IOException or UnauthorizedAccessException or TimeoutException)
        {
            failure = ex.Message; exitCode = 1;
            if (reportPath is null && options.ExitAfterSeconds is null) { desktop.Dispose(); NativeIslandDesktop.ShowStartupError(failure); }
        }
        if (reportPath is not null)
        {
            try
            {
                File.WriteAllText(reportPath, JsonSerializer.Serialize(new {
                    SchemaVersion = 3, Host = options.Minimal ? "Native Win32 CPU feasibility pill" : "Tidlet Monitor native shell",
                    CompletedUtc = DateTimeOffset.UtcNow, Options = options,
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    LoadedWpfAssemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name)
                        .Where(n => n is "PresentationFramework" or "PresentationCore" or "WindowsBase").ToArray(),
                    host.Capability, host.LastObservation, host.SampleAttempts, host.AvailableSamples,
                    host.VisibilityChecks, host.InvisibleChecks, host.ForegroundEverIsland, host.LastLabel,
                    host.State, host.Style, host.UserHidden, host.Suppressed, host.SuppressionReason,
                    LoadedMediaAssemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name)
                        .Where(n => n is "Microsoft.Windows.SDK.NET" or "WinRT.Runtime").ToArray(),
                    media.WorkerStarts, Workers = media.Workers,
                    host.MediaCapability, host.MediaReads, host.MediaCommands, host.MediaStatus, host.LastCompletedMediaStatus, host.FocusStatus, host.CompactNotch,
                    host.DashboardSamples, host.SelectedPage, disk.Requests, host.PointerChecks, host.AnimationFrames, host.SuppressionTransitions, host.HistoryCount,
                    host.ExerciseCompleted, host.DashboardExerciseCompleted, host.StatesObserved, host.ObservedDpi, Frames = frames.Order().ToArray(),
                    host.Recovery, DisplayCount = desktop.ReadDisplays().Count, desktop.NotificationQueries, desktop.NotificationQueryError, desktop.ForegroundSignals,
                    desktop.RenderCount, TimersRemainingAfterExit = desktop.ActiveTimerCount,
                    PollingStoppedOnExit = host.Stopped, Failure = failure, NaturalExit = true, ExitCode = exitCode
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return 1; }
        }
        return exitCode;
    }
}

