using Islet.Core.Capabilities;
using Islet.Core.Island;
using Islet.Core.Metrics;
using Islet.Core.Presentation;
using Islet.Core.Settings;

namespace Islet.Shell.Island;

public sealed partial class NativeIslandHost
{
    private readonly IDiskProvider? _disk;
    private readonly IGpuProvider? _gpu;
    private readonly IProcessProvider? _processes;
    private readonly Dictionary<MonitorPage, CapabilityProbeResult> _support = new();
    private readonly Dictionary<MonitorPage, List<double?>> _moduleHistory = new();
    private readonly List<double?> _uploadHistory = new(60), _writeHistory = new(60);
    private RamReading? _ramReading;
    private NetworkReading? _networkReading;
    private BatteryReading? _batteryReading;
    private DiskReading? _diskReading;
    private GpuReading? _gpuReading;
    private IReadOnlyList<ProcessMetric> _processReadings = Array.Empty<ProcessMetric>();
    private string? _processError;
    private double _lastProcessRefresh = double.NegativeInfinity, _displayScale = 1;
    private MonitorPage _page;
    private ThemeMode _theme = ThemeMode.Dark;
    private readonly string _accent = "#3B82F6";
    public int DashboardSamples { get; private set; }
    public MonitorPage SelectedPage => _page;

    private static T SafeRead<T>(Func<T> read, Func<string, T> missing)
    {
        try { return read(); }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        { return missing("Reading unavailable: " + ex.Message); }
    }

    private void ProbeDashboard()
    {
        void Probe(MonitorPage page, Func<CapabilityProbeResult>? probe) => _support[page] = probe is null
            ? CapabilityProbeResult.Unsupported("module.not-connected", "This reading is not connected in this build.")
            : SafeRead(probe, error => CapabilityProbeResult.Unsupported("module.probe-failed", error));
        Probe(MonitorPage.Memory, _ram is null ? null : _ram.Probe);
        Probe(MonitorPage.Network, _network is null ? null : _network.Probe);
        Probe(MonitorPage.Battery, _battery is null ? null : _battery.Probe);
        Probe(MonitorPage.Disk, _disk is null ? null : _disk.Probe);
        Probe(MonitorPage.Gpu, _gpu is null ? null : _gpu.Probe);
        Probe(MonitorPage.Apps, _processes is null ? null : _processes.Probe);
    }

