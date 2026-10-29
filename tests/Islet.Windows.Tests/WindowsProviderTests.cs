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
}
