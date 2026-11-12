using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    [TestMethod]
    public void AutoGamingModeActivatesOnFullscreenAndPublishesWithoutPersisting()
    {
        Exception? failure=null;var thread=new Thread(()=>{try
        {
            var desktop=new Desktop(false);int saves=0,changed=0;
            using var host=new MonitorBarHost(desktop,new BarSettings{GamingModeAuto=false,Items=[BarModule.Cpu],IntervalSeconds=1},
                ()=>new(10,50,null,"CPU","RAM","Unavailable","No battery",null,null),()=>{},_=>saves++);
            host.SettingsChanged+=()=>changed++;
            desktop.FullScreen=true;desktop.Surfaces.Last().RaiseEnvironmentChanged();
            var until=Environment.TickCount64;PumpUntil(()=>Environment.TickCount64-until>=150);
            Assert.IsFalse(host.CurrentSettings.GamingMode,"Auto gaming mode must stay off when disabled.");
            Assert.AreEqual(0,saves);

            host.ApplySettings(host.CurrentSettings with{GamingModeAuto=true});
            Assert.IsTrue(host.CurrentSettings.GamingMode,"Enabling Auto while already fullscreen activates Gaming Mode.");
            Assert.AreEqual(1,saves);
            Assert.AreEqual(BarModule.FrameRate,host.CurrentSettings.VisibleItems[0]);

            host.SetGamingMode(false);
            Assert.IsFalse(host.CurrentSettings.GamingMode);
            Assert.AreEqual(1,saves,"Runtime GamingMode toggle must not write settings to disk.");

            var checks=host.VisibilityChecks;
            desktop.FullScreen=false;desktop.Surfaces.Last().RaiseEnvironmentChanged();
            PumpUntil(()=>host.VisibilityChecks>checks);
            Assert.IsFalse(host.CurrentSettings.GamingMode);
            desktop.FullScreen=true;desktop.Surfaces.Last().RaiseEnvironmentChanged();
            PumpUntil(()=>host.CurrentSettings.GamingMode);
            Assert.AreEqual(1,saves,"Auto activation must not persist transient GamingMode.");
            Assert.IsTrue(changed>=3);
        }catch(Exception ex){failure=ex;}}){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));if(failure is not null)throw new AssertFailedException("Auto gaming mode lifecycle failed: "+failure.Message,failure);
    }
    [TestMethod]
    public void ThresholdAndFpsColorsRenderAcrossStripStylesAndThemes()
    {
        Exception? failure=null;var thread=new Thread(()=>{try
        {
            foreach(var light in new[]{false,true})
            {
                var bg=light?Color.FromRgb(245,248,251):Color.FromRgb(32,32,34);
                var warn=light?Color.FromRgb(150,83,20):Color.FromRgb(255,159,10);
                var crit=light?Color.FromRgb(189,48,59):Color.FromRgb(255,69,58);
                var good=light?Color.FromRgb(24,123,71):Color.FromRgb(48,209,88);

                var normal=new BarSnapshot(50,50,90,"","","","",null,null,Gpu:50);
                var warning=new BarSnapshot(88,88,90,"","","","",null,null,Gpu:88);
                var critical=new BarSnapshot(96,96,90,"","","","",null,null,Gpu:96);

                foreach(var (module,style) in new[]{(BarModule.Cpu,BarItemStyle.Ring),(BarModule.Ram,BarItemStyle.Bar),(BarModule.Gpu,BarItemStyle.Sparkline),(BarModule.Cpu,BarItemStyle.IconOnly)})
                {
                    var normPixels=RenderPreview(module,style,light,normal);
                    var warnPixels=RenderPreview(module,style,light,warning);
                    var critPixels=RenderPreview(module,style,light,critical);
                    var maxX=module==BarModule.Ram?38:100;
                    Assert.AreEqual(0,CountBlendedColor(normPixels,bg,crit,maxX),$"{module}/{style} light={light} normal had critical pixels");
                    Assert.AreEqual(0,CountBlendedColor(normPixels,bg,warn,maxX),$"{module}/{style} light={light} normal had warning pixels");
                    Assert.IsTrue(CountBlendedColor(warnPixels,bg,warn)>0,$"{module}/{style} light={light} at 88% missing warning color");
                    Assert.IsTrue(CountBlendedColor(critPixels,bg,crit)>0,$"{module}/{style} light={light} at 96% missing critical color");
                }

                var customThreshold=RenderPreview(BarModule.Cpu,BarItemStyle.Ring,light,new BarSnapshot(72,40,90,"","","","",null,null),thresholdPercent:70);
                Assert.IsTrue(CountBlendedColor(customThreshold,bg,warn)>0,$"Custom 70% threshold at 72% light={light} missing warning color");

                var batteryHigh=RenderPreview(BarModule.Battery,BarItemStyle.Battery,light,normal);
                Assert.AreEqual(0,CountBlendedColor(batteryHigh,bg,crit),$"High battery should not trigger critical color light={light}");

                var batteryWarning=RenderPreview(BarModule.Battery,BarItemStyle.Battery,light,normal with{Battery=25});
                var batteryCritical=RenderPreview(BarModule.Battery,BarItemStyle.Ring,light,normal with{Battery=15});
                Assert.IsTrue(CountBlendedColor(batteryWarning,bg,warn)>0,$"25% battery should show amber light={light}");
                Assert.IsTrue(CountBlendedColor(batteryCritical,bg,crit)>0,$"15% battery ring should show red light={light}");

                var fpsGood=RenderPreview(BarModule.FrameRate,BarItemStyle.TwoLine,light,normal with{Frames=new(10.0,"OK")},fpsTarget:60);
                var fpsFair=RenderPreview(BarModule.FrameRate,BarItemStyle.TwoLine,light,normal with{Frames=new(25.0,"OK")},fpsTarget:60);
                var fpsLow=RenderPreview(BarModule.FrameRate,BarItemStyle.TwoLine,light,normal with{Frames=new(50.0,"OK")},fpsTarget:60);
                var fpsMissing=RenderPreview(BarModule.FrameRate,BarItemStyle.TwoLine,light,normal with{Frames=new(null,"Unavailable",true)},fpsTarget:60);
                Assert.IsTrue(CountBlendedColor(fpsGood,bg,good)>0,$"100 fps >= 60 missing green light={light}");
                Assert.IsTrue(CountBlendedColor(fpsFair,bg,warn)>0,$"40 fps >= 30 missing amber light={light}");
                Assert.IsTrue(CountBlendedColor(fpsLow,bg,crit)>0,$"20 fps < 30 missing red light={light}");
                Assert.AreEqual(0,CountBlendedColor(fpsMissing,bg,good)+CountBlendedColor(fpsMissing,bg,warn)+CountBlendedColor(fpsMissing,bg,crit),$"Unavailable FPS should not render status color light={light}");

                var sensorNormal=RenderPreview(BarModule.Sensors,BarItemStyle.TwoLine,light,normal with{SensorTemp=55,SensorReason="OK"});
                var sensorWarn=RenderPreview(BarModule.Sensors,BarItemStyle.TwoLine,light,normal with{SensorTemp=84,SensorReason="Warm"});
                var sensorCrit=RenderPreview(BarModule.Sensors,BarItemStyle.TwoLine,light,normal with{SensorTemp=94,SensorReason="Hot"});
                Assert.AreEqual(0,CountBlendedColor(sensorNormal,bg,warn)+CountBlendedColor(sensorNormal,bg,crit),$"Normal temperature should not alert light={light}");
                Assert.IsTrue(CountBlendedColor(sensorWarn,bg,warn)>0,$"84C sensor should render amber warning light={light}");
                Assert.IsTrue(CountBlendedColor(sensorCrit,bg,crit)>0,$"94C sensor should render red critical light={light}");
            }
        }catch(Exception ex){failure=ex;}}){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)));if(failure is not null)throw new AssertFailedException("Color threshold rendering failed: "+failure.Message,failure);
    }
    private static byte[] RenderPreview(BarModule module,BarItemStyle style,bool light,BarSnapshot sample,int thresholdPercent=85,int fpsTarget=60)
    {
        var method=typeof(MonitorBarHost).GetMethod("PreviewItem",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic,
            [typeof(BarModule),typeof(BarItemStyle),typeof(bool),typeof(double),typeof(BarSnapshot),typeof(int),typeof(int)])!;
        var drawing=(Drawing)method.Invoke(null,[module,style,light,1.0,sample,thresholdPercent,fpsTarget])!;
        var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(light?Color.FromRgb(245,248,251):Color.FromRgb(32,32,34)),null,new Rect(0,0,100,24));
            dc.DrawDrawing(drawing);
        }
        var bitmap=new RenderTargetBitmap(100,24,96,96,PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels=new byte[100*24*4];
        bitmap.CopyPixels(pixels,100*4,0);
        return pixels;
    }
    private static int CountBlendedColor(byte[] pixels,Color bg,Color target,int maxX=100)
    {
        double dr=target.R-bg.R,dg=target.G-bg.G,db=target.B-bg.B;
        double len2=dr*dr+dg*dg+db*db;
        int count=0;
        for(int y=0;y<24;y++)
        for(int x=0;x<maxX;x++)
        {
            int i=(y*100+x)*4;
            double pr=pixels[i+2]-bg.R,pg=pixels[i+1]-bg.G,pb=pixels[i]-bg.B;
            double t=(pr*dr+pg*dg+pb*db)/len2;
            if(t<0.35||t>1.05)continue;
            if(Math.Abs(pr-t*dr)<=20&&Math.Abs(pg-t*dg)<=20&&Math.Abs(pb-t*db)<=20)
                count++;
        }
        return count;
    }
    private sealed class Desktop(bool hidden):IBarDesktop
    {
        public List<Surface> Surfaces=[];
        public BarPoint? Cursor;
        public bool FullScreen;
        public BarPoint? CursorPosition()=>Cursor;
        public IReadOnlyList<BarDisplay> Displays()=>[new("left",true,new(-1000,0,0,800),new(-1000,0,0,760)),new("right",false,new(0,0,1200,900),new(0,0,1200,860))];
        public IBarSurface Attach(nint h,BarDisplay d,BarSettings s,bool tray){var surface=new Surface(this,d,hidden,tray){Hidden=s.TopReveal};Surfaces.Add(surface);return surface;}
    }
    private sealed class Surface(Desktop owner,BarDisplay d,bool hidden,bool tray):IBarSurface
    {
        public bool Disposed;public BarPlacement Placement=>new(new(d.Bounds.Left,0,d.Bounds.Right,24),1,BarMode.Overlay,null);
        public bool ShouldHide=>hidden;public bool TrayPresent{get;private set;}=tray;
        public bool IsFullScreenForeground=>owner.FullScreen;
        public event Action? EnvironmentChanged;public event Action? TrayMenuRequested{add{}remove{}}
        public bool Hidden;public void SetHidden(bool value){Hidden=value;}public void Reposition(){}public int Menu(string? m,bool p)=>0;public void HandleMessage(int m,nint w,nint l){}
        public void RaiseEnvironmentChanged()=>EnvironmentChanged?.Invoke();
        public void Dispose(){Disposed=true;TrayPresent=false;}
    }
}

