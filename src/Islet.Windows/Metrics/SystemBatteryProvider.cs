using System.Runtime.InteropServices;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;
using Microsoft.Win32.SafeHandles;

namespace Islet.Windows.Metrics;

public interface ISystemBatterySource
{
    bool TryRead(out byte acLineStatus, out byte batteryFlag, out byte batteryLifePercent, out int batteryLifeTime, out int error);
    bool TryReadCapacity(out uint designedCapacity, out uint fullChargedCapacity, out uint cycleCount)
    {
        designedCapacity = fullChargedCapacity = cycleCount = 0;
        return false;
    }
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
        {
            double? health = null;
            int? cycles = null;
            uint? designedMWh = null;
            uint? fullMWh = null;

            if (source.TryReadCapacity(out var designed, out var full, out var cycleCount))
            {
                if (designed > 0 && full > 0 && designed != uint.MaxValue && full != uint.MaxValue && (ulong)full <= (ulong)designed*2)
                {
                    health = Math.Clamp((double)full / designed * 100.0, 0, 100);
                    designedMWh = designed;
                    fullMWh = full;
                }
                if (cycleCount > 0 && cycleCount < 100_000)
                    cycles = (int)cycleCount;
            }

            return BatteryReading.Evaluate(flag, percent, lifeTime, null, health, cycles, designedMWh, fullMWh);
        }

        return BatteryReading.Missing(BatteryReadingState.Unavailable, "Battery power status read failed.");
    }

    public void Reset() { }
}

public sealed class NativeSystemBatterySource : ISystemBatterySource
{
    private uint _cachedDesigned, _cachedFull, _cachedCycles;
    private long _lastCapacityTicks = -1;

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

    [StructLayout(LayoutKind.Sequential)]
    private struct BATTERY_QUERY_INFORMATION
    {
        public uint BatteryTag;
        public int InformationLevel;
        public int AtRate;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct BATTERY_INFORMATION
    {
        public uint Capabilities;
        public byte Technology;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Reserved;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Chemistry;
        public uint DesignedCapacity;
        public uint FullChargedCapacity;
        public uint DefaultAlert1;
        public uint DefaultAlert2;
        public uint CriticalBias;
        public uint CycleCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public uint cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        public uint cbSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DevicePath;
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, ref SP_DEVICE_INTERFACE_DETAIL_DATA DeviceInterfaceDetailData, uint DeviceInterfaceDetailDataSize, out uint RequiredSize, IntPtr DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, ref uint lpInBuffer, uint nInBufferSize, out uint lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, ref BATTERY_QUERY_INFORMATION lpInBuffer, uint nInBufferSize, out BATTERY_INFORMATION lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

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

    public bool TryReadCapacity(out uint designedCapacity, out uint fullChargedCapacity, out uint cycleCount)
    {
        var now = Environment.TickCount64;
        if (_lastCapacityTicks > 0 && now - _lastCapacityTicks < 30_000 && _cachedDesigned > 0)
        {
            designedCapacity = _cachedDesigned;
            fullChargedCapacity = _cachedFull;
            cycleCount = _cachedCycles;
            return true;
        }

        try
        {
            var guid = new Guid("72631e54-78a4-11d0-bcf7-00aa00b7b32a");
            var devInfo = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, 0x12);
            if (devInfo == (IntPtr)(-1))
            {
                designedCapacity = fullChargedCapacity = cycleCount = 0;
                return false;
            }

            try
            {
                var ifData = new SP_DEVICE_INTERFACE_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
                if (!SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref guid, 0, ref ifData))
                {
                    designedCapacity = fullChargedCapacity = cycleCount = 0;
                    return false;
                }

                var detail = new SP_DEVICE_INTERFACE_DETAIL_DATA { cbSize = (uint)(IntPtr.Size == 8 ? 8 : 4 + Marshal.SystemDefaultCharSize) };
                if (!SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, ref detail, (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DETAIL_DATA>(), out _, IntPtr.Zero))
                {
                    designedCapacity = fullChargedCapacity = cycleCount = 0;
                    return false;
                }

                using var handle = CreateFile(detail.DevicePath, 0x80000000 | 0x40000000, 1 | 2, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (handle.IsInvalid)
                {
                    designedCapacity = fullChargedCapacity = cycleCount = 0;
                    return false;
                }

                uint wait = 0, tag = 0, returned = 0;
                if (!DeviceIoControl(handle, 0x294040, ref wait, sizeof(uint), out tag, sizeof(uint), out returned, IntPtr.Zero))
                {
                    designedCapacity = fullChargedCapacity = cycleCount = 0;
                    return false;
                }

                var query = new BATTERY_QUERY_INFORMATION { BatteryTag = tag, InformationLevel = 0 };
                var info = new BATTERY_INFORMATION();
                if (!DeviceIoControl(handle, 0x294044, ref query, (uint)Marshal.SizeOf<BATTERY_QUERY_INFORMATION>(), out info, (uint)Marshal.SizeOf<BATTERY_INFORMATION>(), out returned, IntPtr.Zero))
                {
                    designedCapacity = fullChargedCapacity = cycleCount = 0;
                    return false;
                }

                if ((info.Capabilities&0x40000000)==0 && info.DesignedCapacity>0 && info.DesignedCapacity!=uint.MaxValue && info.FullChargedCapacity>0 && info.FullChargedCapacity!=uint.MaxValue)
                {
                    _cachedDesigned = designedCapacity = info.DesignedCapacity;
                    _cachedFull = fullChargedCapacity = info.FullChargedCapacity;
                    _cachedCycles = cycleCount = info.CycleCount;
                    _lastCapacityTicks = now;
                    return true;
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devInfo);
            }
        }
        catch { }

        designedCapacity = fullChargedCapacity = cycleCount = 0;
        return false;
    }
}
