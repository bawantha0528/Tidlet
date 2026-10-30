using Islet.Core.Presentation;

namespace Islet.Core.Tests;

[TestClass]
public sealed class DashboardLayoutTests
{
    [TestMethod]
    public void PercentFormattingRejectsMissingNonFiniteAndImpossibleValues()
    {
        foreach (var value in new double?[] { null, double.NaN, double.PositiveInfinity, -1, 101 })
        { Assert.IsFalse(MonitorFormat.ValidPercent(value)); Assert.AreEqual("Unavailable", MonitorFormat.Percent(value)); }
        Assert.AreEqual("0%", MonitorFormat.Percent(0));
        Assert.AreEqual("100%", MonitorFormat.Percent(100));
    }
    [TestMethod]
    public void CardsFitNarrowAndWideWindowsWithoutOverlapping()
    {
        foreach (var width in new[] { 580.0, 860.0, 1024.0 })
        {
            var boxes = Enumerable.Range(0, 6).Select(i => DashboardLayout.Card(i, 6, width, 560)).ToArray();
            foreach (var box in boxes)
            {
                Assert.IsTrue(box.X >= 28 && box.Y >= 116 && box.Width > 0 && box.Height > 0);
                Assert.IsTrue(box.X + box.Width <= width - 27 && box.Y + box.Height <= 519);
            }
            for (var i = 0; i < boxes.Length; i++) for (var j = i + 1; j < boxes.Length; j++)
                Assert.IsFalse(boxes[i].Contains(boxes[j].X, boxes[j].Y));
        }
    }
    [TestMethod]
    public void TabsAndCardsUseDrawingHitTargetsAndAbsentBatteryIsSkipped()
    {
        MonitorCard Card(MonitorPage page, bool absent = false) => new(page, page.ToString(), "Unavailable", "", null, "No sensor", [], [], Absent: absent);
        var dashboard = new MonitorDashboard([Card(MonitorPage.Cpu), Card(MonitorPage.Battery, true), Card(MonitorPage.Apps)]);
        for (var i = 0; i < DashboardLayout.PageNames.Length; i++)
        {
            var rect = DashboardLayout.Tab(i, 860);
            Assert.AreEqual(400 + i, DashboardLayout.HitTest(rect.X + 2, rect.Y + 2, 860, 560, dashboard));
        }
        var apps = DashboardLayout.Card(1, 2, 860, 560);
        Assert.AreEqual(407, DashboardLayout.HitTest(apps.X + 2, apps.Y + 2, 860, 560, dashboard));
        Assert.AreEqual(0, DashboardLayout.HitTest(0, 0, 860, 560, dashboard));
    }
    [TestMethod]
    public void SmallScreensAndExtremeDpiFitEntireCanvasWithinTheBitmapLimit()
    {
        foreach (var test in new[] { (W: 1024, H: 700, Scale: 1.5), (W: 3840, H: 2100, Scale: 3.0), (W: 7680, H: 4200, Scale: 4.0) })
        {
            var scale = DashboardLayout.FitScale(860, 560, test.Scale, test.W, test.H);
            Assert.IsTrue(scale <= test.Scale);
            Assert.IsTrue(860 * scale <= test.W + 0.01 && 560 * scale <= test.H + 0.01);
            Assert.IsTrue(860 * 560 * scale * scale <= 3_900_001);
        }
    }}