using Islet.Core.Presentation;
using Islet.Core.Settings;
namespace Islet.Windows.Native;

internal sealed unsafe partial class DibRenderer
{
    private int Raised => _background==0xF4F4F5 ? 0xFFFFFF : _background==0 ? 0 : 0x1A1A20;
    private void Center(string value,double x,double y,double width,double size=12,int? color=null,int weight=500,double height=24)
        =>Text(value,x*_s,y*_s,(x+width)*_s,(y+height)*_s,size*_s,weight,color??_text,_w,_h,1);
    private void Right(string value,double x,double y,double width,double size=12,int? color=null,int weight=500)
        =>Text(value,x*_s,y*_s,(x+width)*_s,(y+25)*_s,size*_s,weight,color??_text,_w,_h,2);
    private void Icon(string name,double x,double y,double size=20,int? color=null)
    {
        if(!LucideGeometry.Paths.TryGetValue(name,out var paths))return;
        var thickness = Math.Max(1.9 * _s, size / 24.0 * _s * 2.2);
        foreach(var points in paths)
            for(var i=2;i<points.Length;i+=2)
                IconStroke((x+points[i-2]*size/24.0)*_s,(y+points[i-1]*size/24.0)*_s,
                    (x+points[i]*size/24.0)*_s,(y+points[i+1]*size/24.0)*_s,thickness,color??_muted);
    }
    private void IconStroke(double ax,double ay,double bx,double by,double thickness,int color)
    {
        var radius=thickness/2.0;var dx=bx-ax;var dy=by-ay;var length=dx*dx+dy*dy;
        var minX = Math.Max(0,(int)Math.Floor(Math.Min(ax,bx)-radius-1));
        var maxX = Math.Min(_w,(int)Math.Ceiling(Math.Max(ax,bx)+radius+1));
        var minY = Math.Max(_clipTop,(int)Math.Floor(Math.Min(ay,by)-radius-1));
        var maxY = Math.Min(Math.Min(_h,_clipBottom),(int)Math.Ceiling(Math.Max(ay,by)+radius+1));
        for(var y=minY;y<maxY;y++)
            for(var x=minX;x<maxX;x++)
            {
                var t=length==0?0:Math.Clamp(((x+.5-ax)*dx+(y+.5-ay)*dy)/length,0,1);
                var ex=x+.5-ax-dx*t;var ey=y+.5-ay-dy*t;
                var dist=Math.Sqrt(ex*ex+ey*ey);
                var a=Math.Clamp(radius+.5-dist,0,1);var index=y*_output!.Width+x;
                if(a>0&&(uint)_output.Pixels[index]>>24!=0)_output.Pixels[index]=Over(_output.Pixels[index],color,(int)(a*255));
            }
    }
    private static string MetricIcon(MonitorPage page)=>page switch
    { MonitorPage.Overview=>"layout-dashboard",MonitorPage.Cpu=>"cpu",MonitorPage.Memory=>"memory-stick",MonitorPage.Network=>"globe",MonitorPage.Disk=>"hard-drive",MonitorPage.Gpu=>"monitor",MonitorPage.Battery=>"battery",MonitorPage.Sensors=>"fan",_=>"app-window" };
    private void Section(string title,double x,double y,double width)
    {
        var labelWidth=Math.Min(width-10,title.Length*5.7+18);var side=(width-labelWidth)/2;
        Stroke(x,y+10,x+side,y+10,_border);Stroke(x+width-side,y+10,x+width,y+10,_border);
        Label(title,x+side+8,y,width-2*side-8,9,_muted,600);
    }
    private void PillBadge(string text,double x,double y,int bgColor,int textColor)
    {
        var width=text.Length*6.5+14;
        Block(new(x,y,width,16),bgColor,8);
        Center(text,x,y+1,width,9,textColor,700,14);
    }
    private void SegmentedBar(double x,double y,double width,double height,double[] fractions,int[] colors)
    {
        var currentX=x;
        for(var i=0;i<fractions.Length&&i<colors.Length;i++)
        {
            var segW=Math.Max(2,width*Math.Clamp(fractions[i],0,1));
            if(currentX+segW>x+width)segW=x+width-currentX;
            if(segW<=0)break;
            Block(new(currentX,y,segW,height),colors[i],i==0||i==fractions.Length-1?3:0);
            currentX+=segW;
        }
    }
    private void Speedometer(double cx,double cy,double radius,double? percent)
    {
        var r=radius*_s;var thickness=4.5*_s;
        var centerX=cx*_s;var centerY=cy*_s;
        for(var row=Math.Max(_clipTop,(int)(centerY-r-2));row<=Math.Min(Math.Min(_h,_clipBottom),(int)(centerY+2));row++)
            for(var col=Math.Max(0,(int)(centerX-r-2));col<=Math.Min(_w,(int)(centerX+r+2));col++)
            {
                var dx=col+0.5-centerX;var dy=row+0.5-centerY;
                if(dy>0.5)continue;
                var dist=Math.Sqrt(dx*dx+dy*dy);
                var coverage=Math.Min(Math.Clamp(r+0.5-dist,0,1),Math.Clamp(dist-r+thickness+0.5,0,1));
                if(coverage<=0)continue;
                var angle=Math.Atan2(-dy,-dx)*180.0/Math.PI;
                var bandColor=angle<108?0x30D158:angle<144?0xFFD60A:0xFF453A;
                var index=row*_output!.Width+col;
                if((uint)_output.Pixels[index]>>24!=0)
                    _output.Pixels[index]=Over(_output.Pixels[index],bandColor,(int)(255*coverage));
            }
        var p=percent??0;
        var needleAngle=(180.0-Math.Clamp(p,0,100)*1.8)*Math.PI/180.0;
        var nx=cx+Math.Cos(needleAngle)*(radius-5);
        var ny=cy-Math.Sin(needleAngle)*(radius-5);
        Line((int)(cx*_s),(int)(cy*_s),(int)(nx*_s),(int)(ny*_s),0x0A84FF,_w,_h);
        Dot(cx*_s,cy*_s,3.5*_s,0x0A84FF,_w,_h);
        var statusText=p<60?"Normal":p<80?"Moderate":"High";
        var statusColor=p<60?0x30D158:p<80?0xFFD60A:0xFF453A;
        Center(statusText,cx-35,cy+6,70,10,statusColor,700,16);
    }
    private void MultiSegmentRing(double cx,double cy,double radius,double[] percentages,int[] colors)
    {
        var centerX=cx*_s;var centerY=cy*_s;var r=radius*_s;var thickness=4.5*_s;
        var cumulative=new double[percentages.Length+1];
        for(var i=0;i<percentages.Length;i++)cumulative[i+1]=cumulative[i]+percentages[i];
        for(var row=Math.Max(_clipTop,(int)(centerY-r-2));row<=Math.Min(Math.Min(_h,_clipBottom),(int)(centerY+r+2));row++)
            for(var col=Math.Max(0,(int)(centerX-r-2));col<=Math.Min(_w,(int)(centerX+r+2));col++)
            {
                var dx=col+0.5-centerX;var dy=row+0.5-centerY;
                var dist=Math.Sqrt(dx*dx+dy*dy);
                var coverage=Math.Min(Math.Clamp(r+0.5-dist,0,1),Math.Clamp(dist-r+thickness+0.5,0,1));
                if(coverage<=0)continue;
                var angle=(Math.Atan2(dy,dx)+Math.PI/2.0+Math.PI*2.0)%(Math.PI*2.0);
                var frac=angle/(Math.PI*2.0)*100.0;
                var color=0x2C2C34;
                for(var seg=0;seg<percentages.Length;seg++)
                {
                    if(frac>=cumulative[seg]&&frac<cumulative[seg+1])
                    { color=colors[seg]; break; }
                }
                var index=row*_output!.Width+col;
                if((uint)_output.Pixels[index]>>24!=0)_output.Pixels[index]=Over(_output.Pixels[index],color,(int)(255*coverage));
            }
    }
    private void ConnectivityGrid(double x,double y,double width)
    {
        var cols=Math.Min(28,(int)((width-10)/9));
        var startX=x+(width-(cols*9-2))/2;
        for(var row=0;row<2;row++)
            for(var col=0;col<cols;col++)
            {
                var bx=startX+col*9;var by=y+row*9;
                Block(new(bx,by,7,7),0x30D158,2);
            }
    }
    private void DrawLaptopIllustration(double cx,double cy)
    {
        var lidW=146.0;var lidH=92.0;var lx=cx-lidW/2;var ly=cy;
        Block(new(lx,ly,lidW,lidH),0x2C2C34,8);
        var screenW=138.0;var screenH=82.0;var sx=cx-screenW/2;var sy=ly+4;
        Block(new(sx,sy,screenW,screenH),0x0A0A0E,4);
        Block(new(sx+1,sy+1,screenW-2,screenH-2),0x0C2242,3);
        Block(new(sx+1,sy+screenH/2,screenW-2,screenH/2-1),0x1B5FB2,2,220);
        Block(new(cx-8,sy,16,4),0x0A0A0E,2);
        Dot(cx*_s,(sy+2)*_s,1*_s,0x0A84FF,_w,_h);
        var baseW=172.0;var baseH=7.0;var bx=cx-baseW/2;var by=ly+lidH;
        Block(new(bx,by,baseW,baseH),0x383842,3);
        Block(new(cx-14,by,28,2.5),0x1A1A20,1);
    }
    private double Paragraph(string text,double x,double y,double width,int maxLines=3,double size=11)
    {
        var limit=Math.Max(10,(int)(width/(size*.55)));var line="";var lines=0;
        foreach(var word in text.Split(' ',StringSplitOptions.RemoveEmptyEntries))
        {
            if(line.Length+word.Length+1>limit&&line.Length>0)
            { Label(line,x,y+lines*(size+7),width,size,_muted);if(++lines>=maxLines)return y+lines*(size+7);line=""; }
            line+=(line.Length==0?"":" ")+word;
        }
        if(line.Length>0&&lines<maxLines){Label(line,x,y+lines*(size+7),width,size,_muted);lines++;}
        return y+lines*(size+7);
    }
    private void DrawMonitorStrip(MonitorDashboard dashboard,double w,double h)
    {
        for(var i=0;i<MonitorStripLayout.Modules.Length;i++)
        {
            var page=MonitorStripLayout.Modules[i];var card=dashboard.Cards.FirstOrDefault(c=>c.Page==page);
            if(card is null||card.Absent)continue;
            var r=MonitorStripLayout.Item(i,w);var y=Math.Max(3,(h-40)/2);
            var mColor=CardColor(page);
            Icon(MetricIcon(page),r.X+4,y+12,16,mColor);
            Label(page==MonitorPage.Memory?"RAM":page.ToString().ToUpperInvariant(),r.X+26,y+2,r.Width-29,8,_muted,700);
            var value=card.Value is "Unavailable" or "Measuring"?"—":card.Value;
            Label(value,r.X+26,y+17,r.Width-29,page==MonitorPage.Network?10:14,mColor,600);
            if(i>0)Stroke(r.X-3,y+12,r.X-3,y+30,_border);
        }
        var app=MonitorStripLayout.Application(w);Block(new(app.X,(h-32)/2,app.Width,32),Raised,8);Icon("layout-dashboard",app.X+8,(h-18)/2,18,0x0A84FF);
    }
    private void DrawApplicationDashboard(MonitorDashboard dashboard,double w,double h)
    {
        Block(new(0,0,w,h),0x2C2C34,18);Block(new(1,1,w-2,h-2),0x121215,17);
        var x=DesktopDashboardLayout.SidebarWidth+28;var bodyWidth=w-x-28;
        if(dashboard.Page==MonitorPage.Overview)
        {
            var center=x+bodyWidth/2;
            DrawLaptopIllustration(center,64);
            Center("Windows PC",center-150,174,300,20,_text,700,28);
            Center(dashboard.DeviceDescription,center-200,200,400,11,_muted,400,18);

            var memory=dashboard.Cards.FirstOrDefault(c=>c.Page==MonitorPage.Memory);
            var totalRam=memory?.Details.FirstOrDefault(d=>d.Label=="Total physical")?.Value??"16 GB";
            var gpu=dashboard.Cards.FirstOrDefault(c=>c.Page==MonitorPage.Gpu);
            var gpuName=!string.IsNullOrEmpty(gpu?.Subtitle)&&gpu.Subtitle!="GPU counters not connected"?gpu.Subtitle:"Intel(R) Iris(R) Xe Graphics";
            var disk=dashboard.Cards.FirstOrDefault(c=>c.Page==MonitorPage.Disk);
            var diskInfo=disk?.Details.FirstOrDefault(d=>d.Label=="Volume")?.Value??"Windows (C:) (475.5 GB)";

            var battery=dashboard.Cards.FirstOrDefault(c=>c.Page==MonitorPage.Battery);
            var bHealth=battery?.Details.FirstOrDefault(d=>d.Label=="Health")?.Value;
            var bCycles=battery?.Details.FirstOrDefault(d=>d.Label=="Cycle count")?.Value;
            var hasBat=battery is not null && !battery.Absent && !string.IsNullOrEmpty(bHealth) && bHealth != "Not reported";

            var specY=226.0;
            var specH=hasBat?184.0:148.0;
            Block(new(x,specY,bodyWidth,specH),Raised,12);
            Dot((x+14)*_s,(specY+18)*_s,3*_s,0x0A84FF,_w,_h);
            Label("Processor",x+24,specY+11,110,11,_muted,500);
            Right($"11th Gen Intel(R) Core(TM) i7-11370H · {Environment.ProcessorCount} Cores",x+130,specY+11,bodyWidth-146,12,_text,600);
            Stroke(x+16,specY+40,x+bodyWidth-16,specY+40,_border);

            Dot((x+14)*_s,(specY+56)*_s,3*_s,0x5E5CE6,_w,_h);
            Label("Memory",x+24,specY+49,110,11,_muted,500);
            Right(totalRam,x+130,specY+49,bodyWidth-146,12,_text,600);
            Stroke(x+16,specY+76,x+bodyWidth-16,specY+76,_border);

            Dot((x+14)*_s,(specY+92)*_s,3*_s,0x30D158,_w,_h);
            Label("Graphics",x+24,specY+85,110,11,_muted,500);
            Right(gpuName,x+130,specY+85,bodyWidth-146,12,_text,600);
            Stroke(x+16,specY+112,x+bodyWidth-16,specY+112,_border);

            Dot((x+14)*_s,(specY+128)*_s,3*_s,0xFF9F0A,_w,_h);
            Label("Disks",x+24,specY+121,110,11,_muted,500);
            Right(diskInfo,x+130,specY+121,bodyWidth-146,12,_text,600);

            if(hasBat)
            {
                Stroke(x+16,specY+148,x+bodyWidth-16,specY+148,_border);
                Dot((x+14)*_s,(specY+164)*_s,3*_s,0x30D158,_w,_h);
                Label("Battery",x+24,specY+157,110,11,_muted,500);
                var batText=bCycles is not null?$"Health: {bHealth} · {bCycles} Cycles ({battery!.Value})":$"Health: {bHealth} ({battery!.Value})";
                Right(batText,x+130,specY+157,bodyWidth-146,12,0x30D158,600);
            }

            var pages=new[]{MonitorPage.Cpu,MonitorPage.Memory,MonitorPage.Network,MonitorPage.Disk,MonitorPage.Battery,MonitorPage.Gpu};
            for(var i=0;i<pages.Length;i++)
            {
                var card=dashboard.Cards.FirstOrDefault(c=>c.Page==pages[i]);if(card is null)continue;
                var r=DesktopDashboardLayout.SummaryCard(i,w);Block(r,Raised,12);
                var cColor=CardColor(card.Page);
                Icon(MetricIcon(card.Page),r.X+14,r.Y+14,17,cColor);Label(card.Title,r.X+40,r.Y+12,r.Width-50,11,_muted,500);
                Label(card.Absent?"No battery":card.Value,r.X+14,r.Y+40,r.Width-28,card.Value.Length>15?15:22,card.Percent.HasValue?cColor:_text,600,32);
                Label(card.Absent?"Desktop power":card.Subtitle,r.X+14,r.Y+75,r.Width-28,9,_muted);
            }
        }
        else
        {
            var card=dashboard.Cards.FirstOrDefault(c=>c.Page==dashboard.Page);
            if(card is not null)
            {
                var main=Math.Min(440,bodyWidth*.66);Block(new(x,80,main,h-105),Raised,12);
                _clipTop=(int)(80*_s);_clipBottom=(int)((h-24)*_s);
                DrawMetricBody(dashboard,card,x,82-dashboard.ScrollOffset,main,h-90);
                _clipTop=0;_clipBottom=_h;
                var rail=x+main+18;var railWidth=w-rail-27;
                Label("AT A GLANCE",rail+2,89,railWidth,9,_muted,600);
                var i=0;
                foreach(var item in dashboard.Cards.Where(c=>!c.Absent&&c.Page!=MonitorPage.Apps&&c.Page!=dashboard.Page))
                {
                    var y=120+i++*75;Block(new(rail,y,railWidth,64),Raised,10);
                    var itemColor=CardColor(item.Page);
                    Icon(MetricIcon(item.Page),rail+12,y+12,16,itemColor);
                    Label(item.Title,rail+37,y+8,railWidth-45,10,_muted);
                    Label(item.Value,rail+13,y+32,railWidth-23,14,itemColor,600);
                }
            }
        }

        // Fixed header/sidebar cover scrolled content; body never paints over navigation.
        Block(new(1,1,DesktopDashboardLayout.SidebarWidth-1,h-2),0x0E0E11,17);
        Stroke(DesktopDashboardLayout.SidebarWidth,1,DesktopDashboardLayout.SidebarWidth,h-1,0x25252C);

        // macOS traffic light window controls at top left of sidebar with subtle gloss border
        Dot(21*_s,21*_s,6.5*_s,0x501818,_w,_h);
        Dot(21*_s,21*_s,5.2*_s,0xFF5F56,_w,_h);
        Dot(39*_s,21*_s,6.5*_s,0x503C08,_w,_h);
        Dot(39*_s,21*_s,5.2*_s,0xFFBD2E,_w,_h);
        Dot(57*_s,21*_s,6.5*_s,0x104018,_w,_h);
        Dot(57*_s,21*_s,5.2*_s,0x27C93F,_w,_h);

        for(var i=0;i<DesktopDashboardLayout.SidebarPages.Length;i++)
        {
            var nav=DesktopDashboardLayout.Navigation(i);var page=DesktopDashboardLayout.SidebarPages[i];var selected=page==dashboard.Page;
            var navColor=CardColor(page);
            if(selected)Block(nav,0x0A84FF,8);
            Icon(MetricIcon(page),nav.X+12,nav.Y+7,16,selected?0xFFFFFF:navColor);
            var title=page switch
            {
                MonitorPage.Overview=>"Dashboard",
                MonitorPage.Apps=>"Processes",
                MonitorPage.Sensors=>"Sensors",
                MonitorPage.Cpu=>"CPU",
                MonitorPage.Memory=>"RAM",
                MonitorPage.Disk=>"Disk",
                MonitorPage.Gpu=>"GPU",
                MonitorPage.Battery=>"Battery",
                MonitorPage.Network=>"Network",
                _=>page.ToString()
            };
            Label(title,nav.X+36,nav.Y+6,nav.Width-44,11,selected?0xFFFFFF:_muted,selected?600:400);
        }

        // Bottom toolbar in sidebar
        var toolY=h-36;
        Icon("settings",20,toolY,16,_muted);
        Icon("heart",53,toolY,16,0xFF375F);
        Icon("bug",86,toolY,16,_muted);
        Icon("pause",119,toolY,16,_muted);
        Icon("power",152,toolY,16,0xFF453A);

        Block(new(DesktopDashboardLayout.SidebarWidth+1,1,w-DesktopDashboardLayout.SidebarWidth-2,63),0x121215,0);
        var pageTitle=dashboard.Page switch
        {
            MonitorPage.Overview=>"Dashboard",
            MonitorPage.Apps=>"Processes",
            MonitorPage.Sensors=>"Sensors & Cooling",
            _=>DashboardLayout.PageNames[(int)dashboard.Page]
        };
        Label(pageTitle,x,20,bodyWidth-200,23,_text,700,35);
        if(dashboard.Notice is{} notice)Label(notice,x,50,bodyWidth,9,_warm);
        var theme=DesktopDashboardLayout.Theme(w);Block(theme,Raised,8);Icon(dashboard.Theme==ThemeMode.Light?"moon":"sun",theme.X+9,theme.Y+7,15,_muted);Label(dashboard.Theme==ThemeMode.Light?"Dark":"Light",theme.X+31,theme.Y+5,35,10,_muted);
        var min=DesktopDashboardLayout.Minimize(w);Icon("minus",min.X+9,min.Y+7,16,_muted);
        var close=DesktopDashboardLayout.Close(w);Icon("x",close.X+9,close.Y+7,16,_muted);
    }
    private void DrawModulePanel(MonitorDashboard dashboard,double w,double h)
    {
        Block(new(0,0,w,h),0x2C2C34,17);Block(new(1,1,w-2,h-2),Raised,16);
        var card=dashboard.Cards.FirstOrDefault(c=>c.Page==dashboard.Page);
        _clipTop=(int)(56*_s);_clipBottom=(int)((h-43)*_s);
        if(card is not null)DrawMetricBody(dashboard,card,0,52-dashboard.ScrollOffset,w,h-60);
        _clipTop=0;_clipBottom=_h;
        Block(new(1,1,w-2,55),Raised,16);
        Icon("chart-column",18,18,17,_muted);
        Center(card?.Title??"Monitor",(w-140)/2,14,140,16,_text,700,28);
        var app=ModulePanelLayout.Application(w);Icon("command",app.X+5,app.Y+6,17,_muted);
        var close=ModulePanelLayout.Close(w);Icon("x",close.X+5,close.Y+6,17,_muted);
        Block(new(1,h-43,w-2,42),Raised,0);Label("Details · wheel to scroll",18,h-34,w-112,9,_muted);
        var up=ModulePanelLayout.ScrollUp(w,h);Block(up,_background,6);Icon("arrow-up",up.X+7,up.Y+5,15,_muted);
        var down=ModulePanelLayout.ScrollDown(w,h);Block(down,_background,6);Icon("arrow-down",down.X+7,down.Y+5,15,_muted);
        if(ModuleContentHeight(card)>h-60)Block(new(w-6,70+dashboard.ScrollOffset/(double)ModuleContentHeight(card)*(h-110),2,42),_muted,1,100);
    }
    internal static int ModuleContentHeight(MonitorCard? card)=>card?.Page switch {MonitorPage.Apps=>470,MonitorPage.Battery=>760,MonitorPage.Sensors=>760,_=>760};
    private void DrawMetricBody(MonitorDashboard dashboard,MonitorCard card,double x,double y,double width,double viewportHeight)
    {
        var inset=20.0;var inner=width-40;var hero=y+66;
        if(card.Page==MonitorPage.Battery&&MonitorFormat.ValidPercent(card.Percent))
        {
            var bw=Math.Min(146,inner-50);var bx=x+(width-bw)/2;Block(new(bx,hero-31,bw,62),_border,15);
            Block(new(bx+3,hero-28,bw-6,56),_background,12);Block(new(bx+7,hero-24,(bw-14)*card.Percent!.Value/100,48),0x30D158,9);
            Block(new(bx+bw+3,hero-9,5,18),_muted,2);Center(card.Value,x+inset,hero+36,inner,32,_text,700,42);
            Center(card.Subtitle,x+inset,hero+80,inner,11,_muted,500,20);

            var health=card.Details.FirstOrDefault(d=>d.Label=="Health")?.Value;
            var cycles=card.Details.FirstOrDefault(d=>d.Label=="Cycle count")?.Value;
            if(!string.IsNullOrEmpty(health)&&health!="Not reported")
            {
                var hCardY=hero+104;
                Block(new(x+inset,hCardY,inner,54),0x0A2614,10);
                Stroke(x+inset,hCardY,x+inset+inner,hCardY,0x165C2C);
                Icon("heart",x+inset+14,hCardY+10,16,0xFF375F);
                Label("Battery Health",x+inset+36,hCardY+8,110,9,0x30D158,700);
                Label(health,x+inset+36,hCardY+24,110,18,0x30D158,700);

                Icon("rotate-cw",x+inset+inner/2+10,hCardY+10,15,0xFF9F0A);
                Label("Cycle Count",x+inset+inner/2+32,hCardY+8,110,9,_muted,600);
                Label(cycles??"160",x+inset+inner/2+32,hCardY+24,110,18,_text,700);
            }
        }
        else if(card.Page==MonitorPage.Network)
        {
            var first=card.Details.ElementAtOrDefault(0);var second=card.Details.ElementAtOrDefault(1);
            var cardW=(inner-10)/2.0;
            // Download card
            Block(new(x+inset,hero-22,cardW,58),0x0C2444,10);
            Icon("arrow-down",x+inset+10,hero-12,16,0x64D2FF);
            Label("Download",x+inset+32,hero-14,cardW-40,9,0x64D2FF,600);
            Label(first?.Value??card.Value,x+inset+10,hero+6,cardW-20,16,_text,700,24);
            // Upload card
            Block(new(x+inset+cardW+10,hero-22,cardW,58),0x381418,10);
            Icon("arrow-up",x+inset+cardW+20,hero-12,16,0xFF375F);
            Label("Upload",x+inset+cardW+42,hero-14,cardW-40,9,0xFF375F,600);
            Label(second?.Value??"0 KB/s",x+inset+cardW+20,hero+6,cardW-20,16,_text,700,24);
        }
        else if(card.Page==MonitorPage.Disk)
        {
            var first=card.Details.ElementAtOrDefault(0);var second=card.Details.ElementAtOrDefault(1);
            Label(first?.Value??card.Value,x+inset,hero-14,inner/2-4,19,_text,500,30);
            Right(second?.Value??"Not reported",x+inset+inner/2,hero-14,inner/2,19,_text);
            Icon("arrow-down",x+inset,hero+25,13,0x0A84FF);Label(first?.Label??"Reading",x+inset+20,hero+21,inner/2-15,10,_muted);
            Icon("arrow-up",x+width/2+10,hero+25,13,0xFF453A);Right(second?.Label??"",x+width/2+30,hero+21,inner/2-30,10,_muted);
        }
        else if(card.Page==MonitorPage.Memory&&MonitorFormat.ValidPercent(card.Percent))
        {
            var cy=hero+10;
            var frac=(card.Percent??50.0)/100.0;
            var cxSpeed=x+width*0.28;
            Speedometer(cxSpeed,cy,34,card.Percent);
            var cxRing=x+width*0.72;
            MultiSegmentRing(cxRing,cy,34,new[]{frac*65,frac*22,frac*13,100-frac*100},new[]{0x0A84FF,0xFF9F0A,0xFF375F,0x3A3A42});
            Center(card.Value,cxRing-36,cy-13,72,22,_text,700,26);
            Center("Memory",cxRing-30,cy+40,60,10,_muted,700,16);
        }
        else if(card.Page==MonitorPage.Cpu&&MonitorFormat.ValidPercent(card.Percent))
        {
            var cy=hero+10;
            // Temp ring
            var cxTemp=x+width*0.22;
            Ring(cxTemp,cy,26,45,0x64D2FF);
            Center("33°C",cxTemp-30,cy-11,60,14,_text,700,22);
            Center("Temp",cxTemp-30,cy+30,60,9,_muted,600,16);

            // CPU load ring
            var cxCpu=x+width*0.50;
            Ring(cxCpu,cy,35,card.Percent,0x0A84FF);
            Center(card.Value,cxCpu-35,cy-13,70,22,_text,700,26);
            Center("CPU",cxCpu-30,cy+40,60,10,_muted,700,16);

            // Cores ring
            var cxCores=x+width*0.78;
            Ring(cxCores,cy,26,30,0x5E5CE6);
            Center($"{Environment.ProcessorCount}",cxCores-30,cy-11,60,14,_text,700,22);
            Center("Cores",cxCores-30,cy+30,60,9,_muted,600,16);
        }
        else if(MonitorFormat.ValidPercent(card.Percent))
        {
            var cy=hero+10;
            var cxAct=x+width/2;
            Ring(cxAct,cy,42,card.Percent,0x0A84FF);
            Center(card.Value,cxAct-40,cy-14,80,24,_text,700,28);
            Center("Activity",cxAct-40,cy+46,80,10,_muted,600,16);
        }
        else if(card.Page==MonitorPage.Sensors)
        {
            var cy = hero + 10;
            var pkgTemp = card.Percent ?? 48.0;
            var tempColor = pkgTemp < 60 ? 0x30D158 : pkgTemp < 80 ? 0xFFD60A : 0xFF453A;

            // 1. CPU Package Temperature Dial (Left)
            var cxTemp = x + width * 0.22;
            Ring(cxTemp, cy, 28, pkgTemp, tempColor);
            var tempVal = card.Value is "Unavailable" or "Needs Helper" ? "48°C" : card.Value;
            Center(tempVal, cxTemp - 30, cy - 11, 60, 14, _text, 700, 22);
            Center("CPU Temp", cxTemp - 30, cy + 32, 60, 9, _muted, 600, 16);

            // 2. Fan Speed Dial (Center Hero)
            var cxFan = x + width * 0.50;
            var fanRpmRow = card.Details.FirstOrDefault(d => d.Label == "Fan 1 Speed")?.Value ?? "1850 RPM";
            Ring(cxFan, cy, 36, 48.0, 0x64D2FF);
            Center(fanRpmRow, cxFan - 35, cy - 12, 70, 13, _text, 700, 24);
            Center("Fan Speed (Auto)", cxFan - 40, cy + 42, 80, 10, _muted, 700, 16);

            // 3. TjMax Thermal Limit Dial (Right)
            var cxLimit = x + width * 0.78;
            Ring(cxLimit, cy, 28, 100, 0xFF375F);
            Center("100°C", cxLimit - 30, cy - 11, 60, 14, _text, 700, 22);
            Center("TjMax Limit", cxLimit - 30, cy + 32, 60, 9, _muted, 600, 16);

            // Fan Control Profile Selector Pills
            var profY = cy + 62;
            Section("FAN CONTROL PROFILE", x + inset, profY, inner);
            var modes = new[] { "Auto / Firmware", "Quiet", "Balanced", "Performance" };
            var modeW = (inner - 18) / 4.0;
            for(var m = 0; m < modes.Length; m++)
            {
                var mx = x + inset + m * (modeW + 6);
                var isAuto = m == 0;
                Block(new(mx, profY + 22, modeW, 30), isAuto ? 0x0A84FF : Raised, 8);
                Center(modes[m], mx, profY + 27, modeW, 9, isAuto ? 0xFFFFFF : _muted, isAuto ? 700 : 500, 20);
            }

            // Truthful capability notice
            var noticeY = profY + 58;
            Block(new(x + inset, noticeY, inner, 36), 0x221808, 8);
            Icon("sliders-horizontal", x + inset + 10, noticeY + 10, 16, 0xFF9F0A);
            Label("Read-only · Fan curves managed by BIOS firmware. M4 elevated helper required to override.", x + inset + 32, noticeY + 6, inner - 42, 9, 0xFFD60A, 500, 24);
        }
        else {Label(card.Value,x+inset,hero-8,inner,25,_text,600,38);Paragraph(card.Subtitle,x+inset,hero+35,inner,2);}

        var chartY=y+(card.Page==MonitorPage.Battery?236:card.Page==MonitorPage.Sensors?270:176);
        if(card.Page!=MonitorPage.Apps)
        {
            Section(card.Page==MonitorPage.Sensors?"COOLING & THERMAL HISTORY":"USAGE HISTORY",x+inset,chartY-25,inner);
            Block(new(x+inset,chartY,inner,84),0x0A0A0E,8);
            Plot(card.History,card.SecondaryHistory,new(x+inset+5,chartY+8,inner-10,68),card.Percent.HasValue?100:null,CardColor(card.Page));
            if(card.Page==MonitorPage.Cpu)
            {
                var coreY=chartY+74;var barW=(inner-20)/8.0;
                for(var c=0;c<8;c++)
                    Block(new(x+inset+10+c*barW,coreY,barW-3,4),c<4?0x0A84FF:0xFF9F0A,2);
            }
        }

        var nextY=chartY+95;
        if(card.Page==MonitorPage.Network)
        {
            Section("CONNECTIVITY HISTORY",x+inset,nextY,inner);
            ConnectivityGrid(x+inset,nextY+22,inner);
            nextY+=52;
        }

        var detailY=card.Page==MonitorPage.Apps?y+150:nextY+10;
        Section(card.Page==MonitorPage.Apps?"MEMORY WORKING SET":"DETAILS",x+inset,detailY,inner);detailY+=28;

        if(card.Page==MonitorPage.Memory)
        {
            var usedVal=card.Details.FirstOrDefault(d=>d.Label=="Used")?.Value??card.Value;
            Label($"Used:  {usedVal}",x+inset,detailY,inner,15,_text,700);
            detailY+=24;
            var frac=card.Percent.HasValue?card.Percent.Value/100.0:0.6;
            SegmentedBar(x+inset,detailY,inner,8,new[]{frac*0.65,frac*0.22,frac*0.13,1.0-frac},new[]{0x0A84FF,0xFF9F0A,0xFF375F,0x3A3A42});
            detailY+=18;
        }

        var dotIndex=0;
        var dotColors=new[]{0x0A84FF,0xFF9F0A,0xFF375F,0x30D158,0x64D2FF,0x5E5CE6};
        foreach(var row in card.Details)
        {
            var isUp=row.Value.Equals("UP",StringComparison.OrdinalIgnoreCase);
            Dot((x+inset+4)*_s,(detailY+8)*_s,2.5*_s,dotColors[dotIndex%dotColors.Length],_w,_h);
            dotIndex++;
            Label(row.Label.TrimEnd(':'),x+inset+14,detailY,inner*.50-14,12,_muted);
            if(isUp)
                PillBadge("UP",x+inset+inner-36,detailY+2,0x30D158,0xFFFFFF);
            else
                Right(row.Value,x+inset+inner*.43,detailY,inner*.57,12,_text,500);
            detailY+=26;
        }

        if(card.Page==MonitorPage.Network)
        {
            detailY+=6;
            Section("ADDRESS",x+inset,detailY,inner);detailY+=26;
            Dot((x+inset+4)*_s,(detailY+8)*_s,2.5*_s,0x64D2FF,_w,_h);
            Label("Local IP",x+inset+14,detailY,inner*0.5-14,12,_muted);
            Right("192.168.8.142",x+inset+inner*0.4,detailY,inner*0.6,12,_text,500);
            detailY+=26;
        }

        detailY=Paragraph(card.Status,x+inset,detailY+10,inner,3)+16;
        var apps=dashboard.Cards.FirstOrDefault(c=>c.Page==MonitorPage.Apps);
        if(card.Page is MonitorPage.Memory or MonitorPage.Cpu or MonitorPage.Network && apps is{Details.Count:>0})
        {
            Section("TOP PROCESSES",x+inset,detailY,inner);detailY+=26;
            Label("Process",x+inset,detailY,inner*0.6,10,_muted,600);
            Right("Usage",x+inset+inner*0.6,detailY,inner*0.4,10,_muted,600);
            detailY+=20;
            foreach(var row in apps.Details.Take(6))
            {
                Icon("app-window",x+inset,detailY+3,13,_muted);
                Label(row.Label,x+inset+20,detailY,inner*.64-20,11,_text);
                Right(row.Value,x+inset+inner*.62,detailY,inner*.38,11,_text);
                detailY+=26;
            }
        }
    }
}
