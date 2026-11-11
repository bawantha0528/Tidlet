using System.Text.Json;

namespace Islet.Core.Metrics;

public sealed class BatteryCapacityHistory(string path)
{
    private sealed record Entry(DateOnly Day, uint Design, uint Full);
    private Entry[]? _entries;
    private DateOnly? _recorded;

    public void Record(DateOnly day, BatteryReading reading)
    {
        if (_recorded == day || reading.State != BatteryReadingState.Available ||
            reading.DesignedCapacityMWh is not (> 0 and < uint.MaxValue) ||
            reading.FullChargedCapacityMWh is not (> 0 and < uint.MaxValue)) return;
        var design = reading.DesignedCapacityMWh.Value;
        var existing = Entries();
        if (existing.Length > 0 && existing[^1].Day == day && existing[^1].Design == design)
        {
            _recorded = day;
            return;
        }
        var entries = existing.Where(entry => entry.Day < day && entry.Day.DayNumber >= day.DayNumber - 30 && entry.Design == design)
            .Append(new Entry(day, design, reading.FullChargedCapacityMWh.Value)).ToArray();
        _entries = entries;
        _recorded = day;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(entries));
            File.Move(path + ".tmp", path, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    public string Describe()
    {
        var entries = Entries();
        if (entries.Length == 0) return "Waiting for a full-charge capacity reading.";
        var latest = entries[^1];
        if (entries.Length == 1) return $"{latest.Full / 1000.0:0.00} Wh recorded. A trend needs readings on different days.";
        var first = entries[0];
        return $"{first.Full / 1000.0:0.00} Wh on {first.Day:MMM d} → {latest.Full / 1000.0:0.00} Wh on {latest.Day:MMM d}";
    }

    private Entry[] Entries()
    {
        if (_entries is not null) return _entries;
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length <= 4096)
                return _entries = (JsonSerializer.Deserialize<Entry[]>(File.ReadAllText(path)) ?? [])
                    .Where(entry => entry is not null && entry.Design is > 0 and < uint.MaxValue && entry.Full is > 0 and < uint.MaxValue)
                    .OrderBy(entry => entry.Day).TakeLast(31).ToArray();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
        return _entries = [];
    }
}
