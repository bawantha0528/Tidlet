using System.Windows;
using System.Windows.Media;

namespace Islet.Shell.Bar;

internal static class DashboardIcons
{
    private static readonly IReadOnlyDictionary<string, Geometry> Shapes = new Dictionary<string, string>
    {
        ["Dashboard"] = "M2,2 H6 V6 H2 Z M10,2 H14 V6 H10 Z M2,10 H6 V14 H2 Z M10,10 H14 V14 H10 Z",
        ["CPU"] = "M4,4 H12 V12 H4 Z M6,6 H10 V10 H6 Z M6,1 V4 M10,1 V4 M6,12 V15 M10,12 V15 M1,6 H4 M1,10 H4 M12,6 H15 M12,10 H15",
        ["GPU"] = "M1,3 H14 V12 H1 Z M14,5 H16 M4,12 V14 H8 M9,5 A2.5,2.5 0 1 1 8.99,5",
        ["RAM"] = "M1,4 H15 V11 H1 Z M4,6 V9 M7,6 V9 M10,6 V9 M13,6 V9 M3,11 V13 M6,11 V13 M9,11 V13 M12,11 V13",
        ["Disk"] = "M2,9 L4,3 H12 L14,9 V13 H2 Z M2,9 H14 M5,11 H6 M8,11 H11",
        ["Network"] = "M1,6 Q8,0 15,6 M3,9 Q8,5 13,9 M5.5,11.5 Q8,9.5 10.5,11.5 M8,14 H8.1",
        ["Battery"] = "M1,4 H13 V12 H1 Z M15,6 V10 M4,7 H10 M4,9 H10",
        ["Sensors"] = "M6,3 A2,2 0 0 1 10,3 V9 A3.5,3.5 0 1 1 6,9 Z M8,6 V12 M11,4 H14 M11,7 H13",
        ["FrameRate"] = "M2,11 A6,6 0 1 1 14,11 M8,9 L11.5,5.5 M7.5,9 A0.7,0.7 0 1 1 8.5,9",
        ["Gaming"] = "M5,4 H11 Q13,4 14,7 L15,11 Q15,14 12.5,12.5 L10.5,10.5 H5.5 L3.5,12.5 Q1,14 1,11 L2,7 Q3,4 5,4 Z M4,7 H8 M6,5 V9 M11,7 H11.1 M12.5,8.5 H12.6",
        ["Widgets"] = "M2,2 H14 V14 H2 Z M2,6 H14 M7,6 V14",
        ["About"] = "M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 M8,7 V11 M8,4.5 H8.1",
        ["Report"] = "M3,1.5 H9 L13,5.5 V14.5 H3 Z M9,1.5 V5.5 H13 M5.5,8 H10.5 M5.5,11 H10.5",
        ["Pause"] = "M5,3 V13 M11,3 V13",
        ["Resume"] = "M5,3 L13,8 L5,13 Z",
        ["Exit"] = "M8,1 V7 M4,4 A5.7,5.7 0 1 0 12,4"
    }.ToDictionary(entry => entry.Key, entry =>
    {
        var shape = Geometry.Parse(entry.Value);
        shape.Freeze();
        return shape;
    });

    internal static void Draw(DrawingContext drawing, string icon, Point center, Brush brush)
    {
        if (!Shapes.TryGetValue(icon, out var shape)) return;
        var pen = new Pen(brush, 1.25) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        drawing.PushTransform(new TranslateTransform(center.X - 8, center.Y - 8));
        drawing.DrawGeometry(null, pen, shape);
        drawing.Pop();
    }
}
