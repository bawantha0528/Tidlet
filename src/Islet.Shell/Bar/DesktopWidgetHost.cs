using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Islet.Core.Bar;
using Islet.Core.Popups;

namespace Islet.Shell.Bar;

public sealed class DesktopWidgetHost(
    string path, Func<BarSettings> settings, Func<IPopupSource> create, RecentSampleCache recent,
    Func<IReadOnlyList<BarDisplay>> displays, Func<nint,BarRect> bounds,
    Action<nint,BarRect,bool> position, Func<UsageSnapshot>? usage = null, Action<UsagePeriod>? selectUsage = null) : IDisposable
{
    private const string UsageName = "Data usage";
    private sealed record Placement(string Name, int X, int Y);
    private readonly Dictionary<string, CardWindow> _windows = [];
    private readonly Dictionary<BarModule, PopupSample> _samples = [];
    private readonly HashSet<BarModule> _previous = [];
    private DashboardWidgetReader? _reader;
    private BarModule[] _active = [];
    private Task _cleanup = Task.CompletedTask;
    private bool _hidden;
    private bool _disposed;
    public int Count => _windows.Count;
    public int Frames { get; private set; }
    public bool Drain(TimeSpan timeout) => _cleanup.Wait(timeout);

    public void Restore()
    {
        if (!File.Exists(path)) return;
        try
        {
            if (new FileInfo(path).Length > 16_384) return;
            var placements = JsonSerializer.Deserialize<Placement[]>(File.ReadAllText(path)) ?? [];
            foreach (var entry in placements.Take(DashboardWidgetReader.Modules.Length + 2))
            {
                if (entry is null || entry.Name is null || _windows.ContainsKey(entry.Name)) continue;
                if (entry.Name == "Combined" || entry.Name == UsageName && usage is not null ||
                    DashboardWidgetReader.Modules.Any(module => WidgetCardRenderer.WidgetName(module) == entry.Name))
                    Open(entry.Name, entry.X, entry.Y);
            }
            Refresh();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or Win32Exception)
        {
            Refresh();
        }
    }

    public string Pin(BarModule? module)
    {
        if (module is { } item && !DashboardWidgetReader.Modules.Contains(item)) return "That widget is unavailable.";
        var name = module is { } value ? WidgetCardRenderer.WidgetName(value) : "Combined";
        return PinNamed(name);
    }

    public string PinUsage() => usage is null ? "Data usage is unavailable." : PinNamed(UsageName);

    private string PinNamed(string name)
    {
        try
        {
            if (_windows.TryGetValue(name, out var existing))
            {
                existing.Place(bounds(existing.Handle), !_hidden);
                return $"{name} is already on the desktop.";
            }
            var display = displays().FirstOrDefault(monitor => monitor.Primary);
            if (display is null) return "Windows did not report a desktop display.";
            Open(name, display.Work.Left + 20 + _windows.Count % 3 * 240, display.Work.Top + 48 + _windows.Count / 3 * 210);
            Refresh();
            return Save() ? $"{name} added to desktop. Drag its title to move it." : $"{name} added. Its position could not be saved.";
        }
        catch (Win32Exception)
        {
            return $"{name} could not open on the desktop.";
        }
    }

    public void SetHidden(bool hidden)
    {
        if (_hidden == hidden) return;
        _hidden = hidden;
        foreach (var window in _windows.Values) window.Place(bounds(window.Handle), !hidden);
        Refresh();
    }

    public void Refresh()
    {
        if (_disposed) return;
        var state = settings();
        var requested = _hidden || state.Pause || _windows.Count == 0 ? [] : DashboardWidgetReader.Modules
            .Where(module => _windows.ContainsKey(WidgetCardRenderer.WidgetName(module)) ||
                DashboardWidgetReader.CombinedModules.Contains(module) && _windows.ContainsKey("Combined")).ToArray();
        if (!_active.SequenceEqual(requested))
        {
            StopReader();
            _active = requested;
            if (requested.Length > 0)
            {
                foreach (var module in requested)
                    if (recent.TryGet(module, out var previous))
                    {
                        _samples[module] = previous!;
                        _previous.Add(module);
                    }
                _reader = new(create, _cleanup, samples =>
                {
                    Frames++;
                    for (var i = 0; i < samples.Length; i++)
                    {
                        var module = requested[i];
                        if (samples[i].WarmingUp && _previous.Contains(module) && recent.TryGet(module, out _)) continue;
                        _samples[module] = samples[i];
                        _previous.Remove(module);
                        recent.Store(module, samples[i]);
                    }
                    Draw();
                }, requested);
            }
        }
        Draw();
    }

    private void Open(string name, int x, int y)
    {
        if (displays().Count == 0) return;
        BarModule? module = name is "Combined" or UsageName ? null : DashboardWidgetReader.Modules.First(item => WidgetCardRenderer.WidgetName(item) == name);
        var window = new CardWindow(name, module, x, y, bounds, position, Clamp, () => Save(), () => Remove(name),
            name == UsageName ? usage : null, selectUsage);
        _windows.Add(name, window);
        try { window.Place(Clamp(bounds(window.Handle)), !_hidden); }
        catch { _windows.Remove(name); window.Dispose(); throw; }
    }

    private BarRect Clamp(BarRect rect)
    {
        var available = displays();
        if (available.Count == 0) return rect;
        var centerX = rect.Left + rect.Width / 2;
        var centerY = rect.Top + rect.Height / 2;
        var screen = available.FirstOrDefault(item => centerX >= item.Bounds.Left && centerX < item.Bounds.Right &&
            centerY >= item.Bounds.Top && centerY < item.Bounds.Bottom)
            ?? available.FirstOrDefault(item => item.Primary) ?? available[0];
        var work = screen.Work;
        var x = Math.Clamp(rect.Left, work.Left, Math.Max(work.Left, work.Right - rect.Width));
        var y = Math.Clamp(rect.Top, work.Top, Math.Max(work.Top, work.Bottom - rect.Height));
        return new(x, y, x + rect.Width, y + rect.Height);
    }

    private bool Save()
    {
        try
        {
            var values = _windows.Select(pair =>
            {
                var rect = bounds(pair.Value.Handle);
                return new Placement(pair.Key, rect.Left, rect.Top);
            }).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(values));
            File.Move(path + ".tmp", path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception) { return false; }
    }

    private void Remove(string name)
    {
        if (_windows.Remove(name, out var window)) window.Dispose();
        Save();
        Refresh();
    }

    private void Draw()
    {
        var state = settings();
        var palette = DashboardPalette.For(state.Theme);
        foreach (var window in _windows.Values)
            window.Draw(palette, state, _samples, _previous);
    }

    public void RefreshUsage()
    {
        if (_hidden || !_windows.TryGetValue(UsageName, out var window)) return;
        var state = settings();
        window.Draw(DashboardPalette.For(state.Theme), state, _samples, _previous);
    }

    private void StopReader()
    {
        if (_reader is null) return;
        _reader.Dispose();
        _cleanup = _reader.Cleanup;
        _reader = null;
        _samples.Clear();
        _previous.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopReader();
        foreach (var window in _windows.Values) window.Dispose();
        _windows.Clear();
    }

    private sealed class CardWindow : IDisposable
    {
        private const int Width = 218, Height = 192;
        private readonly HwndSource _source;
        private readonly CardVisual _visual;
        private readonly BarModule? _module;
        private readonly Action<nint,BarRect,bool> _position;
        private readonly Func<nint,BarRect> _bounds;
        private readonly Func<BarRect,BarRect> _clamp;
        private readonly Action _moved;
        private readonly Action _remove;
        private readonly Func<UsageSnapshot>? _usage;
        private bool _visible;
        public nint Handle => _source.Handle;

        public CardWindow(string name, BarModule? module, int x, int y, Func<nint,BarRect> bounds,
            Action<nint,BarRect,bool> position, Func<BarRect,BarRect> clamp, Action moved, Action remove,
            Func<UsageSnapshot>? usage, Action<UsagePeriod>? selectUsage)
        {
            _module = module;
            _bounds = bounds;
            _position = position;
            _clamp = clamp;
            _moved = moved;
            _remove = remove;
            _usage = usage;
            _source = new(new HwndSourceParameters("Tidlet " + name + " widget")
            {
                PositionX = x, PositionY = y, Width = Width, Height = Height,
                WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x80
            });
            _visual = new();
            _source.RootVisual = _visual;
            _source.AddHook(Message);
            _visual.MouseLeftButtonUp += (_, e) =>
            {
                var point = e.GetPosition(_visual);
                if (point.X >= Width - 26 && point.Y <= 30) { _remove(); return; }
                if (_usage is null || selectUsage is null) return;
                foreach (var period in Enum.GetValues<UsagePeriod>())
                    if (UsageCard.Option(CardBounds, period).Contains(point))
                    {
                        selectUsage(period);
                        return;
                    }
            };
            _visual.Measure(new(Width, Height));
            _visual.Arrange(new Rect(0, 0, Width, Height));
            var scale = _source.CompositionTarget.TransformToDevice.M11;
            try { Place(_clamp(new(x, y, x + (int)Math.Round(Width * scale), y + (int)Math.Round(Height * scale))), false); }
            catch { _source.Dispose(); throw; }
        }

        public void Place(BarRect rect, bool show)
        {
            _visible = show;
            _position(Handle, rect, show);
        }

        private nint Message(nint hwnd, int message, nint wp, nint lp, ref bool handled)
        {
            if (message == 0x0010)
            {
                handled = true;
                _remove();
            }
            if (message == 0x0084)
            {
                var rect = _bounds(hwnd);
                var point = _source.CompositionTarget.TransformFromDevice.Transform(new Point(
                    (short)(lp.ToInt64() & 0xffff) - rect.Left, (short)((lp.ToInt64() >> 16) & 0xffff) - rect.Top));
                handled = true;
                return point.Y < 30 && point.X < Width - 26 ? 2 : 1;
            }
            if (message == 0x0232)
            {
                Place(_clamp(_bounds(hwnd)), _visible);
                _moved();
            }
            return 0;
        }

        public void Draw(DashboardPalette palette, BarSettings settings, IReadOnlyDictionary<BarModule, PopupSample> samples, IReadOnlySet<BarModule> previous)
        {
            _source.CompositionTarget.BackgroundColor = palette.BaseColor;
            var scale = _source.CompositionTarget.TransformToDevice.M11;
            using var drawing = _visual.Drawing.RenderOpen();
            if (_usage is null)
                new WidgetCardRenderer(palette, palette.Accent, scale, settings.UseBits, samples, previous, settings.ThresholdPercent, settings.Fahrenheit)
                    .Draw(drawing, CardBounds, _module, settings.Pause, 22);
            else UsageCard.Draw(drawing, CardBounds, palette, scale, _usage());
            var label = MonitorText.Format("×", new Typeface("Segoe UI"), 15, palette.Muted, scale);
            drawing.DrawText(label, MonitorText.Centered(label, new Point(Width - 14, 17), scale));
        }

        public void Dispose() => _source.Dispose();
        private static Rect CardBounds => new(1, 1, Width - 2, Height - 2);

        private sealed class CardVisual : UIElement
        {
            internal DrawingVisual Drawing { get; } = new();
            public CardVisual() => AddVisualChild(Drawing);
            protected override int VisualChildrenCount => 1;
            protected override Visual GetVisualChild(int index) => index == 0 ? Drawing : throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
