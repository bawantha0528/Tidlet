using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Interop;
using Islet.Core.Bar;
using Islet.Core.Metrics;
using Islet.Shell.Bar;
using Islet.Windows.Bar;
using Islet.Windows.Metrics;
using Islet.Windows.Diagnostics;
namespace IsletMonitor;
internal static class BarApplication
{
    public static int Run(string[] args)
    {
        int? seconds=null;string? report=null,capture=null,settingsFile=null,dashboardPage=null;bool gaming=false,reserve=false,bottom=false,all=false;BarModule? popupModule=null;int? closePopupAfter=null,closeDashboardAfter=null,dashboardCycle=null;
        bool profileMemory=false;int? capturePercent=null;var probe=args.Contains("--r1-probe");var seen=new HashSet<string>();
        for(int i=0;i<args.Length;i++)
        {
            if(!seen.Add(args[i]))return 2;
            switch(args[i])
            {
                case "--r1-probe":break;
                case "--gaming":gaming=true;break;
                case "--memory-profile":profileMemory=true;break;
                case "--dashboard-capture-scale":if(++i>=args.Length||!int.TryParse(args[i],out var scalePercent)||scalePercent is not(100 or 150))return 2;capturePercent=scalePercent;break;
                case "--close-dashboard-after":if(++i>=args.Length||!int.TryParse(args[i],out var dc)||dc is<1 or>300)return 2;closeDashboardAfter=dc;break;
                case "--dashboard-cycle-seconds":if(++i>=args.Length||!int.TryParse(args[i],out var cycle)||cycle is<1 or>60)return 2;dashboardCycle=cycle;break;
                case "--dashboard-page":if(++i>=args.Length||args[i] is not("Dashboard" or "CPU" or "GPU" or "RAM" or "Disk" or "Network" or "Battery" or "Sensors" or DashboardHost.GamingPage or "Widgets" or "Settings" or "About" or DashboardHost.BatteryOptimizationPage or DashboardHost.TempCleanupPage))return 2;dashboardPage=args[i];break;
                case "--popup-module":if(++i>=args.Length||!Enum.TryParse<BarModule>(args[i],true,out var module)||!BarSettings.ConfigurableModules.Contains(module))return 2;popupModule=module;break;
                case "--close-popup-after":if(++i>=args.Length||!int.TryParse(args[i],out var close)||close is<1 or>300)return 2;closePopupAfter=close;break;case "--reserve":reserve=true;break;case "--bottom":bottom=true;break;case "--all-monitors":all=true;break;
                case "--exit-after-seconds":if(++i>=args.Length||!int.TryParse(args[i],out var n)||n is <1 or >3600)return 2;seconds=n;break;
                case "--probe-report":if(++i>=args.Length)return 2;report=Path.GetFullPath(args[i]);break;
                case "--capture":if(++i>=args.Length)return 2;capture=Path.GetFullPath(args[i]);break;
                case "--settings-file":if(++i>=args.Length)return 2;settingsFile=Path.GetFullPath(args[i]);break;
                default:return 2;
            }
        }
        if(args.Length>0&&(!probe||seconds is null||report is null||settingsFile is null))return 2;
        if((closeDashboardAfter.HasValue||dashboardCycle.HasValue)&&dashboardPage is null)return 2;
        if(capturePercent.HasValue&&(dashboardPage is null||capture is null))return 2;
        var memory=profileMemory?new MemoryPhaseRecorder():null;Action<string,bool>? phase=memory is null?null:memory.Record;
        phase?.Invoke("app.after-arguments",true);
        Mutex? instance=null;if(!probe){instance=new Mutex(true,@"Local\TidletMonitor.Bar",out var first);if(!first){instance.Dispose();return 0;}}
        using var ownedInstance=instance;
        settingsFile??=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"IsletMonitor","bar.json");
        var settings=BarSettingsFile.Load(settingsFile,out var settingsNotice);
        if(probe)settings=settings with{GamingMode=gaming,Mode=reserve?BarMode.Reserve:BarMode.Overlay,Edge=bottom?BarEdge.Bottom:BarEdge.Top,Monitors=all?MonitorSelection.All:MonitorSelection.Primary};
        RenderOptions.ProcessRenderMode=RenderMode.SoftwareOnly;
        var cpu=new SystemCpuProvider(new NativeSystemCpuSource());var ram=new SystemRamProvider(new NativeSystemRamSource());
        var usagePath=Path.Combine(Path.GetDirectoryName(settingsFile)!,"data-usage.json");
        var capacityHistory=new BatteryCapacityHistory(Path.Combine(Path.GetDirectoryName(settingsFile)!,"battery-capacity.json"));
        static DateOnly LocalDay()=>DateOnly.FromDateTime(DateTime.Now);
        var usage=DataUsage.Load(usagePath,LocalDay());
        UsageSnapshot CurrentUsage()=>usage.Read(LocalDay());
        Action? refreshUsage=null;
        void SelectUsage(UsagePeriod period){usage.Select(period);usage.Save(usagePath,force:true);refreshUsage?.Invoke();}
        var battery=new SystemBatteryProvider(new NativeSystemBatterySource());
        var network=new PhysicalNetworkProvider(new PhysicalNetworkSource(),(download,upload)=>usage.Record(LocalDay(),download,upload));
        var cpuProbe=cpu.Probe();var ramProbe=ram.Probe();var batteryProbe=battery.Probe();var networkProbe=network.Probe();
        using var gpu=new BarGpuSampler(()=>new Islet.Windows.Popups.WindowsPopupSource());
        using var barSensors=new BarSensorSampler();
        using var frames=new PresentMonCapture(Path.Combine(AppContext.BaseDirectory,"PresentMon-2.6.0-x64.exe"));
        var desktop=new WindowsBarDesktop(Path.Combine(Path.GetDirectoryName(settingsFile)!,"bar-recovery"));var recovery=desktop.Recover();
        var reads=0;BarSnapshot? last=null;var sampleClock=System.Diagnostics.Stopwatch.StartNew();
        double cpuAt=double.NegativeInfinity,networkAt=double.NegativeInfinity,gpuAt=double.NegativeInfinity;
        MonitorBarHost? host=null;
        BarSnapshot Read()
        {
            var active=host?.CurrentSettings??settings;
            reads++;var c=cpu.Sample();var r=ram.Sample();var b=battery.Sample();var net=network.Sample();
            usage.Save(usagePath);refreshUsage?.Invoke();capacityHistory.Record(LocalDay(),b);
            var graphics=gpu.Sample(active.VisibleItems.Contains(BarModule.Gpu)&&!active.Pause);
            Islet.Core.Popups.SensorDetails? sensorRead=null;
            if(active.VisibleItems.Contains(BarModule.Sensors)&&!active.Pause)sensorRead=barSensors.Sample();else barSensors.Stop();
            var now=sampleClock.Elapsed.TotalSeconds;
            var priorCpu=c.State==CpuReadingState.WarmingUp&&now-cpuAt<=120&&last?.Cpu is not null;
            var priorNetwork=net.State==NetworkReadingState.WarmingUp&&now-networkAt<=120&&last?.Download is not null;
            var priorGpu=graphics.WarmingUp&&now-gpuAt<=120&&last?.Gpu is not null;
            if(c.State==CpuReadingState.Available)cpuAt=now;
            if(net.State==NetworkReadingState.Available)networkAt=now;
            if(graphics.Percent.HasValue)gpuAt=now;
            return last=new(c.State==CpuReadingState.Available?c.Percent:priorCpu?last?.Cpu:null,r.State==RamReadingState.Available?r.UsedPercent:null,
                b.State==BatteryReadingState.Available?b.Percent:null,priorCpu?"Previous CPU reading · refreshing":c.Explanation,r.Explanation,
                priorNetwork?"Previous traffic reading · refreshing":net.Explanation,b.Explanation,
                net.State==NetworkReadingState.Available?net.BytesReceivedPerSec:priorNetwork?last?.Download:null,
                net.State==NetworkReadingState.Available?net.BytesSentPerSec:priorNetwork?last?.Upload:null,
                CpuLoading:c.State==CpuReadingState.WarmingUp&&!priorCpu,NetworkLoading:net.State==NetworkReadingState.WarmingUp&&!priorNetwork,
                Gpu:priorGpu?last?.Gpu:graphics.Percent,GpuReason:priorGpu?"Previous GPU reading · refreshing":graphics.Explanation,
                GpuLoading:graphics.WarmingUp&&!priorGpu,CpuRefreshing:priorCpu,NetworkRefreshing:priorNetwork,GpuRefreshing:priorGpu,
                Frames:active.GamingMode&&active.VisibleItems.Contains(BarModule.FrameRate)?frames.Read(probe?(uint)Environment.ProcessId:null):default,
                SensorTemp:sensorRead?.CpuPackageCelsius??sensorRead?.MaxTemperatureCelsius,
                SensorReason:sensorRead?.Explanation??"Reading hardware sensors…",SensorLoading:sensorRead is null);
        }
        phase?.Invoke("bar.before",false);
        host=new MonitorBarHost(desktop,settings,Read,()=>{cpu.Reset();network.Reset();gpu.Stop();frames.Stop();barSensors.Stop();},s=>{BarSettingsFile.Save(settingsFile,s);if(settings.GamingMode!=s.GamingMode)frames.Reset();settings=s;},seconds,capture);
        using var ownedHost=host;
        host.Suppressed+=gpu.Stop;host.Suppressed+=frames.Stop;host.Suppressed+=barSensors.Stop;host.Stopping+=gpu.Stop;host.Stopping+=frames.Stop;host.Stopping+=barSensors.Stop;
        phase?.Invoke("bar.visual.prepared",true);
        var recent=new Islet.Core.Popups.RecentSampleCache();
        var cpuTemperature=new CpuTemperatureHelper();
        BarRect? measuredPopupBounds=null;
        void PositionPopup(nint hwnd,BarRect bounds,bool activate)
        {Islet.Windows.Popups.PopupActions.Position(hwnd,bounds,activate);if(probe)measuredPopupBounds=Islet.Windows.Popups.PopupActions.Bounds(hwnd);}
        using var popups=new PopupCoordinator(()=>new Islet.Windows.Popups.WindowsPopupSource(network,includeGpuTemperature:true,cpuTemperature:cpuTemperature.ReadForView),PositionPopup,Islet.Windows.Popups.PopupActions.TaskManager,capture is null?null:Path.ChangeExtension(capture,"popup.png"),probe&&popupModule.HasValue,()=>host.CurrentSettings,recent);
        string Export()
        {
            var path=Path.Combine(Path.GetDirectoryName(settingsFile)!,"Exports","Tidlet-settings-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,BarSettingsFile.Export(host.CurrentSettings));return "Saved: "+path;
        }
        string Import(string path)
        {
            if(new FileInfo(path).Length>262144)throw new InvalidDataException("Settings import exceeds 256 KB.");
            var imported=BarSettingsFile.Import(File.ReadAllText(path)) with{PingEnabled=false,WifiDetailsEnabled=false};
            popups.Close();host.ApplySettings(imported);return "Settings imported. Network checks stay off until you enable them.";
        }
        string Compatibility()
        {
            var device=DeviceOverviewSource.Read();var path=Path.Combine(Path.GetDirectoryName(settingsFile)!,"Reports","Compatibility-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{Version=typeof(BarApplication).Assembly.GetName().Version?.ToString(),Windows=Environment.OSVersion.VersionString,Architecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),Device=device,Cpu=cpuProbe,Ram=ramProbe,Battery=batteryProbe,Network=networkProbe,Sensors="LibreHardwareMonitorLib 0.9.6 (read-only GPU and storage; privileged sensors need an optional helper)"},new JsonSerializerOptions{WriteIndented=true}));return "Report saved: "+path;
        }
        Islet.Core.Popups.IPopupSource ApplicationMetrics(bool includeCpuTemperature){Islet.Core.Popups.IPopupSource source=new Islet.Windows.Popups.WindowsPopupSource(network,includeGpuTemperature:true,cpuTemperature:includeCpuTemperature?cpuTemperature.ReadForView:()=> (cpuTemperature.LastCelsius,false,false));return phase is null?source:new ProfilingPopupSource(source,phase);}
        using var desktopWidgets=new DesktopWidgetHost(Path.Combine(Path.GetDirectoryName(settingsFile)!,"desktop-widgets.json"),
            ()=>host.CurrentSettings,()=>ApplicationMetrics(false),recent,desktop.Displays,Islet.Windows.Popups.PopupActions.Bounds,Islet.Windows.Popups.PopupActions.DesktopWidgetPosition,
            CurrentUsage,SelectUsage);
        refreshUsage=desktopWidgets.RefreshUsage;
        var tempCleanup=new WindowsTempCleanup();
        using var dashboard=new DashboardHost(()=>host.CurrentSettings,s=>{popups.Close();host.ApplySettings(s);},DeviceOverviewSource.Read,()=>ApplicationMetrics(true),()=>desktop.Displays().First(x=>x.Primary),
            new(Islet.Windows.Popups.PopupActions.DashboardPosition,Islet.Windows.Popups.PopupActions.TaskManager,host.Dispose,ClipboardText.Read,Import,Export,Compatibility,()=>{},Islet.Windows.Popups.PopupActions.Bounds,new WindowsPowerModeControl(probe),desktopWidgets.Pin,
                CurrentUsage,SelectUsage,desktopWidgets.PinUsage,SystemDiagnostics.CheckWifi,
                token=>Task.FromResult(SystemDiagnostics.ReadStorageHealth(token)),SystemDiagnostics.OpenStorageCleanup,capacityHistory.Describe,
                tempCleanup.Scan,tempCleanup.Clean,host.SetGamingMode,probe?null:cpuTemperature.ReadOnceAsync,()=>cpuTemperature.LastCelsius,()=>cpuTemperature.LastStatus,()=>ApplicationMetrics(false)),
            probe&&dashboardPage is not null,phase,recent);
        var wasGaming=settings.GamingMode;
        host.SettingsChanged+=()=>{var current=host.CurrentSettings;if(!wasGaming&&current.GamingMode)frames.Reset();else if(wasGaming&&!current.GamingMode)frames.Stop();wasGaming=current.GamingMode;settings=current;popups.Close();dashboard.RefreshSettings();desktopWidgets.Refresh();};
        popups.DashboardRequested+=module=>
        {
            if(!probe)dashboard.Open(module is BarModule.Cpu or BarModule.Ram or BarModule.Gpu
                ? module.ToString().ToUpperInvariant() : module.ToString());
        };
        host.EnvironmentVisibilityChanged+=desktopWidgets.SetHidden;
        host.Stopping+=desktopWidgets.Dispose;
        desktopWidgets.Restore();
        var restoredDesktopWidgets=desktopWidgets.Count;
        host.DashboardRequested+=()=>{if(probe)return;popups.Close();dashboard.Open();};host.SettingsRequested+=()=>{if(probe)return;popups.Close();dashboard.Open("Settings");};
        host.Stopping+=dashboard.Close;
        string? popupDisplay=null;host.PopupDisplay=()=>popups.IsOpen?popupDisplay:null;
        Action<BarModule,BarRect,BarDisplay,double> show=(m,item,display,scale)=>{dashboard.Close();popups.Toggle(m,item,display,scale,!probe);popupDisplay=popups.IsOpen?display.Id:null;};
        if(!probe||popupModule.HasValue)host.ModuleClicked+=show;
        host.Suppressed+=popups.Close;host.Suppressed+=dashboard.Close;
        if(popupModule is{} selected){host.ActivateModule(selected);host.ModuleClicked-=show;}
        if(dashboardPage is not null)dashboard.Open(dashboardPage);
        System.Windows.Threading.DispatcherTimer? applicationClose=null,applicationCycle=null;
        if(closeDashboardAfter is{} closeApplication){applicationClose=new(){Interval=TimeSpan.FromSeconds(closeApplication)};applicationClose.Tick+=(_,_)=>{applicationClose.Stop();applicationCycle?.Stop();dashboard.Close();};applicationClose.Start();}
        if(dashboardCycle is{} cycleSeconds){string[] pages=["CPU","RAM","Dashboard","Sensors"];int pageIndex=0;applicationCycle=new(){Interval=TimeSpan.FromSeconds(cycleSeconds)};applicationCycle.Tick+=(_,_)=>dashboard.Open(pages[++pageIndex%pages.Length]);applicationCycle.Start();}
        host.Suppressed+=()=>applicationCycle?.Stop();
        System.Windows.Threading.DispatcherTimer? closeTimer=null;if(closePopupAfter is{} closeSeconds){closeTimer=new(){Interval=TimeSpan.FromSeconds(closeSeconds)};closeTimer.Tick+=(_,_)=>{closeTimer.Stop();popups.Close();};closeTimer.Start();}
        int? baselineReads=null,baselineFrames=null,baselineApplicationReads=null,baselineApplicationFrames=null;bool? baselineOpen=null,baselineApplicationOpen=null;System.Windows.Threading.DispatcherTimer? baselineTimer=null;
        if(probe){baselineTimer=new(){Interval=TimeSpan.FromSeconds(30)};baselineTimer.Tick+=(_,_)=>{baselineTimer.Stop();baselineReads=popups.Reads;baselineFrames=popups.Frames;baselineOpen=popups.IsOpen;baselineApplicationReads=dashboard.CurrentReads;baselineApplicationFrames=dashboard.CurrentFrames;baselineApplicationOpen=dashboard.IsOpen;};baselineTimer.Start();}
        System.Windows.Threading.DispatcherTimer? dashboardCapture=null;
        if(dashboardPage is not null&&capture is not null&&seconds is{}total){dashboardCapture=new(){Interval=TimeSpan.FromSeconds(Math.Max(1,total-2))};dashboardCapture.Tick+=(_,_)=>{dashboardCapture.Stop();dashboard.Capture(Path.ChangeExtension(capture,"dashboard.png"),capturePercent);};dashboardCapture.Start();}
        host.Run();baselineTimer?.Stop();closeTimer?.Stop();dashboardCapture?.Stop();applicationClose?.Stop();applicationCycle?.Stop();dashboard.Close();var dashboardDrained=dashboard.Drain(TimeSpan.FromSeconds(3));popups.Close();var workersDrained=popups.Drain(TimeSpan.FromSeconds(3));
        var desktopWidgetsDrained=desktopWidgets.Drain(TimeSpan.FromSeconds(3));
        usage.Save(usagePath,force:true);
        barSensors.Stop();var sensorsDrained=barSensors.Drain(TimeSpan.FromSeconds(3));
        frames.Stop();var framesDrained=frames.Drain(TimeSpan.FromSeconds(3));
        gpu.Stop();var gpuDrained=gpu.Drain(TimeSpan.FromSeconds(3));
        phase?.Invoke("app.workers-drained",true);
        if(memory is not null)File.WriteAllText(Path.Combine(Path.GetDirectoryName(report!)!,"memory-phases.json"),JsonSerializer.Serialize(new{AttributionOnly=true,RecordLimit=512,ModuleLimit=128,CollectionSemantics="Last GC snapshot; not live heap or native resident bytes",Records=memory.Snapshot()},new JsonSerializerOptions{WriteIndented=true}));
        if(report is not null)File.WriteAllText(report,JsonSerializer.Serialize(new
        {
            Mode="Tidlet x86 WPF bar and CPU/RAM popups",ProcessArchitecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),PointerSize=IntPtr.Size,
            host.FirstFrame,host.Samples,host.EverHidden,host.VisibilityChecks,host.HiddenMetricReads,host.TimersStopped,host.WindowCount,host.AllTrayRemoved,host.Observations,MeasuredPopupBounds=measuredPopupBounds,
            MetricReads=reads,NetworkReads=network.Reads,LastSnapshot=last,Settings=settings,SettingsNotice=settingsNotice,Recovery=recovery,
            FrameCaptureDrained=framesDrained,SensorWorkersDrained=sensorsDrained,SensorRequests=barSensors.Requests,GpuBarRequests=gpu.Requests,GpuBarWorkersDrained=gpuDrained,
            CpuCapability=cpuProbe,RamCapability=ramProbe,BatteryCapability=batteryProbe,NetworkCapability=networkProbe,
            ProcessRenderPreference=RenderOptions.ProcessRenderMode.ToString(),ActualRenderPipelineVerified=false,
            DashboardPage=dashboardPage,DashboardOpens=dashboard.Opens,DashboardReads=dashboard.Reads,DashboardFrames=dashboard.Frames,DashboardWorkersDrained=dashboardDrained,DashboardPageHistory=dashboard.PageHistory,DashboardPerPixelOpacity=dashboard.PerPixelOpacity,DiagnosticDashboardInputHeld=probe&&dashboardPage is not null,
            DashboardEvents=dashboard.Events,DashboardCoreDetailRequests=dashboard.CoreDetailRequests,DashboardProcessDetailRequests=dashboard.ProcessDetailRequests,BaselineDashboardReads=baselineApplicationReads,BaselineDashboardFrames=baselineApplicationFrames,BaselineDashboardOpen=baselineApplicationOpen,
            DashboardCaptures=dashboard.Captures,RequestedDashboardCapturePercent=capturePercent,
            RestoredDesktopWidgets=restoredDesktopWidgets,DesktopWidgetFrames=desktopWidgets.Frames,DesktopWidgetWorkersDrained=desktopWidgetsDrained,
            ChartWork=popups.Frames,ProcessWork=popups.Reads,DiagnosticPopupInputHeld=probe&&popupModule.HasValue,BaselinePopupReads=baselineReads,BaselinePopupFrames=baselineFrames,BaselinePopupOpen=baselineOpen,PopupEvents=popups.Events,PopupOpens=popups.Opens,PopupCloses=popups.Closes,PresentationAllocatedBytes=popups.PresentationAllocatedBytes,OutstandingPopupWorkers=popups.OutstandingWorkers,PopupWorkersDrained=workersDrained,PopupSample=popups.LastSample,LoadedAssemblies=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetName().Name).ToArray(),NaturalExit=true,ExitCode=0
        },new JsonSerializerOptions{WriteIndented=true,NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals}));
        return 0;
    }
}








