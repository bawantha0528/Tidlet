using System.ComponentModel;
using System.Runtime.InteropServices;
namespace Islet.Windows.Bar;

/// <summary>Read monitor work area and position only our HWND; never reserves or changes desktop work area.</summary>
public static class VisualHostPlacement
{
    public static void Position(nint hwnd, double scale)
    {
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfoW(MonitorFromWindow(hwnd, 2), ref monitor)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var width = (int)Math.Ceiling(240 * scale);
        var height = (int)Math.Ceiling(24 * scale);
        if (!SetWindowPos(hwnd, new nint(-1), monitor.Work.Left + (monitor.Work.Right - monitor.Work.Left - width) / 2,
            monitor.Work.Top, width, height, 0x0010 | 0x0040)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll",ExactSpelling=true)] internal static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", ExactSpelling=true, SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd,nint insertAfter,int x,int y,int width,int height,uint flags);
}
