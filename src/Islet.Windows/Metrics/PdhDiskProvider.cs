using System.Diagnostics;
using System.Runtime.InteropServices;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;

namespace Islet.Windows.Metrics;

public interface IDiskRateQuery : IDisposable
{
    CapabilityProbeResult Open();
    DiskReading Collect(bool prime);
}

/// <summary>One bounded background request at a time; no polling thread or blocking UI wait.</summary>
public sealed class PdhDiskProvider(IDiskRateQuery query) : IDiskProvider, IDisposable
{
    private CapabilityProbeResult? _capability;
    private Task<(int Generation, DiskReading Reading)>? _pending;
    private DiskReading _latest = DiskReading.Missing(DiskReadingState.WarmingUp, "Measuring disk activity…");
    private int _generation, _workerGeneration = -1;
    private bool _disposed;
    private readonly Stopwatch _age = Stopwatch.StartNew();
    public int Requests { get; private set; }
    public CapabilityProbeResult Probe() => _capability ??= query.Open();
    public DiskReading Sample()
    {
        if (_disposed || _capability?.Availability != CapabilityAvailability.Supported)
            return DiskReading.Missing(DiskReadingState.Unavailable, _capability?.Explanation ?? "Disk counters are not available.");
        if (_pending?.IsCompleted == true)
        {
            var completed = _pending.GetAwaiter().GetResult(); _pending = null;
            if (completed.Generation == _generation) { _latest = completed.Reading; _age.Restart(); }
        }
        if (_pending is null)
        {
            var generation = _generation;
            Requests++;
            _pending = Task.Run(() =>
            {
                try
                {
                    var prime = _workerGeneration != generation;
                    _workerGeneration = generation;
                    var value = query.Collect(prime);
                    if (value.State is DiskReadingState.Invalid or DiskReadingState.Unavailable) _workerGeneration = -1;
                    return (generation, value);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
                { _workerGeneration = -1; return (generation, DiskReading.Missing(DiskReadingState.Unavailable, "Disk counters: " + ex.Message)); }
            });
        }
        return _age.Elapsed.TotalSeconds <= 3 ? _latest : DiskReading.Missing(DiskReadingState.Unavailable, "Windows disk counters are taking longer than expected.");
    }
    public void Reset()
    {
        _generation++;
        _latest = DiskReading.Missing(DiskReadingState.WarmingUp, "Measuring disk activity…");
        _age.Restart();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_pending is { IsCompleted: false } pending)
            _ = pending.ContinueWith(_ => query.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        else query.Dispose();
    }
}

public sealed class NativeDiskRateQuery : IDiskRateQuery
{
    private nint _query, _read, _write;
    public CapabilityProbeResult Open()
    {
        uint status;
        try
        {
            if (_query != 0) return CapabilityProbeResult.Supported;
            status = PdhOpenQueryW(null, 0, out _query);
            if (status == 0) status = PdhAddEnglishCounterW(_query, @"\PhysicalDisk(_Total)\Disk Read Bytes/sec", 0, out _read);
            if (status == 0) status = PdhAddEnglishCounterW(_query, @"\PhysicalDisk(_Total)\Disk Write Bytes/sec", 0, out _write);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or ExternalException)
        { Dispose(); return CapabilityProbeResult.Unsupported("disk.pdh-unavailable", "Windows disk counters are unavailable: " + ex.Message); }
        if (status == 0) return CapabilityProbeResult.Supported;
        Dispose();
        return CapabilityProbeResult.Unsupported("disk.pdh-denied", $"Windows disk counters could not be opened (0x{status:X8}). No permission changes were made.");
    }
    public DiskReading Collect(bool prime)
    {
        var status = PdhCollectQueryData(_query);
        if (status != 0) return DiskReading.Missing(DiskReadingState.Unavailable, $"Windows disk counters are unavailable (0x{status:X8}).");
        if (prime) return DiskReading.Missing(DiskReadingState.WarmingUp, "Measuring disk throughput…");
        if (!TryValue(_read, out var read) || !TryValue(_write, out var write))
            return DiskReading.Missing(DiskReadingState.Invalid, "Windows did not return valid disk rates; measuring again.");
        return new(DiskReadingState.Available, (ulong)Math.Round(read), (ulong)Math.Round(write), null,
            "Read/write throughput across all physical disks. This is activity, not storage capacity.");
    }
    internal static bool ValidRate(double value) => double.IsFinite(value) && value >= 0 && value < ulong.MaxValue;
    private static bool TryValue(nint counter, out double value)
    {
        var result = PdhGetFormattedCounterValue(counter, 0x200 | 0x8000, out _, out var formatted);
        value = formatted.Value;
        return result == 0 && formatted.Status <= 1 && ValidRate(value);
    }
    public void Dispose()
    {
        if (_query != 0) PdhCloseQuery(_query);
        _query = _read = _write = 0;
    }
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct CounterValue { [FieldOffset(0)] public uint Status; [FieldOffset(8)] public double Value; }
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint PdhOpenQueryW(string? source, nuint userData, out nint query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint PdhAddEnglishCounterW(nint query, string path, nuint userData, out nint counter);
    [DllImport("pdh.dll", ExactSpelling = true)] private static extern uint PdhCollectQueryData(nint query);
    [DllImport("pdh.dll", ExactSpelling = true)] private static extern uint PdhGetFormattedCounterValue(nint counter, uint format, out uint type, out CounterValue value);
    [DllImport("pdh.dll", ExactSpelling = true)] private static extern uint PdhCloseQuery(nint query);
}