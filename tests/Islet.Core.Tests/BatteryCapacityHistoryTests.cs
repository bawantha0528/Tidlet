using Islet.Core.Metrics;

namespace Islet.Core.Tests;

[TestClass]
public sealed class BatteryCapacityHistoryTests
{
    [TestMethod]
    public void StoresOneReadingPerDayAndSeparatesDifferentDesignCapacities()
    {
        var path = Path.GetTempFileName();
        try
        {
            var day = new DateOnly(2026, 11, 10);
            var first = new BatteryReading(BatteryReadingState.Available, 80, BatteryChargingStatus.Charging, null,
                null, null, "Mock", DesignedCapacityMWh: 60_000, FullChargedCapacityMWh: 40_000);
            var history = new BatteryCapacityHistory(path);
            history.Record(day, first);
            history.Record(day, first with { FullChargedCapacityMWh = 30_000 });
            history = new BatteryCapacityHistory(path);
            history.Record(day.AddDays(1), first with { FullChargedCapacityMWh = 35_000 });
            StringAssert.Contains(history.Describe(), $"{40.0:0.00} Wh");
            StringAssert.Contains(history.Describe(), $"{35.0:0.00} Wh");
            history.Record(day.AddDays(2), first with { DesignedCapacityMWh = 50_000 });
            StringAssert.Contains(history.Describe(), "trend needs readings");
        }
        finally { File.Delete(path); }
    }
}
