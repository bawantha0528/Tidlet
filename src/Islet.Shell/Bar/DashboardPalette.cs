using System.Windows;
using System.Windows.Media;
using Islet.Core.Bar;

namespace Islet.Shell.Bar;

internal sealed class DashboardPalette
{
    private static readonly Lazy<DashboardPalette> Dark = new(() => new(BarTheme.Dark));
    private static readonly Lazy<DashboardPalette> Light = new(() => new(BarTheme.Light));
    internal static DashboardPalette For(BarTheme theme) => WindowsAppTheme.Resolve(theme) == BarTheme.Light ? Light.Value : Dark.Value;

    internal Color BaseColor { get; }
    internal PopupPalette Metrics { get; }
    internal Brush Text => Metrics.Text;
    internal Brush Muted => Metrics.Muted;
    internal Brush Accent { get; }
    internal Brush Card { get; }
    internal Pen CardBorder => Metrics.InsetBorder;
    internal Pen Divider => Metrics.Divider;
    internal Brush ChartFill { get; }
    internal Pen ChartLine { get; }
    internal Pen Grid { get; }
    private readonly Brush _sidebar;
    private static readonly Brush Selected = PopupPalette.Solid(0x0067C4);
    private static readonly Brush Screen = Fade(Color.FromRgb(8, 18, 38), Color.FromRgb(16, 70, 146), 24);
    private static readonly Brush Device = Fade(Color.FromRgb(82, 85, 91), Color.FromRgb(35, 36, 39), 90);
    private static readonly Pen DeviceTrim = PopupPalette.Stroke(PopupPalette.Solid(0x6F7278), 1);

    private DashboardPalette(BarTheme theme)
    {
        var light = theme == BarTheme.Light;
        Metrics = PopupPalette.For(theme);
        BaseColor = light ? Color.FromRgb(227, 234, 242) : Color.FromRgb(21, 21, 21);
        Accent = PopupPalette.Solid(light ? 0x086BB7 : 0x2C93E9);
        Card = PopupPalette.Solid(light ? 0xFFFFFF : 0x252525);
        _sidebar = PopupPalette.Solid(light ? 0xEAF0F6 : 0x1D1D1D);
        ChartFill = PopupPalette.Solid(light ? 0x086BB7 : 0x0A84FF, light ? (byte)65 : (byte)130);
        ChartLine = PopupPalette.Stroke(Metrics.Blue, 1);
        Grid = PopupPalette.Stroke(Metrics.Track, .5);
    }

    internal DrawingGroup CreateBackdrop(double width, double height)
    {
        var result = new DrawingGroup();
        using (var drawing = result.Open())
        {
            drawing.DrawRoundedRectangle(_sidebar, CardBorder, new Rect(10, 10, 195, height - 20), 8, 8);
            drawing.DrawRoundedRectangle(Metrics.Surface, CardBorder, new Rect(216, 10, width - 226, height - 20), 8, 8);
            drawing.DrawLine(Divider, new Point(10, height - 67), new Point(205, height - 67));
        }
        result.Freeze();
        return result;
    }

    internal static void DrawSelection(DrawingContext drawing, Rect bounds) =>
        drawing.DrawRoundedRectangle(Selected, null, bounds, 7, 7);

    internal void DrawFactMarker(DrawingContext drawing, double top, bool processor) =>
        drawing.DrawRoundedRectangle(processor ? Accent : Metrics.BatteryFill, null, new Rect(242, top + 13, 4, 15), 2, 2);

    internal void DrawDevice(DrawingContext drawing, double center)
    {
        drawing.DrawRoundedRectangle(Device, DeviceTrim, new Rect(center - 77, 90, 154, 93), 7, 7);
        drawing.DrawRoundedRectangle(Screen, null, new Rect(center - 72, 95, 144, 83), 4, 4);
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 2; column++)
                drawing.DrawRectangle(Accent, null, new Rect(center - 14 + column * 15, 122 + row * 15, 13, 13));
        }
        drawing.DrawRoundedRectangle(Device, DeviceTrim, new Rect(center - 89, 183, 178, 8), 3, 3);
        drawing.DrawRoundedRectangle(Muted, null, new Rect(center - 12, 183, 24, 2), 1, 1);
    }

    private static Brush Fade(Color start, Color end, double angle)
    {
        var brush = new LinearGradientBrush(start, end, angle);
        brush.Freeze();
        return brush;
    }

}
