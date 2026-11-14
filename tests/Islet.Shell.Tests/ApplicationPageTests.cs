using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
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
    public void SpeedUpShowsCpuReviewForSustainedUsage()=>Sta(()=>
    {
        var settings = new BarSettings { SustainedSeconds = 30 };
        var diagnosis = new SustainedDiagnostics();
        for (int second=0; second<=30; second+=5) diagnosis.Observe(second, 95, 50, null, settings);
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        var cleanupCalls = 0;
        using var host=new DashboardHost(()=>settings,_=>{},()=>new DeviceOverview("Mock","Windows",[],[]),
            ()=>new Source(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{},
                TempScan:(_,_)=>{cleanupCalls++;return new(0,0,[],false);},Diagnostics:diagnosis));
        host.Open();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var window=typeof(DashboardHost).GetField("_window",flags)!.GetValue(host)!;
        var visual=window.GetType().GetField("_visual",flags)!.GetValue(window)!;
        var buttons=(System.Collections.IEnumerable)visual.GetType().GetField("_buttons",flags)!.GetValue(visual)!;
        AutomationPeer? Find(string name)=>buttons.Cast<UIElement>()
            .Select(UIElementAutomationPeer.CreatePeerForElement).FirstOrDefault(peer=>peer?.GetName()==name);
        void Invoke(string name)
        {
            var peer=Find(name);
            Assert.IsNotNull(peer,name);
            ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
        }
        Invoke("Speed up");
        PumpUntil(()=>Find("Review CPU") is not null);
        Assert.AreEqual("Dashboard",host.PageHistory.Last());
        Invoke("Review CPU");
        PumpUntil(()=>host.PageHistory.Last()=="CPU");
        Assert.AreEqual(0,cleanupCalls);
    });

    [TestMethod]
    public void StripColorPickerAppliesAndRestoresSwatches()=>Sta(()=>
    {
        var display = new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        var settings = new BarSettings { Pause = true };
        using var host = new DashboardHost(()=>settings,value=>settings=value,
            ()=>new DeviceOverview("Mock","Windows",[],[]),()=>new Source(),()=>display,
            new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{}));
        host.Open("Settings");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var window = typeof(DashboardHost).GetField("_window",flags)!.GetValue(host)!;
        var visual = (UIElement)window.GetType().GetField("_visual",flags)!.GetValue(window)!;
        visual.GetType().GetField("_scroll",flags)!.SetValue(visual,460.0);
        visual.GetType().GetMethod("Draw")!.Invoke(visual,[1.0]);

        AutomationPeer? Find(string name)
        {
            var buttons = (System.Collections.IEnumerable)visual.GetType().GetField("_buttons",flags)!.GetValue(visual)!;
            return buttons.Cast<UIElement>().Select(UIElementAutomationPeer.CreatePeerForElement)
                .FirstOrDefault(peer=>peer?.GetName()==name);
        }
        void Invoke(string name)
        {
            var peer = Find(name);
            Assert.IsNotNull(peer,name);
            ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
        }
        Invoke("Strip Background: Default · Choose color");
        PumpUntil(()=>Find("Blue") is not null);
        Invoke("Blue");
        PumpUntil(()=>settings.StripColors.GetValueOrDefault(StripColor.Background)=="#0A84FF");
        Invoke("Strip Background: #0A84FF · Choose color");
        PumpUntil(()=>Find("Default") is not null);
        Invoke("Default");
        PumpUntil(()=>settings.StripColors.Count==0);
    });

    [TestMethod]
    public void CpuTemperatureActionShowsTheReadingImmediatelyAndAfterReopen()=>Sta(()=>
    {
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        double? reading=null;
        using var host=new DashboardHost(()=>new BarSettings{Pause=true},_=>{},()=>new DeviceOverview("Mock","Windows",[],[]),
            ()=>new Source(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{},
                ReadCpuTemperature:()=>{reading=49;return Task.FromResult("CPU temperature checked.");},
                LastCpuTemperature:()=>reading,CpuTemperatureStatus:()=>reading.HasValue?"CPU temperature checked.":null));

        host.Open("CPU");
        var button=Find("Check CPU temp");
        Assert.IsNotNull(button);
        ((System.Windows.Automation.Provider.IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button)!.GetPattern(PatternInterface.Invoke)!).Invoke();
        var updated=$"{49d:0.0} °C · Check again";
        PumpUntil(()=>Find(updated) is not null);
        Assert.IsNotNull(Find(updated));
        host.Close();
        host.Open("CPU");
        Assert.IsNotNull(Find(updated));

        UIElement? Find(string label)
        {
            var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
            var visual=(UIElement)window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            var buttons=(System.Collections.IEnumerable)visual.GetType().GetField("_buttons",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(visual)!;
            return buttons.Cast<UIElement>().FirstOrDefault(b=>UIElementAutomationPeer.CreatePeerForElement(b)?.GetName()==label);
        }
    });

    [TestMethod]
    public void EachModulePageAppliesAndHighlightsItsStripStyle()=>Sta(()=>
    {
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        var settings=new BarSettings{Pause=true};
        using var host=new DashboardHost(()=>settings,s=>settings=s,()=>new DeviceOverview("Mock","Windows",[],[]),
            ()=>new Source(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{}));
        foreach(var module in BarSettings.ConfigurableModules)
        {
            var page=module is BarModule.Cpu or BarModule.Gpu or BarModule.Ram?module.ToString().ToUpperInvariant():module.ToString();
            host.Open(page);
            var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
            var visual=(UIElement)window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            var count=BarStyles.Options(module).Count;
            var width=(605-(count-1)*5)/count;
            var point=new Point(235+width*1.5,100);
            AutomationPeer Peer()=>UIElementAutomationPeer.CreatePeerForElement((UIElement)visual.InputHitTest(point))!;
            Assert.AreEqual("Text strip style",Peer().GetName(),page);
            ((System.Windows.Automation.Provider.IInvokeProvider)Peer().GetPattern(PatternInterface.Invoke)!).Invoke();
            PumpUntil(()=>settings.Preference(module).Style==BarItemStyle.TwoLine);
            Assert.AreEqual("Text strip style (selected)",Peer().GetName(),page);
        }
        var restored=BarSettingsFile.Import(BarSettingsFile.Export(settings));
        Assert.IsTrue(BarSettings.ConfigurableModules.All(module=>restored.Preference(module).Style==BarItemStyle.TwoLine));
    });
    [TestMethod]
    public void TempCleanupRequiresScanAndExplicitPermanentConfirmation()=>Sta(()=>
    {
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        var scans=0;var deletes=0;
        using var finishScan = new ManualResetEventSlim();
        using var finishCleanup = new ManualResetEventSlim();
        var plan=new TempCleanupScan(2048,3,[new("stale.tmp",1024,1,1,1)],false);
        using var host=new DashboardHost(()=>new BarSettings{Pause=true},_=>{},()=>new DeviceOverview("Mock","Windows",[],[]),
            ()=>new Source(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{},
                TempScan:(token,report)=>{Interlocked.Increment(ref scans);report(new(3,2048,EligibleOrRemoved:1));Assert.IsTrue(finishScan.Wait(TimeSpan.FromSeconds(8),token));return plan;},
                TempClean:(confirmed,token,report)=>{Assert.AreSame(plan,confirmed);Interlocked.Increment(ref deletes);report(new(1,1024,1,1));Assert.IsTrue(finishCleanup.Wait(TimeSpan.FromSeconds(8),token));return new(1,0,1024,1024);}));
        host.Open("Disk");
        Invoke(new Point(620,82),"Review cleanup");
        PumpUntil(()=>host.PageHistory.LastOrDefault()==DashboardHost.TempCleanupPage);
        Assert.AreEqual(0,scans);
        Assert.AreEqual(0,deletes);
        Invoke(new Point(270,170),"Scan temp files");
        PumpUntil(()=>Status().Contains("3 files checked"));
        Assert.AreEqual("Cancel scan",NameAt(new Point(275,350)));
        finishScan.Set();
        PumpUntil(()=>NameAt(new Point(270,358))=="Review removal");
        Assert.AreEqual(1,scans);
        Invoke(new Point(270,358),"Review removal");
        PumpUntil(()=>NameAt(new Point(270,392))=="Delete permanently (current)");
        Assert.AreEqual(0,deletes);
        Invoke(new Point(420,392),"Cancel");
        PumpUntil(()=>NameAt(new Point(270,358))=="Review removal");
        Assert.AreEqual(0,deletes);
        Invoke(new Point(270,358),"Review removal");
        PumpUntil(()=>NameAt(new Point(270,392))=="Delete permanently (current)");
        Invoke(new Point(270,392),"Delete permanently (current)");
        PumpUntil(()=>Status().Contains("1 of 1 checked"));
        Assert.AreEqual(1,deletes);
        finishCleanup.Set();
        PumpUntil(()=>NameAt(new Point(270,170))=="Scan temp files");

        UIElement Visual()
        {
            var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
            var visual=(UIElement)window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            return visual;
        }
        string Status() => UIElementAutomationPeer.CreatePeerForElement(Visual())!.GetHelpText();
        AutomationPeer? PeerAt(Point point) => Visual().InputHitTest(point) is UIElement hit ? UIElementAutomationPeer.CreatePeerForElement(hit) : null;
        string? NameAt(Point point)=>PeerAt(point)?.GetName();
        void Invoke(Point point,string expected)
        {
            var peer=PeerAt(point)!;
            Assert.AreEqual(expected,peer.GetName());
            ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
        }
    });
    [TestMethod]
    public void DiagnosticsAndCleanupRunOnlyThroughExplicitActions()=>Sta(()=>
    {
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        var wifi=0;var storage=0;var cleanup=0;
        using var host=new DashboardHost(()=>new BarSettings{Pause=true},_=>{},()=>new DeviceOverview("Mock","Windows",[],[]),
            ()=>new Source(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{},
                WifiDiagnostics:_=>{Interlocked.Increment(ref wifi);return Task.FromResult("Mock Wi-Fi result");},
                StorageHealth:_=>{Interlocked.Increment(ref storage);return Task.FromResult("Mock storage result");},
                StorageCleanup:()=>{cleanup++;return "Mock cleanup opened";}));
        host.Open("Network");
        Assert.AreEqual(0,wifi);
        Invoke(new Point(745,81),"Run checks");
        PumpUntil(()=>Volatile.Read(ref wifi)==1);
        host.Open("Disk");
        Assert.AreEqual(0,storage);
        Invoke(new Point(620,82),"Review cleanup");
        PumpUntil(()=>cleanup==1);
        Invoke(new Point(744,82),"Check health");
        PumpUntil(()=>Volatile.Read(ref storage)==1);

        void Invoke(Point point,string name)
        {
            var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
            var visual=(UIElement)window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            var peer=UIElementAutomationPeer.CreatePeerForElement((UIElement)visual.InputHitTest(point))!;
            Assert.AreEqual(name,peer.GetName());
            ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
        }
    });
    [TestMethod]
    [DataRow(1.0)]
    [DataRow(1.5)]
    public void GaugeReadoutsStayCenteredAcrossValueWidths(double scale)=>Sta(()=>
    {
        var helper=typeof(DashboardHost).Assembly.GetType("Islet.Shell.Bar.MonitorText")!;
        var format=helper.GetMethod("Format",BindingFlags.Static|BindingFlags.NonPublic)!;
        var centered=helper.GetMethod("Centered",BindingFlags.Static|BindingFlags.NonPublic)!;
        var center=new Point(69,71);
        foreach(var value in new[]{"9%","99%","100%","1.25 GiB","Unavailable"})
        {
            var text=(FormattedText)format.Invoke(null,[value,new Typeface("Segoe UI"),12.0,Brushes.White,scale])!;
            var origin=(Point)centered.Invoke(null,[text,center,scale])!;
            Assert.AreEqual(center.X,origin.X+text.Width/2,.501/scale,value);
            Assert.AreEqual(center.Y,origin.Y+text.Height/2,.501/scale,value);
        }
    });

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
        var readsBeforeGuide=control.Reads;
        Invoke(new Point(600,77),"80% charge limit");
        PumpUntil(()=>host.PageHistory.LastOrDefault()==DashboardHost.BatteryOptimizationPage);
        Assert.AreEqual(readsBeforeGuide,control.Reads);
        Assert.AreEqual(2,control.Changes.Count);
        Invoke(new Point(700,30),"Back to Battery");
        PumpUntil(()=>host.PageHistory.LastOrDefault()=="Battery");
        Assert.AreEqual(readsBeforeGuide+1,control.Reads);
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
    public void SettingsStripOrderButtonMovesBatteryAndSavesTheOrder()=>Sta(()=>
    {
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        var settings=new BarSettings{Items=[BarModule.Cpu,BarModule.Battery]};
        using var host=new DashboardHost(()=>settings,s=>settings=s,()=>new DeviceOverview("Mock","Windows",[],[]),
            ()=>new Source(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{}));
        host.Open("Settings");
        var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var visual=window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
        var buttons=(System.Collections.IEnumerable)visual.GetType().GetField("_buttons",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(visual)!;
        var button=buttons.Cast<UIElement>().Single(b=>UIElementAutomationPeer.CreatePeerForElement(b)?.GetName()=="Move Battery left");
        ((System.Windows.Automation.Provider.IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button)!.GetPattern(PatternInterface.Invoke)!).Invoke();
        PumpUntil(()=>settings.StripOrderCustomized);
        CollectionAssert.AreEqual(new[]{BarModule.Battery,BarModule.Cpu},settings.NormalItems);
        CollectionAssert.AreEqual(settings.NormalItems,BarSettingsFile.Import(BarSettingsFile.Export(settings)).NormalItems);
        void InvokeSetting(string label)
        {
            var element=buttons.Cast<UIElement>().Single(b=>UIElementAutomationPeer.CreatePeerForElement(b)?.GetName()==label);
            ((System.Windows.Automation.Provider.IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(element)!.GetPattern(PatternInterface.Invoke)!).Invoke();
        }
        InvokeSetting("Screen edge: Bottom");
        PumpUntil(()=>settings.Edge==BarEdge.Bottom && !settings.TopReveal);
        InvokeSetting("Alignment: Left");
        PumpUntil(()=>settings.Alignment==BarAlignment.Left);
        InvokeSetting("Screen edge: Hover");
        PumpUntil(()=>settings.TopReveal);
        InvokeSetting("Customize CPU from strip preview");
        PumpUntil(()=>buttons.Cast<UIElement>().Any(b=>UIElementAutomationPeer.CreatePeerForElement(b)?.GetName()?.StartsWith("All settings:",StringComparison.Ordinal)==true));
    });

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
                InvokeAt(new Point(260,140),"Theme: Dark");
                PumpUntil(()=>NameAt(new Point(640,213))=="Light");
                Assert.AreEqual(BarTheme.Dark,settings.Theme,"Opening the list does not change the setting.");
                Assert.AreEqual("Dark (selected)",NameAt(new Point(640,181)));
                InvokeAt(new Point(260,140),"Dismiss Theme choices");
                PumpUntil(()=>NameAt(new Point(260,140))=="Theme: Dark");
                Assert.AreEqual(BarTheme.Dark,settings.Theme,"Dismissing the list does not change the setting.");
                InvokeAt(new Point(260,140),"Theme: Dark");
                PumpUntil(()=>NameAt(new Point(640,213))=="Light");
                InvokeAt(new Point(640,213),"Light");
                PumpUntil(()=>settings.Theme==BarTheme.Light);
                Assert.IsTrue(host.IsOpen);
                Assert.IsTrue(Pixels(visual)[backgroundPixel]>230);

                string? NameAt(Point point)=>visual.InputHitTest(point) is UIElement element
                    ? UIElementAutomationPeer.CreatePeerForElement(element)?.GetName() : null;
                void InvokeAt(Point point,string name)
                {
                    var row=(UIElement)visual.InputHitTest(point);
                    var peer=UIElementAutomationPeer.CreatePeerForElement(row)!;
                    Assert.AreEqual(name,peer.GetName());
                    ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
                }
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
    public void WidgetsSampleOnlyOnTheirPageAndSkipExpensiveDetails()=>Sta(()=>
    {
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        var sources=new System.Collections.Concurrent.ConcurrentBag<WidgetSource>();
        using var host=new DashboardHost(()=>new BarSettings(),_=>{},()=>new DeviceOverview("Mock","Windows",[],[]),
            ()=>{var source=new WidgetSource();sources.Add(source);return source;},()=>display,
            new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{}));
        host.Open("Dashboard");
        Assert.AreEqual(0,host.CurrentReads);
        host.Open("Widgets");
        PumpUntil(()=>host.CurrentFrames>0);
        CollectionAssert.AreEquivalent(new[]{BarModule.Cpu,BarModule.Gpu,BarModule.Ram,BarModule.Disk,BarModule.Network,BarModule.Battery},sources.Select(source=>source.Module).ToArray());
        Assert.IsTrue(sources.All(source=>source.Details==PopupDetails.None));
        host.Open("Settings");
        Assert.IsTrue(sources.All(source=>source.Disposed));
        Assert.AreEqual(1,host.CurrentReads);
    });

    private sealed class WidgetSource : IPopupSource, IPopupDetailSource
    {
        public BarModule Module { get; private set; }
        public PopupDetails Details { get; private set; } = PopupDetails.All;
        public bool Disposed { get; private set; }
        public CapabilityProbeResult Probe(BarModule module){Module=module;return CapabilityProbeResult.Supported;}
        public PopupSample Read(BarModule module)=>new(35,12,23,65,null,null,TimeSpan.Zero,null,[],[],[],"Mock");
        public void SetDetails(PopupDetails details)=>Details=details;
        public void Dispose()=>Disposed=true;
    }

    [TestMethod]
    public void DataUsageCardAppliesTheSelectedPeriod()=>Sta(()=>
    {
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        var selected=UsagePeriod.Month;
        using var host=new DashboardHost(()=>new BarSettings{Pause=true},_=>{},()=>new DeviceOverview("Mock","Windows",[],[]),
            ()=>new WidgetSource(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{},
                Usage:()=>new(selected,new(42,13),true),SelectUsage:period=>selected=period));
        host.Open("Widgets");
        var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var visual=(UIElement)window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
        var button=(UIElement)visual.InputHitTest(new Point(258,128));
        var peer=UIElementAutomationPeer.CreatePeerForElement(button)!;
        Assert.AreEqual("Data usage: Day",peer.GetName());
        ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
        PumpUntil(()=>selected==UsagePeriod.Day);
    });

    [TestMethod]
    public void DesktopPinsAreRememberedAndDuplicatePinsReuseTheirWindow()=>Sta(()=>
    {
        var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        var layout=Path.Combine(folder,"desktop-widgets.json");
        var positions=new Dictionary<nint,BarRect>();
        BarDisplay[] displays=[new("mock",true,new(0,0,1280,900),new(0,0,1280,860))];
        DesktopWidgetHost Create()=>new(layout,()=>new BarSettings{Pause=true},()=>new WidgetSource(),new(),
            ()=>displays,hwnd=>positions.GetValueOrDefault(hwnd,new(20,48,238,240)),(hwnd,rect,_)=>positions[hwnd]=rect);
        try
        {
            using(var host=Create())
            {
                StringAssert.Contains(host.Pin(BarModule.Cpu),"added to desktop");
                StringAssert.Contains(host.Pin(BarModule.Cpu),"already on the desktop");
                StringAssert.Contains(host.Pin(null),"Combined added");
                StringAssert.Contains(host.Pin(BarModule.Battery),"Battery added");
                Assert.AreEqual(3,host.Count);
                Assert.AreEqual(0,host.Frames);
            }
            using var reopened=Create();
            reopened.Restore();
            Assert.AreEqual(3,reopened.Count);
            Assert.IsTrue(reopened.Drain(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            if(Directory.Exists(folder))
            {
                if(File.Exists(layout))File.Delete(layout);
                Directory.Delete(folder);
            }
        }
    });

    [TestMethod]
    public void EveryModuleHeaderCanTurnItsLastBarItemOffAndBackOn()=>Sta(()=>
    {
        foreach(var module in BarSettings.ConfigurableModules)
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
        var peer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(visual)!;
        StringAssert.Contains(peer.GetName(),"Measuring CPU usage");
        Assert.IsTrue(cold[(220*690+400)*4+3]>0,"Cold start should contain a shaped loading card");
        var sample=new PopupSample(20,5,15,80,2.5,4,TimeSpan.Zero,null,[],[],[],"Previous fixture");
        type.GetMethod("ShowPrevious")!.Invoke(visual,[sample]);var previous=Pixels(visual);Assert.IsFalse(cold.SequenceEqual(previous));
        StringAssert.Contains(peer.GetName(),"Previous reading");
        var history=new SampleRing(121);type.GetMethod("Update")!.Invoke(visual,[sample with{Cpu=null,WarmingUp=true},history,history,1.0]);CollectionAssert.AreEqual(previous,Pixels(visual));Assert.AreEqual(0,history.Count);
        type.GetMethod("Update")!.Invoke(visual,[sample with{Cpu=null,Explanation="Permission denied"},history,history,2.0]);Assert.IsFalse(previous.SequenceEqual(Pixels(visual)),"A real failure must replace the cached view");
        StringAssert.Contains(peer.GetName(),"Permission denied");
        type.GetMethod("ShowMeasuring")!.Invoke(visual,[sample with{Cpu=null,WarmingUp=true}]);
        StringAssert.Contains(peer.GetName(),"Measuring CPU usage");
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
    [TestMethod]
    public void GamingAndThresholdSettingsPersistAndToggleThroughGamingPage()=>Sta(()=>
    {
        var settings=new BarSettings();var gamingCallbacks=new List<bool>();
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        DashboardHost? host=null;
        host=new DashboardHost(()=>settings,s=>{settings=s;host!.RefreshSettings();},()=>new DeviceOverview("Mock","Windows",[],[]),
            ()=>new Source(),()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{},
                SetGamingMode:v=>{gamingCallbacks.Add(v);settings=settings with{GamingMode=v};host!.RefreshSettings();}));
        using(host)
        {
            host.Open(DashboardHost.GamingPage);
            var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
            var visual=(UIElement)window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            var inputSource=PresentationSource.FromVisual(visual);Assert.IsNotNull(inputSource);
            InvokeByPrefix("Gaming Mode: ");
            PumpUntil(()=>settings.GamingMode);
            CollectionAssert.AreEqual(new[]{true},gamingCallbacks);

            InvokeByPrefix("Auto Gaming Mode: ");
            PumpUntil(()=>settings.GamingModeAuto);

            InvokeByPrefix("Strip refresh: ");
            PumpUntil(()=>FindButton("2 seconds") is not null);
            InvokeExact("2 seconds");
            PumpUntil(()=>settings.GamingRefreshSeconds==2);

            InvokeByPrefix("FPS target: ");
            PumpUntil(()=>FindButton("144 fps") is not null);
            InvokeExact("144 fps");
            PumpUntil(()=>settings.FpsTarget==144);

            InvokeByPrefix("Warning threshold: ");
            PumpUntil(()=>FindButton("90%") is not null);
            InvokeExact("90%");
            PumpUntil(()=>settings.ThresholdPercent==90);
            visual.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,inputSource,0,System.Windows.Input.Key.PageDown){RoutedEvent=UIElement.KeyDownEvent});
            InvokeByPrefix("FPS and frame time: ");
            PumpUntil(()=>!settings.GamingItems.Contains(BarModule.FrameRate));
            Assert.IsTrue(settings.Items.Contains(BarModule.Network));
            var restored=BarSettingsFile.Import(BarSettingsFile.Export(settings));
            Assert.IsFalse(restored.GamingItems.Contains(BarModule.FrameRate));
            Assert.AreEqual(2,restored.GamingRefreshSeconds);
            Assert.AreEqual(144,restored.FpsTarget);
            Assert.IsFalse(restored.GamingMode);

            UIElement? FindButton(string exactOrPrefix,bool prefix=false)
            {
                var buttons=(System.Collections.IEnumerable)visual.GetType().GetField("_buttons",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(visual)!;
                foreach(UIElement b in buttons)
                {
                    var name=UIElementAutomationPeer.CreatePeerForElement(b)?.GetName();
                    if(name is not null&&(prefix?name.StartsWith(exactOrPrefix,StringComparison.Ordinal):name==exactOrPrefix))return b;
                }
                return null;
            }
            void InvokeByPrefix(string prefix)
            {
                var b=FindButton(prefix,true);Assert.IsNotNull(b,"Missing button "+prefix);
                ((System.Windows.Automation.Provider.IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(b)!.GetPattern(PatternInterface.Invoke)!).Invoke();
            }
            void InvokeExact(string exact)
            {
                var b=FindButton(exact);Assert.IsNotNull(b,"Missing option "+exact);
                ((System.Windows.Automation.Provider.IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(b)!.GetPattern(PatternInterface.Invoke)!).Invoke();
            }
        }
    });
    [TestMethod]
    public void DashboardOverviewReportsGamingStatusWithoutAToggleOrPolling()=>Sta(()=>
    {
        var settings=new BarSettings();int factories=0;var gamingCalls=new List<bool>();
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        var overview=new DeviceOverview("Mock Laptop","Windows 11",[new("Processor","Mock CPU"),new("Memory","16 GiB"),new("Graphics","Mock GPU"),new("Storage","512 GiB")],[]);
        DashboardHost? host=null;
        host=new DashboardHost(()=>settings,s=>{settings=s;host!.RefreshSettings();},()=>overview,
            ()=>{factories++;return new Source();},()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{},
                SetGamingMode:v=>{gamingCalls.Add(v);settings=settings with{GamingMode=v};host!.RefreshSettings();}));
        using(host)
        {
            host.Open("Dashboard");
            var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
            var visual=(UIElement)window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            var width=(double)visual.GetType().GetProperty("Width")!.GetValue(visual)!;
            var nativeHit=window.GetType().GetMethod("HeaderHitTest",BindingFlags.Instance|BindingFlags.NonPublic)!;
            var bounds=new BarRect(0,0,1280,900);
            nint At(double x,double y){var px=(short)(int)Math.Round(x);var py=(short)(int)Math.Round(y);return (nint)(int)((ushort)px|((uint)(ushort)py<<16));}
            Assert.AreEqual((nint)2,(nint)nativeHit.Invoke(window,[bounds,At(width-80,30),1.0])!);
            var peer=UIElementAutomationPeer.CreatePeerForElement(visual)!;
            PumpUntil(()=>peer.GetHelpText()=="");
            Assert.IsNull(Find("Gaming Mode: Off"));
            settings=settings with{GamingMode=true};
            host.RefreshSettings();
            Assert.AreEqual("Gaming Mode on",peer.GetHelpText());
            Assert.IsNull(Find("Gaming Mode: On"));
            Assert.IsNull(Find("Gaming Mode in strip: On"));

            Invoke("Open Gaming");
            PumpUntil(()=>host.PageHistory.LastOrDefault()==DashboardHost.GamingPage);
            Invoke("Gaming Mode: On");
            PumpUntil(()=>!settings.GamingMode);
            CollectionAssert.AreEqual(new[]{false},gamingCalls);
            Assert.AreEqual(0,factories);
            Assert.AreEqual(0,host.CurrentReads);

            UIElement? Find(string exact)
            {
                var buttons=(System.Collections.IEnumerable)visual.GetType().GetField("_buttons",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(visual)!;
                foreach(UIElement b in buttons)
                    if(UIElementAutomationPeer.CreatePeerForElement(b)?.GetName()==exact)return b;
                return null;
            }
            void Invoke(string exact)
            {
                var b=Find(exact);Assert.IsNotNull(b,"Missing Dashboard button "+exact);
                ((System.Windows.Automation.Provider.IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(b)!.GetPattern(PatternInterface.Invoke)!).Invoke();
            }
        }
    });
    [TestMethod]
    public void LongSensorPopupScrollsToLastRowAndKeepsThumbInBounds()=>Sta(()=>
    {
        var type=typeof(DashboardHost).Assembly.GetType("Islet.Shell.Bar.PopupVisual")!;
        var visual=(UIElement)Activator.CreateInstance(type,[BarModule.Sensors,240d,1d,(Action)(()=>{}),(Action)(()=>{}),false])!;
        var items=Enumerable.Range(1,64).Select(i=>new SensorItem("Long adapter name","GPU",
            $"Temperature channel {i} with a long manufacturer label",SensorKind.Temperature,50+i*.1,"°C")).ToArray();
        var sample=new PopupSample(null,null,null,null,null,null,TimeSpan.Zero,null,[],[],[],"Sensors",
            Sensors:new(null,56.4,null,items,false,"Available hardware sensor readings."));
        var history=new SampleRing(5);
        type.GetMethod("Update")!.Invoke(visual,[sample,history,history,1d]);
        var first=Pixels(visual);
        type.GetMethod("Scroll",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(visual,[100_000d]);
        Assert.IsFalse(first.SequenceEqual(Pixels(visual)));
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var thumb=(Rect)type.GetProperty("ScrollbarThumb",flags)!.GetValue(visual)!;
        var track=(Rect)type.GetProperty("ScrollbarTrack",flags)!.GetValue(visual)!;
        Assert.AreEqual(track.Bottom,thumb.Bottom,.01);
        Assert.IsTrue(thumb.Height>=24);
    });

    [TestMethod]
    public void SensorsPageAndPopupRenderLiveTemperaturesFansAndFahrenheitUnit()=>Sta(()=>
    {
        var display=new BarDisplay("mock",true,new(0,0,1280,900),new(0,0,1280,860));
        var settings=new BarSettings{Fahrenheit=false};
        DashboardHost? host=null;
        var source=new SensorSource();
        host=new DashboardHost(()=>settings,s=>{settings=s;host!.RefreshSettings();},()=>new DeviceOverview("Mock","Windows 11",[],[]),
            ()=>source,()=>display,new((_,_,_)=>{},()=>{},()=>{},()=>"",_=>"",()=>"",()=>"",()=>{}));
        using(host)
        {
            host.Open("Sensors");
            PumpUntil(()=>host.CurrentFrames>=1);
            var window=typeof(DashboardHost).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
            var visual=(UIElement)window.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            var celsiusPixels=Pixels(visual);
            Assert.IsTrue(celsiusPixels.Any(b=>b!=0));

            settings=settings with{Fahrenheit=true};
            host.RefreshSettings();
            var fahrenheitPixels=Pixels(visual);
            Assert.IsFalse(celsiusPixels.SequenceEqual(fahrenheitPixels),"Toggling Fahrenheit must repaint live Sensors page.");
        }
        Assert.IsTrue(source.Disposed);

        var popupSource=new SensorSource();
        using(var popup=new PopupCoordinator(()=>popupSource,(_,_,_)=>{},()=>{},settings:()=>new BarSettings{Fahrenheit=true}))
        {
            popup.Toggle(BarModule.Sensors,new(400,0,454,24),display,1,false);
            PumpUntil(()=>popup.Frames>=1);
            var popupWindow=typeof(PopupCoordinator).GetField("_window",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(popup)!;
            var popupVisual=(UIElement)popupWindow.GetType().GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(popupWindow)!;
            Assert.IsTrue(Pixels(popupVisual).Any(b=>b!=0));
            Assert.AreEqual(6,popup.LastSample?.Sensors?.Items.Length);
            popup.Close();
            Assert.IsTrue(popup.Drain(TimeSpan.FromSeconds(2)));
        }
        Assert.IsTrue(popupSource.Disposed);
    });
    private static byte[] Pixels(Visual visual){var b=new RenderTargetBitmap(690,550,96,96,PixelFormats.Pbgra32);b.Render(visual);var pixels=new byte[690*550*4];b.CopyPixels(pixels,690*4,0);return pixels;}
    private static void Sta(Action body){Exception? failure=null;var thread=new Thread(()=>{try{body();}catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)));if(failure is not null)throw new AssertFailedException("Application page validation failed: "+failure,failure);}
    private sealed class SensorSource:IPopupSource
    {
        public bool Disposed;
        public CapabilityProbeResult Probe(BarModule module)=>CapabilityProbeResult.Supported;
        public PopupSample Read(BarModule module)=>new(20,5,15,80,2.5,4,TimeSpan.FromHours(1),null,[],[],[],"Live sensors",
            Sensors:new(61,72,1450,[
                new("Ryzen","CPU","CPU Package",SensorKind.Temperature,61,"°C"),
                new("RTX","GPU","GPU Core",SensorKind.Temperature,72,"°C"),
                new("Board","Motherboard","Chassis Fan",SensorKind.Fan,1450,"RPM"),
                new("Ryzen","CPU","Package Power",SensorKind.Power,45.2,"W"),
                new("Ryzen","CPU","Core Clock",SensorKind.Clock,4200,"MHz"),
                new("Board","Motherboard","VCore",SensorKind.Voltage,1.21,"V")
            ],true,"Live hardware sensors via LibreHardwareMonitor."));
        public void Dispose()=>Disposed=true;
    }
    private sealed class Source(ManualResetEventSlim? started=null,ManualResetEventSlim? release=null):IPopupSource
    {public int Reads;public bool Disposed;public CapabilityProbeResult Probe(BarModule module)=>CapabilityProbeResult.Supported;public PopupSample Read(BarModule module){Reads++;started?.Set();if(release is not null&&!release.Wait(5000))throw new TimeoutException("Mock release timeout");return new(10,3,7,90,null,null,TimeSpan.Zero,null,[],[],[],"Mock");}public void Dispose()=>Disposed=true;}
}

