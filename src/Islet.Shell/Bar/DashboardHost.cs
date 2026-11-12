using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Islet.Core.Bar;
using Islet.Core.Popups;
namespace Islet.Shell.Bar;

public sealed record DashboardActions(Action<nint,BarRect,bool> Position,Action TaskManager,Action Quit,Func<string> ClipboardText,
    Func<string,string> ImportSettings,Func<string> ExportSettings,Func<string> CompatibilityReport,Action Support,Func<nint,BarRect>? Bounds=null,
    IPowerModeControl? PowerModes=null,Func<BarModule?,string>? DesktopWidget=null,
    Func<UsageSnapshot>? Usage=null,Action<UsagePeriod>? SelectUsage=null,Func<string>? PinUsage=null,
    Func<CancellationToken,Task<string>>? WifiDiagnostics=null,Func<CancellationToken,Task<string>>? StorageHealth=null,
    Func<string>? StorageCleanup=null,Func<string>? BatteryCapacity=null,
    Func<CancellationToken,TempCleanupScan>? TempScan=null,Func<TempCleanupScan,CancellationToken,TempCleanupResult>? TempClean=null,
    Action<bool>? SetGamingMode=null,Func<Task<string>>? ReadCpuTemperature=null,Func<double?>? LastCpuTemperature=null,Func<string?>? CpuTemperatureStatus=null,Func<IPopupSource>? WidgetMetrics=null);
public sealed record DashboardEvent(string Action,string Page,double Seconds,int Reads,int Frames);
public sealed record DashboardCapture(string Path,int Percent,int Width,int Height,double StartedSeconds,double CompletedSeconds);

/// <summary>Lazily creates the application window; does not initialize a WPF Window/control tree.</summary>
public sealed partial class DashboardHost:IDisposable
{
    public const string BatteryOptimizationPage="Battery optimization";
    public const string TempCleanupPage="Temp cleanup";
    private readonly Func<BarSettings> _settings;private readonly Action<BarSettings> _save;private readonly Func<DeviceOverview> _device;
    private readonly Func<IPopupSource> _metrics;private readonly Func<BarDisplay> _display;private readonly DashboardActions _actions;
    private DashboardWindow? _window;private readonly List<Task> _cleanup=[];private readonly bool _diagnostic;private readonly Action<string,bool>? _phase;
    private readonly RecentSampleCache _recent;
    public List<string> PageHistory{get;}=[];
    public List<DashboardCapture> Captures{get;}=[];
    private readonly Stopwatch _clock=Stopwatch.StartNew();public List<DashboardEvent> Events{get;}=[];
    public int CurrentReads=>Reads+(_window?.Reads??0);public int CurrentFrames=>Frames+(_window?.Frames??0);
    public int CoreDetailRequests{get;private set;}public int ProcessDetailRequests{get;private set;}
    public bool? PerPixelOpacity{get;private set;}
    public bool IsOpen=>_window is not null;public int Opens{get;private set;}public int Reads{get;private set;}public int Frames{get;private set;}
    public DashboardHost(Func<BarSettings> settings,Action<BarSettings> save,Func<DeviceOverview> device,Func<IPopupSource> metrics,Func<BarDisplay> display,DashboardActions actions,bool diagnostic=false,Action<string,bool>? phase=null,RecentSampleCache? recent=null)
    {_settings=settings;_save=save;_device=device;_metrics=metrics;_display=display;_actions=actions;_diagnostic=diagnostic;_phase=phase;_recent=recent??new();}
    public void Open(string page="Dashboard")
    {
        if(_window is not null){_window.Select(page);return;}
        _cleanup.RemoveAll(t=>t.IsCompleted);Opens++;_window=new(_settings,_save,_device,_metrics,_display(),_actions,page,Close,_diagnostic,name=>{if(PageHistory.Count<512)PageHistory.Add(name);Record("Select",name);},Task.WhenAll(_cleanup),_phase,_recent);PerPixelOpacity=_window.PerPixelOpacity;Record("Open",page);
    }
    public void Close()
    {
        if(_window is null)return;Record("Close",PageHistory.LastOrDefault()??"Dashboard");var window=_window;_window=null;window.Dispose();Reads+=window.Reads;Frames+=window.Frames;
        CoreDetailRequests+=window.CoreDetailRequests;ProcessDetailRequests+=window.ProcessDetailRequests;
        _cleanup.RemoveAll(x=>x.IsCompleted);_cleanup.AddRange(window.Cleanup);
        _phase?.Invoke("dashboard.closed",false);
    }
    public bool Drain(TimeSpan timeout)=>Task.WaitAll(_cleanup.ToArray(),timeout);
    public void RefreshSettings()=>_window?.RefreshSettings();
    public void Capture(string path,int? percent=null)
    {if(percent.HasValue&&percent is not(100 or 150))throw new ArgumentOutOfRangeException(nameof(percent));if(_window is null)return;_phase?.Invoke("capture.before",true);_window.Capture(path,percent,()=>_clock.Elapsed.TotalSeconds,entry=>{if(Captures.Count<16)Captures.Add(entry);});_phase?.Invoke("capture.after",false);}
    public void Dispose()=>Close();
    private void Record(string action,string page){if(Events.Count<1024)Events.Add(new(action,page,_clock.Elapsed.TotalSeconds,CurrentReads,CurrentFrames));_phase?.Invoke("dashboard."+action+"."+page,false);}

