using Islet.Core.Capabilities;

namespace Islet.Core.Metrics;

public sealed record ProcessMetric(
    int Pid,
    string Name,
    DateTime StartTime,
    double? CpuPercent,
    ulong WorkingSetBytes)
{
    public string FormattedWorkingSet
    {
        get
        {
            var mb = WorkingSetBytes / (1024.0 * 1024.0);
            return mb < 1024 ? $"{mb:F0} MB" : $"{(mb / 1024.0):F1} GB";
        }
    }
}

public enum ProcessTerminationStatus
{
    Success,
    BlockedUnsafe,
    ProcessExited,
    AccessDenied,
    PidReused,
    Cancelled
}

public sealed record ProcessTerminationResult(bool Succeeded, string Message, ProcessTerminationStatus Status);

public interface IProcessExecutor
{
    Task<ProcessTerminationResult> TerminateAsync(int pid, DateTime startTime, CancellationToken cancellationToken);
}

public static class ProcessTerminationPolicy
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system",
        "idle",
        "system idle process",
        "smss",
        "csrss",
        "wininit",
        "services",
        "lsass",
        "dwm",
        "svchost",
        "explorer",
        "fontdrvhost",
        "winlogon"
    };

    public static (bool Allowed, string? BlockReason) ValidateTarget(int targetPid, int currentProcessPid, string? processName)
    {
        if (targetPid <= 4)
            return (false, "Cannot terminate critical system kernel processes (PID <= 4).");

        if (targetPid == currentProcessPid)
            return (false, "Cannot terminate Tidlet's own process.");

        if (!string.IsNullOrWhiteSpace(processName) && ProtectedNames.Contains(processName.TrimEnd(".exe".ToCharArray())))
            return (false, $"Cannot terminate essential Windows process '{processName}'.");

        return (true, null);
    }
}

public interface IProcessProvider
{
    CapabilityProbeResult Probe();
    IReadOnlyList<ProcessMetric> SampleTop(int limit);
    void Reset();
}
