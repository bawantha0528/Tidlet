using System.Runtime.InteropServices;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;

namespace Islet.Windows.Metrics;

public interface ISystemCpuSource
{
    ushort ProcessorGroups { get; }
    bool TryRead(out CpuTimes times, out int error);
}

public sealed class SystemCpuProvider(ISystemCpuSource source) : ICpuProvider
{
    private readonly CpuDelta _delta = new();
    private CapabilityProbeResult? _capability;

    public CapabilityProbeResult Probe()
    {
        _delta.Reset();
        var groups = source.ProcessorGroups;
        if (groups != 1)
            return _capability = CapabilityProbeResult.Unsupported("cpu.processor-groups",
                groups == 0 ? "Windows could not identify processor groups."
                    : "Whole-PC CPU usage is unavailable on this processor-group configuration.");
        if (!source.TryRead(out var times, out var error))
            return _capability = CapabilityProbeResult.Unsupported("cpu.read-failed",
                $"Windows could not read CPU times (error {error}).");
        if (_delta.Update(times).State == CpuReadingState.Invalid)
            return _capability = CapabilityProbeResult.Unsupported("cpu.invalid", "Windows returned inconsistent CPU times.");
        return _capability = CapabilityProbeResult.Supported;
    }

    public CpuReading Sample()
    {
        if (_capability?.Availability != CapabilityAvailability.Supported)
            return CpuReading.Missing(CpuReadingState.Unavailable, _capability?.Explanation ?? "CPU has not been checked.");
        if (source.TryRead(out var times, out var error)) return _delta.Update(times);
        _delta.Reset();
        return CpuReading.Missing(CpuReadingState.Unavailable, $"CPU reading unavailable (Windows error {error}).");
    }

    public void Reset() => _delta.Reset();
}

public sealed class NativeSystemCpuSource : ISystemCpuSource
{
    public ushort ProcessorGroups => GetActiveProcessorGroupCount();

    public bool TryRead(out CpuTimes times, out int error)
    {
        var success = GetSystemTimes(out var idle, out var kernel, out var user);
        error = success ? 0 : Marshal.GetLastPInvokeError();
        times = new(idle.Value, kernel.Value, user.Value);
        return success;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeFileTime
    {
        public uint Low;
        public uint High;
        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern ushort GetActiveProcessorGroupCount();

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out NativeFileTime idle, out NativeFileTime kernel, out NativeFileTime user);
}
