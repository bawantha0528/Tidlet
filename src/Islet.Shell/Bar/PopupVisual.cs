using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media;
using Islet.Core.Bar;
using Islet.Core.Popups;
namespace Islet.Shell.Bar;
internal sealed partial class PopupVisual:UIElement
{
    private readonly DrawingVisual _drawing=new();private readonly BarModule _module;private readonly double _height;
    private readonly Action _taskManager,_close;private readonly bool _diagnostic;private readonly HeaderAction _taskButton,_closeButton;
    private readonly Action? _openDashboard;
    private readonly HeaderAction? _dashboardButton;
    private PopupSample? _sample;private SampleRing? _history,_averages;private double _now,_scale,_scroll,_contentBottom;
    private string _accessible="Measuring Windows readings";
    private bool _useBits;
    internal bool Fahrenheit { get; init; }
    private bool _previous;
    private bool IsMeasuring=>_sample?.WarmingUp==true;
    public void ShowPrevious(PopupSample sample){if(_previous&&ReferenceEquals(_sample,sample))return;_sample=sample;_previous=true;_accessible=MetricLoading.Previous;Redraw(_scale);}
    public void ShowMeasuring(PopupSample sample)
    {
        var partial=MetricLoading.HasPartialDetails(_module,sample)?sample:null;
        if(ReferenceEquals(_sample,partial))return;
        _sample=partial;
        _previous=false;
        _accessible=MetricLoading.For(_module).Title;
        Redraw(_scale);
    }
    public void SetUnits(bool useBits){if(_useBits==useBits)return;_useBits=useBits;_texts.Clear();Redraw(_scale);}
    // One last text value per drawing position; dynamic values cannot grow the cache.
    private readonly Dictionary<(double X,double Y,double Size,Brush Color,bool Bold,bool Icons,double? Width), (string Value,FormattedText Text)> _texts=[];
    private readonly RectangleGeometry _outline,_viewport;
    private readonly PopupPalette _palette;
    private readonly PopupPalette.ChartStyle _chart;
    private static readonly Typeface Face=new(new FontFamily("Segoe UI Variable, Segoe UI"),FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
    private static readonly Typeface Bold=new(new FontFamily("Segoe UI Variable, Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal);
    private static readonly Typeface Icons=new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private Brush Blue=>_palette.Blue;
    private Brush Red=>_palette.Red;
    private Brush Orange=>_palette.Orange;
    private Brush Cyan=>_palette.Cyan;
    private Brush Green=>_palette.Green;
    private Brush Gray=>_palette.Track;
    private Brush Dim=>_palette.Muted;
    private Brush TextBrush=>_palette.Text;
    private Brush ChartBackground=>_palette.Panel;
    private Pen SectionPen=>_palette.Divider;
    private Pen RingPen=>_palette.RingTrack;
    private static readonly int[] AverageMinutes=[1,5,15];
    public PopupVisual(BarModule module,double height,double scale,Action taskManager,Action close,bool diagnostic=false)
        :this(module,height,scale,taskManager,close,diagnostic,BarTheme.Dark){}
    internal PopupVisual(BarModule module,double height,double scale,Action taskManager,Action close,bool diagnostic,BarTheme theme,Action? openDashboard=null)
    {
        _module=module;_palette=PopupPalette.For(theme);_chart=_palette.Chart(module);_height=height;_scale=scale;_taskManager=taskManager;_close=close;_diagnostic=diagnostic;Focusable=true;AddVisualChild(_drawing);
        _accessible=MetricLoading.For(module).Title;
        _outline=new RectangleGeometry(new Rect(0,0,320,height),9,9);_outline.Freeze();
        _viewport=new RectangleGeometry(new Rect(8,43,304,Math.Max(1,height-51)));_viewport.Freeze();
        _taskButton=new("Open Task Manager",taskManager){Focusable=true};_closeButton=new("Close details",close){Focusable=true};AddVisualChild(_taskButton);AddVisualChild(_closeButton);
        _taskButton.Measure(new Size(28,32));_taskButton.Arrange(new Rect(8,4,28,32));_closeButton.Measure(new Size(28,32));_closeButton.Arrange(new Rect(284,4,28,32));
        _openDashboard=openDashboard;
        if(openDashboard is not null)
        {
            _dashboardButton=new("Open Dashboard",openDashboard){Focusable=true};
            AddVisualChild(_dashboardButton);
            _dashboardButton.Measure(new Size(28,32));
            _dashboardButton.Arrange(new Rect(251,4,28,32));
        }
        MouseLeftButtonUp+=(_,e)=>
        {
            var p=e.GetPosition(this);
            if(p.Y>=40)return;
            if(p.X<42)_taskManager();
            else if(p.X>280)_close();
            else if(p.X>=251&&p.X<279)_openDashboard?.Invoke();
        };
        MouseWheel+=(_,e)=>{Scroll(-e.Delta/4.0);e.Handled=true;};
        KeyDown+=(_,e)=>
        {
            if(e.Key==Key.Escape){_close();e.Handled=true;}
            if(e.Key is Key.PageDown or Key.PageUp){Scroll(e.Key==Key.PageDown?_height/2:-_height/2);e.Handled=true;}
            if(e.Key==Key.Tab)
            {
                var focused=Keyboard.FocusedElement;
                var backwards=(Keyboard.Modifiers&ModifierKeys.Shift)!=0;
                if(focused==_taskButton)(backwards?_closeButton:_dashboardButton??_closeButton).Focus();
                else if(focused==_closeButton)(backwards?_dashboardButton??_taskButton:_taskButton).Focus();
                else if(_dashboardButton is not null&&focused==_dashboardButton)(backwards?_taskButton:_closeButton).Focus();
                else (backwards?_closeButton:_taskButton).Focus();
                e.Handled=true;
            }
            if(e.Key is Key.Enter or Key.Space)
            {
                var focused=Keyboard.FocusedElement;
                if(focused==_taskButton)_taskManager();
                else if(focused==_closeButton)_close();
                else if(_dashboardButton is not null&&focused==_dashboardButton)_openDashboard?.Invoke();
                e.Handled=true;
            }
        };
        Redraw(scale);
    }
    protected override int VisualChildrenCount=>_dashboardButton is null?3:4;
    protected override Visual GetVisualChild(int index)=>index switch{0=>_drawing,1=>_taskButton,2=>_closeButton,3 when _dashboardButton is not null=>_dashboardButton,_=>throw new ArgumentOutOfRangeException(nameof(index))};
    protected override AutomationPeer OnCreateAutomationPeer()=>new PopupPeer(this);
    public void Update(PopupSample sample,SampleRing history,SampleRing averages,double now)
    {
        if(sample.WarmingUp)return;
        _previous=false;
        _sample=sample;_history=history;_averages=averages;_now=now;
        TrackModule(sample,now);
        _accessible=_module==BarModule.Cpu?$"CPU {Percent(sample.Cpu)}. {sample.Explanation}":_module==BarModule.Ram?$"RAM {Percent(sample.Memory?.UsedPercent)}. {sample.Memory?.PressureName??"Unavailable"}. {sample.Explanation}":$"{_module} details. {sample.Explanation}";
        Redraw(_scale);
    }
    public void Redraw(double scale)
    {
        Draw(scale);
        if(_scroll<=MaxScroll)return;
        _scroll=MaxScroll;
        Draw(scale);
    }
    private double MaxScroll=>Math.Max(0,_contentBottom-_viewport.Rect.Bottom);
    private void Scroll(double delta)
    {
        var next=Math.Clamp(_scroll+delta,0,MaxScroll);
        if(next==_scroll)return;
        _scroll=next;
        Redraw(_scale);
    }
    private bool Intersects(double y,double height)=>y+height>_viewport.Rect.Top+_scroll&&y<_viewport.Rect.Bottom+_scroll;
    private void Draw(double scale)
    {
        if(_scale!=scale)_texts.Clear();_scale=scale;using var d=_drawing.RenderOpen();
        d.PushClip(_outline);
        d.DrawRoundedRectangle(_palette.Surface,_palette.Border,new Rect(.5,.5,319,_height-1),8.5,8.5);
        var title=_module switch{BarModule.Cpu=>"CPU",BarModule.Ram=>"RAM",BarModule.Gpu=>"GPU",_=>_module.ToString()};
        Text(d,"\uE9D9",24,13,14,_chart.Accent,center:true,icons:true);Text(d,title,45,8,17,TextBrush,bold:true,maxWidth:200);Text(d,"\uE711",297,14,11,Dim,center:true,icons:true);
        if(_dashboardButton is not null)
            for(var i=0;i<4;i++)
                d.DrawRoundedRectangle(TextBrush,null,new Rect(259+i%2*7,13+i/2*7,5,5),1,1);
        d.DrawLine(SectionPen,new Point(14,41),new Point(306,41));
        if(_previous)Text(d,MetricLoading.Previous,160,30,8,Dim,center:true);
        else if(IsMeasuring)Text(d,MetricLoading.For(_module).Title,160,30,8,Dim,center:true);
        else if(_diagnostic)Text(d,"Measurement running · tray Exit stops it",160,30,8,Dim,center:true);
        d.PushClip(_viewport);d.PushTransform(new TranslateTransform(0,-_scroll));
        if(_sample is{}s){if(_module==BarModule.Cpu)Cpu(d,s);else if(_module==BarModule.Ram)Ram(d,s);else DrawModule(d,s);}else Loading(d);
        d.Pop();d.Pop();
        if(MaxScroll>0&&_height>70)d.DrawRoundedRectangle(Dim,null,new Rect(314,47+(_height-70)*Math.Min(1,_scroll/MaxScroll),2,20),1,1);
        d.Pop();
    }
    private void Loading(DrawingContext d)
    {
        var (title,detail)=MetricLoading.For(_module);
        Hero(d,49,116);
        Text(d,title,27,69,15,TextBrush,bold:true,maxWidth:266);
        Text(d,detail,27,102,11,Dim,maxWidth:266);
        _contentBottom=177;
        if(_module is BarModule.Cpu or BarModule.Ram or BarModule.Gpu or BarModule.Disk or BarModule.Network)
        {
            Hero(d,180,76);
            Text(d,MetricLoading.CollectingHistory,160,209,11,Dim,center:true);
            _contentBottom=268;
        }
    }
    private void Cpu(DrawingContext d,PopupSample s)
    {
        if(s.Cpu is not (>=0 and <=100)){Unavailable(d,s.Explanation);return;}
        Hero(d,49,104);
        Ring(d,65,101,31,s.Cpu,Percent(s.Cpu));
        Text(d,"Total CPU usage",113,64,12,TextBrush,bold:true);
        if(s.FrequencyGhz is >0 and <double.PositiveInfinity && s.MaxGhz is >0 and <double.PositiveInfinity)
        {
            MetricBar(d,"Clock speed",$"{s.FrequencyGhz:0.00} GHz",Math.Min(100,s.FrequencyGhz.Value/s.MaxGhz.Value*100),91,_chart.Accent);
            Text(d,$"Reported maximum · {s.MaxGhz:0.00} GHz",113,123,9.5,Dim);
        }
        else if(s.System is >=0 and <=100)
            MetricBar(d,"System CPU",Percent(s.System),s.System.Value,91,Red);
        else if(s.FrequencyGhz is >0 and <double.PositiveInfinity)
            Text(d,$"Clock speed · {s.FrequencyGhz:0.00} GHz",113,94,10.5,Dim);
        double y=161;Section(d,"USAGE HISTORY",ref y);Chart(d,y);y+=94;
        if(s.Cores.Any(v=>double.IsFinite(v)&&v is>=0 and<=100))
        {
            var maxClass=s.Classes.Length>0?s.Classes.Max():-1;var hybrid=s.Classes.Length==s.Cores.Length&&s.Classes.All(c=>c>=0)&&s.Classes.Distinct().Count()>1;double width=290.0/s.Cores.Length;
            for(int i=0;Intersects(y,30)&&i<s.Cores.Length;i++)
            {
                var x=15+i*width;var barWidth=Math.Max(width*.55,width-3);
                d.DrawRoundedRectangle(Gray,null,new Rect(x,y,barWidth,30),2,2);
                if(s.Cores[i] is>0 and<=100)
                {var h=30*s.Cores[i]/100;var color=hybrid&&s.Classes[i]!=maxClass?Cyan:Blue;d.DrawRoundedRectangle(color,null,new Rect(x,y+30-h,barWidth,h),2,2);}
            }
            y+=42;
        }
        Section(d,"DETAILS",ref y);Row(d,"System",Percent(s.System),Red,ref y,s.System.HasValue);Row(d,"User",Percent(s.User),Blue,ref y,s.User.HasValue);Row(d,"Idle",Percent(s.Idle),Gray,ref y,s.Idle.HasValue);
        if(s.Classes.Length==s.Cores.Length&&s.Classes.Length>0&&s.Classes.All(c=>c>=0)&&s.Classes.Distinct().Count()>1)
        {
            var max=s.Classes.Max();
            double p=0,e=0;int pCount=0,eCount=0;
            for(var i=0;i<s.Cores.Length;i++)
            {
                if(s.Cores[i] is not (>=0 and <=100))continue;
                if(s.Classes[i]==max){p+=s.Cores[i];pCount++;}else{e+=s.Cores[i];eCount++;}
            }
            if(pCount>0)Row(d,"P-cores",Percent(p/pCount),Blue,ref y);
            if(eCount>0)Row(d,"E-cores",Percent(e/eCount),Cyan,ref y);
        }
        Row(d,"Uptime",$"{(int)s.Uptime.TotalDays}d {s.Uptime.Hours}h {s.Uptime.Minutes}m",null,ref y);
        if(s.Capabilities?.GetValueOrDefault("temperature")?.Availability==Islet.Core.Capabilities.CapabilityAvailability.NeedsHelper)
            Row(d,"CPU temperature","Sensor helper required",null,ref y);
        y+=6;
        if(_averages is{}averages)
        {
            Section(d,"AVERAGE USAGE",ref y);
            foreach(var minutes in AverageMinutes)
            {
                var avg=averages.Average(_now,minutes*60);Row(d,minutes+" minute"+(minutes==1?"":"s")+(avg.Complete?"":" · warming"),Percent(avg.Percent),null,ref y);
            }
        }
        if(s.FrequencyGhz is{}ghz&&double.IsFinite(ghz)&&ghz>0){y+=4;Section(d,"FREQUENCY",ref y);Row(d,"Reported average",$"{ghz*1000:0} MHz",null,ref y);}
        y+=5;Processes(d,s,ref y);_contentBottom=y+12;
    }
    private void Ram(DrawingContext d,PopupSample s)
    {
        if(s.Memory is not{Valid:true}m){Text(d,"RAM readings unavailable",160,100,12,Dim,center:true);Text(d,s.Explanation,16,135,11,Dim,maxWidth:288);_contentBottom=200;return;}
        var memoryPanelHeight=m.CommitValid?116:92;
        Hero(d,49,memoryPanelHeight);
        Ring(d,65,49+memoryPanelHeight/2.0,31,m.UsedPercent,Percent(m.UsedPercent));
        Text(d,"Physical memory",113,61,12,TextBrush,bold:true);
        Text(d,$"{Bytes(m.Total-m.Available)} used of {Bytes(m.Total)}",113,79,10.5,Dim,maxWidth:181);
        if(m.CommitValid)
        {
            var committed=100.0*m.Committed!.Value/m.CommitLimit!.Value;
            MetricBar(d,"Committed memory",Percent(committed),committed,100,Red);
        }
        Text(d,"Memory pressure · "+m.PressureName,113,49+memoryPanelHeight-23,10.5,Dim,maxWidth:181);
        double y=57+memoryPanelHeight;Section(d,"USAGE HISTORY",ref y);Chart(d,y);y+=98;Section(d,"DETAILS",ref y);
        Row(d,"Used",Bytes(m.Total-m.Available),Blue,ref y);
        d.DrawRoundedRectangle(Gray,null,new Rect(15,y,290,7),3,3);d.DrawRoundedRectangle(_chart.Accent,null,new Rect(15,y,290*m.UsedPercent/100,7),3,3);y+=20;
        Row(d,"Available",Bytes(m.Available),Gray,ref y);Row(d,"System cache*",Bytes(m.Cache),Cyan,ref y,m.Cache.HasValue);
        Row(d,"Committed",Bytes(m.Committed),Orange,ref y,m.CommitValid);Row(d,"Commit limit",Bytes(m.CommitLimit),null,ref y,m.CommitValid);
        Row(d,"Page file in use",Bytes(m.PageFileUsed),null,ref y,m.PageFileUsed.HasValue);Row(d,"Page file total",Bytes(m.PageFileTotal),null,ref y,m.PageFileTotal.HasValue);
        Text(d,"*Cache overlaps Used/Available; it is not added.",15,y+1,9.2,Dim);y+=23;
        Processes(d,s,ref y);_contentBottom=y+12;
    }
    private void Processes(DrawingContext d,PopupSample s,ref double y)
    {
        Section(d,"TOP PROCESSES",ref y);Text(d,"Process",31,y,10,Dim);Text(d,_module==BarModule.Cpu?"CPU":"Memory",305,y,10,Dim,right:true);y+=22;
        if(s.Processes.Length==0){Text(d,s.Explanation,15,y,10.5,Dim,maxWidth:288);y+=32;return;}
        foreach(var process in s.Processes)
        {
            Text(d,"\uE7C3",15,y+1,11,Dim,icons:true);Text(d,process.Name,34,y,11.5,TextBrush,maxWidth:187);
            Text(d,_module==BarModule.Cpu?Percent(process.CpuPercent):Bytes(process.WorkingSet),305,y,11.5,TextBrush,bold:true,right:true);y+=23;
        }
    }
    private void Chart(DrawingContext d,double y)
    {
        if(!Intersects(y,82))return;
        d.DrawRoundedRectangle(ChartBackground,_palette.InsetBorder,new Rect(14,y,292,82),8,8);
        for(int line=1;line<=3;line++)d.DrawLine(SectionPen,new Point(16,y+line*20),new Point(304,y+line*20));
        Text(d,"100%",20,y+4,8.5,Dim);Text(d,"0%",20,y+67,8.5,Dim);
        if(_history is null||_history.Count<2){Text(d,"Collecting a 2-minute history",160,y+36,10,Dim,center:true);return;}
        var geometry=new StreamGeometry();using(var c=geometry.Open())
        {
            bool started=false;Point last=default;
            for(int i=0;i<_history.Count;i++)
            {
                var s=_history.At(i);var age=_now-s.Seconds;if(age>120)continue;
                last=new Point(16+288*(1-age/120),y+80-76*s.Value/100);
                if(!started){c.BeginFigure(new Point(last.X,y+81),true,true);c.LineTo(last,false,false);started=true;}
                else c.LineTo(last,true,false);
            }
            if(started)c.LineTo(new Point(last.X,y+81),false,false);
        }
        geometry.Freeze();d.DrawGeometry(_chart.Fill,_chart.Line,geometry);
    }
    private void Hero(DrawingContext d,double y,double height)=>d.DrawRoundedRectangle(_palette.Panel,_palette.InsetBorder,new Rect(14,y,292,height),9,9);
    private void Ring(DrawingContext d,double x,double y,double radius,double? percent,string value)
    {
        if(!Intersects(y-radius-4,2*radius+8))return;
        d.DrawEllipse(_palette.Panel,RingPen,new Point(x,y),radius,radius);if(percent is{}p&&double.IsFinite(p)&&p>0&&p<=100)Arc(d,x,y,radius,-90,Math.Min(359.99,p*3.6),_chart.Accent,7);
        var text=GetText(value,x,y,18,TextBrush,true,false,null);
        if(text.Width>2*radius-14)
            text=GetText(value,x,y,11,TextBrush,true,false,null);
        d.DrawText(text,MonitorText.Centered(text,new Point(x,y),_scale));
    }
    private void MetricBar(DrawingContext d,string label,string value,double percent,double y,Brush fill)
    {
        const double left=113,width=181;
        if(!Intersects(y,26))return;
        Text(d,label,left,y,11,TextBrush);
        Text(d,value,294,y,11,TextBrush,bold:true,right:true);
        d.DrawRoundedRectangle(Gray,null,new Rect(left,y+17,width,9),3,3);
        if(percent>0)d.DrawRoundedRectangle(fill,null,new Rect(left,y+17,width*percent/100,9),3,3);
    }
    private static void Arc(DrawingContext d,double x,double y,double r,double start,double sweep,Brush color,double thickness)
    {
        Point P(double a)=>new(x+r*Math.Cos(a*Math.PI/180),y+r*Math.Sin(a*Math.PI/180));
        var figure=new PathFigure{StartPoint=P(start),IsClosed=false};figure.Segments.Add(new ArcSegment(P(start+sweep),new Size(r,r),0,sweep>180,SweepDirection.Clockwise,true));
        var geometry=new PathGeometry([figure]);geometry.Freeze();d.DrawGeometry(null,new Pen(color,thickness){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},geometry);
    }
    private void Section(DrawingContext d,string name,ref double y)
    {
        if(Intersects(y,24))
        {
            var text=GetText(name,15,y,10,TextBrush,true,false,280);
            d.DrawText(text,MonitorText.PixelAligned(15,y,_scale));
            d.DrawLine(SectionPen,new Point(Math.Min(305,24+text.Width),y+7),new Point(305,y+7));
        }
        y+=24;
    }
    private void Row(DrawingContext d,string name,string value,Brush? dot,ref double y,bool show=true)
    {
        if(!show)return;
        if(Intersects(y,22))
        {
            var x=15.0;if(dot is not null){d.DrawRoundedRectangle(dot,null,new Rect(15,y+4,6,6),2,2);x=28;}
            Text(d,name,x,y,11.5,TextBrush,maxWidth:142);Text(d,value,305,y,11.5,TextBrush,bold:true,right:true,maxWidth:140);
        }
        y+=22;
    }
    private void Text(DrawingContext d,string value,double x,double y,double size,Brush color,bool bold=false,bool center=false,bool right=false,bool icons=false,double? maxWidth=null)
    {
        if(y>=43&&!Intersects(y,size*3))return;
        var t=GetText(value,x,y,size,color,bold,icons,maxWidth);
        d.DrawText(t,MonitorText.PixelAligned(right?x-t.Width:center?x-t.Width/2:x,y,_scale));
    }
    private FormattedText GetText(string value,double x,double y,double size,Brush color,bool bold,bool icons,double? maxWidth)
    {
        var key=(x,y,size,color,bold,icons,maxWidth);
        if(_texts.TryGetValue(key,out var cached)&&cached.Value==value)return cached.Text;
        var text=MonitorText.Format(value,icons?Icons:bold?Bold:Face,size,color,_scale);
        if(maxWidth is{}width){text.MaxTextWidth=width;text.MaxLineCount=2;text.Trimming=TextTrimming.CharacterEllipsis;}
        // Capability changes can move rows. Bound lifetime retention even across every layout.
        if(_texts.Count>=128)_texts.Clear();_texts[key]=(value,text);return text;
    }
    private static string Percent(double? p)=>p is{}v&&double.IsFinite(v)&&v is>=0 and<=100?$"{v:0}%":"—";
    private static string Bytes(ulong? bytes)=>bytes is{}v?(v>=1073741824?$"{v/1073741824.0:0.00} GiB":v>=1048576?$"{v/1048576.0:0.0} MiB":$"{v/1024.0:0} KiB"):"—";
    private sealed class PopupPeer(PopupVisual owner):UIElementAutomationPeer(owner)
    {
        protected override string GetNameCore()=>owner._accessible;protected override string GetClassNameCore()=>"TidletMetricDetails";
        protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Pane;
    }
    private sealed class HeaderAction(string name,Action action):UIElement
    {
        public HeaderAction():this("",()=>{}){}
        protected override AutomationPeer OnCreateAutomationPeer()=>new HeaderPeer(this,name,action);
        private sealed class HeaderPeer(HeaderAction owner,string name,Action action):UIElementAutomationPeer(owner),IInvokeProvider
        {
            protected override string GetNameCore()=>name;protected override string GetClassNameCore()=>"TidletHeaderAction";protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Button;
            public override object? GetPattern(PatternInterface patternInterface)=>patternInterface==PatternInterface.Invoke?this:base.GetPattern(patternInterface);
            public void Invoke()=>owner.Dispatcher.BeginInvoke(action);
        }
    }
}







