using System.Runtime.CompilerServices;
using Islet.Core.Activities;
using Islet.Core.Capabilities;

namespace Islet.Windows.Media;

/// <summary>Single-flight, exception-safe boundary. Backend creation is deferred until a visible media card needs it.</summary>
public sealed class SystemMediaProvider : IMediaProvider
{
    private readonly Func<IMediaSessionBackend> _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IMediaSessionBackend? _backend;
    private MediaReading _latest = MediaReading.Waiting;
    public SystemMediaProvider(Func<IMediaSessionBackend>? factory = null) => _factory = factory ?? CreateBackend;
    public CapabilityProbeResult Probe() => OperatingSystem.IsWindowsVersionAtLeast(10,0,17763)
        ? CapabilityProbeResult.Supported : CapabilityProbeResult.Unsupported("media.windows-version", "Music controls need Windows 10 version 1809 or later.");
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IMediaSessionBackend CreateBackend() => OperatingSystem.IsWindowsVersionAtLeast(10,0,17763)
        ? new WindowsMediaSessionBackend() : throw new PlatformNotSupportedException();
    public async Task<MediaReading> ReadAsync()
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(false)) return _latest;
        try
        {
            if (Probe().Availability != CapabilityAvailability.Supported)
                return _latest = new(MediaState.Unavailable, "Media unavailable", Probe().Explanation!);
            return _latest = await (_backend ??= _factory()).ReadAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (Recoverable(ex))
        { return _latest = new(MediaState.Unavailable, "Media unavailable", ex is UnauthorizedAccessException ? "Windows denied access to media sessions." : "Windows could not read this player. Try reopening it."); }
        finally { _gate.Release(); }
    }
    public async Task<MediaCommandResult> ExecuteAsync(MediaCommand command, string sessionToken)
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(false)) return new(false, "Player is busy. Try again.");
        try
        {
            if (_backend is null || _latest.SessionToken != sessionToken || !_latest.Allows(command))
                return new(false, "Player changed or this control is unavailable.");
            return await _backend.ExecuteAsync(command, sessionToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (Recoverable(ex)) { return new(false, "The player did not accept that control."); }
        finally { _gate.Release(); }
    }
    private static bool Recoverable(Exception ex) => ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;
}
public interface IMediaSessionBackend
{
    Task<MediaReading> ReadAsync();
    Task<MediaCommandResult> ExecuteAsync(MediaCommand command, string sessionToken);
}