    private static bool ValidRam(RamReading? ram) => ram is { State: RamReadingState.Available }
        && MonitorFormat.ValidPercent(ram.UsedPercent) && ram.TotalBytes > 0
        && ram.AvailableBytes <= ram.TotalBytes && ram.UsedBytes == ram.TotalBytes - ram.AvailableBytes;
    private bool Supported(MonitorPage page) => _support.TryGetValue(page, out var value) && value.Availability == CapabilityAvailability.Supported;
    private string Reason(MonitorPage page) => _support.GetValueOrDefault(page)?.Explanation ?? "Waiting for the next reading.";
    private void ResetDashboard()
    {
        _ram?.Reset(); _network?.Reset(); _battery?.Reset(); _disk?.Reset(); _gpu?.Reset(); _processes?.Reset();
        _moduleHistory.Clear(); _uploadHistory.Clear(); _writeHistory.Clear();
        _ramReading = null; _networkReading = null; _batteryReading = null; _diskReading = null; _gpuReading = null;
        _processReadings = Array.Empty<ProcessMetric>(); _processError = null;
        _lastProcessRefresh = double.NegativeInfinity;
    }
    private static void Append(List<double?> history, double? value)
    {
        if (history.Count == 60) history.RemoveAt(0);
        history.Add(value is { } v && double.IsFinite(v) && v >= 0 ? v : null);
    }
    private void Record(MonitorPage page, double? value)
    {
        if (!_moduleHistory.TryGetValue(page, out var list)) _moduleHistory[page] = list = new(60);
        Append(list, value);
    }
    private IReadOnlyList<double?> History(MonitorPage page) => _moduleHistory.TryGetValue(page, out var list) ? list : Array.Empty<double?>();
    private void SampleDashboard()
    {
        DashboardSamples++;
        if (Supported(MonitorPage.Memory)) _ramReading = SafeRead(_ram!.Sample, e => RamReading.Missing(RamReadingState.Unavailable, e));
        if (Supported(MonitorPage.Network)) _networkReading = SafeRead(_network!.Sample, e => NetworkReading.Missing(NetworkReadingState.Unavailable, e));
        if (Supported(MonitorPage.Battery)) _batteryReading = SafeRead(_battery!.Sample, e => BatteryReading.Missing(BatteryReadingState.Unavailable, e));
        if (!CompactNotch && Supported(MonitorPage.Disk)) _diskReading = SafeRead(_disk!.Sample, e => DiskReading.Missing(DiskReadingState.Unavailable, e));
        if (!CompactNotch && Supported(MonitorPage.Gpu)) _gpuReading = SafeRead(_gpu!.Sample, e => GpuReading.Missing(GpuReadingState.Unavailable, e));
        if (!CompactNotch && Supported(MonitorPage.Apps) && _now() - _lastProcessRefresh >= 4000)
        {
            _lastProcessRefresh = _now(); _processError = null;
            _processReadings = SafeRead(() => _processes!.SampleTop(6).Take(6).ToArray(), e => { _processError = e; return Array.Empty<ProcessMetric>(); });
        }
        Record(MonitorPage.Memory, ValidRam(_ramReading) ? _ramReading!.UsedPercent : null);
        Record(MonitorPage.Network, _networkReading is { State: NetworkReadingState.Available } n ? n.BytesReceivedPerSec : null);
        Append(_uploadHistory, _networkReading is { State: NetworkReadingState.Available } n2 ? n2.BytesSentPerSec : null);
        Record(MonitorPage.Disk, _diskReading is { State: DiskReadingState.Available } d ? d.ReadBytesPerSec : null);
        Append(_writeHistory, _diskReading is { State: DiskReadingState.Available } d2 ? d2.WriteBytesPerSec : null);
        Record(MonitorPage.Gpu, _gpuReading is { State: GpuReadingState.Available } && MonitorFormat.ValidPercent(_gpuReading.UtilizationPercent) ? _gpuReading.UtilizationPercent : null);
        Record(MonitorPage.Battery, _batteryReading is { State: BatteryReadingState.Available } && MonitorFormat.ValidPercent(_batteryReading.Percent) ? _batteryReading.Percent : null);
        var parts = new List<string>();
        if (_ramReading is { State: RamReadingState.Available } r && MonitorFormat.ValidPercent(r.UsedPercent)) parts.Add($"RAM: {r.UsedPercent:F0}%");
        if (_networkReading is { State: NetworkReadingState.Available } net) parts.Add($"Net: ↓ {NetworkReading.FormatRate(net.BytesReceivedPerSec)}");
        if (_batteryReading is { State: BatteryReadingState.Available } b && MonitorFormat.ValidPercent(b.Percent)) parts.Add($"Bat: {b.Percent:F0}%");
        _secondaryLabel = parts.Count == 0 ? null : string.Join("  ·  ", parts);
    }