    private sealed class DashboardWindow:IDisposable
    {
        private readonly HwndSource _source;private readonly DashboardVisual _visual;private readonly Task _deviceTask;private bool _closed;
        public int CoreDetailRequests=>_visual.CurrentCoreDetailRequests;public int ProcessDetailRequests=>_visual.CurrentProcessDetailRequests;
        public bool PerPixelOpacity=>_source.CompositionTarget.UsesPerPixelOpacity;
        public int Reads=>_visual.CurrentReads;public int Frames=>_visual.CurrentFrames;public List<Task> Cleanup=>[.._visual.Cleanup,_deviceTask];
        public DashboardWindow(Func<BarSettings> settings,Action<BarSettings> save,Func<DeviceOverview> device,Func<IPopupSource> metrics,BarDisplay display,DashboardActions actions,string page,Action close,bool diagnostic,Action<string> navigation,Task predecessor,Action<string,bool>? phase,RecentSampleCache recent)
        {
            const double width=860,height=580;var scale=1.0; // Actual HWND scale is read immediately after creation.
            var rect=BarLayout.Popup(new(display.Work.Left+display.Work.Width/2,display.Work.Top+40,display.Work.Left+display.Work.Width/2,display.Work.Top+40),display.Work,(int)width,(int)height,BarEdge.Top);
            phase?.Invoke("dashboard.hwnd.before",false);
            _source=new(new HwndSourceParameters("Tidlet Dashboard"){PositionX=rect.Left,PositionY=rect.Top,Width=rect.Width,Height=rect.Height,WindowStyle=unchecked((int)0x80000000),ExtendedWindowStyle=0x40000,UsesPerPixelOpacity=false});
            _source.CompositionTarget.BackgroundColor=DashboardPalette.For(settings().Theme).BaseColor;
            phase?.Invoke("dashboard.hwnd.after",true);
            scale=_source.CompositionTarget.TransformToDevice.M11;
            var w=Math.Min(width,display.Work.Width/scale-24);var h=Math.Min(height,display.Work.Height/scale-24);
            var pixels=new BarRect(display.Work.Left+(display.Work.Width-(int)(w*scale))/2,display.Work.Top+(display.Work.Height-(int)(h*scale))/2,
                display.Work.Left+(display.Work.Width+(int)(w*scale))/2,display.Work.Top+(display.Work.Height+(int)(h*scale))/2);
            _visual=new(w,h,scale,settings,save,metrics,actions,close,page,navigation,predecessor,recent);_source.RootVisual=_visual;
            _visual.Measure(new Size(w,h));_visual.Arrange(new Rect(0,0,w,h));_source.AddHook(Message);actions.Position(_source.Handle,pixels,true);
            phase?.Invoke("dashboard.visual.prepared",false);
            _visual.Focus();var target=_visual;_deviceTask=Task.Run(()=>{phase?.Invoke("device.inventory.before",true);var result=device();phase?.Invoke("device.inventory.after",true);return result;}).ContinueWith(task=>{if(!_closed&&!target.Dispatcher.HasShutdownStarted)target.Dispatcher.BeginInvoke(new Action(()=>{if(!_closed)target.SetDevice(task.IsCompletedSuccessfully?task.Result:new("Windows PC","Windows",[],["Device details could not be read."]));}));},TaskScheduler.Default);
            nint Message(nint hwnd,int message,nint wp,nint lp,ref bool handled)
            {if(diagnostic&&(message==0x0010||message is >=0x0100 and <=0x0109 or >=0x0200 and <=0x020E)){handled=true;return 0;}if(message==0x0010){close();handled=true;}if(message==0x0084){handled=true;return HeaderHitTest(actions.Bounds?.Invoke(hwnd)??pixels,lp,_source.CompositionTarget.TransformToDevice.M11);}return 0;}
        }
        // WM_NCHITTEST uses signed physical screen coordinates. Interactive header
        // elements must stay HTCLIENT so Windows delivers mouse input to WPF.
        private nint HeaderHitTest(BarRect bounds,nint position,double scale)
        {
            var point=new Point(((short)(position.ToInt64()&0xffff)-bounds.Left)/scale,
                ((short)((position.ToInt64()>>16)&0xffff)-bounds.Top)/scale);
            return point.Y>=0&&point.Y<42&&point.X>220&&point.X<_visual.Width-55
                &&_visual.InputHitTest(point) is not SurfaceAction?2:1;
        }
        public void Select(string page)=>_visual.Select(page);
        public void RefreshSettings()
        {
            _visual.RefreshSettings();
            _source.CompositionTarget.BackgroundColor=_visual.BackgroundColor;
        }
        public void Capture(string path,int? percent,Func<double> now,Action<DashboardCapture> captured)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            foreach(var requested in percent.HasValue?new[]{percent.Value}:new[]{100,150})
            {var started=now();var scale=requested/100.0;_visual.Draw(scale);var image=new RenderTargetBitmap((int)Math.Ceiling(_visual.Width*scale),(int)Math.Ceiling(_visual.Height*scale),96*scale,96*scale,PixelFormats.Pbgra32);image.Render(_visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));var output=Path.ChangeExtension(path,$"{requested}.png");using(var file=File.Create(output))encoder.Save(file);captured(new(output,requested,image.PixelWidth,image.PixelHeight,started,now()));}_visual.Draw(_source.CompositionTarget.TransformToDevice.M11);
        }
        public void Dispose(){if(_closed)return;_closed=true;_visual.Dispose();if(!_source.IsDisposed){_source.RootVisual=null;_source.Dispose();}}
    }

    private sealed partial class DashboardVisual:UIElement,IDisposable
    {
        private static readonly string[] Pages=["Dashboard","CPU","GPU","RAM","Disk","Network","Battery","Sensors",GamingPage,"Widgets","Settings"];
        private readonly DrawingVisual _drawing=new();private readonly List<SurfaceAction> _buttons=[];
        private DrawingGroup _backdrop;
        private DashboardPalette _palette;
        public Color BackgroundColor=>_palette.BaseColor;
        private readonly Func<BarSettings> _settings;private readonly Action<BarSettings> _save;private readonly Func<IPopupSource> _metrics;
        private readonly DashboardActions _actions;private readonly Action _close;private ModulePresenter? _presenter;private Task _previous=Task.CompletedTask;private readonly Action<string> _navigation;
        private readonly RecentSampleCache _recent;
        private PowerModeReading? _powerMode;
        private bool ShowsPowerMode=>_page=="Battery"&&_actions.PowerModes is not null;
        private static readonly (PowerMode Mode,string Label)[] PowerChoices=
            [(PowerMode.Efficiency,"Best power efficiency"),(PowerMode.Balanced,"Balanced"),(PowerMode.Performance,"Best performance")];
        private DeviceOverview? _device;private string _page,_status="";private double _scale,_scroll,_bottom;
        private string? _editing;private string _entry="";private bool _selectAll;private int _caret;
        private BarModule? _settingsModule;
        private static readonly Typeface Face=new("Segoe UI Variable, Segoe UI"),Bold=new(new FontFamily("Segoe UI Variable, Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal);
        private static readonly Typeface SettingsFace=new("Segoe Fluent Icons, Segoe MDL2 Assets");
        private Brush _surface=>_palette.Card;
        private Brush _text=>_palette.Text;
        private Brush _dim=>_palette.Muted;
        private Brush _blue=>_customAccent??_palette.Accent;
        public double Width{get;}public double Height{get;}public int Reads{get;private set;}public int Frames{get;private set;}public List<Task> Cleanup{get;}=[];
        public int CurrentReads=>Reads+(_presenter?.Reads??0)+(_widgetReader?.Reads??0);
        public int CurrentFrames=>Frames+(_presenter?.Frames??0)+(_widgetReader?.Frames??0);
        private int _coreDetailRequests,_processDetailRequests;
        public int CurrentCoreDetailRequests=>_coreDetailRequests+(_presenter?.CoreDetailRequests??0);public int CurrentProcessDetailRequests=>_processDetailRequests+(_presenter?.ProcessDetailRequests??0);
        public DashboardVisual(double width,double height,double scale,Func<BarSettings> settings,Action<BarSettings> save,Func<IPopupSource> metrics,DashboardActions actions,Action close,string page,Action<string> navigation,Task predecessor,RecentSampleCache recent)
        {
            Width=width;Height=height;_scale=scale;_settings=settings;_save=save;_metrics=metrics;_actions=actions;_close=close;_page=page;_navigation=navigation;_previous=predecessor;_recent=recent;Focusable=true;AddVisualChild(_drawing);
            _palette=DashboardPalette.For(settings().Theme);
            RefreshAccent(settings().Accent);
            _backdrop=_palette.CreateBackdrop(width,height);
            MouseWheel+=(_,e)=>{if(_presenter is null){if(_settingsMenu is null)_scroll=Math.Clamp(_scroll-e.Delta/3.0,0,Math.Max(0,_bottom-Height+55));Draw(_scale);e.Handled=true;}};
            KeyDown+=HandleKey;TextInput+=(_,e)=>{if(_editing is not null&&!string.IsNullOrEmpty(e.Text)){Insert(e.Text);e.Handled=true;}};Select(page);
        }
        protected override int VisualChildrenCount=>1+_buttons.Count+(_presenter is null?0:1);
        protected override Visual GetVisualChild(int i)=>i==0?_drawing:i<=_buttons.Count?_buttons[i-1]:_presenter!;
        protected override AutomationPeer OnCreateAutomationPeer()=>new DashboardPeer(this);
        public void SetDevice(DeviceOverview device){_device=device;Draw(_scale);}
        public void Select(string page)
        {
            if(!Pages.Contains(page)&&page is not ("About" or BatteryOptimizationPage or TempCleanupPage))page="Dashboard";
            if(page!=_page){_status="";_inspectionCancellation?.Cancel();_inspectionResult=null;ResetTempCleanup();}
            _settingsModule=null;_settingsMenu=null;_page=page;_navigation(page);_scroll=0;_editing=null;
            StopWidgets();
            _powerMode=ShowsPowerMode?_actions.PowerModes!.Read():null;
            if(_presenter is not null){Reads+=_presenter.Reads;Frames+=_presenter.Frames;_coreDetailRequests+=_presenter.CoreDetailRequests;_processDetailRequests+=_presenter.ProcessDetailRequests;_presenter.Dispose();_previous=_presenter.Cleanup;Cleanup.RemoveAll(t=>t.IsCompleted);Cleanup.Add(_previous);RemoveVisualChild(_presenter);_presenter=null;}
            if(!_settings().Pause&&Enum.TryParse<BarModule>(page,true,out var module))
            {
                var top=StripPreviewTop+76;
                var height=Math.Max(1,Height-top-(ShowsPowerMode?55:30));
                _presenter=new(module,Width-250,height,_scale,_settings().Preference(module).UpdateSeconds,_metrics,_previous,_palette);AddVisualChild(_presenter);
                _presenter.SetUnits(_settings().UseBits);
                _presenter.SetTemperatureUnit(_settings().Fahrenheit);
                _presenter.SetWarningThreshold(_settings().ThresholdPercent);
                if(module==BarModule.Battery)_presenter.SetBatteryCapacity(_actions.BatteryCapacity);
                _presenter.WarmStart(_recent);
                _presenter.Measure(new Size(Width-250,height));_presenter.Arrange(new Rect(230,top,Width-250,height));
            }
            if(page=="Widgets")StartWidgets();
            Draw(_scale);
        }
        public void RefreshSettings()
        {
            var settings=_settings();
            RefreshAccent(settings.Accent);
            var palette=DashboardPalette.For(settings.Theme);
            var themeChanged=palette!=_palette;
            if(themeChanged)
            {
                _palette=palette;
                _backdrop=_palette.CreateBackdrop(Width,Height);
            }
            if(Enum.TryParse<BarModule>(_page,true,out var module)&&
                (settings.Pause!=(_presenter is null)||_presenter is not null&&(themeChanged||_presenter.IntervalSeconds!=settings.Preference(module).UpdateSeconds)))
            {
                Select(_page);
                return;
            }
            _presenter?.SetUnits(settings.UseBits);
            _presenter?.SetTemperatureUnit(settings.Fahrenheit);
            _presenter?.SetWarningThreshold(settings.ThresholdPercent);
            if(_page=="Widgets")
            {
                if(settings.Pause)StopWidgets();
                else if(_widgetReader is null)StartWidgets();
            }
            Draw(_scale);
        }
        public void Draw(double scale)
        {
            var focused=_buttons.Find(static button=>button.IsKeyboardFocused)?.Label;
            _scale=scale;foreach(var button in _buttons)RemoveVisualChild(button);_buttons.Clear();
            using var d=_drawing.RenderOpen();
            d.DrawDrawing(_backdrop);
            Label(d,"TIDLET",28,27,13,_dim,true);Label(d,_page,230,19,26,_text,true);
            if(_settings().Pause)Label(d,"Monitoring paused",230,51,10,_dim);
            Button(d,"Close window","×",new Rect(Width-44,16,28,28),_close);
            if(Enum.TryParse<BarModule>(_page,true,out var selected))
            {
                var current=selected;
                if(current==BarModule.Cpu&&_actions.ReadCpuTemperature is not null)
                {
                    var label=_readingCpuTemperature?"Checking…":"Check CPU temp";
                    if(!_readingCpuTemperature&&_actions.LastCpuTemperature?.Invoke() is double celsius)
                    {
                        var temperature=_settings().Fahrenheit?$"{celsius*1.8+32:0.0} °F":$"{celsius:0.0} °C";
                        label=temperature+" · Check again";
                    }
                    DrawPageButton(d,label,new Rect(Width-294,19,139,27),false,()=>_ = ReadCpuTemperature());
                }
                SettingsButton(d,"Settings for "+_page,new Rect(Width-82,16,28,28),()=>OpenModuleSettings(current));
                var enabled=_settings().Items.Contains(current);var toggle=new Rect(Width-132,23,36,20);
                DrawTogglePill(d,toggle,enabled,_dim);
                Hit((enabled?"Hide ":"Show ")+_page+" in bar",toggle,()=>Change(s=>s.WithItemVisibility(current,!s.Items.Contains(current))));
            }
            else if(_page=="Dashboard"&&_settings().GamingMode)
            {
                var center=new Point(Width-66,30);
                d.DrawEllipse(_surface,_palette.CardBorder,center,13,13);
                DashboardIcons.Draw(d,"Gaming",center,_blue);
            }
            for(int i=0;i<Pages.Length;i++)
            {
                var name=Pages[i];var rect=new Rect(20,85+i*38,175,36);var active=_page==name||name=="Battery"&&_page==BatteryOptimizationPage||name=="Disk"&&_page==TempCleanupPage;
                if(active)DrawSettingsSelection(d,rect);
                var iconBrush=active?SelectionText:_dim;
                if(name=="Settings")DrawSettingsGear(d,new Point(38,rect.Top+17),iconBrush,14);
                else DashboardIcons.Draw(d,name,new Point(38,rect.Top+18),iconBrush);
                Label(d,name,59,rect.Top+8,14,active?SelectionText:_text,active);Hit("Open "+name,rect,()=>Select(name));
            }
            SettingsButton(d,"Settings",new Rect(24,Height-55,28,28),()=>Select("Settings"));
            SidebarButton(d,"About and support","About",new Rect(57,Height-55,28,28),()=>Select("About"));
            SidebarButton(d,"Export compatibility report","Report",new Rect(90,Height-55,28,28),()=>Run(()=>_actions.CompatibilityReport()));
            var paused=_settings().Pause;
            SidebarButton(d,paused?"Resume monitoring":"Pause monitoring",paused?"Resume":"Pause",new Rect(123,Height-55,28,28),()=>Change(s=>s with{Pause=!s.Pause}));
            SidebarButton(d,"Exit Tidlet","Exit",new Rect(156,Height-55,28,28),_actions.Quit);
            if(ShowsPowerMode)DrawPowerMode(d,_actions.PowerModes!);
            if(ShowsInspection)DrawInspection(d);
            if(Enum.TryParse<BarModule>(_page,true,out var previewModule))DrawStripPreviews(d,previewModule);
            if(_page==BatteryOptimizationPage)
                DrawPageButton(d,"Back to Battery",new Rect(Width-195,19,145,27),false,()=>Select("Battery"));
            d.PushClip(new RectangleGeometry(new Rect(220,66,Width-240,Height-120)));d.PushTransform(new TranslateTransform(0,-_scroll));
            if(_page=="Dashboard")Overview(d);else if(_page==GamingPage)Gaming(d);else if(_page=="Settings")Settings(d);else if(_page=="About")About(d);else if(_page=="Widgets")Widgets(d);
            else if(_page==BatteryOptimizationPage)DrawBatteryOptimization(d);
            else if(_page==TempCleanupPage)DrawTempCleanup(d);
            else if(_settings().Pause){var y=StripPreviewTop+85;Label(d,"Monitoring is paused",245,y,18,_text,true);Label(d,"Resume monitoring to show live readings.",245,y+40,12,_dim,false,Width-285);_bottom=y+110;}
            d.Pop();d.Pop();
            if(_presenter is null&&_bottom>Height-55){var track=Height-150;var maximum=Math.Max(1,_bottom-Height+55);d.DrawRoundedRectangle(_dim,null,new Rect(Width-10,80+(track-70)*_scroll/maximum,3,70),1.5,1.5);}
            var status=_status.Length>0?_status:_page switch{"CPU"=>_actions.CpuTemperatureStatus?.Invoke()??"","Settings" or GamingPage=>"Changes are saved automatically.",_=>""};
            if(status.Length>0)Label(d,status,235,Height-38,11,_status.Length>0?_text:_dim,false,Width-260);
            DrawSettingsMenu(d);
            _presenter?.Redraw(scale);
            if(focused is not null)
            {
                var replacement=_buttons.Find(button=>button.Label==focused);
                if(replacement is null)Focus();else replacement.Focus();
            }
        }
        private void DrawPowerMode(DrawingContext drawing,IPowerModeControl control)
        {
            var source=_powerMode?.Source;
            var scope=source switch{PowerSource.PluggedIn=>" · Plugged in",PowerSource.Battery=>" · On battery",_=>""};
            Label(drawing,"Power mode"+scope,230,70,14,_text,true);
            DrawPageButton(drawing,"80% charge limit",new Rect(Width-292,67,119,23),false,()=>Select(BatteryOptimizationPage));
            DrawPageButton(drawing,"Windows settings",new Rect(Width-165,67,135,23),false,()=>Run(control.OpenSettings));
            if(source is not{} current||_powerMode?.Mode is null)
            {
                Label(drawing,_powerMode?.Reason??"Windows did not report a configured power mode.",230,103,11,_dim,false,Width-260);
                return;
            }

            var width=(Width-276)/3;
            for(var index=0;index<PowerChoices.Length;index++)
            {
                var (mode,label)=PowerChoices[index];
                var selected=mode==_powerMode.Mode;
                DrawPageButton(drawing,label,new Rect(230+index*(width+8),96,width,30),selected,()=>
                {
                    var error=mode==_powerMode?.Mode?null:control.Apply(current,mode);
                    _powerMode=control.Read();
                    _status=error??(_powerMode.Source==current&&_powerMode.Mode==mode?"Configured power mode: "+label:
                        "Windows did not confirm the selected mode.");
                    Draw(_scale);
                });
            }

        }
        private void DrawPageButton(DrawingContext drawing,string label,Rect bounds,bool selected,Action action,bool pinned=true)
        {
            drawing.DrawRoundedRectangle(selected?_blue:_surface,_palette.CardBorder,bounds,5,5);
            var text=MonitorText.Format(label,Bold,11,selected?SelectionText:_text,_scale);
            text.MaxTextWidth=bounds.Width-8;
            text.MaxLineCount=1;
            text.Trimming=TextTrimming.CharacterEllipsis;
            drawing.DrawText(text,MonitorText.Centered(text,new Point(bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2),_scale));
            Hit(selected?label+" (current)":label,bounds,action,pinned:pinned);
        }
        private void DrawBatteryOptimization(DrawingContext drawing)
        {
            var model=_device?.Model??"";
            var (utility,steps)=model switch
            {
                var name when name.StartsWith("Acer",StringComparison.OrdinalIgnoreCase)
                    => ("Acer Care Center or AcerSense",
                        "Acer Care Center → Checkup → Battery Charge Limit. In AcerSense, open Personal Settings → Battery & USB Charging and turn on Optimized Battery Charging."),
                var name when name.StartsWith("ASUS",StringComparison.OrdinalIgnoreCase)
                    => ("MyASUS","Open Device Settings and turn on Battery Care Mode. On older models, choose Balanced in Battery Health Charging."),
                var name when name.StartsWith("Dell",StringComparison.OrdinalIgnoreCase)
                    => ("MyDell or Dell Power Manager","Choose Custom charging and set Stop Charging to 80%. Use a start threshold allowed by your model."),
                var name when name.StartsWith("Surface",StringComparison.OrdinalIgnoreCase)||
                    name.StartsWith("Microsoft",StringComparison.OrdinalIgnoreCase)&&name.Contains("Surface",StringComparison.OrdinalIgnoreCase)
                    => ("Surface app","Open Battery & charging → Charging mode → Limit to 80%, if available."),
                _ => ("Laptop manufacturer's utility","Look for a battery charge limit in the manufacturer's app or firmware. Choose 80% when your model offers it.")
            };
            var width=Width-255;
            Label(drawing,"Set an 80% charge limit on supported laptops.",230,82,14,_text,true,width);
            drawing.DrawRoundedRectangle(_surface,_palette.CardBorder,new Rect(230,119,width,124),9,9);
            Label(drawing,utility,246,134,14,_text,true,width-32);
            Label(drawing,steps,246,163,12,_dim,false,width-32);
            Label(drawing,"Check the limit in the manufacturer's utility. Your laptop applies it.",230,258,12,_dim,false,width);
            _bottom=305;
        }
        private void Overview(DrawingContext d)
        {
            var contentWidth=Width-255;
            _palette.DrawDevice(d,230+contentWidth/2);
            Label(d,_device?.Model??"Reading device details…",230,204,22,_text,true,contentWidth,true);
            Label(d,_device?.OperatingSystem??"Windows",230,242,13,_dim,false,contentWidth,true);
            const double top=280,rowHeight=50;
            double y=top;
            var facts=_device?.Facts??[];
            if(facts.Length>0)d.DrawRoundedRectangle(_surface,_palette.CardBorder,new Rect(230,top,contentWidth,rowHeight*facts.Length),11,11);
            foreach(var fact in facts)
            {
                if(y>top)d.DrawLine(_palette.Divider,new Point(245,y),new Point(Width-40,y));
                _palette.DrawFactMarker(d,y,y==top);
                Label(d,fact.Label,255,y+11,13,_text,false,contentWidth*.31);
                var valueWidth=contentWidth*.62;
                Label(d,fact.Value,Width-40-valueWidth,y+11,13,_text,false,valueWidth,right:true);
                y+=rowHeight;
            }
            if(_device is null)
            {
                d.DrawRoundedRectangle(_surface,_palette.CardBorder,new Rect(230,top,contentWidth,50),11,11);
                Label(d,"Preparing device details…",245,top+14,13,_dim);y=top+50;
            }
            y+=16;
            foreach(var limit in _device?.Limitations??[]){Label(d,limit,240,y,11,_dim,false,Width-270);y+=40;}_bottom=y;
        }
        private void DrawTogglePill(DrawingContext d,Rect bounds,bool enabled,Brush? offKnob=null)
        {
            var radius=bounds.Height/2;
            var knob=bounds.Height-6;
            d.DrawRoundedRectangle(enabled?_blue:_palette.Metrics.Track,null,bounds,radius,radius);
            d.DrawEllipse(enabled?Brushes.White:(offKnob??Brushes.White),null,new Point(bounds.Left+(enabled?bounds.Width-radius:radius),bounds.Top+radius),knob/2,knob/2);
        }
        private bool _readingCpuTemperature;
        private async Task ReadCpuTemperature()
        {
            if(_readingCpuTemperature||_actions.ReadCpuTemperature is not { } read)return;
            _readingCpuTemperature=true;
            _status="Checking CPU temperature. Windows may ask for permission.";
            Draw(_scale);
            try
            {
                var result=await read();
                if(_page=="CPU")_status=result;
            }
            finally
            {
                _readingCpuTemperature=false;
                if(IsVisible&&_page=="CPU")Draw(_scale);
            }
        }

        private void ToggleGamingMode()
        {
            var next=!_settings().GamingMode;
            if(_actions.SetGamingMode is{}setGamingMode)
            {
                setGamingMode(next);
                _status=next?"Gaming Mode on. The strip uses your Gaming layout.":"Gaming Mode off.";
                Draw(_scale);
            }
            else Change(s=>s with{GamingMode=next},next?"Gaming Mode on.":"Gaming Mode off.");
        }
        private void Choice(DrawingContext d,string name,string value,ref double y,Action action)
        {
            var rect=new Rect(235,y-3,Width-275,35);
            if(rect.Bottom<=66+_scroll||rect.Top>=Height-54+_scroll){y+=41;return;}
            d.DrawRoundedRectangle(_surface,_palette.CardBorder,rect,6,6);
            Label(d,name,247,y+6,12,_text);Label(d,value,Width-320,y+6,12,_dim,false,265);Hit(name+": "+value,rect,action);
            if(_editing==name){d.DrawRoundedRectangle(_surface,new Pen(_blue,1),rect,6,6);Label(d,_entry.Insert(Math.Clamp(_caret,0,_entry.Length),"│"),247,y+6,12,_blue,true,Width-300);}
            y+=41;
        }
        private void About(DrawingContext d)
        {
            Label(d,"Tidlet Monitor",245,95,26,_text,true);Label(d,"A local Windows system monitor",245,139,14,_dim);
            Label(d,"MIT licensed · No accounts or telemetry",245,185,12,_text);
            Label(d,"UI inspired by Stats (exelban/stats, MIT). Original Windows presentation and assets.",245,228,12,_dim,false,Width-285);
            double y=295;Choice(d,"Troubleshooting","Export compatibility report",ref y,()=>Run(()=>_actions.CompatibilityReport()));
            Label(d,"The report excludes IP/MAC addresses, usernames, serial numbers and process lists.",245,y+12,11,_dim,false,Width-285);_bottom=y+100;
        }
        private void Heading(DrawingContext d,string name,ref double y){y+=12;Label(d,name,243,y,10,_dim,true);y+=32;}
        private void Change(Func<BarSettings,BarSettings> update,string? feedback=null)
        {
            try{_save(update(_settings()).Validate());_status=feedback??"Settings saved.";}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
            {_status="Settings were not saved. "+ex.Message;}
            Draw(_scale);
        }
        private void Run(Func<string> action){try{_status=action();}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or System.Text.Json.JsonException){_status=ex.Message;}Draw(_scale);}
        private void Edit(string field,string value){_editing=field;_entry=value;_caret=value.Length;_selectAll=true;_status="Type a value, Enter to apply, Esc to cancel · Ctrl+V to paste";Focus();Draw(_scale);}
        private void Insert(string text){if(_selectAll){_entry="";_caret=0;_selectAll=false;}if(_entry.Length+text.Length>1024)return;_entry=_entry.Insert(_caret,text);_caret+=text.Length;Draw(_scale);}
        private void HandleKey(object sender,KeyEventArgs e)
        {
            if(HandleSettingsMenuKey(e))return;
            if(_editing is not null)
            {
                if(e.Key==Key.Escape){_editing=null;_status="Edit cancelled";Draw(_scale);}
                else if(e.Key==Key.Enter){var field=_editing;_editing=null;if(field=="Accent color")Change(s=>s with{Accent=_entry.Trim()},"Accent color saved.");else if(field=="Import settings")Run(()=>_actions.ImportSettings(_entry.Trim().Trim('"')));}
                else if(e.Key==Key.Back){if(_selectAll){_entry="";_caret=0;_selectAll=false;}else if(_caret>0){_entry=_entry.Remove(--_caret,1);}Draw(_scale);}
                else if(e.Key==Key.Left){_selectAll=false;_caret=Math.Max(0,_caret-1);}else if(e.Key==Key.Right){_selectAll=false;_caret=Math.Min(_entry.Length,_caret+1);}
                else if(e.Key==Key.Home)_caret=0;else if(e.Key==Key.End)_caret=_entry.Length;
                else if(e.Key==Key.A&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control))_selectAll=true;
                else if(e.Key==Key.V&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control))Insert(_actions.ClipboardText());
                e.Handled=true;return;
            }
            if(e.Key==Key.Escape){_close();e.Handled=true;}
            if(_presenter is null&&e.Key is Key.PageDown or Key.PageUp){_scroll=Math.Clamp(_scroll+(e.Key==Key.PageDown?Height/2:-Height/2),0,Math.Max(0,_bottom-Height+55));Draw(_scale);e.Handled=true;}
            if(e.Key==Key.Tab&&_buttons.Count>0)
            {
                var backwards=(Keyboard.Modifiers&ModifierKeys.Shift)!=0;
                var i=_buttons.FindIndex(static button=>button.IsKeyboardFocused);
                var next=i<0?(backwards?_buttons.Count-1:0):(i+(backwards?-1:1)+_buttons.Count)%_buttons.Count;
                _buttons[next].Focus();e.Handled=true;
            }
        }
        private void Hit(string name,Rect bounds,Action action,bool pinned=false)
        {
            var fixedPosition=pinned||bounds.Left<210||bounds.Top<66;var top=fixedPosition?bounds.Top:bounds.Top-_scroll;
            var bottom=top+bounds.Height;
            if(!fixedPosition){top=Math.Max(66,top);bottom=Math.Min(Height-54,bottom);}
            if(bottom<=top)return;
            var button=new SurfaceAction(name,action);_buttons.Add(button);AddVisualChild(button);
            var visibleBounds=new Rect(bounds.Left,top,bounds.Width,bottom-top);
            button.Measure(visibleBounds.Size);button.Arrange(visibleBounds);
        }
        private void Button(DrawingContext d,string name,string label,Rect rect,Action action){Label(d,label,rect.Left+6,rect.Top+3,17,_dim);Hit(name,rect,action);}
        private void SettingsButton(DrawingContext d,string name,Rect rect,Action action)
        {
            DrawSettingsGear(d,new Point(rect.Left+rect.Width/2,rect.Top+rect.Height/2),_dim,16);
            Hit(name,rect,action);
        }
        private void DrawSettingsGear(DrawingContext d,Point center,Brush brush,double size)
        {
            var text=MonitorText.Format("\uE713",SettingsFace,size,brush,_scale);
            d.DrawText(text,MonitorText.Centered(text,center,_scale));
        }
        private void Label(DrawingContext d,string value,double x,double y,double size,Brush brush,bool bold=false,double? width=null,bool center=false,bool right=false)
        {var t=MonitorText.Format(value,bold?Bold:Face,size,brush,_scale);if(width is{}w){t.MaxTextWidth=w;t.MaxLineCount=2;t.Trimming=TextTrimming.CharacterEllipsis;t.TextAlignment=center?TextAlignment.Center:right?TextAlignment.Right:TextAlignment.Left;}d.DrawText(t,MonitorText.PixelAligned(x,y,_scale));}
        private void SidebarButton(DrawingContext d,string name,string icon,Rect rect,Action action)
        {
            DashboardIcons.Draw(d,icon,new Point(rect.Left+rect.Width/2,rect.Top+rect.Height/2),_dim);
            Hit(name,rect,action);
        }
        public void Dispose(){_inspectionCancellation?.Cancel();ResetTempCleanup();StopWidgets();if(_presenter is not null){Reads+=_presenter.Reads;Frames+=_presenter.Frames;_coreDetailRequests+=_presenter.CoreDetailRequests;_processDetailRequests+=_presenter.ProcessDetailRequests;_presenter.Dispose();Cleanup.Add(_presenter.Cleanup);RemoveVisualChild(_presenter);_presenter=null;}}
        private sealed class DashboardPeer(DashboardVisual owner):UIElementAutomationPeer(owner)
        {protected override string GetNameCore()=>"Tidlet "+owner._page;protected override string GetHelpTextCore()=>owner._page=="Dashboard"&&owner._settings().GamingMode?"Gaming Mode on":"";protected override string GetClassNameCore()=>"TidletDashboard";protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Window;}
    }
    private sealed class SurfaceAction:UIElement
    {
        private static readonly Pen FocusBorder=PopupPalette.Stroke(PopupPalette.Solid(0x52A9FC),1);
        private readonly string _name;private readonly Action _action;
        internal string Label=>_name;
        protected override HitTestResult HitTestCore(PointHitTestParameters parameters)=>new PointHitTestResult(this,parameters.HitPoint);
        public SurfaceAction(string name,Action action){_name=name;_action=action;Focusable=true;IsKeyboardFocusedChanged+=(_,_)=>InvalidateVisual();MouseLeftButtonUp+=(_,e)=>{_action();e.Handled=true;};KeyDown+=(_,e)=>{if(e.Key is Key.Enter or Key.Space){_action();e.Handled=true;}};}
        protected override void OnRender(DrawingContext drawing)
        {
            if(IsKeyboardFocused&&RenderSize.Width>2&&RenderSize.Height>2)
                drawing.DrawRoundedRectangle(null,FocusBorder,new Rect(1,1,RenderSize.Width-2,RenderSize.Height-2),5,5);
        }
        protected override AutomationPeer OnCreateAutomationPeer()=>new ActionPeer(this);
        private sealed class ActionPeer(SurfaceAction owner):UIElementAutomationPeer(owner),IInvokeProvider
        {protected override string GetNameCore()=>owner._name;protected override string GetClassNameCore()=>"TidletAction";protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Button;public override object? GetPattern(PatternInterface p)=>p==PatternInterface.Invoke?this:base.GetPattern(p);public void Invoke()=>owner.Dispatcher.BeginInvoke(owner._action);}
    }
}
