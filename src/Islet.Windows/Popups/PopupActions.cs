using System.Diagnostics;
using Islet.Core.Bar;
namespace Islet.Windows.Popups;
public static class PopupActions
{
    public static Islet.Core.Bar.BarRect Bounds(nint window)
    {if(!Islet.Windows.Bar.BarNative.GetWindowRect(window,out var rect))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError());return new(rect.Left,rect.Top,rect.Right,rect.Bottom);}
    public static void DashboardPosition(nint hwnd,BarRect rect,bool activate)
    {
        if(!Islet.Windows.Bar.BarNative.SetWindowPos(hwnd,new nint(-2),rect.Left,rect.Top,rect.Width,rect.Height,0x0010|0x0040))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError());
        if(activate)Islet.Windows.Bar.BarNative.SetForegroundWindow(hwnd);
    }
    public static void DesktopWidgetPosition(nint hwnd,BarRect rect,bool show)
    {
        const uint noActivate=0x0010, noZOrder=0x0004, showWindow=0x0040, hideWindow=0x0080;
        if(!Islet.Windows.Bar.BarNative.SetWindowPos(hwnd,0,rect.Left,rect.Top,rect.Width,rect.Height,
            noActivate|noZOrder|(show?showWindow:hideWindow)))
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError());
    }
    public static void Position(nint hwnd,BarRect rect,bool activate)
    {
        if(!Islet.Windows.Bar.BarNative.SetWindowPos(hwnd,new nint(-1),rect.Left,rect.Top,rect.Width,rect.Height,0x0010|0x0040))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError());
        if(activate)Islet.Windows.Bar.BarNative.SetForegroundWindow(hwnd);
    }
    public static void TaskManager()
    {
        var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"Taskmgr.exe");
        try{Process.Start(new ProcessStartInfo(path){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(path)!});}
        catch(Exception ex)when(ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {Islet.Windows.Bar.BarNative.MessageBoxW(0,"Windows could not open Task Manager. No system settings were changed.","Tidlet",0x40);}
    }
}

