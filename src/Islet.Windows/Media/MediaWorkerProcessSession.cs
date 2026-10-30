using System.Diagnostics;
using System.Text.Json;
using Islet.Core.Activities;

namespace Islet.Windows.Media;

public sealed record MediaWorkerExit(double CpuSeconds, long? PeakWorkingSetBytes, int ExitCode, string[]? MediaAssemblies = null, string[]? WpfAssemblies = null);
public sealed record MediaWorkerLifetime(int ProcessId, DateTimeOffset StartedUtc, DateTimeOffset? ExitedUtc = null,
    double? CpuSeconds = null, long? PeakWorkingSetBytes = null, int? ExitCode = null, MediaWorkerInfo? Info = null, string[]? FinalMediaAssemblies = null, string[]? FinalWpfAssemblies = null)
{
    public long StartTimeUtcTicks => StartedUtc.UtcDateTime.Ticks;
}
public interface IMediaWorkerSession
{
    int ProcessId { get; }
    DateTimeOffset StartedUtc { get; }
    Task<MediaWorkerExit> Exit { get; }
    Task<MediaWorkerResponse> RequestAsync(MediaWorkerRequest request, CancellationToken cancellationToken);
    void CloseInput();
}

/// <summary>Own child only; no Kill, elevation, shell command, named endpoint or background window.</summary>
public sealed class MediaWorkerProcessSession : IMediaWorkerSession
{
    private readonly Process _process;
    private readonly Task<MediaWorkerExit?> _exitPacket;
    private int _closed;
    public int ProcessId { get; }
    public DateTimeOffset StartedUtc { get; }
    public Task<MediaWorkerExit> Exit { get; }
    public MediaWorkerProcessSession(string executable, Action<ProcessStartInfo>? configureStart = null)
    {
        var start = new ProcessStartInfo(executable) {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("--media-worker");
        configureStart?.Invoke(start);
        _process = Process.Start(start) ?? throw new IOException("Music worker could not start.");
        ProcessId = _process.Id; StartedUtc = _process.StartTime.ToUniversalTime();
        _exitPacket = ReadExitPacketAsync(_process.StandardError.BaseStream);
        Exit = WaitForExitAsync();
    }
    public async Task<MediaWorkerResponse> RequestAsync(MediaWorkerRequest request, CancellationToken cancellationToken)
    {
        await MediaWorkerProtocol.WriteAsync(_process.StandardInput.BaseStream, request, cancellationToken).ConfigureAwait(false);
        var response = await MediaWorkerProtocol.ReadAsync<MediaWorkerResponse>(_process.StandardOutput.BaseStream, cancellationToken)
            .ConfigureAwait(false) ?? throw new EndOfStreamException("Music worker closed its connection.");
        if (response.Version != 1 || response.Info.ProcessId != ProcessId) throw new InvalidDataException("Music worker protocol mismatch.");
        return response;
    }
    public void CloseInput()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        try { _process.StandardInput.Close(); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException) { }
    }
    private static async Task<MediaWorkerExit?> ReadExitPacketAsync(Stream stream)
    {
        // Drain concurrently, even if a runtime failure writes stderr. Never retain unbounded output.
        var retained = new MemoryStream(); var bytes = new byte[1024];
        try
        {
            int count;
            while ((count = await stream.ReadAsync(bytes).ConfigureAwait(false)) != 0)
                if (retained.Length + count <= MediaWorkerProtocol.MaxPacketBytes) retained.Write(bytes, 0, count);
            return JsonSerializer.Deserialize(retained.ToArray(),MediaWorkerJson.Default.MediaWorkerExit);
        }
        catch (Exception ex) when (ex is IOException or JsonException or ObjectDisposedException) { return null; }
        finally { retained.Dispose(); }
    }
    private async Task<MediaWorkerExit> WaitForExitAsync()
    {
        try
        {
            await _process.WaitForExitAsync().ConfigureAwait(false);
            var packet = await _exitPacket.ConfigureAwait(false);
            var cpu = _process.TotalProcessorTime.TotalSeconds;
            return new(Math.Max(cpu, packet?.CpuSeconds ?? 0), packet?.PeakWorkingSetBytes, _process.ExitCode,packet?.MediaAssemblies,packet?.WpfAssemblies);
        }
        finally { _process.Dispose(); }
    }
}
