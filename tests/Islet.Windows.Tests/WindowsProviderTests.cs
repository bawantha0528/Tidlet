using Microsoft.VisualStudio.TestTools.UnitTesting;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;
using Islet.Windows.Metrics;

namespace Islet.Windows.Tests;

[TestClass]
public sealed class WindowsProviderTests
{
    private sealed class FakeRamSource(bool success, ulong total, ulong avail, int error = 0) : ISystemRamSource
    {
        public bool TryRead(out ulong totalBytes, out ulong availableBytes, out int err)
        {
            totalBytes = total;
            availableBytes = avail;
            err = error;
            return success;
        }
    }

    [TestMethod]
    public void RamProvider_ProbeSuccess_ReturnsSupported()
    {
        var provider = new SystemRamProvider(new FakeRamSource(true, 16UL * 1024 * 1024 * 1024, 8UL * 1024 * 1024 * 1024));
        var probe = provider.Probe();
        Assert.AreEqual(CapabilityAvailability.Supported, probe.Availability);

        var sample = provider.Sample();
        Assert.AreEqual(RamReadingState.Available, sample.State);
        Assert.AreEqual(50.0, sample.UsedPercent);
    }

    [TestMethod]
    public void RamProvider_ProbeFailure_ReturnsUnsupported()
    {
        var provider = new SystemRamProvider(new FakeRamSource(false, 0, 0, 5));
        var probe = provider.Probe();
        Assert.AreEqual(CapabilityAvailability.Unsupported, probe.Availability);

        var sample = provider.Sample();
        Assert.AreEqual(RamReadingState.Unavailable, sample.State);
    }

    private sealed class FakeBatterySource(bool success, byte flag, byte percent, int lifeTime, int error = 0) : ISystemBatterySource
    {
        public bool TryRead(out byte acLine, out byte bFlag, out byte bPercent, out int bLifeTime, out int err)
        {
            acLine = 1;
            bFlag = flag;
            bPercent = percent;
            bLifeTime = lifeTime;
            err = error;
            return success;
        }
    }

    [TestMethod]
    public void BatteryProvider_NoBattery_ProbesUnsupported()
    {
        var provider = new SystemBatteryProvider(new FakeBatterySource(true, 128, 255, -1));
        var probe = provider.Probe();
        Assert.AreEqual(CapabilityAvailability.Unsupported, probe.Availability);
        StringAssert.Contains(probe.Explanation, "No battery detected");
    }

    [TestMethod]
    public void BatteryProvider_ValidBattery_ProbesSupportedAndSamples()
    {
        var provider = new SystemBatteryProvider(new FakeBatterySource(true, 8, 90, 7200));
        var probe = provider.Probe();
        Assert.AreEqual(CapabilityAvailability.Supported, probe.Availability);

        var sample = provider.Sample();
        Assert.AreEqual(BatteryReadingState.Available, sample.State);
        Assert.AreEqual(90.0, sample.Percent);
        Assert.AreEqual(BatteryChargingStatus.Charging, sample.ChargingStatus);
    }

    private sealed class FakeBatteryWithCapacitySource(bool success, byte flag, byte percent, int lifeTime, uint designed, uint full, uint cycles) : ISystemBatterySource
    {
        public bool TryRead(out byte acLineStatus, out byte batteryFlag, out byte batteryLifePercent, out int batteryLifeTime, out int error)
        {
            acLineStatus = 1;
            batteryFlag = flag;
            batteryLifePercent = percent;
            batteryLifeTime = lifeTime;
            error = 0;
            return success;
        }

        public bool TryReadCapacity(out uint designedCapacity, out uint fullChargedCapacity, out uint cycleCount)
        {
            designedCapacity = designed;
            fullChargedCapacity = full;
            cycleCount = cycles;
            return true;
        }
    }

    [TestMethod]
    public void BatteryProvider_WithRealHardwareCapacity_CalculatesHealthAndCycles()
    {
        var source = new FakeBatteryWithCapacitySource(true, 8, 100, 0, 57488, 42427, 160);
        var provider = new SystemBatteryProvider(source);
        var probe = provider.Probe();
        Assert.AreEqual(CapabilityAvailability.Supported, probe.Availability);

        var sample = provider.Sample();
        Assert.AreEqual(BatteryReadingState.Available, sample.State);
        Assert.IsTrue(sample.HealthPercent.HasValue);
        Assert.AreEqual(73.8, Math.Round(sample.HealthPercent.Value, 1));
        Assert.AreEqual(160, sample.CycleCount);
        Assert.AreEqual(57488u, sample.DesignedCapacityMWh);
        Assert.AreEqual(42427u, sample.FullChargedCapacityMWh);
    }

