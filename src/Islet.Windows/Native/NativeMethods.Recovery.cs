using System.Runtime.InteropServices;

namespace Islet.Windows.Native;

internal static partial class NativeMethods
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal delegate bool MonitorEnum(nint monitor, nint dc, ref Rect rect, nint data);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void WinEvent(nint hook, uint eventId, nint hwnd, int objectId, int childId, uint thread, uint time);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfoEx
    {
        public uint Size;
        public Rect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string? Device;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NotifyIcon
    {
        public uint Size;
        public nint Window;
        public uint Id, Flags, CallbackMessage;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct HighContrast { public uint Size, Flags; public nint DefaultScheme; }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorEnum callback, nint data);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetMonitorInfoExW(nint monitor, ref MonitorInfoEx info);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] internal static extern nint SetWindowLongPtrW(nint hwnd, int index, nint value);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] internal static extern int SetWindowLongW(nint hwnd, int index, int value);
    [DllImport("shell32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool Shell_NotifyIconW(uint message, ref NotifyIcon data);
    [DllImport("shell32.dll", ExactSpelling = true)] internal static extern int SHQueryUserNotificationState(out int state);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern nint LoadIconW(nint instance, nint resource);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool AppendMenuW(nint menu, uint flags, nuint id, string? label);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint parameters);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern nint GetShellWindow();
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern nint GetDesktopWindow();
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] internal static extern uint RegisterWindowMessageW(string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowTextW(nint hwnd, string text);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern nint SetWinEventHook(uint min, uint max, nint module, WinEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PostMessageW(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("wtsapi32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool WTSRegisterSessionNotification(nint hwnd, uint flags);
    [DllImport("wtsapi32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool WTSUnRegisterSessionNotification(nint hwnd);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] internal static extern nint RegisterPowerSettingNotification(nint recipient, ref Guid guid, uint flags);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterPowerSettingNotification(nint notification);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ReadAnimationPreference(uint action, uint parameter, out int enabled, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ReadHighContrast(uint action, uint parameter, ref HighContrast value, uint flags);
    [DllImport("user32.dll", ExactSpelling = true)] internal static extern uint GetGuiResources(nint process, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] internal static extern int MessageBoxW(nint owner, string text, string caption, uint type);
}
