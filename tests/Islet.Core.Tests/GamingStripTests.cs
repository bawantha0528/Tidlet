using Islet.Core.Bar;


namespace Islet.Core.Tests;

[TestClass]
public sealed class GamingStripTests
{
    [TestMethod]
    public void GamingLayoutPreservesNormalOrderAndDoesNotPersist()
    {
        var normal = new BarSettings { Items = [BarModule.Network, BarModule.Cpu] };
        var gaming = normal with { GamingMode = true };

        CollectionAssert.AreEqual(new[] { BarModule.FrameRate, BarModule.Gpu, BarModule.Cpu, BarModule.Ram },
            BarLayout.Items(gaming, 800, false).Select(item => item.Module).ToArray());
        Assert.AreEqual(BarModule.Battery, BarLayout.Items(gaming, 800, true)[^1].Module);
        var restored = BarSettingsFile.Import(BarSettingsFile.Export(gaming));
        Assert.IsFalse(restored.GamingMode);
        CollectionAssert.AreEqual(normal.Items, restored.Items);
    }

    [TestMethod]
    public void FrameTimingUsesOnlyValidDisplayedFramesFromTheMostActiveSwapChain()
    {
        var samples = new PresentMonFrames();
        samples.Add("game.exe,11,16");
        Assert.IsNull(samples.TakeInterval());
        samples.Add("Application,SwapChainAddress,DisplayedTime");
        samples.Add("game.exe,11,16");
        samples.Add("game.exe,11,24");
        samples.Add("game.exe,11,NA");
        samples.Add("game.exe,11,NaN");
        samples.Add("game.exe,11,0");
        samples.Add("game.exe,22,80");

        Assert.AreEqual(20d, samples.TakeInterval());
        Assert.IsNull(samples.TakeInterval());
    }
}
