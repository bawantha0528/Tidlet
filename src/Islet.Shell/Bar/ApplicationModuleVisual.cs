using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using Islet.Core.Bar;
using Islet.Core.Popups;
namespace Islet.Shell.Bar;

/// <summary>Wide application content, independent of compact top-bar popup layout.</summary>
internal sealed partial class ApplicationModuleVisual:UIElement
{
    private readonly DrawingVisual _drawing=new();private readonly BarModule _module;private readonly double _width,_height;
    private double _scale,_scroll,_bottom,_now;private PopupSample? _sample;private SampleRing? _history,_averages;
    public PopupDetails Details{get;private set;}
    private bool _useBits;
    private bool _fahrenheit;
    private int _thresholdPercent=85;
    private bool _previous;
    private bool IsMeasuring=>_sample?.WarmingUp==true;
    public void ShowPrevious(PopupSample sample){if(_previous&&ReferenceEquals(_sample,sample))return;_sample=sample;_previous=true;Redraw(_scale);}
    public void ShowMeasuring(PopupSample sample)
    {
        var partial=MetricLoading.HasPartialDetails(_module,sample)?sample:null;
        if(ReferenceEquals(_sample,partial))return;
        _sample=partial;
        _previous=false;
        Redraw(_scale);
    }
    public void SetUnits(bool useBits){if(_useBits==useBits)return;_useBits=useBits;_labels.Clear();Redraw(_scale);}
    public void SetTemperatureUnit(bool fahrenheit){if(_fahrenheit==fahrenheit)return;_fahrenheit=fahrenheit;_labels.Clear();Redraw(_scale);}
    public void SetWarningThreshold(int percent){if(_thresholdPercent==percent)return;_thresholdPercent=percent;Redraw(_scale);}
    private Brush UsageBrush(BarModule module,double? percent)=>_palette.Metrics.Status(BarThresholds.UsageLevel(module,percent,_thresholdPercent),Blue);
    private readonly RectangleGeometry _clip;
    private readonly Dictionary<(double,double,double,Brush,bool,TextAlignment,bool,double?), (string,FormattedText)> _labels=[];
    private static readonly Typeface Face=new("Segoe UI Variable, Segoe UI"),Bold=new(new FontFamily("Segoe UI Variable, Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal);
    private readonly DashboardPalette _palette;
    private Brush Text=>_palette.Text;
    private Brush Dim=>_palette.Muted;
    private Brush Blue=>_palette.Metrics.Blue;
    private Brush Red=>_palette.Metrics.Red;
    private Brush Cyan=>_palette.Metrics.Cyan;
    private Brush Gray=>_palette.Metrics.Track;
    private Brush Green=>_palette.Metrics.Green;
    private Brush Orange=>_palette.Metrics.Orange;
    private Brush Track=>_palette.Metrics.Track;
    private Brush Fill=>_palette.ChartFill;
    private Pen Grid=>_palette.Grid;
    private Pen Line=>_palette.ChartLine;
    private static Brush Frozen(Color c){var b=new SolidColorBrush(c);b.Freeze();return b;}
    private static Pen Pen(Brush b,double width){var p=new Pen(b,width);p.Freeze();return p;}
    public ApplicationModuleVisual(BarModule module,double width,double height,double scale)
        :this(module,width,height,scale,DashboardPalette.For(BarTheme.Dark)){}
    internal ApplicationModuleVisual(BarModule module,double width,double height,double scale,DashboardPalette palette)
    {
        _module=module;_width=width;_height=height;_scale=scale;_palette=palette;Focusable=true;AddVisualChild(_drawing);
        _clip=new(new Rect(0,0,width,height));_clip.Freeze();
        MouseWheel+=(_,e)=>{Scroll(-e.Delta/3.0);e.Handled=true;};
        MouseLeftButtonDown+=(_,_)=>Focus();
        KeyDown+=(_,e)=>{if(e.Key is Key.PageDown or Key.PageUp){Scroll(e.Key==Key.PageDown?height/2:-height/2);e.Handled=true;}};
        Redraw(scale);
    }
    protected override int VisualChildrenCount=>1;protected override Visual GetVisualChild(int i)=>i==0?_drawing:throw new ArgumentOutOfRangeException(nameof(i));
    protected override AutomationPeer OnCreateAutomationPeer()=>new PagePeer(this);
    public void Scroll(double delta)
    {
        var previous=Details;_scroll=Math.Clamp(_scroll+delta,0,Math.Max(0,_bottom-_height));
        foreach(var key in _labels.Keys.Where(k=>!Intersects(k.Item2,k.Item3*3)).ToArray())_labels.Remove(key);
        Redraw(_scale);
        if(previous!=Details&&_sample is{}sample)
        {_sample=sample with{Cores=Enumerable.Repeat(double.NaN,sample.Cores.Length).ToArray(),Processes=[]};Redraw(_scale);}
    }
    public void Update(PopupSample sample,SampleRing history,SampleRing averages,double now){if(sample.WarmingUp)return;_previous=false;_sample=sample;_history=history;_averages=averages;_now=now;TrackModule(sample,now);Redraw(_scale);}
    public void Redraw(double scale)
    {
        if(_scale!=scale)_labels.Clear();_scale=scale;
        Details=PopupDetails.None;
        using var d=_drawing.RenderOpen();d.PushClip(_clip);d.PushTransform(new TranslateTransform(0,-_scroll));
        if(_sample is null)Loading(d);
        else if(_module==BarModule.Cpu)Cpu(d);else if(_module==BarModule.Ram)Ram(d);else if(_module is BarModule.Gpu or BarModule.Disk or BarModule.Network or BarModule.Battery or BarModule.Sensors)Module(d);else
        {CardAt(d,0,140);Label(d,_module==BarModule.Sensors?"Optional sensor helper required":"Wide module page awaiting resource validation",20,24,16,Text,true);Label(d,_sample?.Explanation??"Reading capability…",20,65,12,Dim);_bottom=145;}
        if(_previous)Label(d,MetricLoading.Previous,Math.Max(15,_width-215),2,10,Dim);
        else if(IsMeasuring)Label(d,MetricLoading.For(_module).Title,Math.Max(15,_width-215),2,10,Dim);
        d.Pop();if(_bottom>_height){var max=_bottom-_height;d.DrawRoundedRectangle(Dim,null,new Rect(_width-4,(_height-60)*_scroll/max,3,60),1.5,1.5);}d.Pop();
    }
    private void Loading(DrawingContext d)
    {
        var (title,detail)=MetricLoading.For(_module);
        CardAt(d,0,132);
        Label(d,title,20,24,17,Text,true);
        Label(d,detail,20,60,12,Dim);
        _bottom=147;
        if(_module is BarModule.Cpu or BarModule.Ram or BarModule.Gpu or BarModule.Disk or BarModule.Network)
        {
            CardAt(d,158,146);
            Label(d,MetricLoading.CollectingHistory,20,218,12,Dim,centered:true);
            _bottom=320;
        }
    }
    private void Cpu(DrawingContext d)
    {
        var s=_sample;
        if(s?.Cpu is not (>=0 and <=100)){Empty(d,s?.Explanation??"Windows CPU readings are unavailable.");return;}
        CardAt(d,0,142);Ring(d,new(69,71),48,s.Cpu,UsageBrush(BarModule.Cpu,s.Cpu));
        Label(d,Percent(s.Cpu),69,71,24,Text,true,centerOnPoint:true);
        Label(d,"Processor utilization",144,20,16,Text,true);Label(d,s?.FrequencyGhz is{}f?$"{f:0.00} GHz":"Windows CPU readings",144,48,12,Dim);
        var barWidth=_width-170;Bar(d,144,79,barWidth,s?.System,s?.User);
        Label(d,"System: "+Percent(s?.System),144,103,12,Red);Label(d,"User: "+Percent(s?.User),144+barWidth*.35,103,12,Blue);Label(d,"Idle: "+Percent(s?.Idle),144+barWidth*.68,103,12,Dim);
        var temperature=s?.LastCpuPackageCelsius is double temp?SensorTemp(temp)+(s.CpuTemperatureAutomatic?"":" · last check")
            :s?.CpuTemperatureMeasuring==true?"Measuring…":s?.CpuTemperatureAutomatic==true?"Unavailable":"Use Check CPU temp";
        Label(d,"CPU package temperature: "+temperature,144,121,11,s?.LastCpuPackageCelsius is double celsius?SensorTempBrush(celsius):Dim);
        History(d,174,"Usage history");double y=408;
        var coreCount=s?.Cores.Length??Math.Min(32,Environment.ProcessorCount);
        if(coreCount>0)
        {
            Label(d,"Usage per logical processor",6,y,16,Text,true);y+=35;var rows=(coreCount+1)/2;CardAt(d,y,rows*52+12);
            if(Intersects(y,rows*52+12))Details|=PopupDetails.Cores;
            var hybrid=s is not null&&s.Classes.Length==coreCount&&s.Classes.All(c=>c>=0)&&s.Classes.Distinct().Count()>1;var highest=hybrid?s!.Classes.Max():-1;
            for(int i=0;i<coreCount;i++)
            {
                var x=16+(i%2)*(_width/2);var top=y+12+(i/2)*52;var value=s is not null&&i<s.Cores.Length?s.Cores[i]:double.NaN;
                if(!Intersects(top,32))continue;
                var valid=double.IsFinite(value)&&value is>=0 and<=100;
                var kind=hybrid?(s!.Classes[i]==highest?1:0):-1;var label=kind==0?"E · logical ":kind>0?"P · logical ":"Logical processor ";
                var w=_width/2-35;const double valueWidth=84;
                Label(d,label+(i+1),x,top,11,Dim,maxWidth:w-valueWidth-8);
                Label(d,valid?$"{value:0}%":s?.Capabilities?.GetValueOrDefault("cores")?.ReasonCode=="cores.unavailable"?"Unavailable":"Measuring…",
                    x+w-valueWidth,top,11,Text,true,right:true,maxWidth:valueWidth);
                d.DrawRoundedRectangle(Track,null,new Rect(x,top+23,w,6),3,3);if(valid&&value>0)d.DrawRoundedRectangle(kind==0?Cyan:Blue,null,new Rect(x,top+23,w*value/100,6),3,3);
            }
            y+=rows*52+35;
        }
        if(s is not null){Label(d,"Uptime",16,y,12,Dim);Label(d,$"{(int)s.Uptime.TotalDays} days, {s.Uptime.Hours} hours",_width/2,y,12,Text,true);y+=35;}
        Label(d,"Average usage · our samples",16,y,13,Text,true);y+=30;
        foreach(var minutes in new[]{1,5,15}){var a=_averages?.Average(_now,minutes*60);Label(d,minutes+" minute"+(minutes==1?"":"s"),16,y,12,Dim);Label(d,Percent(a?.Percent)+(a?.Complete==false?" · collecting":""),_width/2,y,12,Text);y+=26;}
        Processes(d,ref y,false);Explanation(d,ref y);_bottom=y+15;
    }
    private void Ram(DrawingContext d)
    {
        var m=_sample?.Memory;var valid=m?.Valid==true;var usage=UsageBrush(BarModule.Ram,valid?m!.UsedPercent:null);CardAt(d,0,144);Ring(d,new(69,72),48,valid?m!.UsedPercent:null,usage);
        Label(d,valid?$"{m!.UsedPercent:0}%":"Unavailable",69,72,valid?24:11,valid?Text:Dim,true,centerOnPoint:true);
        Label(d,"Used: "+(valid?Bytes(m!.Total-m.Available):"Unavailable"),144,20,16,Text,true);
        Label(d,"Total: "+(valid?Bytes(m!.Total):"Unavailable"),_width-180,23,12,Dim);
        var w=_width-170;if(Intersects(64,13)){d.DrawRoundedRectangle(Track,null,new Rect(144,64,w,13),4,4);
        if(valid)d.DrawRoundedRectangle(usage,null,new Rect(144,64,w*m!.UsedPercent/100,13),4,4);}
        Label(d,"In use: "+(valid?Bytes(m!.Total-m.Available):"Unavailable"),144,96,12,Blue);Label(d,"Available: "+(valid?Bytes(m!.Available):"Unavailable"),_width-210,96,12,Dim);
        History(d,174,"Usage history");double y=410;var half=(_width-16)/2;
        Label(d,"Memory pressure",6,y,16,Text,true);Label(d,"Page file",half+22,y,16,Text,true);y+=34;
        if(Intersects(y,165)){d.DrawRoundedRectangle(_palette.Card,_palette.CardBorder,new Rect(0,y,half,165),11,11);d.DrawRoundedRectangle(_palette.Card,_palette.CardBorder,new Rect(half+16,y,half,165),11,11);}
        var pressure=valid?m!.Pressure:double.NaN;var center=new Point(half/2,y+105);
        var warning=Math.Min(_thresholdPercent,BarThresholds.CriticalPercent)*1.8;
        var critical=BarThresholds.CriticalPercent*1.8;
        Arc(d,center,62,180,warning,Green,8);
        if(warning<critical)Arc(d,center,62,180+warning,critical-warning,Orange,8);
        Arc(d,center,62,180+critical,180-critical,Red,8);
        if(Intersects(center.Y-62,62)&&double.IsFinite(pressure)){var angle=(180+Math.Clamp(pressure,0,100)*1.8)*Math.PI/180;d.DrawLine(Line,center,new Point(center.X+Math.Cos(angle)*50,center.Y+Math.Sin(angle)*50));}
        var level=BarThresholds.UsageLevel(BarModule.Ram,pressure,_thresholdPercent);
        var pressureName=!valid?"Unavailable":level==ThresholdLevel.Critical?"Critical":level==ThresholdLevel.Warning?"High":"Normal";
        Label(d,pressureName,center.X,y+133,13,_palette.Metrics.Status(level,Text),true,centerOnPoint:true);
        var pageValid=m?.PageFileUsed is{}used&&m.PageFileTotal is>0&&used<=m.PageFileTotal;
        double? pagePercent=pageValid?100.0*m!.PageFileUsed!.Value/m.PageFileTotal!.Value:null;
        Ring(d,new(half+16+half/2,y+74),43,pagePercent,UsageBrush(BarModule.Ram,pagePercent));
        Label(d,pageValid?Bytes(m!.PageFileUsed!.Value):"Not reported",half+16+half/2,y+74,12,Text,true,centerOnPoint:true);
        y+=190;
        if(m?.Cache is{}cache){Label(d,"System cache (may overlap other memory)",16,y,12,Dim);Label(d,Bytes(cache),_width-120,y,12,Text,true,right:true);y+=35;}
        Processes(d,ref y,true);Explanation(d,ref y);_bottom=y+15;
    }
    private void Processes(DrawingContext d,ref double y,bool memory)
    {
        var p=_sample?.Processes??[];var top=y;
        Label(d,"Top processes",6,y,16,Text,true);y+=33;CardAt(d,y,6*29+15);
        if(Intersects(top,33+6*29+15))Details|=PopupDetails.Processes;
        if(p.Length==0)
        {
            var reason=_sample?.Capabilities?.GetValueOrDefault("processes");
            Label(d,reason?.ReasonCode=="processes.unavailable"?reason.Explanation??"Process details unavailable":Details.HasFlag(PopupDetails.Processes)?"Measuring processes…":"Scroll here to measure processes",16,y+7,12,Dim);
        }
        for(int i=0;i<p.Length;i++){var topRow=y+7+i*29;if(!Intersects(topRow,36))continue;var item=p[i];Label(d,item.Name,16,topRow,12,Text);Label(d,memory?Bytes(item.WorkingSet):Percent(item.CpuPercent),_width-120,topRow,12,Text,true,right:true);}y+=6*29+40;
    }
    private bool Intersects(double top,double height)=>top<_scroll+_height&&top+height>_scroll;
    private void Explanation(DrawingContext d,ref double y){if(!string.IsNullOrWhiteSpace(_sample?.Explanation)){Label(d,_sample.Explanation,16,y,11,Dim);y+=48;}}
    private void History(DrawingContext d,double y,string title,SampleRing? series=null)
    {
        Label(d,title,6,y-27,16,Text,true);CardAt(d,y,200);var rect=new Rect(50,y+15,_width-68,150);
        if(!Intersects(y,200))return;
        for(int i=0;i<=4;i++){double top=rect.Top+i*rect.Height/4;d.DrawLine(Grid,new Point(rect.Left,top),new Point(rect.Right,top));Label(d,(100-i*25)+"%",10,top-7,10,Dim);}
        var history=series??_history;
        if(history is not{Count:>=2})
        {
            Label(d,MetricLoading.CollectingHistory,0,rect.Top+60,12,Dim,centered:true);
        }
        else
        {
            var geometry=new StreamGeometry();using(var c=geometry.Open())
            {
                bool started=false;double lastX=0;
                for(int i=0;i<history.Count;i++){var p=history.At(i);if(p.Seconds<_now-120)continue;var x=rect.Left+rect.Width*Math.Clamp((p.Seconds-(_now-120))/120,0,1);if(!started){c.BeginFigure(new(x,rect.Bottom),true,true);started=true;}c.LineTo(new(x,rect.Bottom-rect.Height*p.Value/100),true,false);lastX=x;}
                if(started)c.LineTo(new(lastX,rect.Bottom),true,false);
            }geometry.Freeze();d.DrawGeometry(Fill,Line,geometry);
        }
        Label(d,"2 minutes ago",rect.Left,rect.Bottom+14,10,Dim);Label(d,"Now",rect.Right-26,rect.Bottom+14,10,Dim);
    }
    private void Bar(DrawingContext d,double x,double y,double width,double? system,double? user)
    {if(!Intersects(y,10))return;d.DrawRoundedRectangle(Track,null,new Rect(x,y,width,10),3,3);double offset=0;if(system is >=0 and <=100){var n=width*system.Value/100;if(n>0)d.DrawRectangle(Red,null,new Rect(x,y,n,10));offset=n;}if(user is >=0 and <=100){var n=Math.Min(width-offset,width*user.Value/100);if(n>0)d.DrawRectangle(Blue,null,new Rect(x+offset,y,n,10));}}
    private void CardAt(DrawingContext d,double y,double height){if(Intersects(y,height))d.DrawRoundedRectangle(_palette.Card,_palette.CardBorder,new Rect(0,y,_width-10,height),11,11);}
    private static string Percent(double? value)=>value is{}v&&double.IsFinite(v)&&v is>=0 and<=100?$"{v:0}%":"Unavailable";
    private static string Bytes(ulong value)=>value>=1073741824?$"{value/1073741824.0:0.00} GiB":$"{value/1048576.0:0.0} MiB";
    private void Ring(DrawingContext d,Point center,double radius,double? value,Brush color){if(!Intersects(center.Y-radius-5,2*radius+10))return;d.DrawEllipse(null,Pen(Track,9),center,radius,radius);if(value is>0 and<=100)Arc(d,center,radius,-90,Math.Min(359.99,value.Value*3.6),color,9);}
    private void Arc(DrawingContext d,Point center,double radius,double start,double sweep,Brush color,double thickness)
    {if(!Intersects(center.Y-radius-thickness/2,2*radius+thickness))return;Point At(double degrees)=>new(center.X+radius*Math.Cos(degrees*Math.PI/180),center.Y+radius*Math.Sin(degrees*Math.PI/180));var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(At(start),false,false);c.ArcTo(At(start+sweep),new Size(radius,radius),0,sweep>180,SweepDirection.Clockwise,true,false);}g.Freeze();d.DrawGeometry(null,Pen(color,thickness),g);}
    private void Label(DrawingContext d,string value,double x,double y,double size,Brush brush,bool bold=false,bool right=false,bool centered=false,bool centerOnPoint=false,double? maxWidth=null)
    {
        if(!Intersects(centerOnPoint?y-size:y,size*3))return;
        var alignment=centered?TextAlignment.Center:right?TextAlignment.Right:TextAlignment.Left;
        var key=(x,y,size,brush,bold,alignment,centerOnPoint,maxWidth);
        if(!_labels.TryGetValue(key,out var cached)||cached.Item1!=value)
        {
            var text=MonitorText.Format(value,bold?Bold:Face,size,brush,_scale);
            if(!centerOnPoint)
            {
                text.MaxTextWidth=Math.Max(10,maxWidth??_width-x-12);
                text.MaxLineCount=2;
                text.Trimming=TextTrimming.CharacterEllipsis;
                text.TextAlignment=alignment;
            }
            if(_labels.Count>=192)_labels.Clear();
            _labels[key]=cached=(value,text);
        }
        var origin=centerOnPoint?MonitorText.Centered(cached.Item2,new Point(x,y),_scale):MonitorText.PixelAligned(x,y,_scale);
        d.DrawText(cached.Item2,origin);
    }
    private sealed class PagePeer(ApplicationModuleVisual owner):UIElementAutomationPeer(owner)
    {protected override string GetNameCore()=>owner._module+" application page. "+(owner._previous?MetricLoading.Previous:owner.IsMeasuring||owner._sample is null?MetricLoading.For(owner._module).Title:owner._sample.Explanation);protected override string GetClassNameCore()=>"TidletModulePage";protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Pane;}
}