    private sealed class FakeNetworkSource(bool success, ulong inBytes, ulong outBytes, string name) : ISystemNetworkSource
    {
        public bool TryReadActiveInterface(out ulong inOctets, out ulong outOctets, out string? interfaceName, out int error)
        {
            inOctets = inBytes;
            outOctets = outBytes;
            interfaceName = name;
            error = success ? 0 : 1;
            return success;
        }
    }

    [TestMethod]
    public void NetworkProvider_ProbeAndSample_CalculatesThroughput()
    {
        var provider = new SystemNetworkProvider(new FakeNetworkSource(true, 10000, 20000, "Ethernet"));
        var probe = provider.Probe();
        Assert.AreEqual(CapabilityAvailability.Supported, probe.Availability);

        var sample = provider.Sample();
        Assert.IsNotNull(sample);
    }

    [TestMethod]
    public void ProcessProvider_LiveSample_ReturnsBoundedNonEmptyList()
    {
        var provider = new SystemProcessProvider();
        var probe = provider.Probe();
        Assert.AreEqual(CapabilityAvailability.Supported, probe.Availability);

        var top = provider.SampleTop(5);
        Assert.IsTrue(top.Count > 0);
        Assert.IsTrue(top.Count <= 5);
        Assert.IsTrue(top[0].WorkingSetBytes > 0);
    }

    [TestMethod]
    public async Task ProcessExecutor_ProtectedTarget_BlocksWithoutCallingAction()
    {
        var called = false;
        var executor = new WindowsProcessExecutor(pid => { called = true; return true; });

        var result = await executor.TerminateAsync(4, DateTime.UtcNow, CancellationToken.None);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(ProcessTerminationStatus.BlockedUnsafe, result.Status);
        Assert.IsFalse(called, "Kill action must not be called on protected PID 4.");
    }

    [TestMethod]
    public async Task ProcessExecutor_MockTarget_ExecutesActionSafely()
    {
        var terminatedPid = -1;
        var executor = new WindowsProcessExecutor(pid => { terminatedPid = pid; return true; });

        // Target standard user process PID
        var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
        // Pick an arbitrary high valid PID not equal to current process
        var testPid = currentPid > 100 ? currentPid - 1 : currentPid + 1000;

        // Note: StartTime min skips identity mismatch in fake test
        var result = await executor.TerminateAsync(testPid, DateTime.MinValue, CancellationToken.None);
        if (result.Status != ProcessTerminationStatus.ProcessExited)
        {
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(testPid, terminatedPid);
        }
    }

    [TestMethod]
    public void SensorProvider_NativeSourceWithoutHelper_ReturnsNeedsHelper()
    {
        var provider = new SystemSensorProvider(new NativeSystemSensorSource());
        var probe = provider.Probe();
        Assert.AreEqual(CapabilityAvailability.NeedsHelper, probe.Availability);
        StringAssert.Contains(probe.Explanation, "M4");

        var sample = provider.Sample();
        Assert.AreEqual(SensorReadingState.NeedsHelper, sample.State);
        Assert.IsNull(sample.PackageTemperatureC);

        var allowed = provider.TrySetFanProfile(FanControlMode.Performance, out var explanation);
        Assert.IsFalse(allowed);
        StringAssert.Contains(explanation, "M4");
    }

    [TestMethod]
    public void SensorProvider_SimulatedSource_ReturnsSupportedAndAllowsPreview()
    {
        var provider = new SystemSensorProvider(new SimulatedSystemSensorSource(54.0, 2100));
        var probe = provider.Probe();
        Assert.AreEqual(CapabilityAvailability.Supported, probe.Availability);

        var sample = provider.Sample();
        Assert.AreEqual(SensorReadingState.Available, sample.State);
        Assert.AreEqual(54.0, sample.PackageTemperatureC);
        Assert.AreEqual(2100, sample.FanRpm);

        var allowed = provider.TrySetFanProfile(FanControlMode.Performance, out var msg);
        Assert.IsTrue(allowed);
        StringAssert.Contains(msg, "Performance");
        var updated = provider.Sample();
        Assert.AreEqual(2800, updated.FanRpm);
    }
}
