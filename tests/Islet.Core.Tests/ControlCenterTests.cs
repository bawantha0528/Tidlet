using Islet.Core.Activities;
using Islet.Core.Presentation;

namespace Islet.Core.Tests;

[TestClass]
public sealed class ControlCenterTests
{
    [TestMethod]
    public void DeadlineSurvivesLongHiddenIntervalWithoutTicksAndCompletesOnce()
    {
        var timer = new FocusTimer(); timer.Toggle(1000);
        Assert.AreEqual("24:59",timer.Read(2000).Display);
        Assert.AreEqual(FocusState.Complete,timer.Read(2_000_000).State);
        Assert.AreEqual(TimeSpan.Zero,timer.Read(3_000_000).Remaining);
        timer.Toggle(3_000_000);
        Assert.AreEqual("25:00",timer.Read(3_000_000).Display);
    }
    [TestMethod]
    public void PauseResumeAndResetKeepExactRemainingTime()
    {
        var timer = new FocusTimer(TimeSpan.FromSeconds(10)); timer.Toggle(0); timer.Toggle(1500);
        Assert.AreEqual(FocusState.Paused,timer.Read(900_000).State);
        Assert.AreEqual(TimeSpan.FromMilliseconds(8500),timer.Read(900_000).Remaining);
        timer.Toggle(900_000);
        Assert.AreEqual("00:08",timer.Read(901_000).Display);
        timer.Reset(); Assert.AreEqual(FocusState.Ready,timer.Read(901_000).State);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>timer.Read(double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new FocusTimer(TimeSpan.Zero));
    }
    [TestMethod]
    public void MissingMediaDisablesActionsButLeavesNavigationAndFocusUsable()
    {
        var missing = new ControlCenter(new(MediaState.NoSession,"Not playing","No player"),new FocusTimer().Read(0));
        Assert.AreEqual(0,ControlCenterLayout.HitTest(140,250,missing));
        Assert.AreEqual(ControlCenterLayout.FocusToggle,ControlCenterLayout.HitTest(320,75,missing));
        Assert.AreEqual(ControlCenterLayout.FocusReset,ControlCenterLayout.HitTest(560,75,missing));
        Assert.AreEqual(ControlCenterLayout.FullMonitor,ControlCenterLayout.HitTest(550,25,missing));
        var active = missing with { Media = new(MediaState.Available,"Title","Artist","player",CanToggle:true) };
        Assert.AreEqual(ControlCenterLayout.TogglePlayback,ControlCenterLayout.HitTest(140,250,active));
        Assert.AreEqual(0,ControlCenterLayout.HitTest(140,250,active with { Busy=true }));
        Assert.AreEqual(0,ControlCenterLayout.HitTest(-1,-1,active));
    }
}
