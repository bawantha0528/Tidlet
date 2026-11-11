using Islet.Shell.Bar;
namespace Islet.Shell.Tests;
[TestClass]
public sealed class BarVisualHostTests
{
    [TestMethod]
    public void VisualExperimentIsBoundedAndDoesNotChangeNormalLaunch()
    {
        Assert.IsTrue(BarProbeOptions.TryParse([],out var normal));Assert.IsFalse(normal.VisualHost);
        Assert.IsFalse(BarProbeOptions.TryParse(["--visual-host"],out _));
        Assert.IsFalse(BarProbeOptions.TryParse(["--r0-probe","--visual-host","--exit-after-seconds","3","--probe-report","probe.json"],out _));
        Assert.IsTrue(BarProbeOptions.TryParse(["--r0-probe","--solid","--visual-host","--exit-after-seconds","3","--probe-report","probe.json"],out var options));
        Assert.IsTrue(options.VisualHost);
    }
    [TestMethod]
    public void StaVisualHostExposesUnavailableReasonAndStopsItsDispatcherNaturally()
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                using var host=new BarVisualHost(new(false,1,VisualHost:true),()=> (new("—","CPU denied"),new("75%","RAM available")),(_,_)=>{});
                Assert.IsTrue(host.AccessibilityPeerVerified);
                StringAssert.Contains(host.AccessibleName,"CPU denied");StringAssert.Contains(host.AccessibleName,"75%");
                host.Run();
                Assert.IsTrue(host.TimersStopped);Assert.AreEqual(0,host.MenuCreations);Assert.IsTrue(host.Samples>=1);
                host.Dispose(); // idempotent; no real process or settings action
            }
            catch(Exception ex){failure=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)),"Owned dispatcher failed to stop");
        if(failure is not null)throw new AssertFailedException("STA visual lifecycle failed",failure);
    }
}
