using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Islet.Core.Bar;
namespace Islet.Shell.Bar;

public sealed class MonitorBarHost : IDisposable
{
    private readonly IBarDesktop _desktop;private readonly Func<BarSnapshot> _read;private readonly Action _reset;
    private readonly Action<BarSettings> _save;private readonly List<BarWindow> _windows=[];
    private readonly DispatcherTimer _samples,_visibility;private readonly DispatcherTimer? _exit;
    private DispatcherTimer? _edge;
    private readonly string? _capture;private bool _disposed;private BarSettings _settings;private bool _rebuilding;private BarSnapshot? _lastSnapshot;private IReadOnlyList<object>? _finalObservations;
    public bool EverHidden{get;private set;}public int VisibilityChecks{get;private set;}
    public int Samples{get;private set;} public int HiddenMetricReads{get;private set;}
    public bool TimersStopped{get;private set;}public bool FirstFrame=>_windows.Any(x=>x.FirstFrame);
    public int WindowCount=>_windows.Count;public bool AllTrayRemoved{get;private set;}
    public IReadOnlyList<object> Observations=>_finalObservations??_windows.Select(x=>(object)new{x.Display.Id,x.Display.Bounds,x.Display.Work,x.Scale,x.Hidden,x.FirstFrame,x.Surface.Placement,x.Surface.TrayPresent}).ToArray();
    public event Action<BarModule,BarRect,BarDisplay,double>? ModuleClicked;
    public event Action? Suppressed;
    public event Action? Stopping;
    public event Action? DashboardRequested;public event Action? SettingsRequested;public event Action? SettingsChanged;
    public Func<string?>? PopupDisplay{get;set;}
    public BarSettings CurrentSettings=>_settings;
    public void ApplySettings(BarSettings settings){settings.Validate();_save(settings);_settings=settings;Rebuild();SettingsChanged?.Invoke();}
    public void ActivateModule(BarModule module)
    {
        if(_windows.Count==0)return;
        var w=_windows[0];
        if(_settings.TopReveal&&!w.EnvironmentHidden)
        {
            w.Reveal.Update(w.Display.Bounds,w.Surface.Placement.Pixels,new BarPoint(w.Display.Bounds.Left,w.Display.Bounds.Top),w.Scale,false,Environment.TickCount64);
            ApplyVisibility();
        }
        ModuleClicked?.Invoke(module,w.ItemRect(module),w.Display,w.Scale);
    }
    public MonitorBarHost(IBarDesktop desktop,BarSettings settings,Func<BarSnapshot> read,Action reset,Action<BarSettings> save,int? exitSeconds=null,string? capture=null)
    {
        _desktop=desktop;_settings=settings.Validate();_read=read;_reset=reset;_save=save;_capture=capture;
        _samples=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(settings.IntervalSeconds)};_samples.Tick+=SampleTick;
        _visibility=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(2)};_visibility.Tick+=VisibilityTick;
        if(exitSeconds is{} seconds){_exit=new DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};_exit.Tick+=ExitTick;}
        Rebuild();_visibility.Start();_exit?.Start();
    }
    public void Run()=>Dispatcher.Run();
    private void Rebuild()
    {
        if(_rebuilding||_disposed)return;_rebuilding=true;
        try
        {
            _samples.Stop();_edge?.Stop();foreach(var old in _windows)old.Dispose();_windows.Clear();
            var displays=_desktop.Displays();var selected=_settings.Monitors switch
            {
                MonitorSelection.All=>displays,
                MonitorSelection.Selected=>displays.Where(x=>x.Id==_settings.SelectedMonitor).ToArray(),
                _=>displays.Where(x=>x.Primary).ToArray()
            };
            if(selected.Count==0)selected=displays.Take(1).ToArray();
            foreach(var display in selected)
            {
                var window=new BarWindow(_desktop,display,_settings,_windows.Count==0);
                window.MenuRequested+=module=>Menu(window,module);window.Clicked+=module=>ModuleClicked?.Invoke(module,window.ItemRect(module),window.Display,window.Scale);
                window.RefreshRequested+=()=>Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(UpdateVisibility));
                window.DisplaysChanged+=()=>Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(Rebuild));
                window.ExitRequested+=Dispose;_windows.Add(window);
            }
            foreach(var window in _windows)window.Update(_lastSnapshot??new(null,null,null,"Preparing live readings.","Preparing live readings.","Preparing live readings.","Battery capability is being checked.",null,null,!_settings.Pause));
            _reset();UpdateVisibility();
        }
        catch{Dispose();throw;}
        finally{_rebuilding=false;}
    }
    private void Menu(BarWindow window,BarModule? module)
    {
        var command=window.Surface.Menu(module?.ToString(),_settings.Pause);var next=_settings;
        switch(command)
        {
            case 1:next=next with{Pause=!next.Pause};break;
            case 2:if(module is{} m)next=next.WithItemVisibility(m,false);break;
            case 3:next=next with{Items=new BarSettings().Items,ItemOrder=[]};break;
            case 10:next=next with{Mode=BarMode.Overlay};break;case 11:next=next with{Mode=BarMode.Reserve,RevealAtTop=false};break;
            case 12:next=next with{Edge=BarEdge.Top,RevealAtTop=false};break;case 13:next=next with{Edge=BarEdge.Bottom,RevealAtTop=false};break;
            case 14:next=next with{Alignment=BarAlignment.Left};break;case 15:next=next with{Alignment=BarAlignment.Center};break;case 16:next=next with{Alignment=BarAlignment.Right};break;
            case 17:next=next with{Monitors=MonitorSelection.Primary};break;case 18:next=next with{Monitors=MonitorSelection.All};break;
            case 19:next=next with{Edge=BarEdge.Top,RevealAtTop=true,Mode=BarMode.Overlay};break;
            case 99:Dispose();return;
            case 100:DashboardRequested?.Invoke();return;case 101:SettingsRequested?.Invoke();return;
        }
        if(next!=_settings)
        {
            try{_save(next);_settings=next;Rebuild();SettingsChanged?.Invoke();}catch(IOException){window.SetNotice("Settings could not be saved.");}catch(UnauthorizedAccessException){window.SetNotice("Settings folder is not writable.");}
        }
    }
    private void SampleTick(object? s,EventArgs e)=>Sample();private void VisibilityTick(object? s,EventArgs e)=>UpdateVisibility();private void ExitTick(object? s,EventArgs e)=>Dispose();
    private void UpdateVisibility()
    {
        if(_disposed)return;
        VisibilityChecks++;
        foreach(var window in _windows)
        {window.EnvironmentHidden=window.Surface.ShouldHide;if(window.EnvironmentHidden){EverHidden=true;window.Reveal.Reset();}}
        var environmentHidden=_windows.All(window=>window.EnvironmentHidden);
        ApplyVisibility();
        if(environmentHidden)Suppressed?.Invoke();
        if(_settings.TopReveal&&_settings.Items.Length>0&&!environmentHidden)
        {
            if(_edge is null){_edge=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(120)};_edge.Tick+=EdgeTick;}
            if(!_edge.IsEnabled)_edge.Start();
        }
        else _edge?.Stop();
    }
    private void EdgeTick(object? sender,EventArgs args)
    {
        if(_disposed)return;
        var cursor=_desktop.CursorPosition();var popup=PopupDisplay?.Invoke();var now=Environment.TickCount64;var changed=false;
        foreach(var window in _windows)
        {
            if(window.EnvironmentHidden)continue;
            var wasVisible=window.Reveal.Visible;
            changed|=wasVisible!=window.Reveal.Update(window.Display.Bounds,window.Surface.Placement.Pixels,cursor,window.Scale,popup==window.Display.Id,now);
        }
        if(changed)ApplyVisibility();
    }
    private void ApplyVisibility()
    {
        var wasRunning=_samples.IsEnabled;
        foreach(var window in _windows)window.SetHidden(window.EnvironmentHidden||_settings.Items.Length==0||(_settings.TopReveal&&!window.Reveal.Visible));
        var visible=_windows.Any(x=>!x.Hidden)&&!_settings.Pause;
        var interval=TimeSpan.FromSeconds(_windows.Any(x=>x.Surface.SlowPolling)?Math.Max(5,_settings.IntervalSeconds):_settings.IntervalSeconds);
        if(_samples.Interval!=interval)_samples.Interval=interval;
        if(visible&&!wasRunning){_reset();Sample();_samples.Start();}else if(!visible&&wasRunning){_samples.Stop();_reset();}
    }
    private void Sample()
    {
        if(_disposed||_settings.Pause||_windows.All(x=>x.Hidden))return;
        var snapshot=_read();_lastSnapshot=snapshot;Samples++;foreach(var window in _windows.Where(x=>!x.Hidden))window.Update(snapshot);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Stopping?.Invoke();Suppressed?.Invoke();_samples.Stop();_samples.Tick-=SampleTick;_visibility.Stop();_visibility.Tick-=VisibilityTick;
        if(_edge is not null){_edge.Stop();_edge.Tick-=EdgeTick;}
        if(_exit is not null){_exit.Stop();_exit.Tick-=ExitTick;}TimersStopped=true;
        try
        {
            _finalObservations=Observations; if(_capture is{} path){var i=0;foreach(var w in _windows)w.Capture(i++==0?path:Path.ChangeExtension(path,$"{i}.png"));}
        }
        finally
        {
            foreach(var window in _windows)window.Dispose();AllTrayRemoved=_windows.All(x=>!x.Surface.TrayPresent);
            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        }
    }
    private sealed class BarWindow : IDisposable
    {
        private readonly HwndSource _source;private readonly BarVisual _root;private bool _disposed;
        private readonly TranslateTransform? _drop;
        public TopEdgeReveal Reveal{get;}=new();
        public bool EnvironmentHidden{get;set;}
        public BarDisplay Display{get;}public IBarSurface Surface{get;}public bool Hidden{get;private set;}
        public bool FirstFrame{get;private set;}public double Scale=>_source.CompositionTarget.TransformToDevice.M11;
        public event Action<BarModule?>? MenuRequested;public event Action<BarModule>? Clicked;
        public event Action? RefreshRequested,DisplaysChanged,ExitRequested;
        public BarWindow(IBarDesktop desktop,BarDisplay display,BarSettings settings,bool tray)
        {
            Display=display;_root=new(settings);var b=display.Bounds;Hidden=settings.TopReveal;
            if(settings.TopReveal){_drop=new TranslateTransform();_root.RenderTransform=_drop;}
            _source=new(new HwndSourceParameters("Tidlet monitor bar"){PositionX=b.Left,PositionY=b.Top,Width=b.Width,Height=24,WindowStyle=unchecked((int)0x80000000),ExtendedWindowStyle=0x80,UsesPerPixelOpacity=true});
            _source.RootVisual=_root;_source.AddHook(Message);_source.ContentRendered+=Rendered;
            try{Surface=desktop.Attach(_source.Handle,display,settings,tray);}catch{_source.Dispose();throw;}
            Surface.EnvironmentChanged+=Refresh;Surface.TrayMenuRequested+=TrayMenu;
            _root.Clicked+=module=>Clicked?.Invoke(module);_root.MenuRequested+=m=>MenuRequested?.Invoke(m);
            Resize();
        }
        private void Refresh()=>RefreshRequested?.Invoke();private void TrayMenu()=>MenuRequested?.Invoke(null);
        private void Rendered(object? s,EventArgs e)=>FirstFrame=true;
        private nint Message(nint h,int m,nint w,nint l,ref bool handled)
        {
            Surface?.HandleMessage(m,w,l);
            if(m==0x0010){ExitRequested?.Invoke();handled=true;}
            if(m==0x0007)_root.Focus();
            if(m==0x02E0)_root.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(!_disposed&&Surface is{} surface){surface.Reposition();Resize();}}));
            if(m==0x0005&&Surface is not null)_root.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(!_disposed)Resize();}));
            if(m==0x007E)DisplaysChanged?.Invoke();return 0;
        }
        private void Resize(){_root.Notice=Surface.Placement.Notice;var size=new Size(Surface.Placement.Pixels.Width/Scale,Surface.Placement.Pixels.Height/Scale);_root.SetSize(size,Scale);_root.Measure(size);_root.Arrange(new Rect(size));}
        public void Update(BarSnapshot snapshot)=>_root.Update(snapshot);
        public void SetNotice(string text)=>_root.Notice=text;
        public void SetHidden(bool hidden)
        {
            if(Hidden==hidden)return;
            if(_drop is not null)
            {
                _drop.BeginAnimation(TranslateTransform.YProperty,null);
                if(!hidden&&SystemParameters.ClientAreaAnimation)
                    _drop.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(-BarLayout.HeightDip(BarEdge.Bottom),0,TimeSpan.FromMilliseconds(160)){FillBehavior=FillBehavior.Stop});
            }
            Hidden=hidden;Surface.SetHidden(hidden);Resize();
        }
        public BarRect ItemRect(BarModule module)
        {
            var match=_root.Items.Where(x=>x.Module==module).Select(x=>(BarItemLayout?)x).FirstOrDefault();var p=Surface.Placement.Pixels;
            if(match is null)return new(p.Left+p.Width/2-12,p.Top,p.Left+p.Width/2+12,p.Bottom);
            var i=match.Value;
            return new(p.Left+(int)(i.X*Scale),p.Top,p.Left+(int)((i.X+i.Width)*Scale),p.Bottom);
        }
        public void Capture(string path)
        {
            if(!FirstFrame)return;var p=Surface.Placement.Pixels;var image=new RenderTargetBitmap(p.Width,p.Height,96*Scale,96*Scale,PixelFormats.Pbgra32);image.Render(_root);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);encoder.Save(file);
        }
        public void Dispose(){if(_disposed)return;_disposed=true;Surface.EnvironmentChanged-=Refresh;Surface.TrayMenuRequested-=TrayMenu;Surface.Dispose();_source.ContentRendered-=Rendered;_source.RemoveHook(Message);_source.RootVisual=null;_source.Dispose();}
    }
    private sealed class BarVisual: UIElement
    {
        private readonly DrawingVisual _background=new();private readonly List<ItemVisual> _children=[];private readonly BarSettings _settings;
        private double _width=240,_height=24,_scale=1;private int _focus;
        private BarSnapshot? _snapshot;
        private readonly Brush _stripBackground;
        private readonly Pen _stripBorder;
        private readonly bool _floating;
        private static readonly Pen DarkBorder=CreateBorder(Color.FromRgb(68,68,68));
        private static readonly Pen LightBorder=CreateBorder(Color.FromRgb(179,192,205));
        public string? Notice{get;set;}public BarItemLayout[] Items{get;private set;}=[];
        public event Action<BarModule?>? MenuRequested;public event Action<BarModule>? Clicked;
        public BarVisual(BarSettings settings)
        {
            _settings=settings;Focusable=true;AddVisualChild(_background);
            _floating=settings.Edge==BarEdge.Bottom||settings.TopReveal;
            var light=settings.Theme==BarTheme.Light;
            var background=new LinearGradientBrush(light?Color.FromRgb(250,252,254):Color.FromRgb(39,39,39),
                light?Color.FromRgb(231,237,244):Color.FromRgb(28,28,28),90){Opacity=settings.Opacity};
            background.Freeze();_stripBackground=background;_stripBorder=light?LightBorder:DarkBorder;
            MouseLeftButtonUp+=(_,e)=>{Focus();if(BarLayout.Hit(Items,e.GetPosition(this).X)is{}m)Clicked?.Invoke(m);};
            MouseRightButtonUp+=(_,e)=>{MenuRequested?.Invoke(BarLayout.Hit(Items,e.GetPosition(this).X));e.Handled=true;};
            KeyDown+=(_,e)=>
            {
                if(e.Key==Key.Tab&&_children.Count>0){_focus=(_focus+((Keyboard.Modifiers&ModifierKeys.Shift)!=0?-1:1)+_children.Count)%_children.Count;_children[_focus].Focus();e.Handled=true;}
                else if(e.Key is Key.Enter or Key.Space && Items.Length>0){Clicked?.Invoke(Items[Math.Min(_focus,Items.Length-1)].Module);e.Handled=true;}
                else if(e.Key==Key.Apps || e.Key==Key.F10&&(Keyboard.Modifiers&ModifierKeys.Shift)!=0){MenuRequested?.Invoke(Items.Length>0?Items[Math.Min(_focus,Items.Length-1)].Module:null);e.Handled=true;}
            };
        }
        protected override int VisualChildrenCount=>1+_children.Count;
        protected override Visual GetVisualChild(int index)=>index==0?_background:_children[index-1];
        protected override AutomationPeer OnCreateAutomationPeer()=>new RootPeer(this);
        public void SetSize(Size size,double scale)
        {
            if(_width==size.Width&&_height==size.Height&&_scale==scale&&_snapshot is not null)return;
            _width=size.Width;_height=size.Height;_scale=scale;
            DrawBackground();
            if(_snapshot is{} snapshot)DrawItems(snapshot,false);
        }
        private static Pen CreateBorder(Color color){var brush=new SolidColorBrush(color);brush.Freeze();var pen=new Pen(brush,1);pen.Freeze();return pen;}
        private void DrawBackground()
        {
            using var d=_background.RenderOpen();
            if(_floating)d.DrawRoundedRectangle(_stripBackground,_stripBorder,new Rect(.5,.5,Math.Max(0,_width-1),Math.Max(0,_height-1)),4,4);
            else
            {
                d.DrawRectangle(_stripBackground,null,new Rect(0,0,_width,_height));
                d.DrawLine(_stripBorder,new Point(0,_height-.5),new Point(_width,_height-.5));
            }
        }
        public void Update(BarSnapshot snapshot)
        {
            _snapshot=snapshot;
            DrawItems(snapshot,true);
        }
        private void DrawItems(BarSnapshot snapshot,bool appendHistory)
        {
            var items=BarLayout.Items(_settings,_width,snapshot.Battery.HasValue);
            var layoutChanged=!items.SequenceEqual(Items);
            if(layoutChanged)
            {
                foreach(var c in _children)RemoveVisualChild(c);_children.Clear();Items=items;
                foreach(var item in Items){var c=new ItemVisual(item.Module,()=>Clicked?.Invoke(item.Module),_settings.UseBits,_settings.Preference(item.Module).Style,item.Width>=84,_settings.Theme==BarTheme.Light);_children.Add(c);AddVisualChild(c);}
            }
            for(var index=0;index<_children.Count;index++)
            {
                var child=_children[index];var item=Items[index];
                if(layoutChanged||!appendHistory)
                {
                    child.Measure(new Size(item.Width,_height));
                    child.Arrange(new Rect(item.X,0,item.Width,_height));
                }
                child.Update(snapshot,_scale,_height,Notice,appendHistory);
            }
        }
        private sealed class RootPeer(BarVisual owner):UIElementAutomationPeer(owner)
        {
            protected override string GetClassNameCore()=>"TidletBar";protected override string GetNameCore()=>"Tidlet system monitor";
            protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.ToolBar;
        }
    }
    private sealed class ItemVisual: UIElement
    {
        private readonly DrawingVisual _visual=new();private readonly BarModule _module;private readonly Action _invoke;
        private static readonly Typeface Face=new(new FontFamily("Segoe UI Variable, Segoe UI"),FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
        private static readonly Typeface Bold=new(new FontFamily("Segoe UI Variable, Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal);
        private static readonly Brush BatteryGreen=BatteryBrush(48,209,88),BatteryLow=BatteryBrush(255,159,10);
        private static Brush BatteryBrush(byte r,byte g,byte b){var brush=new SolidColorBrush(Color.FromRgb(r,g,b));brush.Freeze();return brush;}
        private string _name="Waiting for reading";private string _help="";
        private readonly bool _useBits;
        private readonly BarItemStyle _style;
        private readonly bool _wide;
        private readonly bool _light;
        private readonly double[]? _gpuHistory;
        private int _gpuCount,_gpuNext;
        private static readonly Brush TrafficUp=BatteryBrush(241,74,71),TrafficDown=BatteryBrush(44,153,234);
        private static readonly Brush LabelBrush=BatteryBrush(177,177,177),MeterTrack=BatteryBrush(57,60,64);
        private static readonly Brush LightText=BatteryBrush(23,32,43),LightLabel=BatteryBrush(66,77,91),LightTrack=BatteryBrush(199,209,220);
        private static readonly Brush RamFill=MakeRamFill();
        private static readonly Pen GpuTrace=FrozenPen(TrafficDown,1.2),CpuArc=FrozenPen(TrafficDown,2.3);
        private static readonly Pen CpuTrack=FrozenPen(MeterTrack,2.3),BatteryOutline=FrozenPen(LabelBrush,1);
        private static readonly Pen LightCpuTrack=FrozenPen(LightTrack,2.3),LightBatteryOutline=FrozenPen(LightLabel,1);
        private static Pen FrozenPen(Brush brush,double width){var pen=new Pen(brush,width);pen.Freeze();return pen;}
        private static Brush MakeRamFill()
        {
            var brush=new LinearGradientBrush(Color.FromRgb(215,145,44),Color.FromRgb(236,49,54),new Point(39,0),new Point(79,0))
                {MappingMode=BrushMappingMode.Absolute};
            brush.Freeze();
            return brush;
        }
        public ItemVisual(BarModule module,Action invoke,bool useBits,BarItemStyle style,bool wide,bool light)
        {_module=module;_invoke=invoke;_useBits=useBits;_style=style;_wide=wide;_light=light;if(wide&&module==BarModule.Gpu)_gpuHistory=new double[20];Focusable=true;AddVisualChild(_visual);}
        
        protected override int VisualChildrenCount=>1;protected override Visual GetVisualChild(int i)=>i==0?_visual:throw new ArgumentOutOfRangeException(nameof(i));
        protected override AutomationPeer OnCreateAutomationPeer()=>new ItemPeer(this);
        public void Update(BarSnapshot s,double scale,double height,string? notice,bool appendHistory)
        {
            using var d=_visual.RenderOpen();
            var center=height/2;
            string value;var label=_module.ToString().ToUpperInvariant();
            switch(_module)
            {
                case BarModule.Cpu:value=s.Loading||s.CpuLoading?"…":Percent(s.Cpu);_help=s.CpuReason;break;
                case BarModule.Ram:value=s.Loading?"…":Percent(s.Ram);_help=s.RamReason;break;
                case BarModule.Battery:value=Percent(s.Battery);_help=s.BatteryReason;break;
                case BarModule.Gpu:value=s.Loading||s.GpuLoading?"…":Percent(s.Gpu);_help=s.GpuReason;break;
                case BarModule.Disk:case BarModule.Sensors:value=_module==BarModule.Sensors?"Helper":"Details";_help="Click for capability-driven details.";break;
                default:
                    _help=s.NetworkReason;_name=$"Network, download {Rate(s.Download)}, upload {Rate(s.Upload)}";
                    if(_style==BarItemStyle.TwoLine){label="NET ↓";value=s.Loading||s.NetworkLoading?"…":Rate(s.Download);break;}
                    Dot(d,center-5.5,TrafficUp);Dot(d,center+5.5,TrafficDown);
                    Text(d,s.Loading||s.NetworkLoading?"…":Rate(s.Upload),13,center-11,10,true,scale);
                    Text(d,s.Loading||s.NetworkLoading?"…":Rate(s.Download),13,center,10,true,scale);return;
            }
            _name=$"{label} {value}";if(notice is not null)_help+=" "+notice;
            if(_module==BarModule.Battery&&_style!=BarItemStyle.TwoLine)
            {
                d.DrawRoundedRectangle(null,_light?LightBatteryOutline:BatteryOutline,new Rect(0,center-5,18,10),2,2);
                d.DrawRectangle(_light?LightLabel:LabelBrush,null,new Rect(19,center-2,2,4));
                if(s.Battery is{} percent&&ValidPercent(percent)&&percent>0)
                    d.DrawRoundedRectangle(percent<=15?BatteryLow:BatteryGreen,null,new Rect(2,center-3,14*percent/100,6),1,1);
                Text(d,value,26,center-7,11,true,scale);
            }
            else{Text(d,label,0,center-11,8,false,scale);Text(d,value,0,center-1,11,true,scale);}
            if(!_wide)return;
            if(_module==BarModule.Cpu)DrawCpuRing(d,center,s.Loading||s.CpuLoading?null:s.Cpu);
            if(_gpuHistory is not null&&!s.Loading&&!s.GpuLoading&&s.Gpu is{}gpu&&ValidPercent(gpu))DrawGpuHistory(d,center,gpu,appendHistory);
            if(_module==BarModule.Ram)
            {
                d.DrawRoundedRectangle(_light?LightTrack:MeterTrack,null,new Rect(39,center-5.5,40,11),2,2);
                if(!s.Loading&&s.Ram is{}ram&&ValidPercent(ram)&&ram>0)
                    d.DrawRoundedRectangle(RamFill,null,new Rect(39,center-5.5,40*ram/100,11),2,2);
            }
        }
        private void DrawCpuRing(DrawingContext d,double center,double? sample)
        {
            const double x=48,radius=8.5;
            d.DrawEllipse(null,_light?LightCpuTrack:CpuTrack,new Point(x,center),radius,radius);
            if(sample is not{}percent||!ValidPercent(percent)||percent==0)return;
            if(percent==100){d.DrawEllipse(null,CpuArc,new Point(x,center),radius,radius);return;}
            var radians=percent*Math.Tau/100;
            var end=new Point(x+radius*Math.Sin(radians),center-radius*Math.Cos(radians));
            var arc=new StreamGeometry();
            using(var path=arc.Open())
            {
                path.BeginFigure(new Point(x,center-radius),false,false);
                path.ArcTo(end,new Size(radius,radius),0,percent>50,SweepDirection.Clockwise,true,false);
            }
            arc.Freeze();d.DrawGeometry(null,CpuArc,arc);
        }
        private void DrawGpuHistory(DrawingContext d,double center,double sample,bool appendHistory)
        {
            var samples=_gpuHistory!;
            if(appendHistory){samples[_gpuNext]=sample;_gpuNext=(_gpuNext+1)%samples.Length;_gpuCount=Math.Min(samples.Length,_gpuCount+1);}
            if(_gpuCount<2)return;
            var geometry=new StreamGeometry();using(var c=geometry.Open())
            {
                var start=(_gpuNext-_gpuCount+samples.Length)%samples.Length;
                for(int i=0;i<_gpuCount;i++){var point=new Point(78-2.0*(_gpuCount-1-i),center+7-14*samples[(start+i)%samples.Length]/100);if(i==0)c.BeginFigure(point,false,false);else c.LineTo(point,true,false);}
            }
            geometry.Freeze();d.DrawGeometry(null,GpuTrace,geometry);
        }
        private static bool ValidPercent(double value)=>double.IsFinite(value)&&value is>=0 and<=100;
        private static string Percent(double? value)=>value is{}v&&double.IsFinite(v)&&v is>=0 and<=100?$"{v:0}%":"—";
        private string Rate(double? value)=>MetricFormat.TransferRate(value,_useBits,"—");
        private static void Dot(DrawingContext d,double y,Brush brush)=>d.DrawEllipse(brush,null,new Point(3,y),2.5,2.5);
        private void Text(DrawingContext d,string text,double x,double y,double size,bool bold,double scale)
        {
            var brush=_light?(bold?LightText:LightLabel):(bold?Brushes.White:LabelBrush);
            var t=new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,bold?Bold:Face,size,brush,scale);
            d.DrawText(t,new Point(x,y));
        }
        private sealed class ItemPeer(ItemVisual owner):UIElementAutomationPeer(owner),IInvokeProvider
        {
            protected override string GetClassNameCore()=>"TidletMetric";protected override string GetNameCore()=>owner._name;
            protected override string GetHelpTextCore()=>owner._help;protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Button;
            public override object? GetPattern(PatternInterface patternInterface)=>patternInterface==PatternInterface.Invoke?this:base.GetPattern(patternInterface);
            public void Invoke()=>owner.Dispatcher.BeginInvoke(owner._invoke);
        }
    }
}








