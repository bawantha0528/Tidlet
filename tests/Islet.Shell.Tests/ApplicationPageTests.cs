using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Islet.Core.Bar;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;
using Islet.Core.Popups;
using Islet.Shell.Bar;
namespace Islet.Shell.Tests;
[TestClass]
public sealed class ApplicationPageTests
{
    [TestMethod]
    public void BatteryPowerModeUsesExplicitSelectionAndExposesFallback()=>Sta(()=>
    {
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        var control=new FakePowerModes();
        using var host=new DashboardHost(()=>new BarSettings{Pause=true},_=>{},()=>new DeviceOverview("Mock","Windows",[],[]),
            ()=>new Source(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{},PowerModes:control));
        host.Open("Dashboard");
        Assert.AreEqual(0,control.Reads);
        host.Open("Battery");
        Assert.AreEqual(1,control.Reads);
        var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var visual=(UIElement)window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
        Invoke(new Point(660,109),"Best performance");
        PumpUntil(()=>control.Changes.Count==1);
        Assert.AreEqual((PowerSource.PluggedIn,PowerMode.Performance),control.Changes[0]);
        Assert.AreEqual(PowerMode.Performance,control.Current.Mode);

        control.Rejected=true;
        Invoke(new Point(255,109),"Best power efficiency");
        PumpUntil(()=>control.Changes.Count==2);
        Assert.AreEqual(PowerMode.Performance,control.Current.Mode);
        Assert.AreEqual("Windows denied the change.",visual.GetType().GetField("_status",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(visual));

        control.Current=new(null,null,"Unavailable");
        host.Open("Battery");
        Invoke(new Point(720,77),"Windows settings");
        PumpUntil(()=>control.Fallbacks==1);
        Assert.AreEqual(2,control.Changes.Count);
        host.Close();
        Assert.IsTrue(host.Drain(TimeSpan.FromSeconds(2)));

        void Invoke(Point point,string expected)
        {
            var row=(UIElement)visual.InputHitTest(point);
            var peer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(row)!;
            Assert.AreEqual(expected,peer.GetName());
            ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)!).Invoke();
        }
    });

    private sealed class FakePowerModes:IPowerModeControl
    {
        public PowerModeReading Current=new(PowerSource.PluggedIn,PowerMode.Balanced);
        public readonly List<(PowerSource,PowerMode)> Changes=[];
        public bool Rejected;
        public int Reads,Fallbacks;
        public PowerModeReading Read(){Reads++;return Current;}
        public string? Apply(PowerSource source,PowerMode mode)
        {
            Changes.Add((source,mode));
            if(Rejected)return "Windows denied the change.";
            Current=new(source,mode);
            return null;
        }
        public string OpenSettings(){Fallbacks++;return "Opened mock settings.";}
    }

    [TestMethod]
    public void ThemeSettingRepaintsAndPersistsWithoutChangingTheStripLayout()=>Sta(()=>
    {
        var settings=new BarSettings{Edge=BarEdge.Bottom,Items=[BarModule.Ram,BarModule.Cpu,BarModule.Gpu],
            ItemOrder=[BarModule.Ram,BarModule.Cpu,BarModule.Gpu,BarModule.Network]};
        var settingsPath=Path.GetTempFileName();
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        DashboardHost? host=null;
        host=new DashboardHost(()=>settings,s=>{BarSettingsFile.Save(settingsPath,s);settings=s;host!.RefreshSettings();},()=>new DeviceOverview("Mock","Windows",[],[]),
            ()=>new Source(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{}));
        try
        {
            using(host)
            {
                host.Open("Settings");
                var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
                var visual=(UIElement)window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
                const int backgroundPixel=(75*690+550)*4;
                Assert.IsTrue(Pixels(visual)[backgroundPixel]<60);
                var row=(UIElement)visual.InputHitTest(new Point(260,140));
                var peer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(row)!;
                Assert.AreEqual("Theme: Dark",peer.GetName());
                ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)!).Invoke();
                PumpUntil(()=>settings.Theme==BarTheme.Light);
                Assert.IsTrue(host.IsOpen);
                Assert.IsTrue(Pixels(visual)[backgroundPixel]>230);
            }
            Assert.IsTrue(host.Drain(TimeSpan.FromSeconds(2)));
            var restored=BarSettingsFile.Load(settingsPath,out var notice);
            Assert.IsNull(notice);
            Assert.AreEqual(BarTheme.Light,restored.Theme);
            Assert.AreEqual(BarEdge.Bottom,restored.Edge);
            CollectionAssert.AreEqual(settings.Items,restored.Items);
            CollectionAssert.AreEqual(settings.ItemOrder,restored.ItemOrder);
        }
        finally{File.Delete(settingsPath);}
    });

    [TestMethod]
    public void EveryModuleHeaderCanTurnItsLastBarItemOffAndBackOn()=>Sta(()=>
    {
        foreach(var module in Enum.GetValues<BarModule>())
        {
            var page=module switch{BarModule.Cpu=>"CPU",BarModule.Ram=>"RAM",BarModule.Gpu=>"GPU",_=>module.ToString()};var settings=new BarSettings{Items=[module]};DashboardHost? host=null;
            var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
            host=new DashboardHost(()=>settings,s=>{settings=s;host!.RefreshSettings();},()=>new DeviceOverview("Mock","Windows",[],[]),()=>new Source(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{}));
            using(host)
            {
                host.Open(page);Invoke("Hide "+page+" in bar");PumpUntil(()=>settings.Items.Length==0);Assert.IsTrue(host.IsOpen);
                Invoke("Show "+page+" in bar");PumpUntil(()=>settings.Items.Contains(module));Assert.IsTrue(host.IsOpen);CollectionAssert.AreEqual(new[]{module},settings.Items);host.Close();Assert.IsTrue(host.Drain(TimeSpan.FromSeconds(2)));
            }
            void Invoke(string expected)
            {
                var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
                var visual=window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;var width=(double)visual.GetType().GetProperty("Width")!.GetValue(visual)!;
                var hit=((UIElement)visual).InputHitTest(new Point(width-114,33)) as UIElement;Assert.IsNotNull(hit);
                var peer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(hit)!;Assert.AreEqual(expected,peer.GetName());
                var nativeHit=window.GetType().GetMethod("HeaderHitTest",BindingFlags.Instance|BindingFlags.NonPublic)!;
                foreach(var scale in new[]{1.0,1.25,1.5})
                {
                    var bounds=new BarRect(-1400,-300,0,700);
                    nint At(double x,double y){var px=(short)(bounds.Left+(int)Math.Round(x*scale));var py=(short)(bounds.Top+(int)Math.Round(y*scale));return (nint)(int)((ushort)px|((uint)(ushort)py<<16));}
                    Assert.AreEqual((nint)1,(nint)nativeHit.Invoke(window,[bounds,At(width-114,33),scale])!,"Toggle must receive client mouse input");
                    Assert.AreEqual((nint)1,(nint)nativeHit.Invoke(window,[bounds,At(width-68,30),scale])!,"Settings must receive client mouse input");
                    Assert.AreEqual((nint)1,(nint)nativeHit.Invoke(window,[bounds,At(width-30,30),scale])!,"Close must receive client mouse input");
                    Assert.AreEqual((nint)2,(nint)nativeHit.Invoke(window,[bounds,At(400,25),scale])!,"Empty header must remain draggable");
                }
                hit.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
            }
        }
    });
    [TestMethod]
    public void GpuHeaderToggleSavesVisibilityAndKeepsTheApplicationOpen()=>Sta(()=>
    {
        var settings=new BarSettings();var original=settings.Items.ToArray();var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));int saves=0;DashboardHost? host=null;
        host=new DashboardHost(()=>settings,s=>{settings=s;saves++;host!.RefreshSettings();},()=>new DeviceOverview("Mock","Windows",[],[]),()=>new Source(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{}));
        using(host)
        {
            host.Open("GPU");Invoke("Show GPU in bar");PumpUntil(()=>settings.Items.Contains(BarModule.Gpu));Assert.IsTrue(host.IsOpen);Assert.IsTrue(BarLayout.Items(settings,1200,true).Any(i=>i.Module==BarModule.Gpu));
            Invoke("Hide GPU in bar");PumpUntil(()=>!settings.Items.Contains(BarModule.Gpu));Assert.IsTrue(host.IsOpen);Assert.AreEqual(2,saves);CollectionAssert.AreEqual(original,settings.Items);
            CollectionAssert.AreEqual(new[]{"GPU"},host.PageHistory.ToArray(),"Bar visibility changes should keep the open GPU page and its history.");
            host.Close();Assert.IsTrue(host.Drain(TimeSpan.FromSeconds(2)));
        }
        void Invoke(string name)
        {
            var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
            var visual=window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            var width=(double)visual.GetType().GetProperty("Width")!.GetValue(visual)!;
            var hit=((UIElement)visual).InputHitTest(new Point(width-114,33)) as UIElement;
            Assert.IsNotNull(hit,"The GPU toggle must receive hits within its displayed bounds");
            Assert.AreEqual(name,System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(hit)!.GetName());
            var buttons=(System.Collections.IEnumerable)visual.GetType().GetField("_buttons",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(visual)!;
            foreach(UIElement button in buttons){var peer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(button);if(peer?.GetName()==name){((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)!).Invoke();return;}}
            Assert.Fail("Missing accessible toggle: "+name);
        }
    });
    [TestMethod]
    public void ColdAndRecentLoadingStatesRemainShapedAndDoNotInventHistory()=>Sta(()=>
    {
        var type=typeof(DashboardHost).Assembly.GetType("Islet.Shell.Bar.ApplicationModuleVisual",true)!;
        var visual=(UIElement)Activator.CreateInstance(type,[BarModule.Cpu,690.0,550.0,1.0])!;visual.Measure(new Size(690,550));visual.Arrange(new Rect(0,0,690,550));var cold=Pixels(visual);
        Assert.IsTrue(cold[(220*690+400)*4+3]>0,"Cold start should contain a shaped loading card");
        var sample=new PopupSample(20,5,15,80,2.5,4,TimeSpan.Zero,null,[],[],[],"Previous fixture");
        type.GetMethod("ShowPrevious")!.Invoke(visual,[sample]);var previous=Pixels(visual);Assert.IsFalse(cold.SequenceEqual(previous));
        var history=new SampleRing(121);type.GetMethod("Update")!.Invoke(visual,[sample with{Cpu=null,WarmingUp=true},history,history,1.0]);CollectionAssert.AreEqual(previous,Pixels(visual));Assert.AreEqual(0,history.Count);
        type.GetMethod("Update")!.Invoke(visual,[sample with{Cpu=null,Explanation="Permission denied"},history,history,2.0]);Assert.IsFalse(previous.SequenceEqual(Pixels(visual)),"A real failure must replace the cached view");
    });
    [TestMethod]
    public void FirstCompletedReadingAppearsBeforeNextPollWithoutStartingAnExtraRead()=>Sta(()=>
    {
        var type=typeof(DashboardHost).Assembly.GetType("Islet.Shell.Bar.ModulePresenter",true)!;var source=new Source();var clock=System.Diagnostics.Stopwatch.StartNew();
        var page=Activator.CreateInstance(type,[BarModule.Cpu,690.0,550.0,1.0,5,(Func<IPopupSource>)(()=>source),Task.CompletedTask])!;
        try
        {
            var frames=type.GetProperty("Frames")!;PumpUntil(()=>Convert.ToInt32(frames.GetValue(page))==1);Assert.IsTrue(clock.Elapsed.TotalSeconds<4,"First frame waited for the five-second polling timer");
            var until=DateTime.UtcNow.AddMilliseconds(300);PumpUntil(()=>DateTime.UtcNow>=until);Assert.AreEqual(1,source.Reads,"Presentation must not trigger another poll");
        }finally{((IDisposable)page).Dispose();Assert.IsTrue(((Task)type.GetProperty("Cleanup")!.GetValue(page)!).Wait(2000));}
    });
    [TestMethod]
    public void NetworkUnitChangeRedrawsWidePageWithoutMutatingRawData()=>Sta(()=>
    {
        var type=typeof(DashboardHost).Assembly.GetType("Islet.Shell.Bar.ApplicationModuleVisual",true)!;
        var visual=(UIElement)Activator.CreateInstance(type,[BarModule.Network,690.0,550.0,1.0])!;visual.Measure(new Size(690,550));visual.Arrange(new Rect(0,0,690,550));
        var sample=new PopupSample(null,null,null,null,null,null,TimeSpan.Zero,null,[],[],[],"Mock",Network:new(125000,250000,0,0,[],"Mock"));
        var history=new SampleRing(121);type.GetMethod("Update")!.Invoke(visual,[sample,history,history,0.0]);var bytes=Pixels(visual);
        type.GetMethod("SetUnits")!.Invoke(visual,[true]);Assert.IsFalse(bytes.SequenceEqual(Pixels(visual)));Assert.AreEqual(125000,sample.Network!.Download);
        type.GetMethod("SetUnits")!.Invoke(visual,[false]);CollectionAssert.AreEqual(bytes,Pixels(visual));
    });
    [TestMethod]
    public void PausedApplicationDoesNotCreateSourcesAndResumeWaitsForPendingDisposal()=>Sta(()=>
    {
        var settings=new BarSettings{Pause=true};var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        using var started=new ManualResetEventSlim();using var release=new ManualResetEventSlim();var first=new Source(started,release);var second=new Source();int factories=0;bool overlap=false;
        using var host=new DashboardHost(()=>settings,_=>{},()=>new DeviceOverview("Mock","Windows",[],[]),()=>{factories++;if(factories==1)return first;overlap=!first.Disposed;return second;},()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{}));
        host.Open("CPU");Assert.IsTrue(host.IsOpen);Assert.AreEqual(0,factories);Assert.AreEqual(0,host.CurrentReads);
        settings=settings with{Pause=false};host.RefreshSettings();Assert.IsTrue(started.Wait(2000));
        settings=settings with{Pause=true};host.RefreshSettings();Assert.IsTrue(host.IsOpen);var reads=host.CurrentReads;
        settings=settings with{Pause=false};host.RefreshSettings();Assert.AreEqual(1,factories,"Resume must wait for the previous pending read and disposal");
        release.Set();PumpUntil(()=>second.Reads>0);Assert.IsFalse(overlap);
        settings=settings with{Pause=true};host.RefreshSettings();var stopped=host.CurrentReads;var until=DateTime.UtcNow.AddMilliseconds(1200);PumpUntil(()=>DateTime.UtcNow>=until);
        Assert.AreEqual(stopped,host.CurrentReads);Assert.IsTrue(stopped>=reads);Assert.IsTrue(host.IsOpen);host.Close();Assert.IsTrue(host.Drain(TimeSpan.FromSeconds(2)));Assert.IsTrue(first.Disposed);Assert.IsTrue(second.Disposed);
    });
    [TestMethod]
    public void RemainingWidePagesRenderRealSampleShapesAndMissingDataWithoutEmbeddingPopups()=>Sta(()=>
    {
        var type=typeof(DashboardHost).Assembly.GetType("Islet.Shell.Bar.ApplicationModuleVisual",true)!;
        PopupSample Base()=>new(null,null,null,null,null,null,TimeSpan.Zero,null,[],[],[],"Fixture unavailable");
        var samples=new Dictionary<BarModule,PopupSample>{
            [BarModule.Gpu]=Base() with{Gpu=new([new(1,"Integrated fixture",35,0,"Fixture adapter"),new(2,"Discrete fixture",70,4000000000,"Fixture adapter")],"Fixture GPUs")},
            [BarModule.Disk]=Base() with{Disk=new([new("Physical fixture",2048,1024,25)],[new("Mock volume",1000000,600000)],"Separate physical counters and volumes")},
            [BarModule.Network]=Base() with{Network=new(2048,1024,10000,20000,[new("Mock Ethernet","Mock MAC","Mock IP","Mock gateway")],"Fixture network")},
            [BarModule.Battery]=Base() with{Battery=new(new(BatteryReadingState.Available,65,BatteryChargingStatus.Discharging,TimeSpan.FromHours(2),-12000,90,"Fixture battery",200,50000,45000),false,"Fixture capacity scope")}
        };
        foreach(var pair in samples)
        {
            var visual=(UIElement)Activator.CreateInstance(type,[pair.Key,690.0,550.0,1.0])!;visual.Measure(new Size(690,550));visual.Arrange(new Rect(0,0,690,550));
            var history=new SampleRing(121);var update=type.GetMethod("Update")!;
            for(int i=0;i<30;i++)update.Invoke(visual,[pair.Value,history,history,(double)i]);
            var valid=Pixels(visual);Assert.IsTrue(valid[(100*690+600)*4+3]>0,"Wide module card did not extend beyond popup width");
            update.Invoke(visual,[Base(),history,history,29.0]);Assert.IsFalse(valid.SequenceEqual(Pixels(visual)),"Missing data left stale readings");
            update.Invoke(visual,[pair.Value,history,history,29.0]);CollectionAssert.AreEqual(valid,Pixels(visual));
            type.GetMethod("Scroll")!.Invoke(visual,[10000.0]);update.Invoke(visual,[pair.Value,history,history,29.0]);
            type.GetMethod("Scroll")!.Invoke(visual,[-10000.0]);update.Invoke(visual,[pair.Value,history,history,29.0]);CollectionAssert.AreEqual(valid,Pixels(visual));
        }
    });
    [TestMethod]
    public void NewModulePagesRejectImpossibleValuesAndExplainAbsentHardware()=>Sta(()=>
    {
        var type=typeof(DashboardHost).Assembly.GetType("Islet.Shell.Bar.ApplicationModuleVisual",true)!;
        foreach(var module in new[]{BarModule.Gpu,BarModule.Disk,BarModule.Network,BarModule.Battery})
        {
            byte[]? missing=null;
            foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity,-1.0,double.MaxValue})
            {
                var visual=(UIElement)Activator.CreateInstance(type,[module,690.0,550.0,1.0])!;visual.Measure(new Size(690,550));visual.Arrange(new Rect(0,0,690,550));
                var sample=new PopupSample(null,null,null,null,null,null,TimeSpan.Zero,null,[],[],[],"Unavailable fixture");
                sample=module switch{
                    BarModule.Gpu=>sample with{Gpu=new([new(1,"Fixture GPU",invalid,0,"Unavailable fixture")],"Unavailable fixture")},
                    BarModule.Disk=>sample with{Disk=new([new("Fixture disk",invalid,invalid,invalid)],[],"Unavailable fixture")},
                    BarModule.Network=>sample with{Network=new(invalid,invalid,0,0,[],"Unavailable fixture")},
                    _=>sample with{Battery=new(new(BatteryReadingState.Available,invalid,BatteryChargingStatus.Unknown,null,null,null,"Unavailable fixture"),null,"Unavailable fixture")}
                };
                var history=new SampleRing(121);type.GetMethod("Update")!.Invoke(visual,[sample,history,history,1.0]);var pixels=Pixels(visual);
                if(missing is null)missing=pixels;else CollectionAssert.AreEqual(missing,pixels,"Invalid values rendered as readings");
            }
        }
    });
    [TestMethod]
    public void InvalidCoreValuesRenderAsMissingAndScrollingPreservesVisibleContent()=>Sta(()=>
    {
        var type=typeof(DashboardHost).Assembly.GetType("Islet.Shell.Bar.ApplicationModuleVisual",true)!;
        var visual=(UIElement)Activator.CreateInstance(type,[BarModule.Cpu,690.0,550.0,1.0])!;
        visual.Measure(new Size(690,550));visual.Arrange(new Rect(0,0,690,550));
        var history=new SampleRing(121);for(int i=0;i<30;i++)history.Push(i,25);
        PopupSample Sample(double core)=>new(25,5,20,75,2.5,4,TimeSpan.FromHours(1),null,Enumerable.Repeat(core,8).ToArray(),new int[8],[new(1,"Mock process",10,100)],"Mock readings");
        var update=type.GetMethod("Update")!;var scroll=type.GetMethod("Scroll")!;
        update.Invoke(visual,[Sample(double.NaN),history,history,29.0]);var missing=Pixels(visual);
        foreach(var invalid in new[]{double.PositiveInfinity,double.NegativeInfinity,-1.0,101.0,double.MaxValue})
        {update.Invoke(visual,[Sample(invalid),history,history,29.0]);CollectionAssert.AreEqual(missing,Pixels(visual),"Invalid cores must not produce a filled bar");}
        update.Invoke(visual,[Sample(50),history,history,29.0]);var initial=Pixels(visual);Assert.IsFalse(initial.SequenceEqual(missing));
        scroll.Invoke(visual,[10000.0]);update.Invoke(visual,[Sample(50),history,history,29.0]);Assert.IsFalse(initial.SequenceEqual(Pixels(visual)),"Scroll must reveal lower content");
        scroll.Invoke(visual,[-10000.0]);update.Invoke(visual,[Sample(50),history,history,29.0]);CollectionAssert.AreEqual(initial,Pixels(visual));
    });
    [TestMethod]
    public void ScrollingRequestsOnlyVisibleDetailsAndDisposalStopsWork()=>Sta(()=>
    {
        var type=typeof(DashboardHost).Assembly.GetType("Islet.Shell.Bar.ModulePresenter",true)!;var source=new DetailSource();
        var page=Activator.CreateInstance(type,[BarModule.Ram,610.0,480.0,1.0,1,(Func<IPopupSource>)(()=>source),Task.CompletedTask])!;
        var visual=type.GetField("_visual",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(page)!;
        var scroll=visual.GetType().GetMethod("Scroll")!;
        try
        {
            PumpUntil(()=>source.Reads>=2);Assert.IsFalse(source.Interests.Contains(PopupDetails.Processes));Assert.AreEqual("Set:None",source.Order.ToArray()[0]);Assert.AreEqual("Probe",source.Order.ToArray()[1]);
            scroll.Invoke(visual,[10000.0]);PumpUntil(()=>source.Interests.Contains(PopupDetails.Processes));
            var count=source.Reads;scroll.Invoke(visual,[-10000.0]);PumpUntil(()=>source.Reads>count&&source.Last==PopupDetails.None);
        }
        finally{((IDisposable)page).Dispose();Assert.IsTrue(((Task)type.GetProperty("Cleanup")!.GetValue(page)!).Wait(2000));}
        var stopped=source.Reads;var until=DateTime.UtcNow.AddMilliseconds(150);PumpUntil(()=>DateTime.UtcNow>=until);
        Assert.AreEqual(stopped,source.Reads);Assert.IsTrue(source.Disposed);Assert.IsFalse(source.Overlap);
    });
    [TestMethod]
    public void CoreInterestTracksViewportWithoutLosingOffscreenProcessSection()=>Sta(()=>
    {
        var type=typeof(DashboardHost).Assembly.GetType("Islet.Shell.Bar.ApplicationModuleVisual",true)!;
        var visual=Activator.CreateInstance(type,[BarModule.Cpu,610.0,300.0,1.0])!;var details=type.GetProperty("Details")!;
        type.GetMethod("Update")!.Invoke(visual,[new PopupSample(10,3,7,90,null,null,TimeSpan.Zero,null,new double[8],new int[8],[],"Mock"),new SampleRing(121),new SampleRing(901),0.0]);
        Assert.AreEqual(PopupDetails.None,(PopupDetails)details.GetValue(visual)!);
        type.GetMethod("Scroll")!.Invoke(visual,[450.0]);Assert.AreEqual(PopupDetails.Cores,(PopupDetails)details.GetValue(visual)!);
        type.GetMethod("Scroll")!.Invoke(visual,[10000.0]);Assert.AreEqual(PopupDetails.Processes,(PopupDetails)details.GetValue(visual)!);
        type.GetMethod("Scroll")!.Invoke(visual,[-10000.0]);Assert.AreEqual(PopupDetails.None,(PopupDetails)details.GetValue(visual)!);
    });
    private static void PumpUntil(Func<bool> condition)
    {
        var frame=new DispatcherFrame();var clock=System.Diagnostics.Stopwatch.StartNew();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(_,_)=>{if(condition()||clock.Elapsed.TotalSeconds>12){timer.Stop();frame.Continue=false;}};timer.Start();Dispatcher.PushFrame(frame);Assert.IsTrue(condition(),"Timed out waiting for detail lifecycle");
    }
    private sealed class DetailSource:IPopupSource,IPopupDetailSource
    {
        public int Reads;public bool Disposed,Overlap;private bool _reading;public PopupDetails Last;
        public readonly System.Collections.Concurrent.ConcurrentQueue<PopupDetails> Interests=[];public readonly System.Collections.Concurrent.ConcurrentQueue<string> Order=[];
        public void SetDetails(PopupDetails details){Overlap|=_reading||Disposed;Last=details;Order.Enqueue("Set:"+details);}
        public CapabilityProbeResult Probe(BarModule module){Order.Enqueue("Probe");return CapabilityProbeResult.Supported;}
        public PopupSample Read(BarModule module){_reading=true;Overlap|=Disposed;Interests.Enqueue(Last);Interlocked.Increment(ref Reads);_reading=false;return new(10,3,7,90,null,null,TimeSpan.Zero,new(100,60,40,100,null,null,null),[],[],[],"Mock");}
        public void Dispose(){Overlap|=_reading;Disposed=true;}
    }
    [TestMethod]
    public void SingleScaleCaptureWritesOnlyRequestedImageAndRecordsDimensions()=>Sta(()=>
    {
        var directory=Path.Combine(AppContext.BaseDirectory,"capture-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try{foreach(var percent in new[]{100,150})
        {
            var display=new BarDisplay("mock",true,new(0,0,1600,1200),new(0,0,1600,1160));
            using var host=new DashboardHost(()=>new BarSettings(),_=>{},()=>new DeviceOverview("Mock","Windows",[],[]),()=>new Source(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{}));
            host.Open();host.Capture(Path.Combine(directory,"export.png"),percent);Assert.AreEqual(1,host.Captures.Count);
            var capture=host.Captures[0];Assert.AreEqual(percent,capture.Percent);Assert.AreEqual(860*percent/100,capture.Width);Assert.AreEqual(580*percent/100,capture.Height);Assert.IsTrue(capture.CompletedSeconds>=capture.StartedSeconds);
            var images=Directory.GetFiles(directory,"*.png");Assert.AreEqual(1,images.Length);using(var file=File.OpenRead(images[0])){var image=BitmapFrame.Create(file);Assert.AreEqual(capture.Width,image.PixelWidth);Assert.AreEqual(capture.Height,image.PixelHeight);}File.Delete(images[0]);host.Close();Assert.IsTrue(host.Drain(TimeSpan.FromSeconds(2)));
        }}finally{foreach(var file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);}
    });
    [TestMethod]
    public void ReopeningApplicationWaitsForPriorWindowWorkerAndAccountsActiveReads()=>Sta(()=>
    {
        using var started=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var first=new Source(started,release);var second=new Source();int factories=0;bool overlap=false;
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        using var host=new DashboardHost(()=>new BarSettings(),_=>{},()=>new DeviceOverview("Mock","Windows",[],[]),()=>{factories++;if(factories==1)return first;overlap=!first.Disposed;return second;},()=>display,
            new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{}));
        host.Open("CPU");Assert.IsTrue(started.Wait(2000));Assert.AreEqual(1,host.CurrentReads);
        host.Close();host.Open("RAM");Assert.AreEqual(1,factories);
        var unblock=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};var finish=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3.3)};Exception? failure=null;
        unblock.Tick+=(_,_)=>{unblock.Stop();release.Set();};
        finish.Tick+=(_,_)=>{finish.Stop();try{host.Close();Assert.IsTrue(host.Drain(TimeSpan.FromSeconds(2)));Assert.AreEqual(2,factories);Assert.IsFalse(overlap);Assert.IsTrue(first.Disposed&&second.Disposed);Assert.IsTrue(host.Reads>=2);Assert.AreEqual(host.Reads,host.CurrentReads);Assert.AreEqual(2,host.Events.Count(x=>x.Action=="Close"));}catch(Exception ex){failure=ex;}Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);};
        unblock.Start();finish.Start();Dispatcher.Run();if(failure is not null)throw failure;
    });
    [TestMethod]
    public void ApplicationUsesOpaqueTargetAndOverviewNeverCreatesMetricSource()=>Sta(()=>
    {
        int factories=0;var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        using var host=new DashboardHost(()=>new BarSettings(),_=>{},()=>new DeviceOverview("Mock PC","Windows",[],[]),()=>{factories++;return new Source();},()=>display,
            new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"","".ToString,"".ToString,()=>{}));
        Assert.IsNull(host.PerPixelOpacity);host.Open();Assert.AreEqual(false,host.PerPixelOpacity);
        host.Close();Assert.IsTrue(host.Drain(TimeSpan.FromSeconds(2)));Assert.AreEqual(0,factories);Assert.AreEqual(0,host.Reads);Assert.IsFalse(host.IsOpen);
    });
    [TestMethod]
    public void WidePagesRenderBeyondPopupWidthAndRefreshWithoutStaleValues()=>Sta(()=>
    {
        var type=typeof(DashboardHost).Assembly.GetType("Islet.Shell.Bar.ApplicationModuleVisual",true)!;
        foreach(var module in new[]{BarModule.Cpu,BarModule.Ram})
        {
            var visual=(UIElement)Activator.CreateInstance(type,[module,690.0,550.0,1.0])!;
            visual.Measure(new Size(690,550));visual.Arrange(new Rect(0,0,690,550));
            var history=new SampleRing(121);for(int i=0;i<30;i++)history.Push(i,25);
            PopupSample Sample(double cpu)=>new(cpu,5,cpu-5,100-cpu,2.5,4,TimeSpan.FromHours(1),new(100,(ulong)(100-cpu),50,100,20,10,100),[20,40],[0,0],[],"Mock readings");
            var update=type.GetMethod("Update")!;update.Invoke(visual,[Sample(25),history,history,29.0]);var before=Pixels(visual);
            Assert.IsTrue(before[(100*690+600)*4+3]>0,"Wide card did not extend beyond compact popup width");
            update.Invoke(visual,[Sample(75),history,history,29.0]);Assert.IsFalse(before.SequenceEqual(Pixels(visual)),"Metric update did not redraw");
            type.GetMethod("Redraw")!.Invoke(visual,[1.5]);type.GetMethod("Redraw")!.Invoke(visual,[1.0]);
            update.Invoke(visual,[Sample(25),history,history,29.0]);CollectionAssert.AreEqual(before,Pixels(visual));
        }
    });
    [TestMethod]
    public void NavigationWaitsForPredecessorAndDisposedPageStopsReads()=>Sta(()=>
    {
        var type=typeof(DashboardHost).Assembly.GetType("Islet.Shell.Bar.ModulePresenter",true)!;
        var gate=new TaskCompletionSource();var source=new Source();int factories=0;
        var page=Activator.CreateInstance(type,[BarModule.Cpu,690.0,550.0,1.0,1,(Func<IPopupSource>)(()=>{factories++;return source;}),gate.Task])!;
        var release=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};
        var close=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};var finish=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1.1)};var deadline=System.Diagnostics.Stopwatch.StartNew();
        Exception? failure=null;int reads=0;
        release.Tick+=(_,_)=>{release.Stop();try{Assert.AreEqual(0,factories);}catch(Exception ex){failure=ex;}gate.SetResult();};
        close.Tick+=(_,_)=>{if(source.Reads<2&&deadline.Elapsed.TotalSeconds<10)return;close.Stop();((IDisposable)page).Dispose();var cleanup=(Task)type.GetProperty("Cleanup")!.GetValue(page)!;if(!cleanup.Wait(1000))failure=new AssertFailedException("Pending mock read did not drain");reads=source.Reads;finish.Start();};
        finish.Tick+=(_,_)=>{finish.Stop();try{Assert.AreEqual(1,factories);Assert.IsTrue(reads>=2);Assert.AreEqual(reads,source.Reads);Assert.IsTrue(((Task)type.GetProperty("Cleanup")!.GetValue(page)!).Wait(1000));Assert.IsTrue(source.Disposed);}catch(Exception ex){failure=ex;}Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);};
        release.Start();close.Start();Dispatcher.Run();if(failure is not null)throw failure;
    });
    private static byte[] Pixels(Visual visual){var b=new RenderTargetBitmap(690,550,96,96,PixelFormats.Pbgra32);b.Render(visual);var pixels=new byte[690*550*4];b.CopyPixels(pixels,690*4,0);return pixels;}
    private static void Sta(Action body){Exception? failure=null;var thread=new Thread(()=>{try{body();}catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)));if(failure is not null)throw new AssertFailedException("Application page validation failed: "+failure,failure);}
    private sealed class Source(ManualResetEventSlim? started=null,ManualResetEventSlim? release=null):IPopupSource
    {public int Reads;public bool Disposed;public CapabilityProbeResult Probe(BarModule module)=>CapabilityProbeResult.Supported;public PopupSample Read(BarModule module){Reads++;started?.Set();if(release is not null&&!release.Wait(5000))throw new TimeoutException("Mock release timeout");return new(10,3,7,90,null,null,TimeSpan.Zero,null,[],[],[],"Mock");}public void Dispose()=>Disposed=true;}
}

