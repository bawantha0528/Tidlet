using Islet.Core.Island;
using Islet.Core.Settings;

namespace Islet.Core.Tests;

[TestClass]
public sealed class IslandTransitionTests
{
    [TestMethod]
    public void FiniteEasingFinishesAndInterruptionStartsAtCurrentVisualSize()
    {
        var transition = new IslandTransition(new(220, 36));
        transition.Begin(new(640, 300), 0);
        var midway = transition.At(125);
        Assert.IsTrue(midway.Width > 220 && midway.Width < 640);
        transition.Begin(new(240, 42), 125);
        Assert.AreEqual(midway, transition.At(125));
        Assert.AreEqual(new IslandSize(240, 42), transition.At(375));
        Assert.IsFalse(transition.Active);
        Assert.AreEqual(new IslandSize(240, 42), transition.At(100000));
    }

    [TestMethod]
    public void ReducedMotionHasNoAnimationClock()
    {
        var transition = new IslandTransition(IslandSize.For(IslandStyle.Island, IslandState.Collapsed));
        transition.Begin(IslandSize.For(IslandStyle.Bar, IslandState.Expanded), 0, true);
        Assert.IsFalse(transition.Active);
        Assert.AreEqual(new IslandSize(720, 320), transition.At(0));
    }

    [TestMethod]
    public void FullscreenHeuristicExcludesMaximizedCaptionedWindowsAndDesktop()
    {
        var monitor = new PixelRect(-1920, 0, 1920, 1080);
        Assert.IsTrue(FullscreenGeometry.CoversMonitor(monitor, monitor, false, false, false));
        Assert.IsFalse(FullscreenGeometry.CoversMonitor(monitor, monitor, true, false, false));
        Assert.IsFalse(FullscreenGeometry.CoversMonitor(monitor, monitor, false, true, false));
        Assert.IsFalse(FullscreenGeometry.CoversMonitor(monitor, monitor, false, false, true));
        Assert.IsFalse(FullscreenGeometry.CoversMonitor(new(-1920,0,1920,1040), monitor, false, false, false));
    }
}