    private MonitorDashboard CreateDashboard()
    {
        var cards = new List<MonitorCard>();
        var cpu = _history.LastOrDefault();
        cards.Add(new(MonitorPage.Cpu, "Processor", MonitorFormat.Percent(cpu), "Total CPU utilization", cpu,
            _explanation, [new("In use", MonitorFormat.Percent(cpu)), new("Idle", cpu.HasValue ? $"{100 - cpu:0}%" : "Not reported"),
                new("Temperature", "Not reported"), new("Clock speed", "Not reported")], _history));
        var ram = _ramReading;
        var ramOk = ValidRam(ram);
        cards.Add(new(MonitorPage.Memory, "Memory", ramOk ? MonitorFormat.Percent(ram!.UsedPercent) : "Unavailable", "Physical memory in use", ramOk ? ram!.UsedPercent : null,
            ram?.Explanation ?? Reason(MonitorPage.Memory), ramOk ? [new("Used", MonitorFormat.Bytes(ram!.UsedBytes)), new("Available", MonitorFormat.Bytes(ram.AvailableBytes)), new("Installed", MonitorFormat.Bytes(ram.TotalBytes))] : [], History(MonitorPage.Memory)));
        var net = _networkReading;
        var netOk = net?.State == NetworkReadingState.Available;
        cards.Add(new(MonitorPage.Network, "Network", netOk ? MonitorFormat.Rate(net!.BytesReceivedPerSec) : net is null || net.State == NetworkReadingState.WarmingUp ? "Measuring" : "Unavailable", "Download / upload", null,
            net?.Explanation ?? Reason(MonitorPage.Network), netOk ? [new("Download", MonitorFormat.Rate(net!.BytesReceivedPerSec)), new("Upload", MonitorFormat.Rate(net.BytesSentPerSec)), new("Interface", net.InterfaceName ?? "Active adapters")] : [], History(MonitorPage.Network), _uploadHistory, "DOWNLOAD / UPLOAD"));
        var disk = _diskReading;
        var diskOk = disk?.State == DiskReadingState.Available;
        cards.Add(new(MonitorPage.Disk, "Disk activity", diskOk ? MonitorFormat.Rate(disk!.ReadBytesPerSec) : disk is null || disk.State == DiskReadingState.WarmingUp ? "Measuring" : "Unavailable", "All physical disks", null,
            disk?.Explanation ?? Reason(MonitorPage.Disk), diskOk ? [new("Read", MonitorFormat.Rate(disk!.ReadBytesPerSec)), new("Write", MonitorFormat.Rate(disk.WriteBytesPerSec))] : [], History(MonitorPage.Disk), _writeHistory, "READ / WRITE"));
        var gpu = _gpuReading;
        var gpuOk = gpu?.State == GpuReadingState.Available && MonitorFormat.ValidPercent(gpu.UtilizationPercent);
        cards.Add(new(MonitorPage.Gpu, "Graphics", gpuOk ? MonitorFormat.Percent(gpu!.UtilizationPercent) : "Unavailable", gpuOk ? gpu!.AdapterName : "GPU counters not connected", gpuOk ? gpu!.UtilizationPercent : null,
            gpu?.Explanation ?? Reason(MonitorPage.Gpu), [], History(MonitorPage.Gpu)));
        var battery = _batteryReading;
        var batteryOk = battery?.State == BatteryReadingState.Available && MonitorFormat.ValidPercent(battery.Percent);
        var absent = _support.GetValueOrDefault(MonitorPage.Battery)?.ReasonCode == "battery.none";
        cards.Add(new(MonitorPage.Battery, "Battery", batteryOk ? MonitorFormat.Percent(battery!.Percent) : "Unavailable", batteryOk ? battery!.ChargingStatus.ToString() : "Power status", batteryOk ? battery!.Percent : null,
            battery?.Explanation ?? Reason(MonitorPage.Battery), batteryOk ? [new("State", battery!.ChargingStatus.ToString()),
                new("Remaining", battery.EstimatedRemaining is { } t ? $"{(int)t.TotalHours}h {t.Minutes}m" : "Not reported"),
                new("Health", MonitorFormat.ValidPercent(battery.HealthPercent) ? MonitorFormat.Percent(battery.HealthPercent) : "Not reported"),
                new("Charge rate", MonitorFormat.OptionalNumber(battery.ChargeRateMilliwatts / 1000, "W"))] : [], History(MonitorPage.Battery), Absent: absent));
        cards.Add(new(MonitorPage.Apps, "Top apps", _processReadings.Count > 0 ? "Memory footprint" : "Waiting", "Largest accessible processes", null,
            _processError ?? "Sorted by working set. Protected or exited processes may be omitted.",
            _processReadings.Select(p => new DetailRow(p.Name, MonitorFormat.Bytes(p.WorkingSetBytes))).ToArray(), []));
        return new(cards, _page, _theme, _accent, CreateControlCenter(), Style == IslandStyle.Notch && !CompactNotch);
    }
    private bool ExecuteDashboardCommand(int command)
    {
        if (command is >= 400 and <= 407)
        {
            InvalidateMedia();
            _fullDashboard = true;
            _page = (MonitorPage)(command - 400);
            if (UserHidden) SetHidden(false);
            if (State != IslandState.Expanded) SetState(IslandState.Expanded); else BeginTransition();
            return true;
        }
        if (command == DashboardLayout.ThemeCommand)
        { _theme = _theme == ThemeMode.Light ? ThemeMode.Dark : ThemeMode.Light; Draw(); return true; }
        return false;
    }
    private int _dashboardExerciseStep;
    public bool DashboardExerciseCompleted { get; private set; }
    private void ExerciseDashboard()
    {
        if (!_options.ExerciseDashboard || _suppressed) return;
        if (_dashboardExerciseStep > 0)
            CaptureFrame?.Invoke("Dashboard-" + _theme + "-" + _page);
        if (_dashboardExerciseStep == 16)
        {
            _page = _options.Page; _theme = _options.Theme ?? ThemeMode.Dark;
            Draw(); DashboardExerciseCompleted = true;
            _desktop.SetTimer(IslandTimer.DashboardExercise, null);
            return;
        }
        _theme = _dashboardExerciseStep < 8 ? ThemeMode.Dark : ThemeMode.Light;
        _page = (MonitorPage)(_dashboardExerciseStep % 8);
        _dashboardExerciseStep++;
        Draw();
    }}