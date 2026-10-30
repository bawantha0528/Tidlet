using System.Globalization;
using Islet.Core.Settings;

namespace Islet.Core.Presentation;

public enum MonitorPage { Overview, Cpu, Memory, Network, Disk, Gpu, Battery, Apps }
public sealed record DetailRow(string Label, string Value);
public sealed record MonitorCard(MonitorPage Page, string Title, string Value, string Subtitle,
    double? Percent, string Status, IReadOnlyList<DetailRow> Details,
    IReadOnlyList<double?> History, IReadOnlyList<double?>? SecondaryHistory = null,
    string HistoryLabel = "RECENT SAMPLES", bool Absent = false);
public sealed record MonitorDashboard(IReadOnlyList<MonitorCard> Cards, MonitorPage Page = MonitorPage.Overview,
    ThemeMode Theme = ThemeMode.Dark, string Accent = "#3B82F6", ControlCenter? ControlCenter = null, bool CanReturnToNotch = false);

public static class MonitorFormat
{
    public static bool ValidPercent(double? value) => value is { } v && double.IsFinite(v) && v is >= 0 and <= 100;
    public static string Percent(double? value) => ValidPercent(value) ? $"{value:0}%" : "Unavailable";
    public static string Bytes(ulong bytes)
    {
        var value = (double)bytes;
        var units = new[] { "B", "KiB", "MiB", "GiB", "TiB" };
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return value.ToString(unit == 0 ? "0" : "0.0", CultureInfo.CurrentCulture) + " " + units[unit];
    }
    public static string Rate(ulong bytes) => Bytes(bytes) + "/s";
    public static string OptionalNumber(double? value, string unit) => value is { } v && double.IsFinite(v) && v >= 0
        ? v.ToString("0.0", CultureInfo.CurrentCulture) + " " + unit : "Not reported";
}

public readonly record struct DashboardRect(double X, double Y, double Width, double Height)
{
    public bool Contains(double x, double y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}

/// <summary>Shared drawing and hit-test geometry, in device-independent pixels.</summary>
public static class DashboardLayout
{
    public static double FitScale(double widthDip, double heightDip, double dpiScale, int availableWidth, int availableHeight)
    {
        if (!double.IsFinite(widthDip) || !double.IsFinite(heightDip) || !double.IsFinite(dpiScale)
            || widthDip <= 0 || heightDip <= 0 || dpiScale <= 0 || availableWidth < 1 || availableHeight < 1)
            throw new ArgumentOutOfRangeException(nameof(widthDip));
        // Preserve the complete layout on small/high-DPI displays, and the existing DIB allocation ceiling.
        return Math.Min(dpiScale, Math.Min(availableWidth / widthDip,
            Math.Min(availableHeight / heightDip, Math.Sqrt(3_900_000.0 / (widthDip * heightDip)))));
    }
    public const int PageCommand = 400;
    public const int ThemeCommand = 450;
    public static readonly string[] PageNames = ["Overview", "CPU", "Memory", "Network", "Disk", "GPU", "Battery", "Apps"];
    public static DashboardRect Tab(int index, double width) => new(28 + index * ((width - 56) / 8), 69, (width - 56) / 8 - 3, 31);
    public static DashboardRect ThemeButton(double width) => new(width - 92, 20, 64, 28);
    public static DashboardRect Card(int index, int count, double width, double height)
    {
        var columns = width < 680 ? 2 : 3;
        var rows = (int)Math.Ceiling(count / (double)columns);
        var w = (width - 56 - (columns - 1) * 12) / columns;
        var h = (height - 158 - (rows - 1) * 12) / rows;
        return new(28 + index % columns * (w + 12), 116 + index / columns * (h + 12), w, Math.Max(1, h));
    }
    public static int HitTest(double x, double y, double width, double height, MonitorDashboard dashboard)
    {
        if (dashboard.ControlCenter is { } center) return ControlCenterLayout.HitTest(x, y, center);
        if (dashboard.CanReturnToNotch && ControlCenterLayout.ReturnButton(width).Contains(x,y)) return ControlCenterLayout.ReturnToNotch;
        if (ThemeButton(width).Contains(x, y)) return ThemeCommand;
        for (var i = 0; i < PageNames.Length; i++) if (Tab(i, width).Contains(x, y)) return PageCommand + i;
        if (dashboard.Page != MonitorPage.Overview) return 0;
        var cards = dashboard.Cards.Where(c => !c.Absent && c.Page != MonitorPage.Gpu).Take(6).ToArray();
        for (var i = 0; i < cards.Length; i++) if (Card(i, cards.Length, width, height).Contains(x, y)) return PageCommand + (int)cards[i].Page;
        return 0;
    }
}