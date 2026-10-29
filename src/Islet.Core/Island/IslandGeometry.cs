using Islet.Core.Settings;

namespace Islet.Core.Island;

public static class IslandGeometry
{
    public static PixelRect Place(IslandDisplay display, double widthDip, double heightDip, IslandStyle style)
    {
        if (display.Dpi < 48 || display.Dpi > 960 || display.Bounds.Width < 1 || display.Bounds.Height < 1
            || !double.IsFinite(widthDip) || !double.IsFinite(heightDip) || widthDip <= 0 || heightDip <= 0 || widthDip > 10000 || heightDip > 10000)
            throw new ArgumentOutOfRangeException(nameof(display));
        var scale = display.Dpi / 96.0;
        var width = Math.Clamp((int)Math.Round(widthDip * scale), 1, display.Bounds.Width);
        var gap = style == IslandStyle.Island ? Math.Min((int)Math.Round(8 * scale), display.Bounds.Height - 1) : 0;
        var height = Math.Clamp((int)Math.Round(heightDip * scale), 1, Math.Max(1, display.Bounds.Height - gap));
        return new(display.Bounds.X + (display.Bounds.Width - width) / 2, display.Bounds.Y + gap, width, height);
    }

    /// <summary>Custom symmetric contour: Notch uses concave quarter-circle top flares.</summary>
    public static double LeftEdge(double y, double width, double height, double radius, IslandStyle style)
    {
        if (width <= 0 || height <= 0 || y < 0 || y >= height) return width / 2;
        var r = Math.Clamp(radius, 0, Math.Min(width / 4, height / 2));
        if (r == 0) return 0;
        if (style == IslandStyle.Notch)
        {
            if (y < r) return Math.Sqrt(Math.Max(0, r * r - (y - r) * (y - r)));
            if (y > height - r) return 2 * r - Math.Sqrt(Math.Max(0, r * r - (y - height + r) * (y - height + r)));
            return r;
        }
        var cornerY = y < r ? r - y : y > height - r ? y - height + r : 0;
        return cornerY == 0 ? 0 : r - Math.Sqrt(Math.Max(0, r * r - cornerY * cornerY));
    }
}
