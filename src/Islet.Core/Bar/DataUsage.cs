using System.Text.Json;

namespace Islet.Core.Bar;

public enum UsagePeriod { Day, Week, Month }

public readonly record struct UsageBytes(ulong Download, ulong Upload)
{
    public ulong Total => Sum(Download, Upload);
    internal UsageBytes Add(UsageBytes other) => new(Sum(Download, other.Download), Sum(Upload, other.Upload));
    private static ulong Sum(ulong a, ulong b) => ulong.MaxValue - a < b ? ulong.MaxValue : a + b;
}

public sealed record UsageSnapshot(UsagePeriod Period, UsageBytes Bytes, bool HasSamples);

public sealed class DataUsage
{
    private sealed record SavedUsage(UsagePeriod Period, Dictionary<DateOnly, UsageBytes> Days);
    private readonly object _gate = new();
    private readonly Dictionary<DateOnly, UsageBytes> _days = [];
    private UsagePeriod _period = UsagePeriod.Month;
    private bool _dirty;
    private long _lastSave = Environment.TickCount64;

    public void Record(DateOnly day, ulong download, ulong upload)
    {
        lock (_gate)
        {
            var prior = _days.GetValueOrDefault(day);
            if (_days.ContainsKey(day) && download == 0 && upload == 0) return;
            _days[day] = prior.Add(new(download, upload));
            Prune(day);
            _dirty = true;
        }
    }

    public void Select(UsagePeriod period)
    {
        if (!Enum.IsDefined(period)) return;
        lock (_gate)
        {
            if (_period == period) return;
            _period = period;
            _dirty = true;
        }
    }

    public UsageSnapshot Read(DateOnly day)
    {
        lock (_gate)
        {
            var from = _period switch
            {
                UsagePeriod.Week => day.AddDays(-((int)day.DayOfWeek + 6) % 7),
                UsagePeriod.Month => new DateOnly(day.Year, day.Month, 1),
                _ => day
            };
            UsageBytes bytes = default;
            var hasSamples = false;
            foreach (var entry in _days)
            {
                if (entry.Key < from || entry.Key > day) continue;
                bytes = bytes.Add(entry.Value);
                hasSamples = true;
            }
            return new(_period, bytes, hasSamples);
        }
    }

    public static DataUsage Load(string path, DateOnly day)
    {
        var ledger = new DataUsage();
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 32_768) return ledger;
            var saved = JsonSerializer.Deserialize<SavedUsage>(File.ReadAllText(path));
            if (saved?.Days is null || !Enum.IsDefined(saved.Period)) return ledger;
            ledger._period = saved.Period;
            foreach (var item in saved.Days) ledger._days[item.Key] = item.Value;
            ledger.Prune(day);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return ledger;
    }

    public void Save(string path, bool force = false)
    {
        lock (_gate)
        {
            var now = Environment.TickCount64;
            if (!_dirty || !force && now - _lastSave < 60_000) return;
            _lastSave = now;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new SavedUsage(_period, _days)));
                File.Move(path + ".tmp", path, true);
                _dirty = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private void Prune(DateOnly day)
    {
        if (_days.Count <= 40) return;
        foreach (var key in _days.Keys.Where(key => key.DayNumber < day.DayNumber - 39 || key > day).ToArray())
            _days.Remove(key);
    }
}
