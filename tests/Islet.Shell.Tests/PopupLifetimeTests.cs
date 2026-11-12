using System.Windows.Threading;
using Islet.Core.Bar;
using Islet.Core.Capabilities;
using Islet.Core.Popups;
using Islet.Shell.Bar;
namespace Islet.Shell.Tests;
[TestClass]
public sealed class PopupLifetimeTests
{
    [TestMethod]
    public void SensorsPopupStopsAfterTheCapabilityFrame()
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                using var popup=new PopupCoordinator(()=>new Source(),(_,_,_)=>{},()=>{});
                var display=new BarDisplay("mock",true,new(0,0,1000,900),new(0,0,1000,860));
                popup.Toggle(BarModule.Sensors,new(400,0,454,24),display,1,false);
                var frame=new DispatcherFrame();
                var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1.5)};
                timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};
                timer.Start();Dispatcher.PushFrame(frame);
                Assert.AreEqual(1,popup.Reads);
                Assert.AreEqual(1,popup.Frames);
                Assert.IsTrue(popup.IsOpen);
            }
            catch(Exception ex){failure=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(8)));
        if(failure is not null)throw new AssertFailedException("Sensor popup polling failed",failure);
    }

    [TestMethod]
    public void PopupPresentsCompletedFirstReadWithoutWaitingFiveSeconds()
    {
        Exception? failure=null;var thread=new Thread(()=>{try
        {
            using var popup=new PopupCoordinator(()=>new Source(),(_,_,_)=>{},()=>{},settings:()=>new BarSettings{Modules=new(){[BarModule.Cpu]=new(){UpdateSeconds=5}}});
            var clock=System.Diagnostics.Stopwatch.StartNew();popup.Toggle(BarModule.Cpu,new(400,0,454,24),new("mock",true,new(0,0,1000,900),new(0,0,1000,860)),1,false);
            var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
            timer.Tick+=(_,_)=>{if(popup.Frames>0||clock.Elapsed.TotalSeconds>=4){timer.Stop();frame.Continue=false;}};timer.Start();Dispatcher.PushFrame(frame);
            Assert.AreEqual(1,popup.Frames);Assert.AreEqual(1,popup.Reads);Assert.IsTrue(clock.Elapsed.TotalSeconds<4);popup.Close();Assert.IsTrue(popup.Drain(TimeSpan.FromSeconds(2)));
        }catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));if(failure is not null)throw new AssertFailedException("Popup immediate presentation failed",failure);
    }
    [TestMethod]
    public void PausedPopupNeverCreatesMetricProvider()
    {
        using var popup=new PopupCoordinator(()=>throw new AssertFailedException("Paused source started"),(_,_,_)=>throw new AssertFailedException("Paused popup opened"),()=>{},settings:()=>new BarSettings{Pause=true});
        popup.Toggle(BarModule.Cpu,new(400,0,454,24),new("mock",true,new(0,0,1000,900),new(0,0,1000,860)),1,false);
        Assert.IsFalse(popup.IsOpen);Assert.AreEqual(0,popup.Reads);Assert.AreEqual(0,popup.Opens);
    }
    [TestMethod]
    public void ClosedPopupDoesNotCreateProvidersAndRepeatedReadsStopAfterClose()
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                var fake=new Source();int factories=0;
                using var popup=new PopupCoordinator(()=>{factories++;return fake;},(_,_,_)=>{},()=>throw new AssertFailedException("Unrequested action"));
                Assert.AreEqual(0,factories);var display=new BarDisplay("fake",true,new(0,0,1000,900),new(0,0,1000,860));
                popup.Toggle(BarModule.Cpu,new(400,0,454,24),display,1,false);
                var close=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3.2)};var finish=new DispatcherTimer{Interval=TimeSpan.FromSeconds(4.4)};int readsAtClose=0;
                close.Tick+=(_,_)=>{close.Stop();popup.Close();readsAtClose=popup.Reads;};
                finish.Tick+=(_,_)=>
                {
                    finish.Stop();try{Assert.IsTrue(readsAtClose>=2);Assert.AreEqual(readsAtClose,popup.Reads);Assert.AreEqual(0,popup.OutstandingWorkers);Assert.IsTrue(popup.Drain(TimeSpan.FromSeconds(1)));Assert.IsTrue(fake.Disposed);}
                    catch(Exception ex){failure=ex;}finally{Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}
                };
                close.Start();finish.Start();Dispatcher.Run();Assert.AreEqual(1,factories);
            }
            catch(Exception ex){failure=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)));
        if(failure is not null)throw new AssertFailedException("Popup lifetime failed",failure);
    }
    [TestMethod]
    public void LiveSensorsPopupContinuesPollingUntilClosed()
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                var fake=new LiveSensorSource();
                using var popup=new PopupCoordinator(()=>fake,(_,_,_)=>{},()=>{});
                var display=new BarDisplay("mock",true,new(0,0,1000,900),new(0,0,1000,860));
                popup.Toggle(BarModule.Sensors,new(400,0,454,24),display,1,false);
                var frame=new DispatcherFrame();var readsAtClose=0;
                var close=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2.6)};
                var finish=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3.2)};
                close.Tick+=(_,_)=>{close.Stop();popup.Close();readsAtClose=popup.Reads;};
                finish.Tick+=(_,_)=>
                {
                    finish.Stop();
                    try{Assert.IsTrue(readsAtClose>=2,$"Expected live sensors popup to poll; got Reads={readsAtClose}");Assert.IsTrue(popup.Drain(TimeSpan.FromSeconds(1)));Assert.IsTrue(fake.Disposed);}
                    catch(Exception ex){failure=ex;}finally{frame.Continue=false;}
                };
                close.Start();finish.Start();Dispatcher.PushFrame(frame);
            }
            catch(Exception ex){failure=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        if(failure is not null)throw new AssertFailedException("Live sensor popup polling failed",failure);
    }
    private sealed class Source:IPopupSource
    {
        public bool Disposed;
        public CapabilityProbeResult Probe(BarModule module)=>CapabilityProbeResult.Supported;
        public PopupSample Read(BarModule module)=>new(20,5,15,80,2.5,4,TimeSpan.FromHours(1),new(100,50,50,100,20,null,null),[double.NaN,110,-1,25],[0,1,1,0],[],"Fake invalid core values are omitted");
        public void Dispose()=>Disposed=true;
    }
    private sealed class LiveSensorSource:IPopupSource
    {
        public bool Disposed;
        public CapabilityProbeResult Probe(BarModule module)=>CapabilityProbeResult.Supported;
        public PopupSample Read(BarModule module)=>new(20,5,15,80,2.5,4,TimeSpan.FromHours(1),null,[],[],[],"Live sensors",
            Sensors:new(61,68,1420,[new("CPU","CPU","Package",SensorKind.Temperature,61,"°C"),new("Fan","Motherboard","CPU Fan",SensorKind.Fan,1420,"RPM")],false,"Live sensors"));
        public void Dispose()=>Disposed=true;
    }
}

