using System.Diagnostics;
using Islet.Core.Capabilities;
using Islet.Core.Island;
using Islet.Core.Metrics;
using Islet.Core.Settings;

namespace Islet.Shell.Island;

/// <summary>Native presentation path. This type must never initialize WPF.</summary>
public sealed partial class NativeIslandHost : IDisposable
{
    private readonly IIslandDesktop _desktop;
    private readonly ICpuProvider _cpu;
    private readonly IRamProvider? _ram;
    private readonly INetworkProvider? _network;
    private readonly IBatteryProvider? _battery;
    private readonly Func<double> _now;
    private string? _secondaryLabel;
    private IInteractiveIslandDesktop? _interactive;
    private NativeIslandOptions _options = new() { Minimal = true };
    private IslandTransition _transition = new(new(220, 36));
    private readonly List<double?> _history = new(60);
    private readonly Dictionary<string, int> _statesObserved = new(StringComparer.Ordinal);
    private string _label = "CPU  measuring…", _explanation = "Measuring CPU activity.";
    private bool _closed, _ready, _suppressed, _lastPrimary, _lastEscape;
    private DesktopSuppression _environment = new(false, false, null);
    private PixelRect _renderBounds;
    private int _exerciseStep;
    private readonly HashSet<uint> _observedDpi = new();
    public IReadOnlyCollection<uint> ObservedDpi => _observedDpi;
    public event Action<string>? CaptureFrame;
    public CapabilityProbeResult? Capability { get; private set; }
    public NativeIslandStatus? LastObservation { get; private set; }
    public int SampleAttempts { get; private set; }
    public int AvailableSamples { get; private set; }
    public int VisibilityChecks { get; private set; }
    public int InvisibleChecks { get; private set; }
    public bool ForegroundEverIsland { get; private set; }
    public bool Stopped { get; private set; }
    public string LastLabel => _label;
    public IslandStyle Style { get; private set; }
    public IslandState State { get; private set; }
    public bool UserHidden { get; private set; }
    public bool Suppressed => _suppressed;
    public string? SuppressionReason => UserHidden ? "Hidden by you." : _environment.Reason;
    public int PointerChecks { get; private set; }
    public int AnimationFrames { get; private set; }
    public int SuppressionTransitions { get; private set; }
    public bool ExerciseCompleted { get; private set; }
    public IReadOnlyDictionary<string,int> StatesObserved => _statesObserved;
    public int HistoryCount => _history.Count;
    public RecoveryStatus? Recovery => _interactive?.Recovery;

    public NativeIslandHost(IIslandDesktop desktop, ICpuProvider cpu, Func<double>? monotonicMilliseconds = null)
        : this(desktop, cpu, null, null, null, monotonicMilliseconds) { }

    public NativeIslandHost(
        IIslandDesktop desktop,
        ICpuProvider cpu,
        IRamProvider? ram,
        INetworkProvider? network,
        IBatteryProvider? battery,
        Func<double>? monotonicMilliseconds = null,
        IDiskProvider? disk = null, IGpuProvider? gpu = null, IProcessProvider? processes = null, SettingsDocument? settings = null, Islet.Core.Activities.IMediaProvider? media = null)
    {
        _media = media;
        _desktop = desktop;
        _cpu = cpu;
        _ram = ram;
        _network = network;
        _battery = battery;
        _disk = disk; _gpu = gpu; _processes = processes;
        _theme = settings?.Theme == ThemeMode.Light ? ThemeMode.Light : ThemeMode.Dark;
        _accent = settings?.AccentColorHex ?? "#3B82F6";
        var clock = Stopwatch.StartNew();
        _now = monotonicMilliseconds ?? (() => clock.Elapsed.TotalMilliseconds);
    }

    public void Start(int exitAfterSeconds) => Start(new NativeIslandOptions { Minimal = true, ExitAfterSeconds = exitAfterSeconds });

