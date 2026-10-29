using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Islet.Core.Capabilities;
using Islet.Core.Display;
using Islet.Core.Metrics;

namespace Islet.Shell.Island;

/// <summary>Opt-in feasibility host. Not the completed M1 shell.</summary>
public sealed class IslandProbeWindow : Window
{
    private readonly ICpuProvider _cpu;
    private readonly IIslandProbePlatform _platform;
    private readonly PillSurface _surface = new();
    private readonly DispatcherTimer _timer;
    private bool _busy;
    private bool _closed;
    private bool _probed;
    private int _generation;
    private nint _handle;

    public IslandProbeWindow(ICpuProvider cpu, IIslandProbePlatform platform)
    {
        _cpu = cpu;
        _platform = platform;
        Title = "Tidlet Monitor · M1 feasibility probe";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Width = 220;
        Height = 36;
        Left = 0;
        Top = 0;
        Content = _surface;
        AutomationProperties.SetName(this, "Tidlet Monitor CPU feasibility probe");
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick += OnTick;
        SourceInitialized += OnSourceInitialized;
        IsVisibleChanged += OnVisibilityChanged;
        Closed += OnClosed;
    }

    public CapabilityProbeResult? Capability { get; private set; }
    public IslandProbeObservation? LastObservation { get; private set; }
    public int SampleAttempts { get; private set; }
    public int AvailableSamples { get; private set; }
    public int RenderCount => _surface.RenderCount;
    public int VisibilityChecks { get; private set; }
    public int InvisibleChecks { get; private set; }
    public bool ForegroundEverIsland { get; private set; }
    public string? Failure { get; private set; }
    public string LastLabel => _surface.Label;
    public bool Polling => _timer.IsEnabled;
    public event EventHandler? ProbeFailed;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        try { _platform.Configure(_handle, Width, Height, 8); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            Fail(ex.Message);
        }
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _generation++;
        if (!IsVisible || Failure is not null)
        {
            _timer.Stop();
            // The in-flight worker owns provider state. Reset there on the next visible generation.
            _probed = false;
            return;
        }
        _timer.Start();
        _ = RefreshAsync();
    }

    private void OnTick(object? sender, EventArgs e) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_busy || _closed || !IsVisible || Failure is not null) return;
        _busy = true;
        var generation = _generation;
        var probe = !_probed;
        var previousCapability = Capability;
        var result = await Task.Run(() =>
        {
            try
            {
                var capability = probe ? _cpu.Probe() : previousCapability;
                return (Capability: capability, Reading: _cpu.Sample(), Error: (string?)null);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ExternalException
                or DllNotFoundException or EntryPointNotFoundException)
            {
                return (Capability: previousCapability,
                    Reading: CpuReading.Missing(CpuReadingState.Unavailable, ex.Message), Error: ex.Message);
            }
        }).ConfigureAwait(false);
        // Native window callbacks can lack a synchronization context, including in STA tests.
        // Always publish through this window's dispatcher.
        try
        {
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (_closed || !IsVisible || generation != _generation) return;
                    if (result.Error is not null) { Fail(result.Error); return; }
                    Capability = result.Capability;
                    _probed = true;
                    SampleAttempts++;
                    LastObservation = _platform.Observe(_handle);
                    VisibilityChecks++;
                    if (!LastObservation.Visible) InvisibleChecks++;
                    ForegroundEverIsland |= LastObservation.ForegroundIsIsland;
                    if (!LastObservation.RequiredStylesPresent || !LastObservation.Layered)
                    {
                        Fail("Windows did not retain the required transparent island styles.");
                        return;
                    }
                    if (result.Reading.Percent is { } value && result.Reading.State == CpuReadingState.Available)
                    {
                        AvailableSamples++;
                        _surface.SetLabel($"CPU  {value:0}%", result.Reading.Explanation);
                    }
                    else
                    {
                        var text = result.Reading.State == CpuReadingState.WarmingUp ? "CPU  measuring…" : "CPU  unavailable";
                        _surface.SetLabel(text, result.Reading.Explanation);
                    }
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ExternalException
                    or DllNotFoundException or EntryPointNotFoundException) { Fail(ex.Message); }
                finally
                {
                    _busy = false;
                    if (!_closed && IsVisible && generation != _generation) _ = RefreshAsync();
                }
            });
        }
        catch (TaskCanceledException) when (Dispatcher.HasShutdownStarted) { }
    }

    private void Fail(string reason)
    {
        Failure = reason;
        _timer.Stop();
        ProbeFailed?.Invoke(this, EventArgs.Empty);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _generation++;
        _timer.Stop();
        _timer.Tick -= OnTick;
        SourceInitialized -= OnSourceInitialized;
        IsVisibleChanged -= OnVisibilityChanged;
        Closed -= OnClosed;
    }

    private sealed class PillSurface : FrameworkElement
    {
        private static readonly Brush Fill = Frozen(Color.FromArgb(245, 19, 23, 32));
        private static readonly Brush Accent = Frozen(Color.FromRgb(111, 226, 190));
        private static readonly Typeface Face = new("Segoe UI");
        public string Label { get; private set; } = "CPU  measuring…";
        public int RenderCount { get; private set; }

        public void SetLabel(string label, string explanation)
        {
            AutomationProperties.SetHelpText(this, explanation);
            if (label == Label) return;
            Label = label;
            AutomationProperties.SetName(this, label);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            RenderCount++;
            dc.DrawRoundedRectangle(Fill, null, new Rect(RenderSize), RenderSize.Height / 2, RenderSize.Height / 2);
            dc.DrawEllipse(Accent, null, new Point(24, RenderSize.Height / 2), 3, 3);
            var text = new FormattedText(Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                Face, 14, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point(40, (RenderSize.Height - text.Height) / 2));
        }

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
