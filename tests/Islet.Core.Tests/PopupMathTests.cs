using Islet.Core.Popups;
namespace Islet.Core.Tests;
[TestClass]
public sealed class PopupMathTests
{
    [TestMethod]
    public void PressureCombinesPhysicalAndCommitAndFallsBackExplicitly()
    {
        Assert.AreEqual("Normal",new PopupMemory(100,21,70,100,10,null,null).PressureName);
        Assert.AreEqual("Warning",new PopupMemory(100,20,50,100,10,null,null).PressureName);
        Assert.AreEqual("Critical",new PopupMemory(100,50,95,100,10,null,null).PressureName);
        var fallback=new PopupMemory(100,10,null,null,null,null,null);Assert.AreEqual("Warning",fallback.PressureName);Assert.IsFalse(fallback.CommitValid);
        Assert.IsFalse(new PopupMemory(100,101,50,100,0,null,null).Valid);
        Assert.IsFalse(new PopupMemory(100,50,101,100,0,null,null).CommitValid);
    }
    [TestMethod]
    public void FixedRingRejectsInvalidAndNonMonotonicSamplesAndReportsWarmup()
    {
        var ring=new SampleRing(121);ring.Push(0,10);ring.Push(0,90);ring.Push(1,double.NaN);Assert.AreEqual(1,ring.Count);
        Assert.IsFalse(ring.Average(10,60).Complete);
        for(int i=1;i<=150;i++)ring.Push(i,20);
        Assert.AreEqual(121,ring.Count);Assert.AreEqual(30.0,ring.At(0).Seconds);Assert.IsTrue(ring.Average(150,60).Complete);
        Assert.AreEqual(20,ring.Average(150,60).Percent!.Value,.001);
    }
    [TestMethod]
    public void RingPushAndAverageHaveNoManagedAllocationsAfterWarmup()
    {
        var ring=new SampleRing(121);for(int i=0;i<100;i++){ring.Push(i,20);ring.Average(i,60);}
        var before=GC.GetAllocatedBytesForCurrentThread();for(int i=100;i<1000;i++){ring.Push(i,30);ring.Average(i,60);}
        var bytes=GC.GetAllocatedBytesForCurrentThread()-before;Assert.AreEqual(0L,bytes);
    }
    [TestMethod]
    public void ProcessCpuRejectsReusedPidsCounterResetsAndInvalidIntervals()
    {
        Assert.IsNull(PopupProcessMath.Cpu(1,2,0,100,1,8));Assert.IsNull(PopupProcessMath.Cpu(1,1,100,0,1,8));
        Assert.IsNull(PopupProcessMath.Cpu(1,1,0,100,double.NaN,8));
        Assert.AreEqual(12.5,PopupProcessMath.Cpu(1,1,0,10000000,1,8)!.Value,.0001);
        Assert.AreEqual(100,PopupProcessMath.Cpu(1,1,0,ulong.MaxValue,1,8)!.Value,.0001);
    }

}
