using System.Runtime.InteropServices;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;

namespace Islet.Windows.Metrics;

public interface ISystemRamSource
{
    bool TryRead(out ulong totalBytes, out ulong availableBytes, out int error);
}

public sealed class SystemRamProvider(ISystemRamSource source) : IRamProvider
{
    private CapabilityProbeResult? _capability;

    public CapabilityProbeResult Probe()
    {
        if (!source.TryRead(out var total, out var avail, out var error))
            return _capability = CapabilityProbeResult.Unsupported("ram.read-failed",
                $"Windows could not read memory status (error {error}).");

        var test = RamReading.Evaluate(total, avail);
        if (test.State == RamReadingState.Invalid)
            return _capability = CapabilityProbeResult.Unsupported("ram.invalid", test.Explanation);

        return _capability = CapabilityProbeResult.Supported;
    }

    public RamReading Sample()
    {
        if (_capability?.Availability != CapabilityAvailability.Supported)
            return RamReading.Missing(RamReadingState.Unavailable, _capability?.Explanation ?? "RAM has not been probed.");

        if (source.TryRead(out var total, out var avail, out _))
            return RamReading.Evaluate(total, avail);

        return RamReading.Missing(RamReadingState.Unavailable, "Windows memory status read failed.");
    }

    public void Reset() { }
}

public sealed class NativeSystemRamSource : ISystemRamSource
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public bool TryRead(out ulong totalBytes, out ulong availableBytes, out int error)
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref status))
        {
            totalBytes = status.ullTotalPhys;
            availableBytes = status.ullAvailPhys;
            error = 0;
            return true;
        }

        totalBytes = 0;
        availableBytes = 0;
        error = Marshal.GetLastPInvokeError();
        return false;
    }
}
