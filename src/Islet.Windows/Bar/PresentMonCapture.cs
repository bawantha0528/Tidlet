using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Islet.Core.Bar;

namespace Islet.Windows.Bar;

public sealed class PresentMonCapture(string executable) : IDisposable
{
    private const string Digest = "B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF";
    private readonly object _gate = new();
    private CancellationTokenSource? _capture;
    private Task? _worker;
    private PresentMonFrames? _samples;
    private uint _target;
    private string? _unavailable;

    public FrameReading Read(uint? diagnosticTarget = null)
    {
        var window = BarNative.GetForegroundWindow();
        uint pid = 0;
        if (diagnosticTarget is uint requested)
            pid = requested;
        else if (window != 0 && window != BarNative.GetShellWindow() && window != BarNative.GetDesktopWindow())
            BarNative.GetWindowThreadProcessId(window, out pid);

        lock (_gate)
        {
            if (_unavailable is not null)
                return new(null, _unavailable, true);

            if (diagnosticTarget is null && pid == Environment.ProcessId)
                pid = _target;
            if (pid != _target)
            {
                StopLocked();
                _target = pid;
            }

            if (pid == 0)
                return new(null, "Switch to a windowed game to start measuring displayed frames.");

            if (_worker is null || _worker.IsCompleted)
            {
                _capture?.Dispose();
                _capture = new();
                var token = _capture.Token;
                var samples = new PresentMonFrames();
                _samples = samples;
                _worker = Task.Run(() => Capture(pid, samples, token));
            }

            var interval = _samples?.TakeInterval();
            return new(interval, interval.HasValue
                ? $"Displayed frames for process {pid}. Frame time is the mean for the most active swap chain since the last strip update."
                : $"Waiting for displayed frames from process {pid}.");
        }
    }

    private async Task Capture(uint pid, PresentMonFrames samples, CancellationToken token)
    {
        try
        {
            if (!File.Exists(executable))
            {
                Fail("PresentMon 2.6.0 x64 is missing. Put the verified standalone executable beside Tidlet.", token);
                return;
            }

            // Keep the verified executable open without write/delete sharing while starting it.
            using var binary = File.OpenRead(executable);
            if (Convert.ToHexString(SHA256.HashData(binary)) != Digest)
            {
                Fail("The PresentMon executable does not match the official release hash.", token);
                return;
            }

            token.ThrowIfCancellationRequested();
            var session = "Tidlet-" + Guid.NewGuid().ToString("N");
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[]
            {
                "--process_id", pid.ToString(CultureInfo.InvariantCulture), "--output_stdout", "--no_console_stats",
                "--v2_metrics", "--exclude_dropped", "--no_track_input", "--no_track_gpu",
                "--timed", "600", "--terminate_after_timed", "--terminate_on_proc_exit",
                "--session_name", session
            })
                start.ArgumentList.Add(arg);

            using var process = Process.Start(start) ?? throw new InvalidOperationException("PresentMon did not start.");
            using var stop = token.Register(() =>
            {
                try { process.Kill(); }
                catch (InvalidOperationException) { }
                catch (Win32Exception) { }
            });
            var errors = process.StandardError.ReadToEndAsync();
            while (await process.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                lock (_gate)
                    if (!token.IsCancellationRequested)
                        samples.Add(line);
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
            var error = await errors.ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
                await EndOwnedSession(session).ConfigureAwait(false);
                return;
            }
            if (error.Contains("access denied", StringComparison.OrdinalIgnoreCase))
                Fail("Windows denied frame capture. PresentMon needs Performance Log Users access or administrator privileges.", token);
            else if (process.ExitCode != 0)
                Fail($"PresentMon could not capture frames (exit {process.ExitCode}). Toggle Gaming Mode to retry.", token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            Fail("PresentMon could not start or read its frame stream.", token);
        }
    }

    private async Task EndOwnedSession(string session)
    {
        // ETW sessions may survive their controller. Only stop the random name we created.
        var stop = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        stop.ArgumentList.Add("--session_name");
        stop.ArgumentList.Add(session);
        stop.ArgumentList.Add("--terminate_existing_session");
        using var process = Process.Start(stop);
        if (process is null)
            return;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { process.Kill(); }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
        }
    }

    private void Fail(string reason, CancellationToken token)
    {
        lock (_gate)
            if (!token.IsCancellationRequested)
                _unavailable = reason;
    }

    private void StopLocked()
    {
        _capture?.Cancel();
        _samples = null;
        _target = 0;
    }

    public void Stop()
    {
        lock (_gate)
            StopLocked();
    }

    public void Reset()
    {
        lock (_gate)
        {
            StopLocked();
            _unavailable = null;
        }
    }

    public bool Drain(TimeSpan timeout) => _worker?.Wait(timeout) ?? true;

    public void Dispose()
    {
        Stop();
        _capture?.Dispose();
    }
}
