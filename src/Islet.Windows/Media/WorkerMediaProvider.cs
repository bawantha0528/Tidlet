using Islet.Core.Activities;
using Islet.Core.Capabilities;

namespace Islet.Windows.Media;

/// <summary>Visibility-scoped worker client. WinRT types must never be instantiated in this process.</summary>
public sealed class WorkerMediaProvider : IMediaProvider, IMediaVisibilityAware, IDisposable
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<IMediaWorkerSession> _start;
    private readonly Action<MediaWorkerLifetime>? _record;
    private readonly List<MediaWorkerLifetime> _workers = new();
    private readonly List<Task> _exits = new();
    private IMediaWorkerSession? _session;
    private CancellationTokenSource? _requestLifetime;
    private MediaReading _latest = MediaReading.Waiting;
    private bool _visible, _disposed;
    private int _generation, _starts;
    private long _retryAfter;
    public string? JournalFailure { get; private set; }
    private void Record(MediaWorkerLifetime lifetime)
    {
        try { _record?.Invoke(lifetime); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { JournalFailure = ex.Message; }
    }
    public int WorkerStarts => Volatile.Read(ref _starts);
    public IReadOnlyList<MediaWorkerLifetime> Workers { get { lock (_sync) return _workers.ToArray(); } }
    public WorkerMediaProvider(Func<IMediaWorkerSession> start, Action<MediaWorkerLifetime>? record = null)
    { _start = start; _record = record; }
    public CapabilityProbeResult Probe() => OperatingSystem.IsWindowsVersionAtLeast(10,0,17763)
        ? CapabilityProbeResult.Supported : CapabilityProbeResult.Unsupported("media.windows-version", "Music controls need Windows 10 version 1809 or later.");
    public void SetVisible(bool visible)
    {
        lock (_sync)
        {
            if (_disposed || _visible == visible) return;
            _visible = visible; _generation++;
            _latest = MediaReading.Waiting;
            if (!visible) RetireLocked();
        }
    }
    private void RetireLocked()
    {
        _requestLifetime?.Cancel();
        _session?.CloseInput();
    }
    private IMediaWorkerSession? GetSessionLocked()
    {
        if (!_visible || _disposed) return null;
        if (_session is not null) return _requestLifetime!.IsCancellationRequested ? null : _session;
        if (Environment.TickCount64 < _retryAfter) return null;
        var session = _start();
        _session = session; _requestLifetime = new();
        _starts++;
        var lifetime = new MediaWorkerLifetime(session.ProcessId, session.StartedUtc);
        if (_workers.Count == 64) _workers.RemoveAt(0);
        _workers.Add(lifetime); Record(lifetime);
        _exits.RemoveAll(t => t.IsCompleted);
        _exits.Add(ObserveExitAsync(session));
        return session;
    }
    private async Task ObserveExitAsync(IMediaWorkerSession session)
    {
        // Ensure a synchronously completed fake/failed process cannot mutate construction state inline.
        await Task.Yield();
        MediaWorkerExit result;
        try { result = await session.Exit.ConfigureAwait(false); }
        catch { result = new(0, null, -1); }
        lock (_sync)
        {
            var index = _workers.FindIndex(w => w.ProcessId == session.ProcessId && w.StartedUtc == session.StartedUtc);
            if (index >= 0)
            {
                var lifetime = _workers[index] with { ExitedUtc = DateTimeOffset.UtcNow, CpuSeconds = result.CpuSeconds,
                    PeakWorkingSetBytes = result.PeakWorkingSetBytes, ExitCode = result.ExitCode,
                    FinalMediaAssemblies = result.MediaAssemblies, FinalWpfAssemblies = result.WpfAssemblies };
                _workers[index] = lifetime; Record(lifetime);
            }
            if (ReferenceEquals(_session,session))
            {
                var expected = _requestLifetime?.IsCancellationRequested == true;
                _session = null; _requestLifetime?.Dispose(); _requestLifetime = null;
                if (_visible && !expected) { _latest = Missing("Music worker stopped. Reopen the media view to retry."); _retryAfter = Environment.TickCount64 + 5000; }
            }
        }
    }
    private static MediaReading Missing(string reason) => new(MediaState.Unavailable,"Media unavailable",reason);
    private async Task<MediaWorkerResponse?> RequestAsync(MediaWorkerRequest request)
    {
        IMediaWorkerSession? session; int generation; CancellationToken token;
        lock (_sync)
        {
            session = GetSessionLocked(); generation = _generation;
            if (session is null) return null;
            token = _requestLifetime!.Token;
        }
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            var response = await session.RequestAsync(request,deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
            lock (_sync)
            {
                if (!_visible || _generation != generation || !ReferenceEquals(_session,session)) return null;
                var index = _workers.FindIndex(w => w.ProcessId == session.ProcessId && w.StartedUtc == session.StartedUtc);
                if (index >= 0) _workers[index] = _workers[index] with { Info = response.Info };
                return response;
            }
        }
        catch
        {
            lock (_sync)
            {
                if (ReferenceEquals(_session,session)) { _retryAfter = Environment.TickCount64 + 5000; RetireLocked(); }
            }
            throw;
        }
    }
    public async Task<MediaReading> ReadAsync()
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(false)) { lock(_sync) return _latest; }
        try
        {
            if (Probe().Availability != CapabilityAvailability.Supported) return Missing(Probe().Explanation!);
            var response = await RequestAsync(new()).ConfigureAwait(false);
            lock (_sync)
            {
                if (!_visible) return MediaReading.Waiting;
                return _latest = response?.Reading ?? Missing("Music is starting or stopping. Please wait.");
            }
        }
        catch (Exception ex) when (Recoverable(ex))
        { lock(_sync) return _latest = !_visible ? MediaReading.Waiting : Missing("Music worker is unavailable. Reopen the media view to retry."); }
        finally { _gate.Release(); }
    }
    public async Task<MediaCommandResult> ExecuteAsync(MediaCommand command,string sessionToken)
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(false)) return new(false,"Player is busy. Try again.");
        try
        {
            lock (_sync) if (!_visible || !_latest.Allows(command) || _latest.SessionToken != sessionToken)
                return new(false,"Player changed or this control is unavailable.");
            var response = await RequestAsync(new(Command:command,SessionToken:sessionToken)).ConfigureAwait(false);
            return response?.Result ?? new(false,"The media view closed or the player changed.");
        }
        catch (Exception ex) when (Recoverable(ex)) { return new(false,"The player did not accept that control."); }
        finally { _gate.Release(); }
    }
    public async Task WaitForExitAsync(TimeSpan timeout)
    {
        Task[] exits; lock(_sync) exits = _exits.ToArray();
        await Task.WhenAll(exits).WaitAsync(timeout).ConfigureAwait(false);
    }
    public void Dispose()
    {
        lock (_sync) { if(_disposed)return; _visible=false; _disposed=true; _generation++; _latest=MediaReading.Waiting; RetireLocked(); }
    }
    private static bool Recoverable(Exception ex) => ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;
}
