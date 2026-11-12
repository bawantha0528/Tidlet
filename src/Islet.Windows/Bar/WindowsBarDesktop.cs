using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Islet.Core.Bar;
using static Islet.Windows.Bar.BarNative;
namespace Islet.Windows.Bar;
public sealed class WindowsBarDesktop(string recoveryDirectory) : IBarDesktop
{
    public IReadOnlyList<BarDisplay> Displays()
    {
        var result=new List<BarDisplay>();
        bool Callback(nint monitor,nint dc,ref Rect r,nint data)
        {
            var info=new MonitorInfoEx{Size=(uint)Marshal.SizeOf<MonitorInfoEx>()};
            if(GetMonitorInfoExW(monitor,ref info)&&info.Monitor.Right>info.Monitor.Left)
                result.Add(new(info.Device??monitor.ToString(),(info.Flags&1)!=0,ToRect(info.Monitor),ToRect(info.Work)));
            return true;
        }
        if(!EnumDisplayMonitors(0,0,Callback,0))throw new Win32Exception(Marshal.GetLastPInvokeError());
        return result;
    }
    public IBarSurface Attach(nint handle,BarDisplay display,BarSettings settings,bool tray)=>new Surface(handle,display,settings,tray,recoveryDirectory);
    public BarPoint? CursorPosition()=>GetCursorPos(out var point)?new BarPoint(point.X,point.Y):null;
    private static BarRect ToRect(Rect r)=>new(r.Left,r.Top,r.Right,r.Bottom);
    private sealed record Journal(long Window,int Process,long Start,string Display,BarRect Work);
    public string Recover()
    {
        if(!Directory.Exists(recoveryDirectory))return "No stale reservation journal.";
        var count=0;
        foreach(var path in Directory.EnumerateFiles(recoveryDirectory,"*.json"))
        {
            try
            {
                var saved=JsonSerializer.Deserialize<Journal>(File.ReadAllText(path));if(saved is null)continue;
                // A reused/live HWND must never be removed. Dead handles can only identify a stale reservation.
                if(IsWindow(new nint(saved.Window)))continue;
                var data=new AppBar{Size=(uint)Marshal.SizeOf<AppBar>(),Window=new nint(saved.Window)};
                SHAppBarMessage(1,ref data);File.Delete(path);count++;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or OverflowException){ }
        }
        return $"Processed {count} dead-window reservation journals; work-area recovery must be verified against observation.";
    }
    public static (int AppBar,int Tray) NativeSizes=>(Marshal.SizeOf<AppBar>(),Marshal.SizeOf<NotifyIcon>());
    private sealed class Surface : IBarSurface
    {
        private readonly nint _window;private readonly BarDisplay _display;private readonly BarSettings _settings;private readonly string _journal;
        private readonly WinEvent _event;private readonly nint _hook;private readonly BarReservation _reservation;private bool _reserved=>_reservation.Active;private bool _hidden,_disposed,_positioning,_sessionLocked,_screenOff,_lidClosed;private nint _screenPower,_lidPower;
        private NotifyIcon _tray;
        private readonly uint _taskbarCreated=RegisterWindowMessageW("TaskbarCreated");
        public BarPlacement Placement{get;private set;}
        public bool TrayPresent{get;private set;}
        public bool SlowPolling=>GetSystemPowerStatus(out var power)&&power.Saver==1;
        public event Action? EnvironmentChanged;
        public event Action? TrayMenuRequested;
        public Surface(nint window,BarDisplay display,BarSettings settings,bool tray,string directory)
        {
            _window=window;_display=display;_settings=settings;_hidden=settings.TopReveal;
            _reservation=new(()=>{var data=new AppBar{Size=(uint)Marshal.SizeOf<AppBar>(),Window=_window};SHAppBarMessage(1,ref data);});
            _journal=Path.Combine(directory,$"{Environment.ProcessId}-{window.ToInt64():X}.json");
            _event=(_,_,_,_,_,_,_)=>EnvironmentChanged?.Invoke();
            _hook=SetWinEventHook(3,3,0,_event,0,0,2); // foreground, out of context, skip our process
            try
            {
                WTSRegisterSessionNotification(window,0);
                var screen=new Guid("6fe69556-704a-47a0-8f24-c28d936fda47");var lid=new Guid("ba3e0f4d-b817-4094-a2d1-d56379e6a0f3");
                _screenPower=RegisterPowerSettingNotification(window,ref screen,0);_lidPower=RegisterPowerSettingNotification(window,ref lid,0);
                Reposition();if(!_hidden&&settings.Items.Length>0)ShowWindow(_window,4);if(tray)AddTray();
            }
            catch{Dispose();throw;}
        }
        private void AddTray()
        {
            nint icon=0;
            var hasAppIcon=Environment.ProcessPath is{} path&&ExtractIconExW(path,0,0,out icon,1)==1&&icon!=0;
            _tray=new NotifyIcon{Size=(uint)Marshal.SizeOf<NotifyIcon>(),Window=_window,Id=1,Flags=1|2|4,CallbackMessage=0x8401,
                Icon=hasAppIcon?icon:LoadIconW(0,new nint(32512)),Tip=Placement.Notice??"Tidlet monitor — right-click for options",Info="",InfoTitle=""};
            try{TrayPresent=Shell_NotifyIconW(0,ref _tray);}
            finally
            {
                if(icon!=0)DestroyIcon(icon);
                _tray.Icon=0;
            }
        }
        public bool ShouldHide
        {
            get
            {
                if(_sessionLocked||_screenOff||_lidClosed)return true;
                if(SHQueryUserNotificationState(out var state)==0 && state is 2 or 3 or 4)return true;
                var foreground=GetForegroundWindow();if(foreground==0||foreground==GetShellWindow()||foreground==GetDesktopWindow())return false;
                GetWindowThreadProcessId(foreground,out var pid);if(pid==Environment.ProcessId)return false;
                if(!GetWindowRect(foreground,out var r))return false;var b=_display.Bounds;
                return r.Left<=b.Left&&r.Top<=b.Top&&r.Right>=b.Right&&r.Bottom>=b.Bottom;
            }
        }
        public bool IsFullScreenForeground
        {
            get
            {
                if(_sessionLocked||_screenOff||_lidClosed)return false;
                if(SHQueryUserNotificationState(out var state)==0 && state==3)return true; // QUNS_RUNNING_D3D_FULLSCREEN
                var foreground=GetForegroundWindow();if(foreground==0||foreground==GetShellWindow()||foreground==GetDesktopWindow()||IsIconic(foreground))return false;
                GetWindowThreadProcessId(foreground,out var pid);if(pid==Environment.ProcessId)return false;
                var style=IntPtr.Size==4?GetWindowLongW(foreground,-16):GetWindowLongPtrW(foreground,-16).ToInt64();
                if((style&0x00C00000L)==0x00C00000L)return false; // Exclude normal captioned/maximized desktop windows
                if(!GetWindowRect(foreground,out var r))return false;
                var monitorBounds=_display.Bounds;
                var info=new MonitorInfoEx{Size=(uint)Marshal.SizeOf<MonitorInfoEx>()};
                if(GetMonitorInfoExW(VisualHostPlacement.MonitorFromWindow(foreground,2),ref info)&&info.Monitor.Right>info.Monitor.Left)
                    monitorBounds=ToRect(info.Monitor);
                return r.Left<=monitorBounds.Left&&r.Top<=monitorBounds.Top&&r.Right>=monitorBounds.Right&&r.Bottom>=monitorBounds.Bottom;
            }
        }
        public void Reposition()
        {
            if(_disposed||_positioning)return;_positioning=true;
            try
            {
                var scale=Math.Max(1,GetDpiForWindow(_window))/96.0;var b=_display.Bounds;int height=(int)Math.Ceiling(BarLayout.HeightDip(_settings)*scale);
                var rect=new Rect{Left=b.Left,Right=b.Right,Top=_settings.Edge==BarEdge.Top?b.Top:b.Bottom-height,Bottom=_settings.Edge==BarEdge.Top?b.Top+height:b.Bottom};
                string? notice=null;var reserve=_settings.Mode==BarMode.Reserve&&!_settings.TopReveal&&!_hidden&&_settings.VisibleItems.Count>0;
                if(reserve&&_settings.Edge==BarEdge.Top&&TopTaskbar(b)){reserve=false;notice="A taskbar is already at the top; using Overlay.";}
                if(reserve)
                {
                    var data=new AppBar{Size=(uint)Marshal.SizeOf<AppBar>(),Window=_window,Callback=0x8400,Edge=_settings.Edge==BarEdge.Top?1u:3u,Bounds=rect};
                    if(!_reserved)
                    {
                        // Save before registration so an abrupt exit still leaves a recovery identity.
                        Directory.CreateDirectory(Path.GetDirectoryName(_journal)!);
                        using var self=Process.GetCurrentProcess();File.WriteAllText(_journal,JsonSerializer.Serialize(new Journal(_window.ToInt64(),Environment.ProcessId,self.StartTime.ToUniversalTime().Ticks,_display.Id,_display.Work)));
                        _reservation.Acquire(()=>SHAppBarMessage(0,ref data)!=0);
                    }
                    if(_reserved)
                    {
                        SHAppBarMessage(2,ref data);
                        if(_settings.Edge==BarEdge.Top)data.Bounds.Bottom=data.Bounds.Top+height;else data.Bounds.Top=data.Bounds.Bottom-height;
                        SHAppBarMessage(3,ref data);rect=data.Bounds;
                    }
                    else{notice="Windows could not reserve the bar; using Overlay.";File.Delete(_journal);}
                }
                else Release();
                if(!_reserved&&(_settings.Edge==BarEdge.Bottom||_settings.TopReveal))
                {
                    var display=_display;
                    var info=new MonitorInfoEx{Size=(uint)Marshal.SizeOf<MonitorInfoEx>()};
                    if(GetMonitorInfoExW(VisualHostPlacement.MonitorFromWindow(_window,2),ref info))display=display with{Bounds=ToRect(info.Monitor),Work=ToRect(info.Work)};
                    var bottom=BarLayout.CompactOverlay(display,_settings,scale,_settings.Edge);
                    rect=new Rect{Left=bottom.Left,Top=bottom.Top,Right=bottom.Right,Bottom=bottom.Bottom};
                }
                                if(!GetWindowRect(_window,out var old)||old.Left!=rect.Left||old.Top!=rect.Top||old.Right!=rect.Right||old.Bottom!=rect.Bottom)
                    if(!SetWindowPos(_window,new nint(-1),rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top,0x0010|(_hidden?0u:0x0040u)))throw new Win32Exception(Marshal.GetLastPInvokeError());
                Placement=new(ToRect(rect),scale,_reserved?BarMode.Reserve:BarMode.Overlay,notice);
            }
            finally{_positioning=false;}
        }
        private static bool TopTaskbar(BarRect monitor)
        {
            var taskbar=FindWindowW("Shell_TrayWnd",null);
            if(taskbar!=0&&GetWindowRect(taskbar,out var r)&&IntersectsTop(r,monitor))return true;
            nint after=0;while((after=FindWindowExW(0,after,"Shell_SecondaryTrayWnd",null))!=0)
                if(GetWindowRect(after,out r)&&IntersectsTop(r,monitor))return true;
            return false;
        }
        private static bool IntersectsTop(Rect r,BarRect m)=>r.Top<=m.Top&&r.Bottom>m.Top&&r.Bottom<m.Top+m.Height/2&&r.Right>m.Left&&r.Left<m.Right;
        public void SetHidden(bool hidden)
        {
            if(_hidden==hidden)return;_hidden=hidden;Reposition();ShowWindow(_window,hidden?0:4);
        }
        public void HandleMessage(int message,nint wParam,nint lParam)
        {
            if(message==0x8400&&wParam.ToInt32()==1){Reposition();EnvironmentChanged?.Invoke();}
            if(message==0x8401&&((lParam.ToInt64()&0xffff) is 0x0205 or 0x007b or 0x0203))TrayMenuRequested?.Invoke();
            if(message==_taskbarCreated){Release();Reposition();if(TrayPresent)AddTray();}
            if(message==0x02B1){_sessionLocked=wParam.ToInt32()==7?true:wParam.ToInt32()==8?false:_sessionLocked;EnvironmentChanged?.Invoke();}
                        if(message==0x0218&&wParam.ToInt32()==0x8013&&lParam!=0)
            {
                var guid=Marshal.PtrToStructure<Guid>(lParam);var length=Marshal.ReadInt32(lParam,16);
                if(length==4)
                {
                    var value=Marshal.ReadInt32(lParam,20);
                    if(guid==new Guid("6fe69556-704a-47a0-8f24-c28d936fda47"))_screenOff=value==0;
                    if(guid==new Guid("ba3e0f4d-b817-4094-a2d1-d56379e6a0f3"))_lidClosed=value==0;
                    EnvironmentChanged?.Invoke();
                }
            }
            if(message is 0x007E or 0x001A){if(_settings.Edge==BarEdge.Bottom||_settings.TopReveal)Reposition();EnvironmentChanged?.Invoke();}
            if(message==0x0047&&_reserved){var d=new AppBar{Size=(uint)Marshal.SizeOf<AppBar>(),Window=_window};SHAppBarMessage(9,ref d);}
        }
        public int Menu(string? module,bool paused)
        {
            var menu=CreatePopupMenu();if(menu==0)return 0;
            try
            {
                AppendMenuW(menu,0,100,"Open Dashboard");AppendMenuW(menu,0,101,"Settings");AppendMenuW(menu,0x800,0,null);
                Add(1,paused?"Resume monitoring":"Pause monitoring");if(!_settings.GamingMode){if(module is not null)Add(2,$"Hide {module}");Add(3,"Restore default items");}
                Add(20,"Gaming Mode",_settings.GamingMode);
                Add(21,"Auto Gaming Mode",_settings.GamingModeAuto);
                Add(10,"Overlay mode",_settings.Mode==BarMode.Overlay);Add(11,"Reserve work area",_settings.Mode==BarMode.Reserve);
                Add(12,"Top edge",_settings.Edge==BarEdge.Top&&!_settings.TopReveal);Add(19,"Top · reveal on hover",_settings.TopReveal);Add(13,"Bottom · above taskbar",_settings.Edge==BarEdge.Bottom);
                Add(14,"Align left",_settings.Alignment==BarAlignment.Left);Add(15,"Align center",_settings.Alignment==BarAlignment.Center);Add(16,"Align right",_settings.Alignment==BarAlignment.Right);
                Add(17,"Primary monitor",_settings.Monitors==MonitorSelection.Primary);Add(18,"All monitors",_settings.Monitors==MonitorSelection.All);
                AppendMenuW(menu,0x800,0,null);Add(99,"Exit Tidlet");
                GetCursorPos(out var point);SetForegroundWindow(_window);
                return (int)TrackPopupMenuEx(menu,0x0100|0x0002,point.X,point.Y,_window,0);
                void Add(uint id,string title,bool selected=false)=>AppendMenuW(menu,selected?8u:0u,id,title);
            }
            finally{DestroyMenu(menu);PostMessageW(_window,0,0,0);}
        }
        private void Release()
        {
            _reservation.Dispose();
            try{File.Delete(_journal);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;Release();if(TrayPresent){TrayPresent=!Shell_NotifyIconW(2,ref _tray);}
            if(_hook!=0)UnhookWinEvent(_hook);WTSUnRegisterSessionNotification(_window);if(_screenPower!=0)UnregisterPowerSettingNotification(_screenPower);if(_lidPower!=0)UnregisterPowerSettingNotification(_lidPower);
        }
    }
}






