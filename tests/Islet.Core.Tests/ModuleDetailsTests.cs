using Islet.Core.Metrics;
using Islet.Core.Popups;
namespace Islet.Core.Tests;
[TestClass]
public sealed class ModuleDetailsTests
{
    [TestMethod]
    public void TransferHistoryHasFixedWindowStorageRejectsGarbageAndKeepsUnits()
    {
        var history=new TransferHistory();history.Push(0,double.NaN,1);history.Push(0,-1,0);Assert.AreEqual(0,history.Count);
        for(int i=0;i<300;i++)history.Push(i,i*1000,i*2000);
        Assert.AreEqual(121,history.Count);Assert.AreEqual(179.0,history.At(0).Seconds);Assert.AreEqual(598000.0,history.Scale(299));
        history.Push(299,1,1);Assert.AreEqual(598000.0,history.Scale(299));
        Assert.IsFalse(new VolumeCapacity("C:",10,11).Valid);
    }
    [TestMethod]
    public void GpuUsesBusiestEngineAndSeparatesHybridAdapters()
    {
        var result=GpuEngineMath.Usage(new Dictionary<string,double>{
            ["pid_1_luid_0x00000000_0x00000123_phys_0_eng_0_engtype_3D"]=40,
            ["pid_2_luid_0x00000000_0x00000123_phys_0_eng_0_engtype_3D"]=50,
            ["pid_1_luid_0x00000000_0x00000123_phys_0_eng_1_engtype_Copy"]=80,
            ["pid_1_luid_0x00000001_0x00000123_phys_0_eng_0_engtype_3D"]=7,
            ["invalid"]=100,["pid_3_luid_0x00000000_0x00000123_phys_0_eng_1_engtype_Copy"]=double.NaN});
        Assert.AreEqual(90.0,result[0x123]);Assert.AreEqual(7.0,result[0x100000123]);Assert.AreEqual(2,result.Count);
    }
    [TestMethod]
    public void UnknownBatteryFlagsAreExplainedAndNotInventedAsNoBattery()
    {var result=BatteryReading.Evaluate(255,255,-1,null,null);Assert.AreEqual(BatteryReadingState.Unavailable,result.State);StringAssert.Contains(result.Explanation,"unknown");}
}
