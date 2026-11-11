namespace Islet.Core.Presentation;

public static class DesktopDashboardLayout
{
    public const int OpenApplication=480, CloseApplication=902, MinimizeApplication=903, ToggleEdgeBar=460, FanCommandBase=520;
    public const double SidebarWidth=190;
    public static readonly MonitorPage[] SidebarPages = [
        MonitorPage.Overview,
        MonitorPage.Cpu,
        MonitorPage.Memory,
        MonitorPage.Network,
        MonitorPage.Disk,
        MonitorPage.Gpu,
        MonitorPage.Battery,
        MonitorPage.Apps,
        MonitorPage.Sensors
    ];
    public static DashboardRect MacClose => new(14,14,14,14);
    public static DashboardRect MacMinimize => new(32,14,14,14);
    public static DashboardRect MacZoom => new(50,14,14,14);
    public static DashboardRect Navigation(int row) => new(14,56+row*34,162,30);
    public static DashboardRect Close(double width) => new(width-48,12,34,30);
    public static DashboardRect Minimize(double width) => new(width-86,12,34,30);
    public static DashboardRect Theme(double width) => new(width-170,12,70,30);
    public static DashboardRect EdgeOption(double height) => new(14,height-94,162,36);
    public static DashboardRect StyleOption(int style,double height) => new(14+style*54,height-49,50,30);
    public static DashboardRect BottomTool(int index,double height) => new(16+index*33,height-38,28,28);
    public static DashboardRect FanPill(int index, double x, double y, double width)
    {
        var pillW = (width - 18) / 4.0;
        return new(x + index * (pillW + 6), y, pillW, 28);
    }
    public static DashboardRect OverviewFanCard(double width) => new(SidebarWidth + 28, 324, width - SidebarWidth - 56, 76);
    public static DashboardRect OverviewFanPill(int index, double width) => FanPill(index, SidebarWidth + 40, 362, width - SidebarWidth - 80);
    public static int HitTest(double x,double y,double width,double height,MonitorDashboard dashboard)
    {
        if(MacClose.Contains(x,y)||Close(width).Contains(x,y))return CloseApplication;
        if(MacMinimize.Contains(x,y)||Minimize(width).Contains(x,y))return MinimizeApplication;
        if(MacZoom.Contains(x,y)||Theme(width).Contains(x,y))return DashboardLayout.ThemeCommand;
        if(BottomTool(0,height).Contains(x,y)||EdgeOption(height).Contains(x,y))return ToggleEdgeBar;
        if(BottomTool(4,height).Contains(x,y))return 999;
        for(var i=0;i<3;i++)if(StyleOption(i,height).Contains(x,y))return 200+i;
        for(var i=0;i<SidebarPages.Length;i++)if(Navigation(i).Contains(x,y))return DashboardLayout.PageCommand+(int)SidebarPages[i];
        if(dashboard.Page==MonitorPage.Overview)
        {
            for(var m=0;m<4;m++)if(OverviewFanPill(m,width).Contains(x,y))return FanCommandBase+m;
            for(var i=0;i<6;i++)if(SummaryCard(i,width).Contains(x,y))return DashboardLayout.PageCommand+(int)new[]{MonitorPage.Cpu,MonitorPage.Memory,MonitorPage.Network,MonitorPage.Disk,MonitorPage.Battery,MonitorPage.Gpu}[i];
            if(OverviewFanCard(width).Contains(x,y))return DashboardLayout.PageCommand+(int)MonitorPage.Sensors;
        }
        if(dashboard.Page==MonitorPage.Sensors)
        {
            var main=Math.Min(440,(width-SidebarWidth-56)*0.66);
            for(var m=0;m<4;m++)if(FanPill(m,SidebarWidth+48,148-dashboard.ScrollOffset,main-40).Contains(x,y))return FanCommandBase+m;
        }
        return 0;
    }
    public static DashboardRect SummaryCard(int index,double width)
    { var w=(width-SidebarWidth-64-24)/3.0;return new(SidebarWidth+32+index%3*(w+12),412+index/3*72,w,64); }
}
public static class ModulePanelLayout
{
    public const int Dismiss=482;
    public static DashboardRect Application(double width)=>new(width-74,12,28,28);
    public static DashboardRect Close(double width)=>new(width-40,12,28,28);
    public static DashboardRect ScrollUp(double width,double height)=>new(width-82,height-36,30,26);
    public static DashboardRect ScrollDown(double width,double height)=>new(width-45,height-36,30,26);
    public static int HitTest(double x,double y,double width,double height=650)=>Application(width).Contains(x,y)?DesktopDashboardLayout.OpenApplication:Close(width).Contains(x,y)?Dismiss:ScrollUp(width,height).Contains(x,y)?483:ScrollDown(width,height).Contains(x,y)?484:0;
    public static int ClampScroll(int offset,int contentHeight,int viewportHeight)=>Math.Clamp(offset,0,Math.Max(0,contentHeight-viewportHeight));
}
public static class MonitorStripLayout
{
    public static readonly MonitorPage[] Modules=[MonitorPage.Cpu,MonitorPage.Memory,MonitorPage.Network,MonitorPage.Battery];
    public static DashboardRect Application(double width)=>new(width-44,6,34,38);
    public static DashboardRect Item(int index,double width)=>new(12+index*((width-68)/4),5,(width-68)/4-3,40);
    public static int HitTest(double x,double y,double width,MonitorDashboard dashboard)
    {
        if(Application(width).Contains(x,y))return DesktopDashboardLayout.OpenApplication;
        for(var i=0;i<Modules.Length;i++)if(Item(i,width).Contains(x,y)&&!dashboard.Cards.Any(c=>c.Page==Modules[i]&&c.Absent))return DashboardLayout.PageCommand+(int)Modules[i];
        return 0;
    }
}
/// <summary>Armed edge detection is distinct from explicitly hidden. Caller owns the bounded pointer sampler.</summary>
public sealed class EdgeBarReveal
{
    private double? _entered,_left;
    public bool Revealed {get;private set;}
    public bool Update(double now,bool enabled,bool suppressed,bool atEdge,bool inside,bool panelOpen)
    {
        if(!enabled||suppressed||!double.IsFinite(now)||now<0){Reset();return false;}
        if(panelOpen){Revealed=true;_left=null;return true;}
        if(Revealed)
        {
            if(atEdge||inside)_left=null;
            else{_left??=now;if(now-_left>=350)Reset();}
        }
        else if(atEdge){_entered??=now;if(now-_entered>=150){Revealed=true;_left=null;}}
        else _entered=null;
        return Revealed;
    }
    public void Reset(){Revealed=false;_entered=_left=null;}
}