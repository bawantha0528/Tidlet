using System.ComponentModel;
using System.Runtime.InteropServices;
using Islet.Core.Display;

namespace Islet.Windows.Display;

/// <summary>Experimental primary-display host only. See docs/API_REGISTER.md for ABI sources.</summary>
public sealed class IslandProbePlatform : IIslandProbePlatform
{
    public const long RequiredStyles = 0x08000000L | 0x80L | 0x20L;
    public const long LayeredStyle = 0x80000L;
    private const int ExtendedStyle = -20;

    public void Configure(nint window, double widthDip, double heightDip, double gapDip)
    {
        var style = ReadStyle(window);
        WriteStyle(window, (style | RequiredStyles) & ~0x40000L);
        var monitor = MonitorFromWindow(0, 1); // null HWND + DEFAULTTOPRIMARY
        if (monitor == 0) throw new InvalidOperationException("Windows could not locate the primary display.");
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfoW(monitor, ref info)) throw NativeError("Read display bounds");
        // Move onto the chosen display before querying its per-window DPI.
        Position(window, info.Monitor.Left, info.Monitor.Top, 1, 1);
        var dpi = GetDpiForWindow(window);
        if (dpi == 0) throw new InvalidOperationException("Windows could not read the island DPI.");
        var width = Math.Min(info.Monitor.Right - info.Monitor.Left, Pixels(widthDip, dpi));
        var height = Math.Min(info.Monitor.Bottom - info.Monitor.Top, Pixels(heightDip, dpi));
        var gap = Math.Min(Pixels(gapDip, dpi), Math.Max(0, info.Monitor.Bottom - info.Monitor.Top - height));
        Position(window, info.Monitor.Left + (info.Monitor.Right - info.Monitor.Left - width) / 2,
            info.Monitor.Top + gap, width, height);
    }

    public IslandProbeObservation Observe(nint window)
    {
        var style = ReadStyle(window);
        if (!GetWindowRect(window, out var rect)) throw NativeError("Read island bounds");
        return new(IsWindowVisible(window), (style & RequiredStyles) == RequiredStyles,
            (style & LayeredStyle) != 0, GetForegroundWindow() == window, GetDpiForWindow(window),
            rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public static int Pixels(double dip, uint dpi)
    {
        if (!double.IsFinite(dip) || dip < 0 || dip > 10000 || dpi == 0 || dpi > 9600)
            throw new ArgumentOutOfRangeException(nameof(dip));
        return checked((int)Math.Round(dip * dpi / 96.0, MidpointRounding.AwayFromZero));
    }

    private static void Position(nint window, int x, int y, int width, int height)
    {
        if (!SetWindowPos(window, -1, x, y, width, height, 0x0010 | 0x0020))
            throw NativeError("Position island without activation");
    }

    private static long ReadStyle(nint window)
    {
        var result = IntPtr.Size == 8 ? GetWindowLongPtrW(window, ExtendedStyle).ToInt64()
            : GetWindowLongW(window, ExtendedStyle);
        var error = Marshal.GetLastPInvokeError();
        if (result == 0 && error != 0) throw new Win32Exception(error, "Read island window styles");
        return result;
    }

    private static void WriteStyle(nint window, long style)
    {
        var result = IntPtr.Size == 8 ? SetWindowLongPtrW(window, ExtendedStyle, new nint(style)).ToInt64()
            : SetWindowLongW(window, ExtendedStyle, unchecked((int)style));
        var error = Marshal.GetLastPInvokeError();
        if (result == 0 && error != 0) throw new Win32Exception(error, "Apply island window styles");
    }

    private static Win32Exception NativeError(string action) => new(Marshal.GetLastPInvokeError(), action);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint GetWindowLongPtrW(nint window, int index);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int GetWindowLongW(nint window, int index);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nint SetWindowLongPtrW(nint window, int index, nint value);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int SetWindowLongW(nint window, int index, int value);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out Rect rect);
}
