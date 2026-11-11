using Islet.Core.Presentation;
using Islet.Core.Settings;
namespace Islet.Core.Tests;
[TestClass]
public sealed class DesktopPresentationTests
{
    [TestMethod]
    public void ApplicationSidebarAndWindowCommandsDoNotOverlapAcrossSupportedSizes()
    {
        foreach(var width in new[]{720.0,980,1280})foreach(var height in new[]{560.0,680,900})
        {
            var dashboard=new MonitorDashboard([],Surface:MonitorSurface.Application);
            for(var i=0;i<8;i++){var r=DesktopDashboardLayout.Navigation(i);Assert.AreEqual(400+i,DashboardLayout.HitTest(r.X+5,r.Y+5,width,height,dashboard));}
            var close=DesktopDashboardLayout.Close(width);Assert.AreEqual(902,DashboardLayout.HitTest(close.X+5,close.Y+5,width,height,dashboard));
            var min=DesktopDashboardLayout.Minimize(width);Assert.AreEqual(903,DashboardLayout.HitTest(min.X+5,min.Y+5,width,height,dashboard));
            var edge=DesktopDashboardLayout.EdgeOption(height);Assert.AreEqual(460,DashboardLayout.HitTest(edge.X+5,edge.Y+5,width,height,dashboard));
            Assert.AreEqual(0,DashboardLayout.HitTest(-1,-1,width,height,dashboard));
        }
    }
    [TestMethod]
    public void ModuleHeaderAndScrollButtonsRouteSeparatelyFromMetricContent()
    {
        var dashboard=new MonitorDashboard([],MonitorPage.Memory,Surface:MonitorSurface.Module);
        Assert.AreEqual(480,DashboardLayout.HitTest(274,23,340,650,dashboard));
        Assert.AreEqual(482,DashboardLayout.HitTest(310,23,340,650,dashboard));
        Assert.AreEqual(483,DashboardLayout.HitTest(263,620,340,650,dashboard));
        Assert.AreEqual(484,DashboardLayout.HitTest(305,620,340,650,dashboard));
        Assert.AreEqual(0,DashboardLayout.HitTest(30,300,340,650,dashboard));
        Assert.AreEqual(0,ModulePanelLayout.ClampScroll(-50,760,590));
        Assert.AreEqual(170,ModulePanelLayout.ClampScroll(999,760,590));
    }
    [TestMethod]
    public void EdgeRevealRequiresDwellAllowsPointerTransitAndSuppressionAlwaysWins()
    {
        var edge=new EdgeBarReveal();
        Assert.IsFalse(edge.Update(0,true,false,true,false,false));
        Assert.IsFalse(edge.Update(100,true,false,true,false,false));
        Assert.IsTrue(edge.Update(151,true,false,true,false,false));
        Assert.IsTrue(edge.Update(170,true,false,false,false,false));
        Assert.IsTrue(edge.Update(400,true,false,false,true,false));
        Assert.IsTrue(edge.Update(500,true,false,false,false,true));
        Assert.IsFalse(edge.Update(501,true,true,false,false,true));
        Assert.IsFalse(edge.Update(600,true,false,false,false,false));
        Assert.IsFalse(edge.Update(700,true,false,true,false,false));
        Assert.IsTrue(edge.Update(851,true,false,true,false,false));
        Assert.IsTrue(edge.Update(900,true,false,false,false,false));
        Assert.IsFalse(edge.Update(1251,true,false,false,false,false));
        Assert.IsFalse(edge.Update(double.NaN,true,false,true,false,false));
    }
    [TestMethod]
    public void NewPreferenceDefaultsOffAndSurvivesVersionOneJsonRoundTrip()
    {
        var old=System.Text.Json.JsonSerializer.Deserialize<SettingsDocument>("{\"SchemaVersion\":1}")!;
        Assert.IsFalse(old.BarAutoReveal);Assert.IsTrue(old.HasSupportedSchema);
        var enabled=old with{BarAutoReveal=true};
        var copy=System.Text.Json.JsonSerializer.Deserialize<SettingsDocument>(System.Text.Json.JsonSerializer.Serialize(enabled));
        Assert.AreEqual(enabled,copy);Assert.AreNotEqual(old,enabled);
    }
    [TestMethod]
    public void OverviewFanCardAndSummaryCardsRouteWithoutOverlap()
    {
        var width = 980.0; var height = 680.0;
        var dashboard = new MonitorDashboard([], MonitorPage.Overview, Surface: MonitorSurface.Application);
        for (var m = 0; m < 4; m++)
        {
            var pill = DesktopDashboardLayout.OverviewFanPill(m, width);
            Assert.AreEqual(DesktopDashboardLayout.FanCommandBase + m, DashboardLayout.HitTest(pill.X + 2, pill.Y + 2, width, height, dashboard));
        }
        var fanCard = DesktopDashboardLayout.OverviewFanCard(width);
        Assert.AreEqual(DashboardLayout.PageCommand + (int)MonitorPage.Sensors, DashboardLayout.HitTest(fanCard.X + 5, fanCard.Y + 5, width, height, dashboard));
        var card0 = DesktopDashboardLayout.SummaryCard(0, width);
        Assert.AreEqual(DashboardLayout.PageCommand + (int)MonitorPage.Cpu, DashboardLayout.HitTest(card0.X + 2, card0.Y + 2, width, height, dashboard));
    }
}