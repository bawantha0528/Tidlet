using Islet.Core.Bar;
using Islet.Core.Popups;
namespace Islet.Core.Tests;
[TestClass]
public sealed class RecentSampleCacheTests
{
    private static PopupSample Cpu(double? value)=>new(value,5,15,80,2.5,4,TimeSpan.Zero,null,[20],[0],[new(1,"Private process",10,100)],"Fixture");
    [TestMethod]
    public void CacheExpiresWithoutRefreshingItsAgeAndNeverTurnsMissingIntoZero()
    {
        double time=0;var cache=new RecentSampleCache(()=>time);Assert.IsFalse(cache.TryGet(BarModule.Cpu,out _));
        cache.Store(BarModule.Cpu,Cpu(20));time=119;cache.Store(BarModule.Cpu,Cpu(null) with{WarmingUp=true});Assert.IsTrue(cache.TryGet(BarModule.Cpu,out var sample));Assert.AreEqual(20,sample!.Cpu);
        time=121;Assert.IsFalse(cache.TryGet(BarModule.Cpu,out _));time=200;cache.Store(BarModule.Cpu,Cpu(0));Assert.IsTrue(cache.TryGet(BarModule.Cpu,out sample));Assert.AreEqual(0,sample!.Cpu);
        cache.Store(BarModule.Cpu,Cpu(double.NaN));Assert.IsFalse(cache.TryGet(BarModule.Cpu,out _));
        cache.Store(BarModule.Cpu,Cpu(20));time=199;Assert.IsFalse(cache.TryGet(BarModule.Cpu,out _));
    }
    [TestMethod]
    public void SessionCacheDropsPrivateDetailsAndBoundsAdapterLists()
    {
        var cache=new RecentSampleCache(()=>0);var source=Cpu(20) with{Network=new(1024,2048,3000,4000,[new("Private interface","Private MAC","Private IP","Private gateway")],"Private explanation")};
        cache.Store(BarModule.Network,source);Assert.IsTrue(cache.TryGet(BarModule.Network,out var cached));Assert.AreEqual(0,cached!.Processes.Length);Assert.AreEqual(0,cached.Cores.Length);Assert.AreEqual(0,cached.Network!.Adapters.Length);Assert.AreEqual(1024,cached.Network.Download);Assert.IsFalse(cached.Explanation.Contains("Private"));
        var adapters=Enumerable.Range(0,50).Select(i=>new GpuActivity((ulong)i,"Fixture",20,0,"")).ToArray();cache.Store(BarModule.Gpu,Cpu(null) with{Gpu=new(adapters,"")});adapters[0]=new(0,"Changed",90,0,"");
        Assert.IsTrue(cache.TryGet(BarModule.Gpu,out cached));Assert.AreEqual(32,cached!.Gpu!.Adapters.Length);Assert.AreEqual("Fixture",cached.Gpu.Adapters[0].Name);Assert.IsNull(cached.Network);
    }
}
