using System.Windows.Threading;
using Islet.Core.Bar;
using Islet.Core.Popups;

namespace Islet.Shell.Bar;

internal sealed class DashboardWidgetReader : IDisposable
{
    internal static readonly BarModule[] CombinedModules = [BarModule.Cpu, BarModule.Gpu, BarModule.Ram, BarModule.Disk];
    internal static readonly BarModule[] Modules = [BarModule.Cpu, BarModule.Gpu, BarModule.Ram, BarModule.Battery, BarModule.Disk, BarModule.Network];
    private readonly BarModule[] _modules;
    private readonly IPopupSource?[] _sources;
    private readonly Func<IPopupSource> _create;
    private readonly Action<PopupSample[]> _present;
    private readonly DispatcherTimer _timer;
    private readonly Task _predecessor;
    private Task<PopupSample[]>? _pending;
    private bool _closed;

    internal int Reads { get; private set; }
    internal int Frames { get; private set; }
    internal Task Cleanup { get; private set; } = Task.CompletedTask;

    internal DashboardWidgetReader(Func<IPopupSource> create, Task predecessor, Action<PopupSample[]> present, BarModule[]? modules = null)
    {
        _create = create;
        _predecessor = predecessor;
        _present = present;
        _modules = modules ?? Modules;
        _sources = new IPopupSource?[_modules.Length];
        _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += Tick;
        Tick(null, EventArgs.Empty);
        _timer.Start();
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (_closed || _pending is not null || !_predecessor.IsCompleted) return;
        Reads++;
        _pending = Task.Run(Read);
        var dispatcher = _timer.Dispatcher;
        _ = _pending.ContinueWith(_ =>
        {
            if (!dispatcher.HasShutdownStarted)
                dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Present));
        }, TaskScheduler.Default);
    }

    private PopupSample[] Read()
    {
        var samples = new PopupSample[_modules.Length];
        for (var index = 0; index < _modules.Length; index++)
        {
            var module = _modules[index];
            try
            {
                var source = _sources[index];
                if (source is null)
                {
                    source = _create();
                    try
                    {
                        if (source is IPopupDetailSource detailed) detailed.SetDetails(PopupDetails.None);
                        source.Probe(module);
                    }
                    catch
                    {
                        source.Dispose();
                        throw;
                    }
                    _sources[index] = source;
                }
                samples[index] = source.Read(module);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not AccessViolationException and not StackOverflowException)
            {
                samples[index] = new(null, null, null, null, null, null, TimeSpan.Zero, null, [], [], [],
                    $"{module} readings are unavailable.");
            }
        }
        return samples;
    }

    private void Present()
    {
        if (_closed || _pending?.IsCompleted != true) return;
        var samples = _pending.GetAwaiter().GetResult();
        _pending = null;
        Frames++;
        _present(samples);
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _timer.Stop();
        _timer.Tick -= Tick;
        if (_pending is { IsCompleted: false } pending)
            Cleanup = pending.ContinueWith(_ => DisposeSources(), TaskScheduler.Default);
        else
        {
            DisposeSources();
            Cleanup = _predecessor;
        }
    }

    private void DisposeSources()
    {
        foreach (var source in _sources) source?.Dispose();
        Array.Clear(_sources);
    }
}
