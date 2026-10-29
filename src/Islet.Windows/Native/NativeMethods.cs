using System.Runtime.InteropServices;

namespace Islet.Windows.Native;

// ABI references and constant sources: docs/API_REGISTER.md, native renderer section.
internal static partial class NativeMethods
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowClass
    {
        public uint Size, Style;
        public nint Proc;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background, MenuName, ClassName, SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Size { public int Width, Height; public Size(int w, int h) { Width = w; Height = h; } }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Message { public nint Hwnd; public uint Id; public nuint WParam; public nint LParam; public uint Time; public Point Position; public uint Private; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapHeader
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, ImageSize;
        public int XPels, YPels;
        public uint ColorsUsed, ColorsImportant;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfo { public BitmapHeader Header; public uint Color; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Blend { public byte Op, Flags, Alpha, Format; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] internal static extern nint GetModuleHandleW(string? module);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] internal static extern ushort RegisterClassExW(ref WindowClass value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterClassW(string name, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] internal static extern nint CreateWindowExW(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern nint DefWindowProcW(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern void PostQuitMessage(int code);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] internal static extern int GetMessageW(out Message message, nint hwnd, uint min, uint max);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern nint DispatchMessageW(ref Message message);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] internal static extern nuint SetTimer(nint hwnd, nuint id, uint milliseconds, nint callback);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool KillTimer(nint hwnd, nuint id);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UpdateLayeredWindow(nint hwnd, nint destinationDc, ref Point destination, ref Size size, nint sourceDc, ref Point source, uint key, ref Blend blend, uint flags);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] internal static extern nint GetWindowLongPtrW(nint hwnd, int index);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] internal static extern int GetWindowLongW(nint hwnd, int index);
    [DllImport("gdi32.dll", ExactSpelling = true)] internal static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)] internal static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll", ExactSpelling = true)] internal static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll", ExactSpelling = true)] internal static extern uint SetTextColor(nint dc, uint color);
    [DllImport("gdi32.dll", ExactSpelling = true)] internal static extern uint SetBkColor(nint dc, uint color);
    [DllImport("gdi32.dll", ExactSpelling = true)] internal static extern int SetBkMode(nint dc, int mode);
    [DllImport("gdi32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GdiFlush();
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] internal static extern nint CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeout, uint charset, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] internal static extern int DrawTextW(nint dc, string text, int length, ref Rect rect, uint format);
}
