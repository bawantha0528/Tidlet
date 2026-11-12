using System.Windows.Media;
using Islet.Core.Bar;

namespace Islet.Shell.Bar;

internal sealed class PopupPalette
{
    private static readonly Lazy<PopupPalette> Dark = new(() => new(BarTheme.Dark));
    private static readonly Lazy<PopupPalette> Light = new(() => new(BarTheme.Light));
    internal static PopupPalette For(BarTheme theme) => theme == BarTheme.Light ? Light.Value : Dark.Value;

    internal Brush Surface { get; }
    internal Brush Panel { get; }
    internal Brush Track { get; }
    internal Brush Text { get; }
    internal Brush Muted { get; }
    internal Brush Blue { get; }
    internal Brush Red { get; }
    internal Brush Orange { get; }
    internal Brush Cyan { get; }
    internal Brush Green { get; }
    internal Pen Border { get; }
    internal Pen InsetBorder { get; }
    internal Pen Divider { get; }
    internal Pen RingTrack { get; }
    internal Pen BatteryOutline { get; }
    internal Brush BatteryFill { get; }
    internal ChartStyle Download { get; }
    internal ChartStyle Upload { get; }
    private readonly ChartStyle _blueChart, _greenChart;
    internal ChartStyle Chart(BarModule module) => module==BarModule.Battery ? _greenChart : _blueChart;

    private PopupPalette(BarTheme theme)
    {
        var light = theme == BarTheme.Light;
        Surface = Solid(light ? 0xFAFBFD : 0x1F1F1F);
        Panel = Solid(light ? 0xF1F4F8 : 0x1A1A1A);
        Track = Solid(light ? 0xCED5DF : 0x464646);
        Text = light ? PopupTheme.Get("text",theme) : Solid(0xF4F4F4);
        Muted = light ? PopupTheme.Get("secondary",theme) : Solid(0xBEBEBE);
        Blue = PopupTheme.Get("blue",theme);
        Red = PopupTheme.Get("red",theme);
        Orange = PopupTheme.Get("orange",theme);
        Cyan = PopupTheme.Get("cyan",theme);
        Green = PopupTheme.Get("green",theme);
        Border = Stroke(Solid(light ? 0xB6C0CE : 0x454545), 1);
        InsetBorder = Stroke(Solid(light ? 0xCAD2DD : 0x3C3C3C), 1);
        Divider = Stroke(Solid(light ? 0xD7DEE7 : 0x383838), 1);
        RingTrack = Stroke(Track, 7);
        BatteryOutline = Stroke(Muted, 1.5);
        BatteryFill = Solid(light ? 0x188447 : 0x259D54);
        Download = new(light ? 0x116CB4 : 0x4AABF3, light ? 82 : 150);
        Upload = new(light ? 0xBC3437 : 0xF66B68, light ? 74 : 142);
        _blueChart = new(light ? 0x086BB7 : 0x2896E9, light ? 85 : 198);
        _greenChart = new(light ? 0x187B47 : 0x2CA860, light ? 80 : 198);
    }

    internal Brush Status(ThresholdLevel level, Brush normal) => level switch
    {
        ThresholdLevel.Warning => Orange,
        ThresholdLevel.Critical => Red,
        _ => normal
    };

    internal sealed class ChartStyle(int rgb, int alpha)
    {
        internal Brush Fill { get; } = Solid(rgb, (byte)alpha);
        internal Pen Line { get; } = Stroke(Solid(rgb), 1.25);
        internal Brush Accent => Line.Brush;
    }

    internal static Brush Solid(int rgb, byte alpha = 255)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        brush.Freeze();
        return brush;
    }

    internal static Pen Stroke(Brush brush, double width)
    {
        var pen = new Pen(brush, width);
        pen.Freeze();
        return pen;
    }
}