    public void Start(NativeIslandOptions options)
    {
        _options = options;
        Style = options.Style; State = options.State; UserHidden = options.Hidden;
        _page = options.Page; if (options.Theme.HasValue) _theme = options.Theme.Value;
        _transition = new(DesiredSize());
        _desktop.Input += OnInput;
        _desktop.Create();
        if (!options.Minimal)
        {
            _interactive = _desktop as IInteractiveIslandDesktop
                ?? throw new InvalidOperationException("This desktop does not support the interactive island.");
            _interactive.EnableRecovery();
        }
        Capability = _cpu.Probe();
        ProbeDashboard();
        MediaCapability = _media is null ? CapabilityProbeResult.Unsupported("media.not-connected","Media controls are not connected in this build.")
            : SafeRead(_media.Probe, e => CapabilityProbeResult.Unsupported("media.probe-failed",e));
        if (Capability.Availability != CapabilityAvailability.Supported)
        { _label = "CPU  unavailable"; _explanation = Capability.Explanation!; }
        _ready = true;
        if (options.ExitAfterSeconds is { } duration) _desktop.SetTimer(IslandTimer.Exit, checked(duration * 1000));
        if (options.Minimal)
        {
            Draw(); _desktop.Show(true); Observe();
            _desktop.SetTimer(IslandTimer.Metric, 4000);
        }
        else
        {
            Reconcile(initial: true);
            if (options.Exercise) _desktop.SetTimer(IslandTimer.Exercise, 600);
            if (options.ExerciseDashboard) _desktop.SetTimer(IslandTimer.DashboardExercise, 1100);
        }
    }

    private void OnInput(IslandInput input)
    {
        if (_closed || !_ready) return;
        if (input.Kind == IslandInputKind.Close || input is { Kind: IslandInputKind.Timer, Value: (int)IslandTimer.Exit })
        { Stop(); return; }
        if (input.Kind == IslandInputKind.Timer)
        {
            switch ((IslandTimer)input.Value)
            {
                case IslandTimer.Metric: if (!_suppressed) Sample(); break;
                case IslandTimer.Pointer: if (!_suppressed) CheckPointer(); break;
                case IslandTimer.Animation:
                    if (!_suppressed) { AnimationFrames++; Draw(); if (!_transition.Active) _desktop.SetTimer(IslandTimer.Animation, null); }
                    break;
                case IslandTimer.Reconcile: Reconcile(); break;
                case IslandTimer.Exercise: Exercise(); break;
                case IslandTimer.DashboardExercise: ExerciseDashboard(); break;
            }
            return;
        }
        switch (input.Kind)
        {
            case IslandInputKind.Toggle:
                if (UserHidden) { UserHidden = false; State = IslandState.Expanded; _transition = new(DesiredSize()); Reconcile(initial: true); }
                else SetState(State == IslandState.Expanded ? IslandState.Collapsed : IslandState.Expanded);
                break;
            case IslandInputKind.LeftClick:
                if (State == IslandState.Expanded && !_transition.Active)
                    ExecuteCommand(Islet.Core.Presentation.DashboardLayout.HitTest(input.X / _displayScale, input.Y / _displayScale,
                        _renderBounds.Width / _displayScale, _renderBounds.Height / _displayScale, CreateDashboard()));
                else SetState(IslandState.Expanded);
                break;
            case IslandInputKind.ContextMenu: OpenMenu(); break;
            case IslandInputKind.Command: ExecuteCommand(input.Value); break;
            case IslandInputKind.DisplayChanged:
            case IslandInputKind.DpiChanged:
                Reconcile();
                if (!_suppressed) { _transition.Finish(); Draw(); }
                break;
            case IslandInputKind.PowerChanged: Reconcile(); break;
        }
    }

    private void Sample()
    {
        SampleAttempts++;
        var reading = _cpu.Sample();
        bool changed;
        if (reading.State == CpuReadingState.Available && reading.Percent is { } value && double.IsFinite(value) && value is >= 0 and <= 100)
        {
            AvailableSamples++;
            AddHistory(value);
            var label = $"CPU  {value:0}%";
            changed = _label != label;
            _label = label;
            _explanation = value < 20 ? "Your processor is handling a light workload."
                : value < 75 ? "Your processor is working on your apps." : "Your processor is busy right now.";
        }
        else
        {
            AddHistory(null);
            var label = reading.State == CpuReadingState.WarmingUp ? "CPU  measuring…" : "CPU  unavailable";
            changed = _label != label;
            _label = label; _explanation = reading.Explanation;
        }

        if (State == IslandState.Expanded) { SampleDashboard(); PollMedia(); }
        else _secondaryLabel = null;
        if (changed || State != IslandState.Collapsed) Draw();
        Observe();
    }

    private void AddHistory(double? value)
    {
        if (_history.Count == 60) _history.RemoveAt(0);
        _history.Add(value);
    }

