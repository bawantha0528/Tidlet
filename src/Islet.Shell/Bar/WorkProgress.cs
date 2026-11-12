using System.Windows;
using System.Windows.Media;

namespace Islet.Shell.Bar;

internal static class WorkProgress
{
    public static void Draw(DrawingContext drawing, Rect track, Brush background, Brush accent, double? fraction = null)
    {
        var radius = track.Height / 2;
        drawing.DrawRoundedRectangle(background, null, track, radius, radius);
        if (fraction is double completed)
        {
            var width = track.Width * Math.Clamp(completed, 0, 1);
            if (width > 0) drawing.DrawRoundedRectangle(accent, null, new Rect(track.Location, new Size(width, track.Height)), radius, radius);
            return;
        }

        // Separate marks distinguish an unknown duration from a partially complete bar.
        for (int i = 0; i < 3; i++)
        {
            var dot = new Point(track.Left + radius + i * (track.Height + 3), track.Top + radius);
            drawing.DrawEllipse(accent, null, dot, radius, radius);
        }
    }
}
