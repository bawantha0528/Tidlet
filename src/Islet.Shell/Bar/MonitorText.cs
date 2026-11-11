using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Islet.Shell.Bar;

internal static class MonitorText
{
    internal static FormattedText Format(string value,Typeface face,double size,Brush brush,double scale) =>
        new(value,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,face,size,brush,null,TextFormattingMode.Display,scale);

    internal static Point PixelAligned(double x,double y,double scale) =>
        new(Math.Round(x*scale)/scale,Math.Round(y*scale)/scale);

    internal static Point Centered(FormattedText text,Point center,double scale) =>
        PixelAligned(center.X-text.Width/2,center.Y-text.Height/2,scale);
}