    private void Reconcile(bool initial = false)
    {
        if (_interactive is null) return;
        _environment = UserHidden ? new(false, false, null) : _interactive.ReadSuppression();
        var suppress = UserHidden || _environment.Suppressed;
        _interactive.ObserveForeground(!UserHidden && !_environment.PowerInactive);
        _desktop.SetTimer(IslandTimer.Reconcile, UserHidden || _environment.PowerInactive ? null : 1000);
        if (!initial && suppress == _suppressed) return;
        _suppressed = suppress;
        SuppressionTransitions++;
        if (suppress)
        {
            InvalidateMedia();
            _transition.Finish();
            _desktop.SetTimer(IslandTimer.Metric, null);
            _desktop.SetTimer(IslandTimer.Pointer, null);
            _desktop.SetTimer(IslandTimer.Animation, null);
            _desktop.Show(false);
        }
        else
        {
            _cpu.Reset();
            ResetDashboard();
            _label = Capability?.Availability == CapabilityAvailability.Supported ? "CPU  measuring…" : "CPU  unavailable";
            _history.Clear();
            _transition.Finish();
            _interactive.SetClickThrough(State == IslandState.Collapsed);
            Draw();
            _desktop.Show(true);
            ConfigureVisibleTimers();
        }
        Observe();
    }

    private void ConfigureVisibleTimers()
    {
        _desktop.SetTimer(IslandTimer.Metric, State == IslandState.Expanded ? 1000 : State == IslandState.Hover ? 2000 : 4000);
        _desktop.SetTimer(IslandTimer.Pointer, 100);
    }

    private void CheckPointer()
    {
        if (_interactive is null) return;
        PointerChecks++;
        if (MediaVisible && (_mediaRead is { IsCompleted: true } || _mediaCommand is { IsCompleted: true })) { PollMedia(); Draw(); }
        var pointer = _interactive.ReadPointer();
        if (!pointer.Available) return;
        var x = pointer.X - _renderBounds.X;
        var y = pointer.Y - _renderBounds.Y;
        var edge = IslandGeometry.LeftEdge(y + 0.5, _renderBounds.Width, _renderBounds.Height,
            Math.Min(_renderBounds.Height / 2.0, 20 * _desktop.ReadDisplay().Dpi / 96.0), Style);
        var inside = y >= 0 && y < _renderBounds.Height && x >= edge && x < _renderBounds.Width - edge;
        var primaryPressed = pointer.PrimaryDown && !_lastPrimary;
        var escapePressed = pointer.EscapeDown && !_lastEscape;
        _lastPrimary = pointer.PrimaryDown; _lastEscape = pointer.EscapeDown;
        if ((_options.Exercise && !ExerciseCompleted) || _options.ExerciseDashboard) return; // deterministic diagnostic driver; real pointer reads still occur.
        if (State == IslandState.Collapsed && inside) SetState(IslandState.Hover);
        else if (State == IslandState.Hover && !inside) SetState(IslandState.Collapsed);
        else if (State == IslandState.Expanded && (escapePressed || (!inside && primaryPressed))) SetState(IslandState.Collapsed);
    }

    public void SetState(IslandState state)
    {
        if (_closed || State == state) return;
        if (State != IslandState.Expanded && state == IslandState.Expanded) ResetDashboard();
        if (state != IslandState.Expanded) InvalidateMedia();
        State = state;
        BeginTransition();
    }

    public void SetStyle(IslandStyle style)
    {
        if (_closed || Style == style) return;
        InvalidateMedia();
        if (style == IslandStyle.Notch) { _fullDashboard = false; _page = Islet.Core.Presentation.MonitorPage.Overview; }
        Style = style;
        BeginTransition();
    }

    public void SetHidden(bool hidden)
    {
        if (_closed || UserHidden == hidden) return;
        UserHidden = hidden; Reconcile();
    }

    private void BeginTransition()
    {
        _transition.Begin(DesiredSize(), _now(), _interactive?.Recovery.ReducedMotion == true);
        if (_suppressed) { _transition.Finish(); return; }
        _interactive?.SetClickThrough(State == IslandState.Collapsed);
        ConfigureVisibleTimers();
        Draw();
        _desktop.SetTimer(IslandTimer.Animation, _transition.Active ? 16 : null);
    }

    private void Draw()
    {
        if (_suppressed || _closed) return;
        (_media as Islet.Core.Activities.IMediaVisibilityAware)?.SetVisible(MediaVisible);
        var display = _desktop.ReadDisplay();
        var size = _transition.At(_now());
        _displayScale = display.Dpi / 96.0;
        var placementSize = size;
        if (State == IslandState.Expanded)
        {
            _displayScale = Islet.Core.Presentation.DashboardLayout.FitScale(size.Width, size.Height, _displayScale,
                Math.Min(display.Bounds.Width, display.WorkArea.Width),
                Math.Max(1, Math.Min(display.Bounds.Height, display.WorkArea.Y + display.WorkArea.Height - display.Bounds.Y) - (Style == IslandStyle.Island ? (int)Math.Ceiling(8 * _displayScale) : 0)));
            var factor = _displayScale / (display.Dpi / 96.0);
            placementSize = new(size.Width * factor, size.Height * factor);
        }
        _renderBounds = IslandGeometry.Place(display, placementSize.Width, placementSize.Height, Style);
        _desktop.Render(new(Style, State, _renderBounds, _displayScale, _label,
            _explanation, _history, _interactive?.Recovery.HighContrast == true, _secondaryLabel, CreateDashboard()));
    }

