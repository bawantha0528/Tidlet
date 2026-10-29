using System.ComponentModel;
using System.IO;
using Islet.Core.Island;
using static Islet.Windows.Native.NativeMethods;

namespace Islet.Windows.Native;

internal sealed unsafe class DibRenderer : IDisposable
{
    private sealed class Surface : IDisposable
    {
        public nint Dc, Bitmap, Previous, Bits;
        public int Width, Height;
        public Span<int> Pixels => new((void*)Bits, checked(Width * Height));

        public Surface(int width, int height)
        {
            if (width < 1 || height < 1 || (long)width * height > 4_000_000)
                throw new ArgumentOutOfRangeException(nameof(width), "Island surface exceeds its allocation limit.");
            Width = width; Height = height;
            try
            {
                Dc = CreateCompatibleDC(0);
                if (Dc == 0) throw new InvalidOperationException("Windows could not allocate the drawing context.");
                var info = new BitmapInfo { Header = new BitmapHeader {
                    Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 } };
                Bitmap = CreateDIBSection(Dc, ref info, 0, out Bits, 0, 0);
                if (Bitmap == 0 || Bits == 0) throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError(), "Create island bitmap");
                Previous = SelectObject(Dc, Bitmap);
                if (Previous == 0 || Previous == -1) throw new InvalidOperationException("Windows could not select the island bitmap.");
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (Dc != 0 && Previous != 0 && Previous != -1) SelectObject(Dc, Previous);
            if (Bitmap != 0) DeleteObject(Bitmap);
            if (Dc != 0) DeleteDC(Dc);
            Bitmap = Dc = Previous = Bits = 0;
        }
    }

    private Surface? _output, _mask;
    private readonly Dictionary<(int, int), nint> _fonts = new();
    public int FrameCount { get; private set; }
    private double _fontScale;

