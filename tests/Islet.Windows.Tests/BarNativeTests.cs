using Islet.Windows.Bar;
namespace Islet.Windows.Tests;
[TestClass]
public sealed class BarNativeTests
{
    [TestMethod]
    public void CpuTemperatureRefreshIsLimitedToOneCheckPerInterval()
    {
        long next=0;
        Assert.IsTrue(Islet.Windows.Metrics.CpuTemperatureHelper.TryScheduleAutomatic(ref next,100));
        Assert.IsFalse(Islet.Windows.Metrics.CpuTemperatureHelper.TryScheduleAutomatic(ref next,15099));
        Assert.IsTrue(Islet.Windows.Metrics.CpuTemperatureHelper.TryScheduleAutomatic(ref next,15100));
        Assert.IsFalse(Islet.Windows.Metrics.CpuTemperatureHelper.TryScheduleAutomatic(ref next,15100));
    }

    [TestMethod]
    public void CpuTemperatureWorkerProtocolSeparatesReadingsFromErrors()
    {
        var reading=Islet.Windows.Metrics.CpuTemperatureHelper.Encode(58.4);
        Assert.AreEqual(58.4,Islet.Windows.Metrics.CpuTemperatureHelper.Decode(reading));
        Assert.IsNull(Islet.Windows.Metrics.CpuTemperatureHelper.Decode(10));
        Assert.IsNull(Islet.Windows.Metrics.CpuTemperatureHelper.Decode(-1));
        Assert.IsNull(Islet.Windows.Metrics.CpuTemperatureHelper.Decode(Islet.Windows.Metrics.CpuTemperatureHelper.Encode(double.NaN)));
        Assert.IsNull(Islet.Windows.Metrics.CpuTemperatureHelper.Decode(Islet.Windows.Metrics.CpuTemperatureHelper.Encode(151)));
    }

    [TestMethod]
    public void BatteryTemperatureUsesTenthsOfKelvinAndRejectsInvalidReadings()
    {
        Assert.AreEqual(24.95, Islet.Windows.Metrics.NativeSystemBatterySource.DecodeTemperature(2981)!.Value, 0.001);
        Assert.IsNull(Islet.Windows.Metrics.NativeSystemBatterySource.DecodeTemperature(0));
        Assert.IsNull(Islet.Windows.Metrics.NativeSystemBatterySource.DecodeTemperature(uint.MaxValue));
    }

