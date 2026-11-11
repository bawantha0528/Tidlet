using Islet.Core.Bar;
namespace Islet.Core.Tests;

[TestClass]
public sealed class TopEdgeRevealTests
{
    [TestMethod]
    public void RevealUsesPhysicalMonitorEdgeAndStaysOpenAcrossTheGapAndPopup()
    {
        var monitor=new BarRect(-1920,-400,0,680);
        var display=new BarDisplay("secondary",false,monitor,new(-1920,-370,0,650));
        var settings=new BarSettings{RevealAtTop=true};
        var strip=BarLayout.CompactOverlay(display,settings,1.5,BarEdge.Top);
        Assert.AreEqual(-364,strip.Top);Assert.AreEqual(51,strip.Height);
        var reveal=new TopEdgeReveal();
        bool Move(int x,int y,long time,bool popup=false)=>reveal.Update(monitor,strip,new BarPoint(x,y),1.5,popup,time);
        Assert.IsFalse(Move(0,-400,0));
        Assert.IsFalse(Move(-1,-397,10));
        Assert.IsTrue(Move(-1,-398,20));
        Assert.IsTrue(Move(strip.Left+1,-380,600));
        Assert.IsTrue(Move(strip.Left+1,strip.Bottom+50,1200,true));
        Assert.IsTrue(Move(strip.Left+1,strip.Bottom+50,1220));
        Assert.IsTrue(Move(strip.Left+1,strip.Bottom+50,1669));
        Assert.IsFalse(Move(strip.Left+1,strip.Bottom+50,1670));
        Assert.IsFalse(Move(strip.Left+1,strip.Top+1,1700,true));
    }

    [TestMethod]
    public void HoverOptionRoundTripsAndDoesNotChangeExistingTopOrBottomLayouts()
    {
        Assert.IsFalse(BarSettingsFile.Import("{\"Version\":1}").TopReveal);
        var saved=BarSettingsFile.Import(BarSettingsFile.Export(new BarSettings{RevealAtTop=true}));
        Assert.IsTrue(saved.TopReveal);Assert.AreEqual(34,BarLayout.HeightDip(saved),.001);
        Assert.IsFalse((saved with{Edge=BarEdge.Bottom}).TopReveal);
        Assert.AreEqual(24,BarLayout.HeightDip(new BarSettings()),.001);
    }
}
