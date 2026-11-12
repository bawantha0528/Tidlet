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
        var items=BarLayout.Items(gaming,800,true,out var indicator);
        Assert.IsNotNull(indicator);
        Assert.AreEqual(indicator.Value+BarLayout.GamingIndicatorWidth+10,items[0].X);
        BarLayout.Items(normal,800,true,out var normalIndicator);
        Assert.IsNull(normalIndicator);
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

    [TestMethod]
    public void FpsTargetAndThresholdPercentHaveCorrectDefaultsAndSurviveExchange()
    {
        var defaults = new BarSettings();
        Assert.AreEqual(60, defaults.FpsTarget);
        Assert.AreEqual(85, defaults.ThresholdPercent);
        Assert.IsFalse(defaults.GamingModeAuto);

        var custom = new BarSettings { FpsTarget = 144, ThresholdPercent = 90, GamingModeAuto = true };
        var restored = BarSettingsFile.Import(BarSettingsFile.Export(custom));
        Assert.AreEqual(144, restored.FpsTarget);
        Assert.AreEqual(90, restored.ThresholdPercent);
        Assert.IsTrue(restored.GamingModeAuto);
        Assert.IsFalse(restored.GamingMode);
    }

    [TestMethod]
    public void FpsTargetAndThresholdPercentValidateRange()
    {
        new BarSettings { FpsTarget = 30, ThresholdPercent = 50 }.Validate();
        new BarSettings { FpsTarget = 360, ThresholdPercent = 100 }.Validate();

        Assert.ThrowsExactly<InvalidDataException>(() => new BarSettings { FpsTarget = 29 }.Validate());
        Assert.ThrowsExactly<InvalidDataException>(() => new BarSettings { FpsTarget = 361 }.Validate());
        Assert.ThrowsExactly<InvalidDataException>(() => new BarSettings { ThresholdPercent = 49 }.Validate());
        Assert.ThrowsExactly<InvalidDataException>(() => new BarSettings { ThresholdPercent = 101 }.Validate());
    }

    [TestMethod]
    public void MeterWarningLevelsUseTheDirectionOfEachReading()
    {
        Assert.AreEqual(ThresholdLevel.Normal,BarThresholds.BatteryChargeLevel(100));
        Assert.AreEqual(ThresholdLevel.Warning,BarThresholds.BatteryChargeLevel(25));
        Assert.AreEqual(ThresholdLevel.Critical,BarThresholds.BatteryChargeLevel(15));
        Assert.AreEqual(ThresholdLevel.Warning,BarThresholds.BatteryHealthLevel(80));
        Assert.AreEqual(ThresholdLevel.Critical,BarThresholds.BatteryHealthLevel(60));
        Assert.AreEqual(ThresholdLevel.Warning,BarThresholds.StorageLevel(90));
        Assert.AreEqual(ThresholdLevel.Critical,BarThresholds.StorageLevel(95));
        Assert.AreEqual(ThresholdLevel.Warning,BarThresholds.UsageLevel(BarModule.Disk,86,85));
        Assert.AreEqual(ThresholdLevel.Warning,BarThresholds.TemperatureLevel(80));
        Assert.AreEqual(ThresholdLevel.Critical,BarThresholds.TemperatureLevel(90));
        Assert.AreEqual(ThresholdLevel.Normal,BarThresholds.TemperatureLevel(double.NaN));
        Assert.AreEqual(ThresholdLevel.Normal,BarThresholds.BatteryChargeLevel(null));
    }

    [TestMethod]
    public void ThresholdAndFrameLevelsEvaluateBoundariesAndInvalidInputsSafely()
    {
        foreach (var module in new[] { BarModule.Cpu, BarModule.Gpu, BarModule.Ram })
        {
            Assert.AreEqual(ThresholdLevel.Normal, BarThresholds.UsageLevel(module, 84.9, 85));
            Assert.AreEqual(ThresholdLevel.Warning, BarThresholds.UsageLevel(module, 85.0, 85));
            Assert.AreEqual(ThresholdLevel.Warning, BarThresholds.UsageLevel(module, 94.9, 85));
            Assert.AreEqual(ThresholdLevel.Critical, BarThresholds.UsageLevel(module, 95.0, 85));
            Assert.AreEqual(ThresholdLevel.Warning, BarThresholds.UsageLevel(module, 70.0, 70));
            Assert.AreEqual(ThresholdLevel.Normal, BarThresholds.UsageLevel(module, double.NaN, 85));
            Assert.AreEqual(ThresholdLevel.Normal, BarThresholds.UsageLevel(module, -1, 85));
            Assert.AreEqual(ThresholdLevel.Normal, BarThresholds.UsageLevel(module, 101, 85));
        }

        Assert.AreEqual(ThresholdLevel.Normal, BarThresholds.UsageLevel(BarModule.Battery, 98, 85));
        Assert.AreEqual(ThresholdLevel.Normal, BarThresholds.UsageLevel(BarModule.Network, 98, 85));

        Assert.AreEqual(FrameRateLevel.Unknown, BarThresholds.FrameLevel(null, 60));
        Assert.AreEqual(FrameRateLevel.Unknown, BarThresholds.FrameLevel(0, 60));
        Assert.AreEqual(FrameRateLevel.Unknown, BarThresholds.FrameLevel(-5, 60));
        Assert.AreEqual(FrameRateLevel.Unknown, BarThresholds.FrameLevel(double.NaN, 60));
        Assert.AreEqual(FrameRateLevel.Good, BarThresholds.FrameLevel(10.0, 60));   // 100 fps >= 60
        Assert.AreEqual(FrameRateLevel.Good, BarThresholds.FrameLevel(16.0, 60));   // 62.5 fps >= 60
        Assert.AreEqual(FrameRateLevel.Fair, BarThresholds.FrameLevel(25.0, 60));   // 40 fps >= 30
        Assert.AreEqual(FrameRateLevel.Fair, BarThresholds.FrameLevel(1000.0 / 30, 60));
        Assert.AreEqual(FrameRateLevel.Low, BarThresholds.FrameLevel(50.0, 60));    // 20 fps < 30
        Assert.AreEqual(FrameRateLevel.Fair, BarThresholds.FrameLevel(10.0, 144));  // 100 fps < 144, >= 72
        Assert.AreEqual(FrameRateLevel.Low, BarThresholds.FrameLevel(20.0, 144));   // 50 fps < 72
    }
}
