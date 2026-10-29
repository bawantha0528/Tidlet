using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Islet.Shell;
using Islet.Windows;

namespace IsletMonitor;

public partial class App : Application
{
    private DispatcherTimer? _exitTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!WindowsPlatform.IsAvailable)
        {
            Shutdown(1);
            return;
        }

        int? exitAfterSeconds = null;
        if (e.Args.Length > 0)
        {
            if (e.Args.Length != 2 || e.Args[0] != "--exit-after-seconds"
                || !int.TryParse(e.Args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                || seconds < 1 || seconds > 3600)
            {
                Shutdown(2);
                return;
            }

            exitAfterSeconds = seconds;
        }

        MainWindow = new ScaffoldWindow();
        MainWindow.Show();

        // An explicitly requested, naturally exiting harness host; never kills a process.
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
}