    [TestMethod]
    public void NativeLayoutsMatchDocumentedWindowsHeadersOnThisArchitecture()
    {
        Assert.AreEqual(1352,PhysicalNetworkSource.NativeRowSize);
        Assert.AreEqual(IntPtr.Size==4?36:48,WindowsBarDesktop.NativeSizes.AppBar);
        Assert.AreEqual(IntPtr.Size==4?956:976,WindowsBarDesktop.NativeSizes.Tray);
    }
    [TestMethod]
    public void VirtualFilterTunnelAndDownInterfacesNeverEnterTotals()
    {
        Assert.IsTrue(PhysicalNetworkSource.Include(71,1,1));Assert.IsTrue(PhysicalNetworkSource.Include(6,5,1));
        Assert.IsFalse(PhysicalNetworkSource.Include(6,0,1));Assert.IsFalse(PhysicalNetworkSource.Include(6,3,1));
        Assert.IsFalse(PhysicalNetworkSource.Include(131,1,1));Assert.IsFalse(PhysicalNetworkSource.Include(71,1,2));
    }
    [TestMethod]
    public void AdapterChangeAndCounterResetRebaselineInsteadOfSpiking()
    {
        var source=new Fake();var provider=new PhysicalNetworkProvider(source);
        Assert.AreEqual(Islet.Core.Metrics.NetworkReadingState.WarmingUp,provider.Sample().State);
        source.Rows=[new(2,100,100,"Physical B")];Assert.AreEqual(Islet.Core.Metrics.NetworkReadingState.WarmingUp,provider.Sample().State);
        source.Rows=[new(2,0,0,"Physical B")];Assert.AreEqual(Islet.Core.Metrics.NetworkReadingState.WarmingUp,provider.Sample().State);
        source.Fail=true;Assert.AreEqual(Islet.Core.Metrics.NetworkReadingState.Unavailable,provider.Sample().State);
    }
    [TestMethod]
    public void RecordedTotalsUseByteDeltasAndExcludeResetsAndPausedGaps()
    {
        ulong download=0,upload=0;
        var source=new Fake();var provider=new PhysicalNetworkProvider(source,(down,up)=>{download+=down;upload+=up;});provider.Snapshot();
        source.Rows=[new(1,200,400,"Physical A")];var first=provider.Snapshot();
        Assert.AreEqual(100UL,first.RecordedReceived);Assert.AreEqual(300UL,first.RecordedSent);
        var repeat=provider.Snapshot();Assert.AreEqual(first.RecordedReceived,repeat.RecordedReceived);
        source.Rows=[new(1,10,10,"Physical A")];var reset=provider.Snapshot();Assert.AreEqual(100UL,reset.RecordedReceived);
        provider.Reset();source.Rows=[new(1,10000,10000,"Physical A")];var paused=provider.Snapshot();Assert.AreEqual(100UL,paused.RecordedReceived);
        Assert.AreEqual(100UL,download);
        Assert.AreEqual(300UL,upload);
    }
    [TestMethod]
    public void PresentMonCaptureReportsMissingExecutableCleanlyAndDrains()
    {
        var missing=Path.Combine(Path.GetTempPath(),"missing-presentmon-"+Guid.NewGuid()+".exe");
        using var capture=new PresentMonCapture(missing);
        var deadline=DateTime.UtcNow.AddSeconds(5);
        Islet.Core.Bar.FrameReading reading=default;
        while(DateTime.UtcNow<deadline)
        {
            reading=capture.Read((uint)Environment.ProcessId);
            if(reading.Unavailable)break;
            Thread.Sleep(25);
        }
        Assert.IsTrue(reading.Unavailable,"Missing PresentMon binary must report Unavailable.");
        Assert.IsNull(reading.Milliseconds);
        StringAssert.Contains(reading.Status,"missing");
        capture.Stop();
        Assert.IsTrue(capture.Drain(TimeSpan.FromSeconds(2)));
        capture.Reset();
        Assert.IsTrue(capture.Drain(TimeSpan.FromSeconds(2)));
    }
    [TestMethod]
    public void LibreHardwareSensorSummarizePrioritizesCpuPackageAndFiltersInvalidValues()
    {
        var raw=new List<Islet.Core.Popups.SensorItem>
        {
            new("CPU","CPU","Core #0",Islet.Core.Popups.SensorKind.Temperature,54,"°C"),
            new("CPU","CPU","CPU Package",Islet.Core.Popups.SensorKind.Temperature,62.5,"°C"),
            new("CPU","CPU","Bogus NaN",Islet.Core.Popups.SensorKind.Temperature,double.NaN,"°C"),
            new("CPU","CPU","Bogus High",Islet.Core.Popups.SensorKind.Temperature,220,"°C"),
            new("GPU","GPU","GPU Hot Spot",Islet.Core.Popups.SensorKind.Temperature,74,"°C"),
            new("Motherboard","Motherboard","Chassis Fan",Islet.Core.Popups.SensorKind.Fan,1350,"RPM"),
            new("Motherboard","Motherboard","Bad Fan",Islet.Core.Popups.SensorKind.Fan,-10,"RPM"),
            new("CPU","CPU","Zero Power Stub",Islet.Core.Popups.SensorKind.Power,0,"W"),
            new("Motherboard","Motherboard","Idle Fan",Islet.Core.Popups.SensorKind.Fan,0,"RPM")
        };
        var userSummary=Islet.Windows.Metrics.LibreHardwareSensorReader.Summarize(raw,elevated:false);
        Assert.AreEqual(5,userSummary.Items.Length);
        Assert.AreEqual(62.5,userSummary.CpuPackageCelsius);
        Assert.AreEqual(74.0,userSummary.MaxTemperatureCelsius);
        Assert.AreEqual(1350,userSummary.MaxFanRpm);
        Assert.IsFalse(userSummary.IsElevated);
        StringAssert.Contains(userSummary.Explanation,"LibreHardwareMonitor");

        var emptyUser=Islet.Windows.Metrics.LibreHardwareSensorReader.Summarize([],elevated:false);
        Assert.AreEqual(0,emptyUser.Items.Length);
        StringAssert.Contains(emptyUser.Explanation,"PawnIO");

        var partialUser=Islet.Windows.Metrics.LibreHardwareSensorReader.Summarize([new("GPU","GPU","GPU Core",Islet.Core.Popups.SensorKind.Temperature,52,"°C")],elevated:false);
        StringAssert.Contains(partialUser.Explanation,"PawnIO");
        var coresOnly=Islet.Windows.Metrics.LibreHardwareSensorReader.Summarize([raw[0]],elevated:false);
        Assert.IsNull(coresOnly.CpuPackageCelsius);
        Assert.AreEqual(54d,coresOnly.MaxTemperatureCelsius);
    }
    [TestMethod]
    public void WindowsPopupSourceSensorsProbesReadsAndDisposesCleanly()
    {
        using var source=new Islet.Windows.Popups.WindowsPopupSource();
        var probe=source.Probe(Islet.Core.Bar.BarModule.Sensors);
        var sample=source.Read(Islet.Core.Bar.BarModule.Sensors);
        Assert.AreEqual(sample.Sensors!.Items.Length > 0 ? Islet.Core.Capabilities.CapabilityAvailability.Supported
            : Islet.Core.Capabilities.CapabilityAvailability.NeedsHelper,probe.Availability);
        Assert.IsNotNull(sample.Sensors);
        Assert.IsFalse(string.IsNullOrWhiteSpace(sample.Sensors.Explanation));
        if(sample.Sensors.CpuPackageCelsius is double cpuTemp)
            Assert.IsTrue(cpuTemp is>0 and<=150);
    }
    private sealed class Fake:ISystemPhysicalNetworkSource
    {
        public PhysicalCounter[] Rows=[new(1,100,100,"Physical A")];public bool Fail;
        public bool TryRead(out PhysicalCounter[] counters,out string reason){counters=Rows;reason="Synthetic fixture";return !Fail;}
    }
}
