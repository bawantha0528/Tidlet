namespace Islet.Core.Bar;

public enum SustainedProblem { None, Cpu, Memory, Battery }

public sealed class SustainedDiagnostics
{
    private readonly double?[] _since = new double?[3];
    private readonly bool[] _notified = new bool[3];
    private double? _lastSample;
    private double _nextAlert;

    public bool HasSample => _lastSample.HasValue;
    public double? CpuPercent { get; private set; }
    public double? MemoryPercent { get; private set; }
    public bool HasReadings => CpuPercent.HasValue || MemoryPercent.HasValue;
    public SustainedProblem Active { get; private set; }

    public void Reset()
    {
        Array.Clear(_since);
        Array.Clear(_notified);
        _lastSample = null;
        CpuPercent = null;
        MemoryPercent = null;
        Active = SustainedProblem.None;
    }

    public SustainedProblem Observe(double seconds, double? cpu, double? memory, double? unpluggedBattery, BarSettings settings)
    {
        if (_lastSample is double previous && (seconds <= previous || seconds - previous > 15)) Reset();
        _lastSample = seconds;
        CpuPercent = Valid(cpu) ? cpu : null;
        MemoryPercent = Valid(memory) ? memory : null;
        Observe(0, Valid(cpu) && cpu >= settings.SustainedUsagePercent);
        Observe(1, Valid(memory) && memory >= settings.SustainedUsagePercent);
        Observe(2, Valid(unpluggedBattery) && unpluggedBattery <= settings.SustainedBatteryPercent);

        Active = SustainedProblem.None;
        var alert = SustainedProblem.None;
        for (int i = 0; i < _since.Length; i++)
        {
            if (_since[i] is not double start || seconds - start < settings.SustainedSeconds) continue;
            var problem = (SustainedProblem)(i + 1);
            if (i < 2 && Active == SustainedProblem.None) Active = problem;
            if (!settings.SustainedAlerts || _notified[i] || seconds < _nextAlert) continue;
            _notified[i] = true;
            _nextAlert = seconds + 300;
            alert = problem;
        }
        return alert;

        void Observe(int index, bool problem)
        {
            if (problem) _since[index] ??= seconds;
            else
            {
                _since[index] = null;
                _notified[index] = false;
            }
        }
    }

    private static bool Valid(double? value) => value is >= 0 and <= 100;

    public static string Describe(SustainedProblem problem) => problem switch
    {
        SustainedProblem.Cpu => "CPU usage has stayed high. Open CPU to see which processes are busiest.",
        SustainedProblem.Memory => "Physical memory usage has stayed high. Open RAM to see which processes are using it.",
        SustainedProblem.Battery => "Battery charge has stayed low while unplugged.",
        _ => "No sustained CPU or memory pressure detected in the available readings."
    };
}
