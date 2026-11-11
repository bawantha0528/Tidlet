using System.Collections.Concurrent;
using Islet.Core.Activities;
using Islet.Core.Capabilities;

namespace Islet.Windows.Media;

/// <summary>A demand-started MTA actor; all COM references stay on its thread. No child process or SDK projection.</summary>
public sealed class NativeMediaProvider : IMediaProvider, IMediaVisibilityAware, IDisposable
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<INativeMediaSession> _factory;
    private Actor? _actor;
    private Task _lastExit = Task.CompletedTask;
    private bool _visible, _disposed;
    private int _generation, _starts, _active;
    public int ThreadStarts => Volatile.Read(ref _starts);
    public int ActiveThreads => Volatile.Read(ref _active);
    public int WorkerStarts => 0;
    public IReadOnlyList<MediaWorkerLifetime> Workers => Array.Empty<MediaWorkerLifetime>();
    public string? JournalFailure => null;
    public NativeMediaProvider(Func<INativeMediaSession>? factory = null) => _factory = factory ?? (() => new NativeMediaSession());
    public CapabilityProbeResult Probe() => OperatingSystem.IsWindowsVersionAtLeast(10,0,17763)
        ? CapabilityProbeResult.Supported : CapabilityProbeResult.Unsupported("media.windows-version", "Music controls need Windows 10 version 1809 or later.");
    public void SetVisible(bool visible)
    {
        lock (_sync)
        {
            if (_disposed || _visible == visible) return;
            _visible = visible; _generation++;
            if (!visible) Retire();
        }
    }
    private void Retire()
    {
        if (_actor is not { } actor) return;
        _actor = null; actor.Cancel.Cancel(); actor.Queue.CompleteAdding(); _lastExit = actor.Exit.Task;
    }
    public async Task<MediaReading> ReadAsync()
    {
        if (Probe().Availability != CapabilityAvailability.Supported) return new(MediaState.Unavailable, "Media unavailable", Probe().Explanation!);
        return await Send((backend, cancel) => backend.Read(cancel), MediaReading.Waiting,
            ex => new(MediaState.Unavailable, "Media unavailable", ex is UnauthorizedAccessException ? "Windows denied access to media sessions." : "Windows could not read this player. Try reopening it.")).ConfigureAwait(false);
    }
    public Task<MediaCommandResult> ExecuteAsync(MediaCommand command, string token) => Send((backend,cancel) => backend.Execute(command,token,cancel),
        new MediaCommandResult(false,"Player is busy or no longer visible."), _ => new(false,"The player did not accept that control."));
    private async Task<T> Send<T>(Func<INativeMediaSession,CancellationToken,T> operation, T inactive, Func<Exception,T> failed)
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(false)) return inactive;
        try
        {
            Task prior; int generation;
            lock (_sync) { if (_disposed || !_visible) return inactive; prior = _lastExit; generation = _generation; }
            await prior.ConfigureAwait(false);
            Task<T> result;
            lock (_sync)
            {
                if (_disposed || !_visible || generation != _generation) return inactive;
                if (_actor is null)
                {
                    _actor = new Actor(); var actor = _actor;
                    Interlocked.Increment(ref _starts);
                    new Thread(() => Run(actor)) { IsBackground = true, Name = "Tidlet media MTA" }.Start();
                }
                var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                _actor.Queue.Add(new Work((backend,cancel) =>
                {
                    try { completion.TrySetResult(operation(backend,cancel)); }
                    catch (OperationCanceledException) { completion.TrySetResult(inactive); }
                    catch (Exception ex) when (Recoverable(ex)) { completion.TrySetResult(failed(ex)); }
                }, ex => completion.TrySetResult(failed(ex))));
                result = completion.Task;
            }
            var value = await result.ConfigureAwait(false);
            lock (_sync) return _visible && !_disposed && generation == _generation ? value : inactive;
        }
        finally { _gate.Release(); }
    }
    private void Run(Actor actor)
    {
        Interlocked.Increment(ref _active);
        try
        {
            using var backend = _factory();
            foreach (var work in actor.Queue.GetConsumingEnumerable())
                work.Invoke(backend,actor.Cancel.Token);
        }
        catch (Exception ex) when (Recoverable(ex))
        {
            // Stop accepting work before draining, so a failed activation cannot leave a caller waiting forever.
            lock (_sync)
            {
                actor.Queue.CompleteAdding();
                if (ReferenceEquals(_actor,actor)) { _actor = null; _lastExit = actor.Exit.Task; }
            }
            while (actor.Queue.TryTake(out var work)) work.Fail(ex);
        }
        finally
        {
            Interlocked.Decrement(ref _active);
            actor.Exit.TrySetResult();
            // Queue/cancellation handles are disposed only after the actor has left its native apartment.
            actor.Queue.Dispose(); actor.Cancel.Dispose();
        }
    }
    public async Task WaitForExitAsync(TimeSpan timeout)
    {
        Task exit; lock (_sync) exit = _lastExit;
        await exit.WaitAsync(timeout).ConfigureAwait(false);
    }
    public void Dispose() { lock (_sync) { if (_disposed) return; _disposed = true; _visible = false; _generation++; Retire(); } }
    private static bool Recoverable(Exception ex) => ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;
    private sealed record Work(Action<INativeMediaSession,CancellationToken> Invoke, Action<Exception> Fail);
    private sealed class Actor
    {
        public readonly BlockingCollection<Work> Queue = new(1);
        public readonly CancellationTokenSource Cancel = new();
        public readonly TaskCompletionSource Exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public interface INativeMediaSession : IDisposable
{
    MediaReading Read(CancellationToken cancellation);
    MediaCommandResult Execute(MediaCommand command, string token, CancellationToken cancellation);
}