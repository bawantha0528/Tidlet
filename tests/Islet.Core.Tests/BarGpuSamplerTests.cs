using Islet.Core.Bar;
using Islet.Core.Capabilities;
using Islet.Core.Popups;
namespace Islet.Core.Tests;
[TestClass]
public sealed class BarGpuSamplerTests
{
    private static PopupSample Sample(params GpuActivity[] adapters)=>new(null,null,null,null,null,null,TimeSpan.Zero,null,[],[],[],"GPU unavailable",Gpu:new(adapters,"GPU sample unavailable"));
    [TestMethod]
    public void HybridSummaryUsesBusiestValidAdapterAndRejectsGarbage()
    {
        var result=BarGpuSampler.Summarize(Sample(new(1,"Integrated",40,0,""),new(2,"Discrete",70,0,""),new(3,"Invalid",double.NaN,0,""),new(4,"Impossible",101,0,"")));
        Assert.AreEqual(70,result.Percent);StringAssert.Contains(result.Explanation,"Integrated");StringAssert.Contains(result.Explanation,"Discrete");
        Assert.IsNull(BarGpuSampler.Summarize(Sample(new GpuActivity(1,"Invalid",double.PositiveInfinity,0,""))).Percent);
        Assert.AreEqual(0,BarGpuSampler.Summarize(Sample(new GpuActivity(1,"Idle",0,0,""))).Percent);
        Assert.IsTrue(BarGpuSampler.Summarize(Sample() with{WarmingUp=true}).WarmingUp);
    }
    [TestMethod]
    public void OffDoesNotCreateSourceAndReenableWaitsForPendingDisposal()
    {
        using var started=new ManualResetEventSlim();using var release=new ManualResetEventSlim();var first=new Source(started,release);var second=new Source();int factories=0;bool overlap=false;
        using var gpu=new BarGpuSampler(()=>{if(Interlocked.Increment(ref factories)==1)return first;overlap|=!first.Disposed;return second;});
        gpu.Sample(false);Assert.AreEqual(0,factories);gpu.Sample(true);Assert.IsTrue(started.Wait(2000));gpu.Stop();gpu.Sample(true);Assert.AreEqual(1,factories);
        release.Set();Assert.IsTrue(gpu.Drain(TimeSpan.FromSeconds(2)));gpu.Sample(true);Assert.IsTrue(SpinWait.SpinUntil(()=>second.Reads>0,2000));
        gpu.Stop();Assert.IsTrue(gpu.Drain(TimeSpan.FromSeconds(2)));var requests=gpu.Requests;gpu.Sample(false);Assert.AreEqual(requests,gpu.Requests);Assert.IsFalse(overlap);Assert.IsTrue(first.Disposed);Assert.IsTrue(second.Disposed);
    }
    [TestMethod]
    public void DeniedProbeExplainsMissingReadingWithoutCallingRead()
    {
        var source=new Source(denied:true);using var gpu=new BarGpuSampler(()=>source);gpu.Sample(true);
        Assert.IsTrue(SpinWait.SpinUntil(()=>source.Probes>0,2000));gpu.Stop();Assert.IsTrue(gpu.Drain(TimeSpan.FromSeconds(2)));Assert.AreEqual(0,source.Reads);Assert.IsTrue(source.Disposed);
    }
    private sealed class Source(ManualResetEventSlim? started=null,ManualResetEventSlim? release=null,bool denied=false):IPopupSource
    {
        public int Reads,Probes;public bool Disposed;
        public CapabilityProbeResult Probe(BarModule module){Interlocked.Increment(ref Probes);return denied?CapabilityProbeResult.Unsupported("denied","Access denied"):CapabilityProbeResult.Supported;}
        public PopupSample Read(BarModule module){Assert.IsFalse(Disposed);Interlocked.Increment(ref Reads);started?.Set();if(release is not null&&!release.Wait(3000))throw new TimeoutException("Mock release missing");return Sample(new GpuActivity(1,"Fixture",30,0,""));}
        public void Dispose()=>Disposed=true;
    }
}
