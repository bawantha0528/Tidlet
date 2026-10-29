using Islet.Core.Island;
using Islet.Core.Settings;

namespace Islet.Core.Tests;

[TestClass]
public sealed class IslandGeometryTests
{
    [TestMethod]
    public void PlacementCentersOnNegativeCoordinateDisplaysAtTheirDpi()
    {
        var display = new IslandDisplay("left", new(-1920, -200, 1920, 1080), new(-1920, -200, 1920, 1040), 144);
        var pill = IslandGeometry.Place(display, 220, 36, IslandStyle.Island);
        Assert.AreEqual(new PixelRect(-1125, -188, 330, 54), pill);
        var notch = IslandGeometry.Place(display, 220, 36, IslandStyle.Notch);
        Assert.AreEqual(-200, notch.Y);
        Assert.AreEqual(pill.X, notch.X);
    }

    [TestMethod]
    public void ShapeIsBoundedAndNotchTopFlaresOutInsteadOfRoundingIn()
    {
        Assert.IsTrue(IslandGeometry.LeftEdge(0.01, 240, 60, 18, IslandStyle.Notch) < 1);
        Assert.IsTrue(IslandGeometry.LeftEdge(0.01, 240, 60, 18, IslandStyle.Island) > 17);
        Assert.AreEqual(18.0, IslandGeometry.LeftEdge(25, 240, 60, 18, IslandStyle.Notch));
        Assert.IsTrue(IslandGeometry.LeftEdge(59, 240, 60, 18, IslandStyle.Notch) > 18);
        foreach (var style in Enum.GetValues<IslandStyle>())
            for (var y = 0; y < 60; y++)
            {
                var edge = IslandGeometry.LeftEdge(y + 0.5, 240, 60, 18, style);
                Assert.IsTrue(edge >= 0 && edge <= 120 && double.IsFinite(edge));
            }
    }

    [TestMethod]
    public void PlacementClampsOversizeViewsAndRejectsInvalidGeometry()
    {
        var display = new IslandDisplay("tiny", new(0, 0, 200, 100), new(0, 0, 200, 100), 96);
        var bounds = IslandGeometry.Place(display, 640, 320, IslandStyle.Island);
        Assert.AreEqual(new PixelRect(0, 8, 200, 92), bounds);
        Assert.Throws<ArgumentOutOfRangeException>(() => IslandGeometry.Place(display, double.NaN, 30, IslandStyle.Island));
        Assert.Throws<ArgumentOutOfRangeException>(() => IslandGeometry.Place(display with { Dpi = 0 }, 220, 36, IslandStyle.Island));
    }
}
