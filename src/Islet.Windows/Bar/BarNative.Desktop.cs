using System.Runtime.InteropServices;
namespace Islet.Windows.Bar;
internal static partial class BarNative
{
    [StructLayout(LayoutKind.Sequential)]internal struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]internal struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]internal struct AppBar{public uint Size;public nint Window;public uint Callback,Edge;public Rect Bounds;public nint Param;}
    [DllImport("shell32.dll",ExactSpelling=true)]internal static extern nuint SHAppBarMessage(uint message,ref AppBar data);
    [DllImport("user32.dll",ExactSpelling=true)]internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll",ExactSpelling=true,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool SetWindowPos(nint hwnd,nint insertAfter,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll",ExactSpelling=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool ShowWindow(nint hwnd,int command);
    [DllImport("user32.dll",ExactSpelling=true)]internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll",ExactSpelling=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool GetWindowRect(nint hwnd,out Rect rect);
    [DllImport("user32.dll",ExactSpelling=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll",ExactSpelling=true)]internal static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]internal static extern nint FindWindowW(string name,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]internal static extern nint FindWindowExW(nint parent,nint after,string? name,string? title);
    [StructLayout(LayoutKind.Sequential)]internal struct PowerStatus{public byte Ac,Flag,Percent,Saver;public uint Remaining,Full;}
    [DllImport("kernel32.dll",ExactSpelling=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool GetSystemPowerStatus(out PowerStatus status);}