    public void Draw(nint window, IslandFrame frame)
    {
        var width = frame.Bounds.Width;
        var height = frame.Bounds.Height;
        EnsureSize(width, height);
        if (_fontScale != frame.Scale)
        {
            foreach (var font in _fonts.Values) DeleteObject(font);
            _fonts.Clear();
            _fontScale = frame.Scale;
        }
        var output = _output!;
        output.Pixels.Clear();
        var radius = Math.Min(height / 2.0, 20 * frame.Scale);
        for (var y = 0; y < height; y++)
        {
            var edge = IslandGeometry.LeftEdge(y + 0.5, width, height, radius, frame.Style);
            var start = Math.Max(0, (int)Math.Floor(edge));
            var end = Math.Min(width - 1, (int)Math.Ceiling(width - edge));
            for (var x = start; x <= end; x++)
            {
                var coverage = Math.Clamp(Math.Min(x + 1 - edge, width - edge - x), 0, 1);
                var alpha = (int)Math.Round(coverage * 248);
                output.Pixels[y * output.Width + x] = Premultiply(frame.HighContrast ? 0 : 0x141A25, frame.HighContrast && alpha > 0 ? 255 : alpha);
            }
        }
        DrawContent(frame, width, height);
        var destination = new Point(frame.Bounds.X, frame.Bounds.Y);
        var size = new Size(width, height);
        var source = new Point(0, 0);
        var blend = new Blend { Alpha = 255, Format = 1 };
        if (!UpdateLayeredWindow(window, 0, ref destination, ref size, output.Dc, ref source, 0, ref blend, 2))
            throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError(), "Publish island frame");
        FrameCount++;
    }

    private void DrawContent(IslandFrame frame, int width, int height)
    {
        var s = frame.Scale;
        var dipHeight = height / s;
        if (frame.State == IslandState.Collapsed || dipHeight < 70)
        {
            var inset = frame.Style == Core.Settings.IslandStyle.Notch ? 32 : 22;
            Dot(inset * s, height / 2.0, 3 * s, 0x76E4C3, width, height);
            Text(frame.Label, (inset + 15) * s, 0, width - 12 * s, height, 14 * s, 600, 0xF1F5FB, width, height);
            return;
        }
        if (frame.State == IslandState.Hover || dipHeight < 180)
        {
            Text(frame.Label, 28 * s, 16 * s, width - 18 * s, 49 * s, 23 * s, 600, 0xF1F5FB, width, height);
            Text("Click to see recent activity", 28 * s, 57 * s, width - 18 * s, 78 * s, 11 * s, 400, 0xBAC6DA, width, height);
            return;
        }
        Text("Tidlet Monitor", 24 * s, 16 * s, width - 24 * s, 44 * s, 17 * s, 600, 0xF1F5FB, width, height);
        Dot(width - 30 * s, 30 * s, 4 * s, 0x76E4C3, width, height);
        Text(frame.Label, 24 * s, 55 * s, width - 24 * s, 94 * s, 28 * s, 600, 0xF1F5FB, width, height);
        Text(frame.Explanation, 24 * s, 104 * s, width - 24 * s, 128 * s, 12 * s, 400, 0xBAC6DA, width, height);
        Text("Recent activity", 24 * s, 140 * s, width - 24 * s, 159 * s, 11 * s, 400, 0xBAC6DA, width, height);
        var left = (int)(26 * s); var right = (int)(width - 26 * s);
        var top = (int)(170 * s); var bottom = (int)(height - 58 * s);
        if (bottom > top && right > left)
        {
            for (var grid = 0; grid < 3; grid++)
                Line(left, top + (bottom - top) * grid / 2, right, top + (bottom - top) * grid / 2,
                    frame.HighContrast ? 0x808080 : 0x303C50, width, height);
            (int X, int Y)? prior = null;
            for (var index = 0; index < frame.History.Count; index++)
            {
                var value = frame.History[index];
                if (value is null || !double.IsFinite(value.Value)) { prior = null; continue; }
                var x = left + (right - left) * index / 59;
                var y = bottom - (int)Math.Round((bottom - top) * Math.Clamp(value.Value, 0, 100) / 100);
                if (prior is { } p)
                {
                    Line(p.X, p.Y, x, y, 0x76E4C3, width, height);
                    Line(p.X, p.Y + 1, x, y + 1, 0x76E4C3, width, height);
                }
                else Dot(x, y, Math.Max(1, s), 0x76E4C3, width, height);
                prior = (x, y);
            }
        }
        Text("Esc to collapse  ·  Right-click for options", 24 * s, height - 38 * s,
            width - 24 * s, height - 16 * s, 11 * s, 400, 0xBAC6DA, width, height);
    }

    private void Line(int x0, int y0, int x1, int y1, int color, int width, int height)
    {
        var dx = Math.Abs(x1 - x0); var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0); var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
            {
                var offset = y0 * _output!.Width + x0;
                if ((uint)_output.Pixels[offset] >> 24 != 0)
                    _output.Pixels[offset] = Over(_output.Pixels[offset], color, 255);
            }
            if (x0 == x1 && y0 == y1) break;
            var twice = error * 2;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    private void EnsureSize(int width, int height)
    {
        if (_output is not null && _output.Width == width && _output.Height == height) return;
        _output?.Dispose(); _mask?.Dispose();
        _output = null; _mask = null;
        try { _output = new Surface(width, height); _mask = new Surface(width, height); }
        catch { _output?.Dispose(); _mask?.Dispose(); _output = _mask = null; throw; }
    }

    private void Text(string text, double x, double y, double right, double bottom,
        double fontPixels, int weight, int color, int width, int height)
    {
        if (string.IsNullOrEmpty(text) || right <= x || bottom <= y || y >= height || x >= width) return;
        var mask = _mask!;
        mask.Pixels.Clear();
        var key = ((int)Math.Round(fontPixels), weight);
        if (!_fonts.TryGetValue(key, out var font))
        {
            font = CreateFontW(-key.Item1, 0, 0, 0, weight, 0, 0, 0, 1, 0, 0, 4, 0, "Segoe UI");
            if (font == 0) throw new InvalidOperationException("Windows could not create the island font.");
            _fonts.Add(key, font);
        }
        var previous = SelectObject(mask.Dc, font);
        if (previous == 0 || previous == -1) throw new InvalidOperationException("Windows could not select the island font.");
        try
        {
            if (SetTextColor(mask.Dc, 0xFFFFFF) == uint.MaxValue || SetBkColor(mask.Dc, 0) == uint.MaxValue
                || SetBkMode(mask.Dc, 1) == 0) throw new InvalidOperationException("Windows could not configure text drawing.");
            var rect = new Rect { Left = (int)x, Top = (int)y, Right = (int)right, Bottom = (int)bottom };
            if (DrawTextW(mask.Dc, text, text.Length, ref rect, 0x20 | 0x4 | 0x800 | 0x8000) == 0)
                throw new InvalidOperationException("Windows could not draw island text.");
            if (!GdiFlush()) throw new InvalidOperationException("Windows could not finish island text drawing.");
            var src = mask.Pixels;
            var dst = _output!.Pixels;
            for (var row = Math.Max(0, (int)y); row < Math.Min(height, (int)Math.Ceiling(bottom)); row++)
                for (var col = Math.Max(0, (int)x); col < Math.Min(width, (int)Math.Ceiling(right)); col++)
                {
                    var index = row * mask.Width + col;
                    var value = src[index];
                    var coverage = Math.Max(value & 255, Math.Max((value >> 8) & 255, (value >> 16) & 255));
                    if (coverage > 0 && (uint)dst[index] >> 24 != 0)
                        dst[index] = Over(dst[index], color, coverage);
                }
        }
        finally { SelectObject(mask.Dc, previous); }
    }

    private void Dot(double cx, double cy, double radius, int color, int width, int height)
    {
        var pixels = _output!.Pixels;
        for (var y = Math.Max(0, (int)(cy - radius - 1)); y < Math.Min(height, cy + radius + 1); y++)
            for (var x = Math.Max(0, (int)(cx - radius - 1)); x < Math.Min(width, cx + radius + 1); x++)
            {
                var coverage = Math.Clamp(radius + 0.5 - Math.Sqrt((x + 0.5 - cx) * (x + 0.5 - cx) + (y + 0.5 - cy) * (y + 0.5 - cy)), 0, 1);
                if (coverage > 0) pixels[y * _output.Width + x] = Over(pixels[y * _output.Width + x], color, (int)(coverage * 255));
            }
    }

    internal static int Premultiply(int color, int alpha) =>
        unchecked((int)((uint)alpha << 24)) | (((color >> 16) & 255) * alpha / 255 << 16)
        | (((color >> 8) & 255) * alpha / 255 << 8) | ((color & 255) * alpha / 255);

    internal static int Over(int background, int color, int alpha)
    {
        var inverse = 255 - alpha;
        var a = alpha + (int)((uint)background >> 24) * inverse / 255;
        var r = ((color >> 16) & 255) * alpha / 255 + ((background >> 16) & 255) * inverse / 255;
        var g = ((color >> 8) & 255) * alpha / 255 + ((background >> 8) & 255) * inverse / 255;
        var b = (color & 255) * alpha / 255 + (background & 255) * inverse / 255;
        return unchecked((int)((uint)a << 24)) | r << 16 | g << 8 | b;
    }

    public void SaveBitmap(string path)
    {
        var surface = _output ?? throw new InvalidOperationException("No rendered frame exists.");
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        var length = checked(surface.Width * surface.Height * 4);
        writer.Write((ushort)0x4D42); writer.Write(54 + length); writer.Write(0); writer.Write(54);
        writer.Write(40); writer.Write(surface.Width); writer.Write(-surface.Height);
        writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(0); writer.Write(length);
        writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        foreach (var pixel in surface.Pixels) writer.Write(pixel);
    }

    public void Dispose()
    {
        _output?.Dispose(); _mask?.Dispose(); _output = _mask = null;
        foreach (var font in _fonts.Values) DeleteObject(font);
        _fonts.Clear();
    }
}
