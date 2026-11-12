using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Islet.Core.Bar;
using Islet.Core.Popups;
namespace Islet.Shell.Bar;
public sealed class PopupCoordinator:IDisposable
{
    private readonly Func<IPopupSource> _factory;private readonly Action<nint,BarRect,bool> _position;private readonly Action _taskManager;
    private readonly Func<BarSettings>? _settings;
    private readonly RecentSampleCache _recent;
    private PopupWindow? _window;private long _lastDismiss;private BarModule? _lastModule;private readonly string? _capture;private readonly bool _holdInput;
    public int Reads{get;private set;}public int Frames{get;private set;}public long PresentationAllocatedBytes{get;private set;}
    private readonly Stopwatch _lifetime=Stopwatch.StartNew();public List<object> Events{get;}=[];
    private int _workers;private readonly List<Task> _cleanups=[];
    public int Opens{get;private set;}public int Closes{get;private set;}public int OutstandingWorkers=>Volatile.Read(ref _workers);
    public bool IsOpen=>_window is not null;public BarModule? Module=>_window?.Module;
    public PopupSample? LastSample{get;private set;}
    public event Action<BarModule>? DashboardRequested;
    public PopupCoordinator(Func<IPopupSource> factory,Action<nint,BarRect,bool> position,Action taskManager,string? capture=null,bool holdInput=false,Func<BarSettings>? settings=null,RecentSampleCache? recent=null)
    {_factory=factory;_position=position;_taskManager=taskManager;_capture=capture;_holdInput=holdInput;_settings=settings;_recent=recent??new();}
    public void Toggle(BarModule module,BarRect item,BarDisplay display,double scale,bool activate=true)
    {
        if(!Enum.IsDefined(module))return;
        var settings=_settings?.Invoke();
        if(settings?.Pause==true){Close();return;}
        if(settings?.Preference(module).PopupEnabled==false)return;
        if(_window?.Module==module){Close();return;}
        if(_window is null&&_lastModule==module&&Stopwatch.GetElapsedTime(_lastDismiss).TotalMilliseconds<200)return;
        Close();Opens++;if(Events.Count<64)Events.Add(new{Action="Open",Module=module.ToString(),Seconds=_lifetime.Elapsed.TotalSeconds});
        _window=new PopupWindow(module,item,display,scale,activate,_factory,_position,_taskManager,
            ()=>{_lastModule=module;_lastDismiss=Stopwatch.GetTimestamp();Close();},
            DashboardRequested is null?null:()=>{Close();DashboardRequested?.Invoke(module);},
            (sample,allocated)=>{LastSample=sample;Frames++;PresentationAllocatedBytes+=allocated;_recent.Store(module,sample);},()=>Reads++,CanRead,delta=>Interlocked.Add(ref _workers,delta),_holdInput,settings?.Preference(module).UpdateSeconds??1,settings?.UseBits??false,WindowsAppTheme.Resolve(settings?.Theme??BarTheme.Dark),_recent,settings?.Fahrenheit??false,settings?.ThresholdPercent??85);
    }
    private bool CanRead()
    {
        _cleanups.RemoveAll(static task=>task.IsCompletedSuccessfully);
        return OutstandingWorkers==0&&_cleanups.TrueForAll(static task=>task.IsCompleted);
    }
    public void Close()
    {
        if(_window is null)return;var window=_window;_window=null;Closes++;if(Events.Count<64)Events.Add(new{Action="Close",Module=window.Module.ToString(),Seconds=_lifetime.Elapsed.TotalSeconds});
        try{if(_capture is{}path)window.Capture(path);}finally{window.Dispose();_cleanups.RemoveAll(t=>t.IsCompleted);_cleanups.Add(window.Cleanup);}
    }
    public bool Drain(TimeSpan timeout)=>Task.WaitAll(_cleanups.ToArray(),timeout);
    public void Dispose()=>Close();
    private sealed class PopupWindow:IDisposable
    {
        private readonly HwndSource _source;private readonly PopupVisual _visual;private readonly DispatcherTimer _timer;
        private readonly Func<IPopupSource> _factory;private readonly Action _readStarted;private readonly Func<bool> _canRead;private readonly Action<int> _workerDelta;
        public Task Cleanup{get;private set;}=Task.CompletedTask;
        private readonly Action<PopupSample,long> _frame;private IPopupSource? _sensor;private Task<PopupSample>? _pending;
        private readonly SampleRing _history=new(121),_averages=new(901);private readonly Stopwatch _clock=Stopwatch.StartNew();
        private bool _closed;public BarModule Module{get;}
        private readonly double _scale;private readonly double _height;
        private readonly RecentSampleCache _recent;
        public PopupWindow(BarModule module,BarRect item,BarDisplay display,double scale,bool activate,Func<IPopupSource> factory,
            Action<nint,BarRect,bool> position,Action taskManager,Action dismiss,Action? openDashboard,Action<PopupSample,long> frame,Action readStarted,Func<bool> canRead,Action<int> workerDelta,bool holdInput,int intervalSeconds,bool useBits,BarTheme theme,RecentSampleCache recent,bool fahrenheit,int thresholdPercent)
        {
            Module=module;_factory=factory;_readStarted=readStarted;_frame=frame;_scale=scale;_canRead=canRead;_workerDelta=workerDelta;_recent=recent;
            var contentHeight=module switch{BarModule.Cpu=>684,BarModule.Ram=>660,BarModule.Network=>600,BarModule.Battery=>460,BarModule.Disk=>520,BarModule.Sensors=>520,_=>680};var bounds=BarLayout.Popup(item,display.Work,(int)Math.Ceiling(320*scale),(int)Math.Ceiling(contentHeight*scale),item.Top<display.Bounds.Top+display.Bounds.Height/2?BarEdge.Top:BarEdge.Bottom);
            _height=bounds.Height/scale;
            Action inputDismiss=holdInput?()=>{}:dismiss;Action inputTask=holdInput?()=>{}:taskManager;
            _visual=new PopupVisual(module,_height,scale,inputTask,inputDismiss,holdInput,theme,holdInput&&openDashboard is not null?()=>{}:openDashboard)
                { Fahrenheit=fahrenheit,ThresholdPercent=thresholdPercent };
            _visual.SetUnits(useBits);
            if(recent.TryGet(module,out var previous))_visual.ShowPrevious(previous!);
            _source=new(new HwndSourceParameters("Tidlet "+module+" details"){PositionX=bounds.Left,PositionY=bounds.Top,Width=bounds.Width,Height=bounds.Height,
                WindowStyle=unchecked((int)0x80000000),ExtendedWindowStyle=0x80,UsesPerPixelOpacity=true});
            _source.RootVisual=_visual;_source.AddHook(Message);_visual.Measure(new Size(320,_height));_visual.Arrange(new Rect(0,0,320,_height));
            _visual.KeyDown+=(_,e)=>{if(e.Key==Key.Escape){inputDismiss();e.Handled=true;}};
            _timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(intervalSeconds)};_timer.Tick+=Tick;
            position(_source.Handle,bounds,activate);if(activate)_visual.Focus();Tick(null,EventArgs.Empty);_timer.Start();
            nint Message(nint hwnd,int message,nint w,nint l,ref bool handled)
            {
                if(message==0x0007)_visual.Focus();
                if(!holdInput&&message==0x0006&&(w.ToInt64()&0xffff)==0)_visual.Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>{if(!_closed)dismiss();}));
                if(message==0x0010){dismiss();handled=true;}return 0;
            }
        }
        private void Tick(object? sender,EventArgs e)
        {
            if(_closed)return;
            PresentCompleted();
            if(_pending is null&&_canRead())
            {
                _readStarted();_workerDelta(1);_pending=Task.Run(()=>
                {
                    try
                    {
                        if(_sensor is null){_sensor=_factory();_sensor.Probe(Module);}
                        return _sensor.Read(Module);
                    }
                    catch(Exception ex) when(ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
                    {return new PopupSample(null,null,null,null,null,null,TimeSpan.Zero,null,[],[],[],"Windows readings unavailable: "+ex.Message);}
                    finally{_workerDelta(-1);}
                });
                _=_pending.ContinueWith(_=>{if(!_visual.Dispatcher.HasShutdownStarted)_visual.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(PresentCompleted));},TaskScheduler.Default);
            }
        }
        private void PresentCompleted()
        {
            if(_closed||_pending?.IsCompleted!=true)return;
            var sample=_pending.GetAwaiter().GetResult();_pending=null;
            if(sample.WarmingUp)
            {
                if(_recent.TryGet(Module,out var previous))_visual.ShowPrevious(previous!);
                else _visual.ShowMeasuring(sample);
            }
            var value=Module==BarModule.Cpu?sample.Cpu:sample.Memory?.UsedPercent;
            var now=_clock.Elapsed.TotalSeconds;if(value is{}v){_history.Push(now,v);_averages.Push(now,v);}
            var before=GC.GetAllocatedBytesForCurrentThread();_visual.Update(sample,_history,_averages,now);_frame(sample,GC.GetAllocatedBytesForCurrentThread()-before);
            if(Module==BarModule.Sensors&&!sample.WarmingUp&&sample.Sensors is not{Items.Length:>0})_timer.Stop();
        }
        public void Capture(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            foreach(var scale in new[]{1.0,1.5})
            {
                _visual.Redraw(scale);
                var image=new RenderTargetBitmap((int)(320*scale),(int)Math.Ceiling(_height*scale),96*scale,96*scale,PixelFormats.Pbgra32);image.Render(_visual);
                var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.ChangeExtension(path,$"{(int)(scale*100)}.png"));png.Save(file);
            }
            _visual.Redraw(_scale);
        }
        public void Dispose()
        {
            if(_closed)return;_closed=true;_timer.Stop();_timer.Tick-=Tick;
            if(_pending is{IsCompleted:false} pending)Cleanup=pending.ContinueWith(_=>_sensor?.Dispose(),CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
            else _sensor?.Dispose();
            _source.RootVisual=null;_source.Dispose();
        }
    }
}





