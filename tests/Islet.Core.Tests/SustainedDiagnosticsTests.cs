using Islet.Core.Bar;

namespace Islet.Core.Tests;

[TestClass]
public sealed class SustainedDiagnosticsTests
{
    private static readonly BarSettings Settings = new() { SustainedAlerts = true, SustainedSeconds = 30 };

    [TestMethod]
    public void AlertsOnceForContinuousHighCpuAndDiagnosisClearsOnRecovery()
    {
        var engine = new SustainedDiagnostics();
        for (int seconds = 0; seconds < 30; seconds += 5)
            Assert.AreEqual(SustainedProblem.None, engine.Observe(seconds, 93, 50, null, Settings));
        Assert.AreEqual(SustainedProblem.Cpu, engine.Observe(30, 93, 50, null, Settings));
        Assert.AreEqual(SustainedProblem.Cpu, engine.Active);
        Assert.AreEqual(SustainedProblem.None, engine.Observe(35, 93, 50, null, Settings));
        Assert.AreEqual(SustainedProblem.None, engine.Observe(40, 60, 50, null, Settings));
        Assert.AreEqual(SustainedProblem.None, engine.Active);
    }

    [TestMethod]
    public void MissingReadingsAndSamplingGapsBreakTheStreak()
    {
        var engine = new SustainedDiagnostics();
        engine.Observe(0, 95, null, null, Settings);
        engine.Observe(5, double.NaN, null, null, Settings);
        Assert.IsFalse(engine.HasReadings);
        engine.Observe(10, 95, null, null, Settings);
        Assert.AreEqual(SustainedProblem.None, engine.Observe(40, 95, null, null, Settings));
    }

    [TestMethod]
    public void AlertsAreOptInAndDifferentProblemsShareTheCooldown()
    {
        var engine = new SustainedDiagnostics();
        for (int seconds = 0; seconds <= 30; seconds += 5)
            Assert.AreEqual(SustainedProblem.None, engine.Observe(seconds, 20, 95, null, Settings with { SustainedAlerts = false }));
        Assert.AreEqual(SustainedProblem.Memory, engine.Active);
        engine.Reset();
        for (int seconds = 40; seconds < 70; seconds += 5)
            engine.Observe(seconds, 95, 95, 10, Settings);
        Assert.AreEqual(SustainedProblem.Cpu, engine.Observe(70, 95, 95, 10, Settings));
        for (int seconds = 75; seconds < 370; seconds += 5)
            Assert.AreEqual(SustainedProblem.None, engine.Observe(seconds, 95, 95, 10, Settings));
        Assert.AreEqual(SustainedProblem.Memory, engine.Observe(370, 95, 95, 10, Settings));
    }
}
