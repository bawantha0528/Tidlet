using System.Runtime.InteropServices;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;

namespace Islet.Windows.Metrics;

public interface ISystemBatterySource
{
    bool TryRead(out byte acLineStatus, out byte batteryFlag, out byte batteryLifePercent, out int batteryLifeTime, out int error);
}

public sealed class SystemBatteryProvider(ISystemBatterySource source) : IBatteryProvider
{
    private CapabilityProbeResult? _capability;

    public CapabilityProbeResult Probe()
    {
        if (!source.TryRead(out _, out var flag, out var percent, out var lifeTime, out var error))
            return _capability = CapabilityProbeResult.Unsupported("battery.read-failed",
                $"Windows power status read failed (error {error}).");

        var test = BatteryReading.Evaluate(flag, percent, lifeTime, null, null);
        if (test.State == BatteryReadingState.Unavailable)
            return _capability = CapabilityProbeResult.Unsupported("battery.none", test.Explanation);

        if (test.State == BatteryReadingState.Invalid)
            return _capability = CapabilityProbeResult.Unsupported("battery.invalid", test.Explanation);

        return _capability = CapabilityProbeResult.Supported;
    }

    public BatteryReading Sample()
    {
        if (_capability?.Availability != CapabilityAvailability.Supported)
            return BatteryReading.Missing(BatteryReadingState.Unavailable, _capability?.Explanation ?? "Battery has not been probed.");

        if (source.TryRead(out _, out var flag, out var percent, out var lifeTime, out _))
            return BatteryReading.Evaluate(flag, percent, lifeTime, null, null);

        return BatteryReading.Missing(BatteryReadingState.Unavailable, "Battery power status read failed.");
    }

    public void Reset() { }
}

public sealed class NativeSystemBatterySource : ISystemBatterySource
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    public bool TryRead(out byte acLineStatus, out byte batteryFlag, out byte batteryLifePercent, out int batteryLifeTime, out int error)
    {
        if (GetSystemPowerStatus(out var status))
        {
            acLineStatus = status.ACLineStatus;
            batteryFlag = status.BatteryFlag;
            batteryLifePercent = status.BatteryLifePercent;
            batteryLifeTime = status.BatteryLifeTime;
            error = 0;
            return true;
        }

        acLineStatus = 255;
        batteryFlag = 255;
        batteryLifePercent = 255;
        batteryLifeTime = -1;
        error = Marshal.GetLastPInvokeError();
        return false;
    }
}
