using Islet.Core.Popups;

namespace Islet.Windows.Metrics;

/// <summary>Called from the strip dispatcher. Device discovery, reads and close run on a worker.</summary>
public sealed class BarSensorSampler : IDisposable
{
    private readonly LibreHardwareSensorReader _reader = new();
    private Task<SensorDetails>? _read;
    private Task _release = Task.CompletedTask;
    private SensorDetails? _last;

    public int Requests { get; private set; }

    public SensorDetails? Sample()
    {
        if (!_release.IsCompleted)
            return null;

        if (_read?.IsCompleted == true)
        {
            _last = _read.GetAwaiter().GetResult();
            _read = null;
        }

        if (_read is null)
        {
            Requests++;
            _read = Task.Run(_reader.Read);
        }

        return _last;
    }

    public void Stop()
    {
        if (_read is not { } pending)
            return;

        _read = null;
        _last = null;
        _release = Task.Run(async () =>
        {
            try { await pending.ConfigureAwait(false); }
            finally { _reader.Stop(); }
        });
    }

    public bool Drain(TimeSpan timeout) => _release.Wait(timeout);

    public void Dispose()
    {
        Stop();
        if (_release.IsCompleted)
            _reader.Dispose();
    }
}
