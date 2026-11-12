using System.Windows;
using System.Windows.Media;
using Islet.Core.Bar;
using Islet.Core.Metrics;
using Islet.Core.Popups;

namespace Islet.Shell.Bar;

internal sealed class WidgetCardRenderer(DashboardPalette palette, Brush accent, double scale, bool useBits,
    IReadOnlyDictionary<BarModule, PopupSample> samples, IReadOnlySet<BarModule> previous, int thresholdPercent, bool fahrenheit)
{
    private static readonly Typeface Face = new("Segoe UI Variable, Segoe UI");
    private static readonly Typeface Bold = new(Face.FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private readonly DashboardPalette _palette = palette;
    private readonly Brush _blue = accent;
    private readonly double _scale = scale;
    private readonly bool _useBits = useBits;
    private readonly IReadOnlyDictionary<BarModule, PopupSample> _samples = samples;
    private readonly IReadOnlySet<BarModule> _previous = previous;
    private Brush _text => _palette.Text;
    private Brush _dim => _palette.Muted;

    internal void Draw(DrawingContext drawing, Rect card, BarModule? module, bool paused, double titleInset = 0)
    {
        drawing.DrawRoundedRectangle(_palette.Card, _palette.CardBorder, card, 15, 15);
        if (module is not { } item)
        {
            if (!paused && DashboardWidgetReader.CombinedModules.All(module => !_samples.ContainsKey(module)))
                LoadingWidget(drawing, card, "Combined", "Waiting for system readings…");
            else CombinedWidget(drawing, card);
            return;
        }
        var sample = _samples.GetValueOrDefault(item);
        if (!paused && (sample is null || sample.WarmingUp && !WidgetAvailable(item, sample)) && !_previous.Contains(item))
        {
            LoadingWidget(drawing, card, WidgetName(item), MetricLoading.For(item).Title + "…");
            return;
        }
        WidgetTitle(drawing, WidgetName(item), card);
        if (item == BarModule.Network) NetworkWidget(drawing, card, sample?.Network);
        else GaugeWidget(drawing, card, item, sample);
        var status = paused ? "Paused" : _previous.Contains(item) ? "Previous reading"
            : sample is not null && WidgetAvailable(item, sample) ? null
            : sample is null || sample.WarmingUp ? "Measuring…" : "Unavailable";
        if (status is not null)
            Label(drawing, status, card.Left + card.Width * .42, card.Top + 12, 9, _dim, width: card.Width * .58 - 12 - titleInset, right: true);
    }

    private void Label(DrawingContext drawing, string value, double x, double y, double size, Brush brush,
        bool bold = false, double? width = null, bool center = false, bool right = false, int maxLines = 2)
    {
        var text = MonitorText.Format(value, bold ? Bold : Face, size, brush, _scale);
        if (width is { } available)
        {
            text.MaxTextWidth = Math.Max(1, available);
            text.MaxLineCount = maxLines;
            text.Trimming = TextTrimming.CharacterEllipsis;
            text.TextAlignment = center ? TextAlignment.Center : right ? TextAlignment.Right : TextAlignment.Left;
        }
        drawing.DrawText(text, MonitorText.PixelAligned(x, y, _scale));
    }

    private void LoadingWidget(DrawingContext drawing, Rect card, string title, string status)
    {
        WidgetTitle(drawing, title, card);
        Label(drawing, status, card.Left + 12, card.Top + 67, 12, _dim, width: card.Width - 24, center: true);
        WorkProgress.Draw(drawing, new Rect(card.Left + 20, card.Top + 108, card.Width - 40, 4), _palette.Metrics.Track, _blue);
    }

    private void WidgetTitle(DrawingContext drawing, string title, Rect card) =>
        Label(drawing, title, card.Left + 12, card.Top + 10, 12, _text, true, card.Width - 24);

    private void GaugeWidget(DrawingContext drawing, Rect card, BarModule module, PopupSample? sample)
    {
        var center = new Point(card.Left + card.Width / 2, card.Top + 76);
        var system = module == BarModule.Cpu ? ValidWidgetPercent(sample?.System) : null;
        var charge = WidgetPercent(module, sample);
        var level = MeterLevel(module, charge);
        var accent = _palette.Metrics.Status(level, module == BarModule.Battery ? _palette.Metrics.BatteryFill : _blue);
        var first = level == ThresholdLevel.Normal ? system : null;
        WidgetRing(drawing, center, 34, charge, first, accent);
        var row = card.Top + 119;
        switch (module)
        {
            case BarModule.Cpu:
                WidgetRow(drawing, card, "System", WidgetPercentText(system), row, _palette.Metrics.Cyan);
                WidgetRow(drawing, card, "User", WidgetPercentText(ValidWidgetPercent(sample?.User)), row + 21, _blue);
                TemperatureRow(drawing, card, "CPU temp · "+TemperatureAge(sample?.CpuTemperatureSampledAt), sample?.LastCpuPackageCelsius, row + 42);
                break;
            case BarModule.Gpu:
                var adapter = BusiestGpu(sample);
                WidgetRow(drawing, card, "Peak usage", WidgetPercentText(ValidWidgetPercent(adapter?.Usage)), row, _blue);
                if (adapter is not null) Label(drawing, adapter.Name, card.Left + 12, row + 22, 10, _dim, width: card.Width - 24, maxLines: 1);
                TemperatureRow(drawing, card, "Peak GPU temp", PeakGpuTemperature(sample), row + 42);
                break;
            case BarModule.Ram:
                if (sample?.Memory is { Valid: true } memory)
                {
                    WidgetRow(drawing, card, "Used", WidgetBytes(memory.Total - memory.Available), row, _blue);
                    WidgetRow(drawing, card, "Available", WidgetBytes(memory.Available), row + 21, _palette.Metrics.Track);
                    WidgetRow(drawing, card, "Pressure", memory.PressureName, row + 42);
                }
                break;
            case BarModule.Disk:
                if (WidgetVolume(sample) is { } volume)
                {
                    WidgetRow(drawing, card, "Used", WidgetBytes(volume.Total - volume.Free), row, _blue);
                    WidgetRow(drawing, card, "Free", WidgetBytes(volume.Free), row + 21, _palette.Metrics.Track);
                    if (!_previous.Contains(module)) WidgetRow(drawing, card, "Volume", volume.Name, row + 42);
                }
                break;
            case BarModule.Battery:
                if (sample?.Battery is { } battery && charge.HasValue)
                {
                    var reading = battery.Reading;
                    var status = reading.ChargingStatus == BatteryChargingStatus.Unknown
                        ? battery.OnAc switch { true => "Plugged in", false => "On battery", _ => null }
                        : reading.ChargingStatus.ToString();
                    WidgetRow(drawing, card, "Status", status, row, accent);
                    WidgetRow(drawing, card, "Health", WidgetPercentText(ValidWidgetPercent(reading.HealthPercent)), row + 21, _palette.Metrics.Status(BarThresholds.BatteryHealthLevel(reading.HealthPercent), _palette.Metrics.Green));
                    if (reading.ChargeRateMilliwatts is { } power && double.IsFinite(power))
                        WidgetRow(drawing, card, "Power", $"{Math.Abs(power) / 1000:0.0} W", row + 39);
                    TemperatureRow(drawing, card, "Battery temp", battery.TemperatureCelsius, row + 55, thermalWarning: false);
                }
                else if (sample?.Battery?.Reading is { } missing)
                    Label(drawing, missing.Explanation, card.Left + 12, row, 10, _dim, width: card.Width - 24);
                break;
        }
    }

    private void NetworkWidget(DrawingContext drawing, Rect card, NetworkDetails? sample)
    {
        var half = (card.Width - 24) / 2;
        var x = card.Left + 12;
        var bits = _useBits;
        Label(drawing, MetricFormat.TransferRate(sample?.Download, bits, "—"), x, card.Top + 49, 15, _blue, true, half, center: true);
        Label(drawing, MetricFormat.TransferRate(sample?.Upload, bits, "—"), x + half, card.Top + 49, 15, _palette.Metrics.Red, true, half, center: true);
        Label(drawing, "Download", x, card.Top + 77, 10, _dim, width: half, center: true);
        Label(drawing, "Upload", x + half, card.Top + 77, 10, _dim, width: half, center: true);
        if (sample?.Adapters is not { Length: > 0 } adapters) return;
        WidgetRow(drawing, card, "Interface", adapters.Length == 1 ? adapters[0].Name : $"{adapters.Length} adapters", card.Top + 117);
        if (adapters.Length == 1) WidgetRow(drawing, card, "Local IP", adapters[0].Address, card.Top + 138);
    }

    private void CombinedWidget(DrawingContext drawing, Rect card)
    {
        WidgetTitle(drawing, "Combined", card);
        for (var index = 0; index < DashboardWidgetReader.CombinedModules.Length; index++)
        {
            var module = DashboardWidgetReader.CombinedModules[index];
            var x = card.Left + card.Width * (index % 2 == 0 ? .26 : .74);
            var y = card.Top + 69 + index / 2 * 72;
            var center = new Point(x, y);
            var percent = WidgetPercent(module, _samples.GetValueOrDefault(module));
            WidgetRing(drawing, center, Math.Min(26, card.Width * .18), percent, accent: _palette.Metrics.Status(MeterLevel(module, percent), _blue));
            var sample = _samples.GetValueOrDefault(module);
            var temperature = module == BarModule.Cpu ? sample?.LastCpuPackageCelsius
                : module == BarModule.Gpu ? PeakGpuTemperature(sample) : null;
            var label = MonitorText.Format(temperature is > 0 and <= 150
                ? WidgetName(module) + " · " + FormatTemperature(temperature.Value, 0) + (module == BarModule.Cpu ? " · "+TemperatureAge(sample?.CpuTemperatureSampledAt) : "") : WidgetName(module), Face, 10,
                temperature is > 0 and <= 150 ? _palette.Metrics.Status(BarThresholds.TemperatureLevel(temperature), _dim) : _dim, _scale);
            drawing.DrawText(label, MonitorText.Centered(label, new Point(x, y + 35), _scale));
        }
    }

    private static string TemperatureAge(DateTimeOffset? sampledAt)
    {
        if (sampledAt is null) return "cached";
        var age=DateTimeOffset.UtcNow-sampledAt.Value;
        if(age.TotalSeconds<60)return "just now";
        if(age.TotalHours<1)return $"{(int)age.TotalMinutes}m ago";
        return $"{(int)age.TotalHours}h ago";
    }

    private static double? PeakGpuTemperature(PopupSample? sample) =>
        sample?.Gpu?.Adapters.Where(adapter => adapter.TemperatureCelsius is > 0 and <= 150)
            .Select(adapter => adapter.TemperatureCelsius).DefaultIfEmpty().Max();

    private string FormatTemperature(double celsius, int decimals = 1) =>
        (fahrenheit ? celsius * 1.8 + 32 : celsius).ToString(decimals == 0 ? "0" : "0.0") + (fahrenheit ? " °F" : " °C");

    private void TemperatureRow(DrawingContext drawing, Rect card, string title, double? celsius, double y, bool thermalWarning = true)
    {
        var valid = thermalWarning ? celsius is > 0 and <= 150 : celsius is >= -40 and <= 100;
        if (!valid || celsius is not double value) return;
        var marker = thermalWarning ? _palette.Metrics.Status(BarThresholds.TemperatureLevel(value), _palette.Metrics.Green) : _blue;
        WidgetRow(drawing, card, title, FormatTemperature(value), y, marker);
    }

    private void WidgetRow(DrawingContext drawing, Rect card, string name, string? value, double y, Brush? marker = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var x = card.Left + 12;
        if (marker is not null)
        {
            drawing.DrawRoundedRectangle(marker, null, new Rect(x, y + 4, 7, 7), 2, 2);
            x += 12;
        }
        var split = card.Left + card.Width * .52;
        Label(drawing, name, x, y, 10, _dim, width: split - x);
        Label(drawing, value, split, y, 10, _text, true, card.Right - split - 12, right: true);
    }

    private ThresholdLevel MeterLevel(BarModule module, double? percent) => module switch
    {
        BarModule.Battery => BarThresholds.BatteryChargeLevel(percent),
        BarModule.Disk => BarThresholds.StorageLevel(percent),
        _ => BarThresholds.UsageLevel(module, percent, thresholdPercent)
    };

    private void WidgetRing(DrawingContext drawing, Point center, double radius, double? value, double? first = null, Brush? accent = null)
    {
        const double thickness = 6;
        drawing.DrawEllipse(null, PopupPalette.Stroke(_palette.Metrics.Track, thickness), center, radius, radius);
        if (value is > 0 and <= 100)
        {
            var split = Math.Min(value.Value, first ?? 0);
            if (split > 0) WidgetArc(drawing, center, radius, -90, split * 3.6, _palette.Metrics.Cyan);
            if (split < value) WidgetArc(drawing, center, radius, -90 + split * 3.6, (value.Value - split) * 3.6, accent ?? _blue);
        }
        var text = MonitorText.Format(WidgetPercentText(value) ?? "—", Bold, radius > 26 ? 20 : 13, _text, _scale);
        drawing.DrawText(text, MonitorText.Centered(text, center, _scale));
    }

    private static void WidgetArc(DrawingContext drawing, Point center, double radius, double start, double sweep, Brush brush)
    {
        Point At(double degrees)
        {
            var radians = degrees * Math.PI / 180;
            return new(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
        }
        var arc = new StreamGeometry();
        using (var path = arc.Open())
        {
            path.BeginFigure(At(start), false, false);
            path.ArcTo(At(start + Math.Min(359.99, sweep)), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, true, false);
        }
        arc.Freeze();
        drawing.DrawGeometry(null, PopupPalette.Stroke(brush, 6), arc);
    }

    private static bool WidgetAvailable(BarModule module, PopupSample sample) =>
        module == BarModule.Network ? sample.Network is { Download: not null, Upload: not null } : WidgetPercent(module, sample).HasValue;

    private static GpuActivity? BusiestGpu(PopupSample? sample) =>
        sample?.Gpu?.Adapters.Where(adapter => ValidWidgetPercent(adapter.Usage).HasValue).MaxBy(adapter => adapter.Usage);
    private static VolumeCapacity? WidgetVolume(PopupSample? sample) => sample?.Disk?.Volumes.FirstOrDefault(volume => volume.Valid);
    private static double? WidgetPercent(BarModule module, PopupSample? sample) => ValidWidgetPercent(module switch
    {
        BarModule.Cpu => sample?.Cpu,
        BarModule.Gpu => BusiestGpu(sample)?.Usage,
        BarModule.Ram => sample?.Memory?.UsedPercent,
        BarModule.Disk => WidgetVolume(sample)?.UsedPercent,
            BarModule.Battery when sample?.Battery?.Reading is { State: BatteryReadingState.Available } battery => battery.Percent,
        _ => null
    });
    private static double? ValidWidgetPercent(double? value) => value is >= 0 and <= 100 ? value : null;
    private static string? WidgetPercentText(double? value) => value is { } percent ? $"{percent:0}%" : null;
    private static string WidgetBytes(ulong bytes) => $"{bytes / 1073741824.0:0.0} GiB";
    internal static string WidgetName(BarModule module) =>
        module is BarModule.Cpu or BarModule.Ram or BarModule.Gpu ? module.ToString().ToUpperInvariant() : module.ToString();
}
