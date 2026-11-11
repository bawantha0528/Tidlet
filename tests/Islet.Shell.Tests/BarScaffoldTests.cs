using Islet.Core.Metrics;
using Islet.Shell.Bar;
namespace Islet.Shell.Tests;
[TestClass]
public sealed class BarScaffoldTests
{
    [TestMethod]
    public void InvalidAndMissingValuesAreExplicitInsteadOfFakeZero()
    {
        Assert.AreEqual("—",BarText.Cpu(new(CpuReadingState.Invalid,double.NaN,"Bad CPU sample")).Value);
        Assert.AreEqual("—",BarText.Cpu(new(CpuReadingState.Unavailable,null,"Denied")).Value);
        Assert.AreEqual("0%",BarText.Cpu(new(CpuReadingState.Available,0,"Idle CPU")).Value);
        Assert.AreEqual("—",BarText.Ram(RamReading.Evaluate(100,200)).Value);
        Assert.AreEqual("75%",BarText.Ram(RamReading.Evaluate(100,25)).Value);
    }
    [TestMethod]
    public void DiagnosticLaunchRequiresBoundedNaturalExitAndRejectsLegacyRoutes()
    {
        Assert.IsTrue(BarProbeOptions.TryParse([],out _));
        Assert.IsTrue(BarProbeOptions.TryParse(["--r0-probe","--exit-after-seconds","345","--probe-report","probe.json"],out var options));
        Assert.AreEqual(345,options.ExitSeconds);
        Assert.IsFalse(BarProbeOptions.TryParse(["--r0-probe"],out _));
        Assert.IsFalse(BarProbeOptions.TryParse(["--style","Notch"],out _));
        Assert.IsFalse(BarProbeOptions.TryParse(["--solid","--solid"],out _));
    }
    [TestMethod]
    public void SoftwarePreferenceIsOptInAndConfinedToBoundedSolidDiagnostics()
    {
        Assert.IsTrue(BarProbeOptions.TryParse([],out var normal));Assert.IsFalse(normal.SoftwareRendering);
        Assert.IsFalse(BarProbeOptions.TryParse(["--software-rendering"],out _));
        Assert.IsFalse(BarProbeOptions.TryParse(["--r0-probe","--software-rendering","--exit-after-seconds","8","--probe-report","probe.json"],out _));
        Assert.IsTrue(BarProbeOptions.TryParse(["--r0-probe","--solid","--software-rendering","--exit-after-seconds","8","--probe-report","probe.json"],out var software));
        Assert.IsTrue(software.SoftwareRendering);Assert.IsFalse(software.Transparent);
        Assert.IsFalse(BarProbeOptions.TryParse(["--r0-probe","--solid","--software-rendering","--software-rendering","--exit-after-seconds","8","--probe-report","probe.json"],out _));
    }}