using System.Diagnostics;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;

namespace Islet.Windows.Metrics;

public sealed class SystemProcessProvider : IProcessProvider
{
    private CapabilityProbeResult? _capability;

    public CapabilityProbeResult Probe()
    {
        return _capability = CapabilityProbeResult.Supported;
    }

    public IReadOnlyList<ProcessMetric> SampleTop(int limit)
    {
        if (limit <= 0) return Array.Empty<ProcessMetric>();

        var list = new List<ProcessMetric>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var name = p.ProcessName;
                var pid = p.Id;
                var ws = (ulong)p.WorkingSet64;
                DateTime start;
                try { start = p.StartTime; }
                catch { start = DateTime.MinValue; }

                list.Add(new(pid, name, start, null, ws));
            }
            catch
            {
                // Protected or exited process - continue safely
            }
            finally
            {
                p.Dispose();
            }
        }

        return list.OrderByDescending(x => x.WorkingSetBytes).Take(limit).ToList();
    }

    public void Reset() { }
}

public sealed class WindowsProcessExecutor(Func<int, bool>? killAction = null) : IProcessExecutor
{
    public Task<ProcessTerminationResult> TerminateAsync(int pid, DateTime startTime, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (allowed, reason) = ProcessTerminationPolicy.ValidateTarget(pid, Process.GetCurrentProcess().Id, null);
        if (!allowed)
            return Task.FromResult(new ProcessTerminationResult(false, reason!, ProcessTerminationStatus.BlockedUnsafe));

        try
        {
            using var target = Process.GetProcessById(pid);
            if (target.HasExited)
                return Task.FromResult(new ProcessTerminationResult(false, "Process has already exited.", ProcessTerminationStatus.ProcessExited));

            if (startTime != DateTime.MinValue)
            {
                try
                {
                    if (target.StartTime != startTime)
                        return Task.FromResult(new ProcessTerminationResult(false, "Process ID was reused by another process.", ProcessTerminationStatus.PidReused));
                }
                catch
                {
                    // If start time cannot be read due to permissions, proceed with caution
                }
            }

            var (_, nameReason) = ProcessTerminationPolicy.ValidateTarget(pid, Process.GetCurrentProcess().Id, target.ProcessName);
            if (!allowed)
                return Task.FromResult(new ProcessTerminationResult(false, nameReason!, ProcessTerminationStatus.BlockedUnsafe));

            if (killAction is not null)
            {
                var success = killAction(pid);
                return Task.FromResult(new ProcessTerminationResult(success, success ? "Process terminated." : "Kill failed.",
                    success ? ProcessTerminationStatus.Success : ProcessTerminationStatus.AccessDenied));
            }

            target.Kill();
            return Task.FromResult(new ProcessTerminationResult(true, $"Process {target.ProcessName} (PID {pid}) terminated successfully.", ProcessTerminationStatus.Success));
        }
        catch (ArgumentException)
        {
            return Task.FromResult(new ProcessTerminationResult(false, "Process not found or already exited.", ProcessTerminationStatus.ProcessExited));
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return Task.FromResult(new ProcessTerminationResult(false, $"Access denied (error {ex.NativeErrorCode}).", ProcessTerminationStatus.AccessDenied));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ProcessTerminationResult(false, ex.Message, ProcessTerminationStatus.AccessDenied));
        }
    }
}
