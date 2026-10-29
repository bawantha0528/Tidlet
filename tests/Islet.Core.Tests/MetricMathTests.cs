using Microsoft.VisualStudio.TestTools.UnitTesting;
using Islet.Core.Metrics;

namespace Islet.Core.Tests;

[TestClass]
public sealed class MetricMathTests
{
    [TestMethod]
    public void RamEvaluation_ValidMemory_CalculatesUsedAndPercent()
    {
        ulong total = 16L * 1024 * 1024 * 1024; // 16 GiB
        ulong avail = 8L * 1024 * 1024 * 1024;  // 8 GiB
        var reading = RamReading.Evaluate(total, avail);

        Assert.AreEqual(RamReadingState.Available, reading.State);
        Assert.AreEqual(50.0, reading.UsedPercent);
        Assert.AreEqual(avail, reading.UsedBytes);
        StringAssert.Contains(reading.Explanation, "8.0 GiB used of 16.0 GiB");
    }

    [TestMethod]
    public void RamEvaluation_ZeroTotal_RejectsInvalid()
    {
        var reading = RamReading.Evaluate(0, 0);
        Assert.AreEqual(RamReadingState.Invalid, reading.State);
        Assert.IsNull(reading.UsedPercent);
    }

    [TestMethod]
    public void RamEvaluation_AvailableGreaterThanTotal_RejectsInvalid()
    {
        var reading = RamReading.Evaluate(1000, 2000);
        Assert.AreEqual(RamReadingState.Invalid, reading.State);
    }

    [TestMethod]
    public void NetworkDelta_MonotonicRates_CalculatesThroughput()
    {
        var delta = new NetworkDelta();
        var t0 = TimeSpan.FromSeconds(10);
        var t1 = TimeSpan.FromSeconds(11);

        var first = delta.Update(new(1000, 2000, t0), "Ethernet");
        Assert.AreEqual(NetworkReadingState.WarmingUp, first.State);

        var second = delta.Update(new(1000 + 10240, 2000 + 20480, t1), "Ethernet");
        Assert.AreEqual(NetworkReadingState.Available, second.State);
        Assert.AreEqual(10240UL, second.BytesReceivedPerSec);
        Assert.AreEqual(20480UL, second.BytesSentPerSec);
        StringAssert.Contains(second.Explanation, "10.0 KB/s");
    }

    [TestMethod]
    public void NetworkDelta_CounterDecrease_TriggersWarmup()
    {
        var delta = new NetworkDelta();
        delta.Update(new(5000, 5000, TimeSpan.FromSeconds(1)), "Wi-Fi");
        var reading = delta.Update(new(1000, 1000, TimeSpan.FromSeconds(2)), "Wi-Fi");

        Assert.AreEqual(NetworkReadingState.WarmingUp, reading.State);
    }

    [TestMethod]
    public void NetworkDelta_ZeroOrNegativeElapsed_RejectsInvalid()
    {
        var delta = new NetworkDelta();
        delta.Update(new(1000, 1000, TimeSpan.FromSeconds(5)), "Wi-Fi");
        var reading = delta.Update(new(2000, 2000, TimeSpan.FromSeconds(5)), "Wi-Fi");

        Assert.AreEqual(NetworkReadingState.Invalid, reading.State);
    }

    [TestMethod]
    public void BatteryEvaluation_NoBatteryFlag_ReturnsNoBatteryState()
    {
        var reading = BatteryReading.Evaluate(128, 255, -1, null, null);
        Assert.AreEqual(BatteryReadingState.Unavailable, reading.State);
        Assert.IsNull(reading.Percent);
        StringAssert.Contains(reading.Explanation, "No battery detected");
    }

    [TestMethod]
    public void BatteryEvaluation_ValidCharging_CalculatesStatus()
    {
        var reading = BatteryReading.Evaluate(8, 85, 3600, 25000, 95.5);
        Assert.AreEqual(BatteryReadingState.Available, reading.State);
        Assert.AreEqual(85.0, reading.Percent);
        Assert.AreEqual(BatteryChargingStatus.Charging, reading.ChargingStatus);
        Assert.AreEqual(25000.0, reading.ChargeRateMilliwatts);
        Assert.AreEqual(95.5, reading.HealthPercent);
    }

    [TestMethod]
    public void BatteryEvaluation_InvalidPercentOver100_RejectsInvalid()
    {
        var reading = BatteryReading.Evaluate(1, 150, -1, null, null);
        Assert.AreEqual(BatteryReadingState.Invalid, reading.State);
    }

    [TestMethod]
    public void DiskDelta_MonotonicThroughput_CalculatesRates()
    {
        var delta = new DiskDelta();
        var t0 = TimeSpan.FromSeconds(1);
        var t1 = TimeSpan.FromSeconds(2);

        var first = delta.Update(new(0, 0, t0), 5.0);
        Assert.AreEqual(DiskReadingState.WarmingUp, first.State);

        var second = delta.Update(new(1024 * 1024, 2 * 1024 * 1024, t1), 25.0);
        Assert.AreEqual(DiskReadingState.Available, second.State);
        Assert.AreEqual(1024UL * 1024, second.ReadBytesPerSec);
        Assert.AreEqual(2UL * 1024 * 1024, second.WriteBytesPerSec);
        Assert.AreEqual(25.0, second.ActiveTimePercent);
        StringAssert.Contains(second.Explanation, "1.0 MB/s");
    }

    [TestMethod]
    public void GpuEvaluation_ValidUtilization_ClampsAndFormats()
    {
        var reading = GpuReading.Evaluate("NVIDIA RTX 4080", 75.4, 4UL * 1024 * 1024 * 1024, 16UL * 1024 * 1024 * 1024);
        Assert.AreEqual(GpuReadingState.Available, reading.State);
        Assert.AreEqual(75.4, reading.UtilizationPercent);
        StringAssert.Contains(reading.Explanation, "75% load");
        StringAssert.Contains(reading.Explanation, "4.0 / 16.0 GiB VRAM");
    }

    [TestMethod]
    public void JitterCalculator_SamplesHistory_ComputesMeanAbsoluteDifference()
    {
        var calc = new JitterCalculator();
        Assert.IsNull(calc.Record(10.0)); // 1 sample: no jitter yet

        var jitter2 = calc.Record(20.0); // diff = 10
        Assert.AreEqual(10.0, jitter2);

        var jitter3 = calc.Record(15.0); // diffs = 10, 5 -> mean = 7.5
        Assert.AreEqual(7.5, jitter3);
    }

    [TestMethod]
    public void ProcessTerminationPolicy_ProtectsCriticalAndOwnProcess()
    {
        var (allowedIdle, _) = ProcessTerminationPolicy.ValidateTarget(0, 100, "Idle");
        Assert.IsFalse(allowedIdle);

        var (allowedSystem, _) = ProcessTerminationPolicy.ValidateTarget(4, 100, "System");
        Assert.IsFalse(allowedSystem);

        var (allowedSelf, _) = ProcessTerminationPolicy.ValidateTarget(100, 100, "IsletMonitor");
        Assert.IsFalse(allowedSelf);

        var (allowedCsrss, _) = ProcessTerminationPolicy.ValidateTarget(450, 100, "csrss.exe");
        Assert.IsFalse(allowedCsrss);

        var (allowedApp, _) = ProcessTerminationPolicy.ValidateTarget(2050, 100, "notepad.exe");
        Assert.IsTrue(allowedApp);
    }
}
