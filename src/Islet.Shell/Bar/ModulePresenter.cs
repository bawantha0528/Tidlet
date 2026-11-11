using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Islet.Core.Bar;
using Islet.Core.Popups;
namespace Islet.Shell.Bar;

/// <summary>Owns one wide application page and its visible-only metric source.</summary>
internal sealed class ModulePresenter:UIElement,IDisposable
{
    private readonly ApplicationModuleVisual _visual;private readonly DispatcherTimer _timer;
    private readonly Func<IPopupSource> _factory;private readonly BarModule _module;
    private readonly SampleRing _history=new(121),_averages=new(901);private readonly Stopwatch _clock=Stopwatch.StartNew();
    private Task<PopupSample>? _pending;private IPopupSource? _source;private bool _closed;private readonly Task _predecessor;
    private PopupDetails _pendingDetails;
    private RecentSampleCache? _recent;
    public void WarmStart(RecentSampleCache recent){_recent=recent;if(recent.TryGet(_module,out var sample))_visual.ShowPrevious(sample!);}
    public int Reads{get;private set;}public int Frames{get;private set;}public Task Cleanup{get;private set;}=Task.CompletedTask;
    public int CoreDetailRequests{get;private set;}public int ProcessDetailRequests{get;private set;}
    public int IntervalSeconds { get; }
    public ModulePresenter(BarModule module,double width,double height,double scale,int seconds,Func<IPopupSource> factory,Task predecessor)
        :this(module,width,height,scale,seconds,factory,predecessor,DashboardPalette.For(BarTheme.Dark)){}
    internal ModulePresenter(BarModule module,double width,double height,double scale,int seconds,Func<IPopupSource> factory,Task predecessor,DashboardPalette palette)
    {
        _module=module;_factory=factory;_predecessor=predecessor;IntervalSeconds=seconds;_visual=new(module,width,height,scale,palette);AddVisualChild(_visual);
        _visual.Measure(new Size(width,height));_visual.Arrange(new Rect(0,0,width,height));
        _timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(seconds)};_timer.Tick+=Tick;Tick(null,EventArgs.Empty);_timer.Start();
    }
    protected override int VisualChildrenCount=>1;protected override Visual GetVisualChild(int i)=>i==0?_visual:throw new ArgumentOutOfRangeException(nameof(i));
    private void Tick(object? sender,EventArgs e)
    {
        if(_closed||!_predecessor.IsCompleted)return;
        PresentCompleted();if(_module==BarModule.Sensors&&Frames>0)return;
        if(_pending is null)
        {
            var details=_visual.Details;
            _pendingDetails=details;
            if(details.HasFlag(PopupDetails.Cores))CoreDetailRequests++;
            if(details.HasFlag(PopupDetails.Processes))ProcessDetailRequests++;
            Reads++;_pending=Task.Run(()=>
            {
                try{if(_source is null){_source=_factory();if(_source is IPopupDetailSource initial)initial.SetDetails(details);_source.Probe(_module);}
                    else if(_source is IPopupDetailSource current)current.SetDetails(details);return _source.Read(_module);}
                catch(Exception ex) when(ex is not OutOfMemoryException and not AccessViolationException and not StackOverflowException)
                {return new PopupSample(null,null,null,null,null,null,TimeSpan.Zero,null,[],[],[],"Windows readings unavailable: "+ex.Message);}
            });
            _=_pending.ContinueWith(_=>{if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(PresentCompleted));},TaskScheduler.Default);
        }
    }
    private void PresentCompleted()
    {
        if(_closed||_pending?.IsCompleted!=true)return;
        var sample=_pending.GetAwaiter().GetResult();_pending=null;var now=_clock.Elapsed.TotalSeconds;
        if(sample.WarmingUp)
        {
            if(_recent?.TryGet(_module,out var previous)==true)_visual.ShowPrevious(previous!);
            else _visual.ShowMeasuring(sample);
        }
        if(_pendingDetails!=_visual.Details)sample=sample with{Cores=Enumerable.Repeat(double.NaN,sample.Cores.Length).ToArray(),Processes=[]};
        var value=_module==BarModule.Cpu?sample.Cpu:sample.Memory?.UsedPercent;if(value is{}v){_history.Push(now,v);_averages.Push(now,v);}
        _recent?.Store(_module,sample);_visual.Update(sample,_history,_averages,now);Frames++;
        if(_module==BarModule.Sensors)_timer.Stop();
    }
    public void Redraw(double scale)=>_visual.Redraw(scale);
    public void SetUnits(bool useBits)=>_visual.SetUnits(useBits);
    public void Dispose()
    {
        if(_closed)return;_closed=true;_timer.Stop();_timer.Tick-=Tick;
        if(_pending is{IsCompleted:false} task)Cleanup=task.ContinueWith(_=>_source?.Dispose(),TaskScheduler.Default);else{_source?.Dispose();Cleanup=_predecessor;}
    }
}
