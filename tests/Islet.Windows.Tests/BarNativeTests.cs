using Islet.Windows.Bar;
namespace Islet.Windows.Tests;
[TestClass]
public sealed class BarNativeTests
{
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
    private sealed class Fake:ISystemPhysicalNetworkSource
    {
        public PhysicalCounter[] Rows=[new(1,100,100,"Physical A")];public bool Fail;
        public bool TryRead(out PhysicalCounter[] counters,out string reason){counters=Rows;reason="Synthetic fixture";return !Fail;}
    }
}
