using System.Runtime.InteropServices;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;
using Islet.Windows.Display;
using Islet.Windows.Metrics;

namespace Islet.Windows.Tests;

[TestClass]
public sealed class IslandProbeTests
{
    [TestMethod]
    [DataRow((ushort)0)]
    [DataRow((ushort)2)]
    public void UnknownAndMultipleProcessorGroupsDoNotPublishPartialCpu(ushort groups)
    {
        var source = new Source { ProcessorGroups = groups };
        var provider = new SystemCpuProvider(source);
        Assert.AreEqual(CapabilityAvailability.Unsupported, provider.Probe().Availability);
        Assert.IsNull(provider.Sample().Percent);
        Assert.AreEqual(0, source.Reads);
    }

    [TestMethod]
    public void DeniedReadAndRecoveryResetTheBaseline()
    {
        var source = new Source { Success = false };
        var provider = new SystemCpuProvider(source);
        Assert.AreEqual("cpu.read-failed", provider.Probe().ReasonCode);
        Assert.IsNull(provider.Sample().Percent);
        source.Success = true;
        Assert.AreEqual(CapabilityAvailability.Supported, provider.Probe().Availability);
        source.Times = new(20, 40, 60);
        Assert.AreEqual(80.0, provider.Sample().Percent);
        source.Success = false;
        Assert.AreEqual(CpuReadingState.Unavailable, provider.Sample().State);
        source.Success = true;
        Assert.AreEqual(CpuReadingState.WarmingUp, provider.Sample().State);
    }

    [TestMethod]
    public void InvalidStartupTimesDisableTheCapability()
    {
        var provider = new SystemCpuProvider(new Source { Times = new(50, 10, 30) });
        Assert.AreEqual("cpu.invalid", provider.Probe().ReasonCode);
        Assert.IsNull(provider.Sample().Percent);
    }

    [TestMethod]
    public void NativeLayoutsMatchWindowsAbiAndSignedMonitorCoordinates()
    {
        Assert.AreEqual(8, Marshal.SizeOf<NativeSystemCpuSource.NativeFileTime>());
        Assert.AreEqual(16, Marshal.SizeOf<IslandProbePlatform.Rect>());
        Assert.AreEqual(40, Marshal.SizeOf<IslandProbePlatform.MonitorInfo>());
        Assert.AreEqual(new nint(4), Marshal.OffsetOf<IslandProbePlatform.MonitorInfo>("Monitor"));
        Assert.AreEqual(new nint(20), Marshal.OffsetOf<IslandProbePlatform.MonitorInfo>("Work"));
        var time = new NativeSystemCpuSource.NativeFileTime { Low = 1, High = 1 };
        Assert.AreEqual(4294967297UL, time.Value);
        var rect = new IslandProbePlatform.Rect { Left = -1920, Right = 0 };
        Assert.AreEqual(1920, rect.Right - rect.Left);
    }

    [TestMethod]
    public void DpiConversionHasBoundedValidatedInputs()
    {
        Assert.AreEqual(330, IslandProbePlatform.Pixels(220, 144));
        Assert.AreEqual(8, IslandProbePlatform.Pixels(8, 96));
        Assert.Throws<ArgumentOutOfRangeException>(() => IslandProbePlatform.Pixels(double.NaN, 96));
        Assert.Throws<ArgumentOutOfRangeException>(() => IslandProbePlatform.Pixels(220, 0));
    }

    private sealed class Source : ISystemCpuSource
    {
        public ushort ProcessorGroups { get; init; } = 1;
        public bool Success { get; set; } = true;
        public CpuTimes Times { get; set; } = new(10, 20, 30);
        public int Reads { get; private set; }
        public bool TryRead(out CpuTimes times, out int error)
        { Reads++; times = Times; error = Success ? 0 : 5; return Success; }
    }
}
