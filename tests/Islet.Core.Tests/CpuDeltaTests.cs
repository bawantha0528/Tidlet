using Islet.Core.Metrics;

namespace Islet.Core.Tests;

[TestClass]
public sealed class CpuDeltaTests
{
    [TestMethod]
    public void FirstReadingHasNoInventedValue()
    {
        var reading = new CpuDelta().Update(new(10, 20, 30));
        Assert.AreEqual(CpuReadingState.WarmingUp, reading.State);
        Assert.IsNull(reading.Percent);
    }

    [TestMethod]
    public void KernelIncludesIdleAndZeroIsARealReading()
    {
        var delta = new CpuDelta();
        delta.Update(new(100, 200, 300));
        Assert.AreEqual(75.0, delta.Update(new(150, 300, 400)).Percent);
        Assert.AreEqual(0.0, delta.Update(new(250, 400, 400)).Percent);
        Assert.AreEqual(100.0, delta.Update(new(250, 500, 400)).Percent);
    }

    [TestMethod]
    public void ResetFailureAndInconsistentSamplesNeverBecomeZero()
    {
        var delta = new CpuDelta();
        delta.Update(new(10, 20, 30));
        Assert.IsNull(delta.Update(new(10, 20, 30)).Percent);
        Assert.AreEqual(CpuReadingState.WarmingUp, delta.Update(new(1, 2, 3)).State);
        Assert.AreEqual(CpuReadingState.Invalid, delta.Update(new(10, 2, 3)).State);
        Assert.AreEqual(CpuReadingState.WarmingUp, delta.Update(new(1, 2, 3)).State);
        delta.Reset();
        Assert.IsNull(delta.Update(new(20, 30, 40)).Percent);
    }

    [TestMethod]
    public void IdleCannotAdvanceFasterThanKernelEvenWithPlausibleAbsoluteTimes()
    {
        var delta = new CpuDelta();
        delta.Update(new(10, 100, 100));
        var reading = delta.Update(new(30, 110, 200));
        Assert.AreEqual(CpuReadingState.Invalid, reading.State);
        Assert.IsNull(reading.Percent);
    }

    [TestMethod]
    public void ExtremeValidDeltasDoNotOverflow()
    {
        var delta = new CpuDelta();
        delta.Update(new(0, 0, 0));
        var result = delta.Update(new(0, ulong.MaxValue, ulong.MaxValue));
        Assert.AreEqual(100.0, result.Percent);
        Assert.IsTrue(double.IsFinite(result.Percent!.Value));
    }
}