    private void Observe()
    {
        LastObservation = _desktop.Observe();
        VisibilityChecks++;
        if (LastObservation.Dpi > 0) _observedDpi.Add(LastObservation.Dpi);
        if (!LastObservation.Visible) InvisibleChecks++;
        ForegroundEverIsland |= LastObservation.ForegroundIsIsland;
        var key = _suppressed ? "Suppressed" : Style + "/" + State;
        _statesObserved[key] = _statesObserved.GetValueOrDefault(key) + 1;
    }

    private void OpenMenu()
    {
        if (_interactive is null) return;
        var items = new List<IslandMenuItem> {
            new(100, "Open monitor"), new(101, "Collapse"), new(102, UserHidden ? "Show island" : "Hide island"), new(0, "") };
        foreach (var style in Enum.GetValues<IslandStyle>()) items.Add(new(200 + (int)style, style.ToString(), style == Style));
        items.Add(new(0, ""));
        var displays = _interactive.ReadDisplays();
        for (var index = 0; index < displays.Count; index++)
            items.Add(new(300 + index, "Display " + (index + 1), displays[index].Id == _desktop.ReadDisplay().Id));
        if (!_interactive.Recovery.HotkeyAvailable)
            items.Add(new(900, "Ctrl+Alt+I unavailable — use this menu", false, false));
        items.Add(new(0, ""));
        for (var page = 0; page < Islet.Core.Presentation.DashboardLayout.PageNames.Length; page++)
            items.Add(new(400 + page, Islet.Core.Presentation.DashboardLayout.PageNames[page], (int)_page == page));
        items.Add(new(450, "Switch light / dark theme"));
        items.Add(new(0, "")); items.Add(new(999, "Exit Tidlet Monitor"));
        var command = _interactive.OpenMenu(items);
        ExecuteCommand(command);
    }

    private void ExecuteCommand(int command)
    {
        if (ExecuteControlCenterCommand(command) || ExecuteDashboardCommand(command)) return;
        switch (command)
        {
            case 100: if (UserHidden) SetHidden(false); SetState(IslandState.Expanded); break;
            case 101: SetState(IslandState.Collapsed); break;
            case 102: SetHidden(!UserHidden); break;
            case 999: Stop(); break;
            case >= 200 and <= 202: SetStyle((IslandStyle)(command - 200)); break;
            case >= 300 and < 400:
                var displays = _interactive?.ReadDisplays();
                var index = command - 300;
                if (displays is not null && index < displays.Count)
                {
                    _interactive!.SelectDisplay(displays[index].Id);
                    _transition.Finish();
                    Draw(); Observe();
                }
                break;
        }
    }

    private void Exercise()
    {
        if (!_options.Exercise || _interactive is null) return;
        if (!_suppressed && !_transition.Active)
        {
            Sample();
            var key = Style + "-" + State;
            CaptureFrame?.Invoke(key);
            Observe();
        }
        var step = _exerciseStep++;
        if (step < 9)
        {
            SetStyle((IslandStyle)(step / 3));
            SetState((IslandState)(step % 3));
        }
        else if (step == 9) SetHidden(true);
        else if (step == 10) SetHidden(false);
        else if (step is 11 or 12)
        {
            var displays = _interactive.ReadDisplays();
            if (displays.Count > 0)
            {
                _interactive.SelectDisplay(displays[step == 11 && displays.Count > 1 ? 1 : 0].Id);
                _transition.Finish(); Draw(); Observe();
            }
        }
        else if (step < 23) SetState(step % 2 == 0 ? IslandState.Collapsed : IslandState.Expanded);
        else
        {
            SetStyle(_options.Style); SetState(_options.State);
            ExerciseCompleted = true;
            _desktop.SetTimer(IslandTimer.Exercise, null);
        }
    }

    public void Stop()
    {
        if (_closed) return;
        Observe();
        InvalidateMedia();
        _closed = true;
        _interactive?.ObserveForeground(false);
        foreach (var timer in Enum.GetValues<IslandTimer>()) _desktop.SetTimer(timer, null);
        _desktop.Close();
        Stopped = true;
    }

    public void Dispose()
    {
        InvalidateMedia();
        _desktop.Input -= OnInput;
        _interactive?.ObserveForeground(false);
        foreach (var timer in Enum.GetValues<IslandTimer>()) _desktop.SetTimer(timer, null);
    }
}
