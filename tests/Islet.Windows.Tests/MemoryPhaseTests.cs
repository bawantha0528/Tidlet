using Islet.Core.Bar;
using Islet.Core.Capabilities;
using Islet.Core.Popups;
using Islet.Windows.Diagnostics;
namespace Islet.Windows.Tests;
[TestClass]
public sealed class MemoryPhaseTests
{
    [TestMethod]
    public void DetailCounterPlanKeepsOffscreenAndRamCoreWorkInactive()
    {
        Assert.AreEqual((false,false),Islet.Windows.Popups.WindowsPopupSource.CounterNeeds(BarModule.Cpu,PopupDetails.None));
        Assert.AreEqual((true,false),Islet.Windows.Popups.WindowsPopupSource.CounterNeeds(BarModule.Cpu,PopupDetails.Cores));
        Assert.AreEqual((false,true),Islet.Windows.Popups.WindowsPopupSource.CounterNeeds(BarModule.Ram,PopupDetails.All));
        Assert.AreEqual((true,true),Islet.Windows.Popups.WindowsPopupSource.CounterNeeds(BarModule.Cpu,PopupDetails.All));
        Assert.AreEqual((true,false),Islet.Windows.Popups.WindowsPopupSource.CounterNeeds(BarModule.Cpu,PopupDetails.All,false));
    }
    [TestMethod]
    public void ComparisonRequiresExplicitDiagnosticArgumentsAndNeverChangesNormalDefault()
    {
        var report=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
        Assert.IsTrue(ProviderComparison.TryParse(["--provider-comparison","with",report],out var enabled,out _));Assert.IsTrue(enabled);
        Assert.IsTrue(ProviderComparison.TryParse(["--provider-comparison","without",report],out enabled,out _));Assert.IsFalse(enabled);
        Assert.IsFalse(ProviderComparison.TryParse(["--r1-probe","without",report],out _,out _));
        Assert.IsFalse(ProviderComparison.TryParse(["--provider-comparison","without","relative.json"],out _,out _));
        Assert.IsFalse(ProviderComparison.TryParse(["--provider-comparison","without",report,"--extra"],out _,out _));
        using var normal=new Islet.Windows.Popups.WindowsPopupSource();Assert.IsTrue(normal.ProcessCountersEnabled);
        using var diagnostic=new Islet.Windows.Popups.WindowsPopupSource(null,processCounters:false,phase:null);Assert.IsFalse(diagnostic.ProcessCountersEnabled);
    }
    [TestMethod]
    public void RecorderIsBoundedAndRecordsModuleBasenamesAndDistinctCounters()
    {
        var recorder=new MemoryPhaseRecorder(2);recorder.Record("one",true);recorder.Record("two");recorder.Record("ignored");var entries=recorder.Snapshot();
        Assert.AreEqual(2,entries.Length);Assert.AreEqual("one",entries[0].Name);Assert.AreEqual("two",entries[1].Name);
        Assert.IsTrue(entries[1].Seconds>=entries[0].Seconds);Assert.IsTrue(entries[1].ApproximateAllocatedBytes>=entries[0].ApproximateAllocatedBytes);
        Assert.IsTrue(entries[0].WorkingSetBytes>0);Assert.IsTrue(entries[0].PeakWorkingSetBytes>=entries[0].WorkingSetBytes);Assert.IsTrue(entries[0].PrivateBytes>0);
        Assert.IsNotNull(entries[0].ModuleBasenames);Assert.IsTrue(entries[0].ModuleBasenames!.Length<=128);Assert.IsTrue(entries[0].ModuleBasenames!.All(n=>!n.Contains('/')&&!n.Contains('\\')));
        Assert.IsNull(entries[1].ModuleBasenames);if(entries[0].LastCollection is{}gc)Assert.IsTrue(gc.Index>0&&gc.HeapSizeBytes>=0&&gc.CommittedBytes>=0);
    }
    [TestMethod]
    public void SourceDecoratorPreservesReadsAndDisposalAndProfilesOnlyFirstSample()
    {
        var calls=new List<string>();var source=new Source();using(var wrapper=new ProfilingPopupSource(source,(name,_)=>calls.Add(name)))
        {Assert.AreEqual(CapabilityProbeResult.Supported,wrapper.Probe(BarModule.Cpu));Assert.AreSame(source.Sample,wrapper.Read(BarModule.Cpu));wrapper.Read(BarModule.Cpu);Assert.AreEqual(2,source.Reads);}
        Assert.IsTrue(source.Disposed);CollectionAssert.AreEqual(new[]{"provider.Cpu.probe.before","provider.Cpu.probe.after","provider.Cpu.first-sample.before","provider.Cpu.first-sample.after"},calls);
    }
    private sealed class Source:IPopupSource
    {public bool Disposed;public int Reads;public PopupSample Sample=new(1,null,null,null,null,null,TimeSpan.Zero,null,[],[],[],"Mock");public CapabilityProbeResult Probe(BarModule module)=>CapabilityProbeResult.Supported;public PopupSample Read(BarModule module){Reads++;return Sample;}public void Dispose()=>Disposed=true;}
}
