using Islet.Core.Bar;

namespace Islet.Core.Tests;

[TestClass]
public sealed class DataUsageTests
{
    [TestMethod]
    public void PeriodsUseLocalCalendarDatesAndRestoreTheSelection()
    {
        var ledger = new DataUsage();
        var today = new DateOnly(2026, 11, 2);
        ledger.Record(today.AddDays(-2), 100, 10);
        ledger.Record(today.AddDays(-1), 200, 20);
        Assert.AreEqual(new UsageBytes(200, 20), ledger.Read(today).Bytes);
        ledger.Select(UsagePeriod.Day);
        Assert.IsFalse(ledger.Read(today).HasSamples);
        ledger.Record(today, 300, 30);
        Assert.AreEqual(330UL, ledger.Read(today).Bytes.Total);
        ledger.Select(UsagePeriod.Week);
        Assert.AreEqual(330UL, ledger.Read(today).Bytes.Total);
        Assert.AreEqual(330UL, ledger.Read(today.AddDays(-1)).Bytes.Total);
        ledger.Select(UsagePeriod.Month);
        Assert.AreEqual(550UL, ledger.Read(today).Bytes.Total);

        var path = Path.GetTempFileName();
        try
        {
            ledger.Select(UsagePeriod.Week);
            ledger.Save(path, force: true);
            var restored = DataUsage.Load(path, today).Read(today);
            Assert.AreEqual(UsagePeriod.Week, restored.Period);
            Assert.AreEqual(new UsageBytes(300, 30), restored.Bytes);
        }
        finally { File.Delete(path); }
    }
}
