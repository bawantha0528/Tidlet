using Islet.Core.Bar;
using Islet.Shell.Bar;
namespace Islet.Shell.Tests;
[TestClass]
public sealed class MonitorBarHostTests
{
    [TestMethod]
    public void HoverStartsHiddenRevealsKeepsPopupAndSuspendsSampling()
    {
        Exception? failure=null;var thread=new Thread(()=>{try
        {
            var desktop=new Desktop(false);int reads=0;
            using var host=new MonitorBarHost(desktop,new BarSettings{RevealAtTop=true,Items=[BarModule.Cpu],IntervalSeconds=1},
                ()=>{reads++;return new(10,50,null,"CPU","RAM","Unavailable","No battery",null,null);},()=>{},_=>{});
            var surface=desktop.Surfaces.Last();Assert.IsTrue(surface.Hidden);Assert.AreEqual(0,reads);
            desktop.Cursor=new BarPoint(-100,0);PumpUntil(()=>!surface.Hidden);Assert.IsTrue(reads>0);
            host.PopupDisplay=()=>"left";desktop.Cursor=new BarPoint(-100,400);
            var before=Environment.TickCount64;PumpUntil(()=>Environment.TickCount64-before>=700);Assert.IsFalse(surface.Hidden);
            host.PopupDisplay=null;PumpUntil(()=>surface.Hidden);var stopped=reads;
            before=Environment.TickCount64;PumpUntil(()=>Environment.TickCount64-before>=1100);Assert.AreEqual(stopped,reads);
        }catch(Exception ex){failure=ex;}}){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));if(failure is not null)throw new AssertFailedException("Top-edge reveal lifecycle failed",failure);
    }
    private static void PumpUntil(Func<bool> condition)
    {
        var frame=new System.Windows.Threading.DispatcherFrame();var start=Environment.TickCount64;
        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(_,_)=>{if(condition()||Environment.TickCount64-start>=2200){timer.Stop();frame.Continue=false;}};
        timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);Assert.IsTrue(condition(),"Timed out waiting for the reveal state.");
    }
    [TestMethod]
    public void EmptyLayoutHidesBarKeepsTrayAndStopsPollingUntilAnItemIsEnabled()
    {
        Exception? failure=null;var thread=new Thread(()=>{try
        {
            var desktop=new Desktop(false);int reads=0,suppressed=0;
            using var host=new MonitorBarHost(desktop,new BarSettings{Items=[],IntervalSeconds=1},()=>{reads++;return new(10,50,null,"CPU","RAM","Network","No battery",null,null);},()=>{},_=>{});
            host.Suppressed+=()=>suppressed++;Assert.AreEqual(0,reads);Assert.IsTrue(desktop.Surfaces.Last().Hidden);Assert.IsTrue(desktop.Surfaces.Last().TrayPresent);
            host.ApplySettings(host.CurrentSettings.WithItemVisibility(BarModule.Cpu,true));Assert.AreEqual(1,reads);Assert.IsFalse(desktop.Surfaces.Last().Hidden);
            host.ApplySettings(host.CurrentSettings.WithItemVisibility(BarModule.Cpu,false));Assert.IsTrue(desktop.Surfaces.Last().Hidden);Assert.IsTrue(desktop.Surfaces.Last().TrayPresent);Assert.IsFalse(host.EverHidden);
            var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1200)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreEqual(1,reads);Assert.AreEqual(0,suppressed,"Empty layout must keep the application available");
        }catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));if(failure is not null)throw new AssertFailedException("Empty bar lifecycle failed",failure);
    }
    [TestMethod]
    public void PausePublishesSettingsWithoutPretendingEnvironmentIsHidden()
    {
        Exception? failure=null;var thread=new Thread(()=>{try
        {
            var desktop=new Desktop(false);int reads=0,changed=0,suppressed=0;
            using var host=new MonitorBarHost(desktop,new BarSettings(),()=>{reads++;return new(10,50,null,"CPU","RAM","Unavailable","No battery",null,null);},()=>{},_=>{});
            host.SettingsChanged+=()=>changed++;host.Suppressed+=()=>suppressed++;
            var before=reads;host.ApplySettings(host.CurrentSettings with{Pause=true});Assert.AreEqual(before,reads);Assert.AreEqual(1,changed);Assert.AreEqual(0,suppressed);
            host.ApplySettings(host.CurrentSettings with{Pause=false});Assert.IsTrue(reads>before);Assert.AreEqual(2,changed);Assert.AreEqual(0,suppressed);
        }catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));if(failure is not null)throw new AssertFailedException("Pause behavior failed",failure);
    }
    [TestMethod]
    public void AllMonitorsShareOneMetricSampleAndReleaseEverySurface()=>Run(false);
    [TestMethod]
    public void HiddenDisplaysDoNotReadMetrics()=>Run(true);
    [TestMethod]
    public void VisibilityChecksDoNotPostponeVisibleMetricPolling()=>Run(false,7);
    private static void Run(bool hidden,int seconds=1)
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                var platform=new Desktop(hidden);int reads=0;
                using var host=new MonitorBarHost(platform,new BarSettings{Monitors=MonitorSelection.All},()=>
                {reads++;return new(10,50,null,"CPU","RAM","Network unavailable","No battery",null,null);},()=>{},_=>{},seconds);
                host.Run();if(seconds==1)Assert.AreEqual(hidden?0:1,reads);else Assert.IsTrue(reads>=3,"Visibility checks postponed metric polling");Assert.AreEqual(2,host.WindowCount);
                Assert.IsTrue(host.TimersStopped);Assert.IsTrue(platform.Surfaces.All(x=>x.Disposed));Assert.IsTrue(host.AllTrayRemoved);
            }
            catch(Exception ex){failure=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)));
        if(failure is not null)throw new AssertFailedException("Bar lifecycle failed",failure);
    }
    private sealed class Desktop(bool hidden):IBarDesktop
    {
        public List<Surface> Surfaces=[];
        public BarPoint? Cursor;
        public BarPoint? CursorPosition()=>Cursor;
        public IReadOnlyList<BarDisplay> Displays()=>[new("left",true,new(-1000,0,0,800),new(-1000,0,0,760)),new("right",false,new(0,0,1200,900),new(0,0,1200,860))];
        public IBarSurface Attach(nint h,BarDisplay d,BarSettings s,bool tray){var surface=new Surface(d,hidden,tray){Hidden=s.TopReveal};Surfaces.Add(surface);return surface;}
    }
    private sealed class Surface(BarDisplay d,bool hidden,bool tray):IBarSurface
    {
        public bool Disposed;public BarPlacement Placement=>new(new(d.Bounds.Left,0,d.Bounds.Right,24),1,BarMode.Overlay,null);
        public bool ShouldHide=>hidden;public bool TrayPresent{get;private set;}=tray;
        public event Action? EnvironmentChanged{add{}remove{}}public event Action? TrayMenuRequested{add{}remove{}}
        public bool Hidden;public void SetHidden(bool value){Hidden=value;}public void Reposition(){}public int Menu(string? m,bool p)=>0;public void HandleMessage(int m,nint w,nint l){}
        public void Dispose(){Disposed=true;TrayPresent=false;}
    }
}

