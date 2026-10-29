using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Islet.Shell;
using Islet.Shell.Island;
using Islet.Windows;
using Islet.Windows.Display;
using Islet.Windows.Metrics;

namespace IsletMonitor;

public partial class App : Application
{
    private DispatcherTimer? _exitTimer;
    private IslandProbeWindow? _probe;
    private string? _reportPath;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = e.Args;
        if (args.Length > 0 && args[0] == "--scaffold") args = args[1..];
        if (!WindowsPlatform.IsAvailable) { Shutdown(1); return; }
        var probe = args.Length == 5 && args[0] == "--island-probe"
            && args[1] == "--exit-after-seconds" && args[3] == "--probe-report"
            && !string.IsNullOrWhiteSpace(args[4]);
        int? exitAfterSeconds = null;
        if (args.Length > 0)
        {
            var index = probe ? 2 : 1;
            if ((!probe && (args.Length != 2 || args[0] != "--exit-after-seconds"))
                || !int.TryParse(args[index], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                || seconds < 1 || seconds > 3600)
            {
                Shutdown(2);
                return;
            }
            exitAfterSeconds = seconds;
        }

        if (probe)
        {
            try { _reportPath = Path.GetFullPath(args[4]); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            { Shutdown(2); return; }
            _probe = new IslandProbeWindow(new SystemCpuProvider(new NativeSystemCpuSource()), new IslandProbePlatform());
            _probe.ProbeFailed += (_, _) => Dispatcher.BeginInvoke(() => Shutdown(1));
            MainWindow = _probe;
        }
        else MainWindow = new ScaffoldWindow();
        MainWindow.Show();

        // Explicitly requested natural exit; never terminates another process.
        if (exitAfterSeconds is { } duration)
        {
            _exitTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(duration) };
            _exitTimer.Tick += OnExitTimer;
            _exitTimer.Start();
        }
    }

    private void OnExitTimer(object? sender, EventArgs e)
    {
        _exitTimer!.Stop();
        _exitTimer.Tick -= OnExitTimer;
        _exitTimer = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _exitTimer?.Stop();
        if (_probe is not null && _reportPath is not null)
        {
            try
            {
                File.WriteAllText(_reportPath, JsonSerializer.Serialize(new
                {
                    SchemaVersion = 1,
                    Host = "M1 minimal transparent WPF feasibility probe",
                    CompletedUtc = DateTimeOffset.UtcNow,
                    ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    _probe.Capability,
                    _probe.LastObservation,
                    _probe.SampleAttempts,
                    _probe.AvailableSamples,
                    _probe.VisibilityChecks,
                    _probe.InvisibleChecks,
                    _probe.ForegroundEverIsland,
                    _probe.RenderCount,
                    _probe.LastLabel,
                    _probe.Failure,
                    PollingStoppedOnExit = !_probe.Polling,
                    NaturalExit = true,
                    ExitCode = e.ApplicationExitCode
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { e.ApplicationExitCode = 1; }
        }
        base.OnExit(e);
    }
}
