using System.ComponentModel;
using System.Runtime.InteropServices;
using Islet.Core.Island;
using Islet.Core.Presentation;
using Islet.Core.Settings;
using static Islet.Windows.Native.NativeMethods;
namespace Islet.Windows.Native;

/// <summary>One deliberately focusable application window. It never changes the ambient window's activation styles.</summary>
internal sealed class NativeMonitorWindow : IDisposable
{
    private readonly WindowProc _procedure;
    private readonly Action<MonitorWindowEvent> _input;
    private readonly string _className="TidletDashboard."+Guid.NewGuid().ToString("N");
    private nint _window,_instance,_name;
    private bool _registered,_drawing,_disposed;
    private DibRenderer? _renderer;
    private MonitorDashboard? _dashboard;
    private double _scale=1;
    private int _scroll;
    public bool Visible=>_window!=0&&IsWindowVisible(_window)&&!IsIconic(_window);
    public nint Handle=>_window;
    public int RenderCount{get;private set;}
    public string? Failure{get;private set;}
    public NativeMonitorWindow(Action<MonitorWindowEvent> input){_input=input;_procedure=Procedure;}
    public void Show(MonitorDashboard dashboard,IslandDisplay display,bool activate)
    {
        _dashboard=dashboard with{Surface=MonitorSurface.Application,ControlCenter=null};
        if(_window==0)
        {
            _instance=GetModuleHandleW(null);_name=Marshal.StringToHGlobalUni(_className);
            var cls=new WindowClass{Size=(uint)Marshal.SizeOf<WindowClass>(),Proc=Marshal.GetFunctionPointerForDelegate(_procedure),Instance=_instance,ClassName=_name,Cursor=LoadCursorW(0,32512),Icon=LoadIconW(0,32512)};
            if(RegisterClassExW(ref cls)==0)throw Error("Register dashboard window");_registered=true;
            var scale=DashboardLayout.FitScale(980,680,display.Dpi/96.0,Math.Max(1,display.WorkArea.Width-24),Math.Max(1,display.WorkArea.Height-24));
            var width=(int)(980*scale);var height=(int)(680*scale);
            _window=CreateWindowExW(0x00080000|0x00040000,_className,"Tidlet Monitor",0x800E0000,
                display.WorkArea.X+(display.WorkArea.Width-width)/2,display.WorkArea.Y+(display.WorkArea.Height-height)/2,width,height,0,0,_instance,0);
            if(_window==0)throw Error("Create dashboard window");
        }
        Render();ShowWindow(_window,activate?5:4);if(!IsWindowVisible(_window))ShowWindow(_window,activate?5:4);
        if(activate)SetForegroundWindow(_window);
        NotifyVisibility();
    }
    public void Update(MonitorDashboard dashboard)
    {
        if(_dashboard?.Page!=dashboard.Page)_scroll=0;
        _dashboard=dashboard with{Surface=MonitorSurface.Application,ControlCenter=null};
        if(Visible)Render();
    }
    public void Hide()
    {
        if(_window!=0)ShowWindow(_window,0);
        _renderer?.Dispose();_renderer=null;_scroll=0;NotifyVisibility();
    }
    public void SaveBitmap(string path)=> (_renderer??throw new InvalidOperationException("Dashboard has no rendered buffer.")).SaveBitmap(path);
    private void NotifyVisibility()=>_input(new(MonitorWindowEventKind.VisibilityChanged,Visible?1:0));
    private void Render()
    {
        if(_drawing||_window==0||_dashboard is null||IsIconic(_window))return;
        _drawing=true;
        try
        {
            if(!GetWindowRect(_window,out var rect))throw Error("Read dashboard bounds");
            var width=rect.Right-rect.Left;var height=rect.Bottom-rect.Top;
            _scale=Math.Min(Math.Max(1,GetDpiForWindow(_window))/96.0,Math.Min(width/720.0,height/560.0));
            var maxContentH = _dashboard.Page switch
            {
                MonitorPage.Overview => 620,
                MonitorPage.Cpu => 860,
                MonitorPage.Network => 880,
                _ => 780
            };
            _scroll=ModulePanelLayout.ClampScroll(_scroll,maxContentH,Math.Max(1,(int)(height/_scale)-80));
            _renderer??=new DibRenderer();
            _renderer.Draw(_window,new(IslandStyle.Island,IslandState.Expanded,new(rect.Left,rect.Top,width,height),_scale,"Dashboard","Tidlet Monitor",Array.Empty<double?>(),Dashboard:_dashboard with{ScrollOffset=_scroll}));
            RenderCount++;
            var pIndex = (int)_dashboard.Page;
            var pTitle = pIndex < DashboardLayout.PageNames.Length ? DashboardLayout.PageNames[pIndex] : _dashboard.Page.ToString();
            SetWindowTextW(_window,"Tidlet Monitor — "+pTitle);
        }
        finally{_drawing=false;}
    }
    private nint Procedure(nint hwnd,uint message,nuint wParam,nint lParam)
    {
        try
        {
            switch(message)
            {
                case 0x10:Hide();return 0;
                case 0x201:
                    if(_dashboard is null)return 0;
                    var x=unchecked((short)(lParam.ToInt64()&65535))/_scale;var y=unchecked((short)((lParam.ToInt64()>>16)&65535))/_scale;
                    if(!GetWindowRect(hwnd,out var bounds))return 0;
                    var command=DesktopDashboardLayout.HitTest(x,y,(bounds.Right-bounds.Left)/_scale,(bounds.Bottom-bounds.Top)/_scale,_dashboard);
                    if(command==DesktopDashboardLayout.CloseApplication)Hide();
                    else if(command==DesktopDashboardLayout.MinimizeApplication){ShowWindow(hwnd,6);NotifyVisibility();}
                    else if(command!=0)_input(new(MonitorWindowEventKind.Command,command));
                    return 0;
                case 0x20A:
                    _scroll+=unchecked((short)((wParam.ToUInt64()>>16)&65535))>0?-72:72;Render();return 0;
                case 0x100:
                    var key=(int)wParam;
                    if(key==27){Hide();return 0;}
                    if(key is >=49 and <=56){_input(new(MonitorWindowEventKind.Command,400+key-49));return 0;}
                    if(key is 38 or 40 && _dashboard is not null){_input(new(MonitorWindowEventKind.Command,400+((int)_dashboard.Page+(key==40?1:DesktopDashboardLayout.SidebarPages.Length-1))%DesktopDashboardLayout.SidebarPages.Length));return 0;}
                    if(key is 33 or 34){_scroll+=key==33?-240:240;Render();return 0;}
                    break;
                case 0x5: if(_window!=0){if(wParam==1)NotifyVisibility();else{Render();NotifyVisibility();}}return 0;
                case 0x2E0:
                    var suggested=Marshal.PtrToStructure<Rect>(lParam);
                    SetWindowPos(hwnd,0,suggested.Left,suggested.Top,suggested.Right-suggested.Left,suggested.Bottom-suggested.Top,0x14);Render();return 0;
                case 0x24:
                    var limits=Marshal.PtrToStructure<MinMax>(lParam);var work=WorkArea(hwnd);
                    limits.MinTrack=new(Math.Min((int)(720*_scale),work.Right-work.Left-20),Math.Min((int)(560*_scale),work.Bottom-work.Top-20));
                    limits.MaxTrack=new(work.Right-work.Left,work.Bottom-work.Top);Marshal.StructureToPtr(limits,lParam,false);return 0;
                case 0x84:
                    if(!GetWindowRect(hwnd,out var r))break;
                    var sx=unchecked((short)(lParam.ToInt64()&65535))-r.Left;var sy=unchecked((short)((lParam.ToInt64()>>16)&65535))-r.Top;
                    var edge=(int)Math.Ceiling(6*_scale);var left=sx<edge;var right=sx>=r.Right-r.Left-edge;var top=sy<edge;var bottom=sy>=r.Bottom-r.Top-edge;
                    if(top)return left?13:right?14:12;if(bottom)return left?16:right?17:15;if(left)return 10;if(right)return 11;
                    if(sy<65*_scale&&sx>190*_scale&&sx<(r.Right-r.Left)-185*_scale)return 2;
                    return 1;
                case 0x14:return 1;
            }
            return DefWindowProcW(hwnd,message,wParam,lParam);
        }
        catch(Exception ex)when(ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {
            Failure=ex.Message;
            // A failed auxiliary window is isolated; ambient monitoring remains alive.
            if(_window!=0)ShowWindow(_window,0);_renderer?.Dispose();_renderer=null;
            _input(new(MonitorWindowEventKind.VisibilityChanged,0));return 0;
        }
    }
    private static Rect WorkArea(nint window)
    {var monitor=MonitorFromWindow(window,2);var info=new MonitorInfo{Size=(uint)Marshal.SizeOf<MonitorInfo>()};if(!GetMonitorInfoW(monitor,ref info))throw Error("Read dashboard monitor");return info.Work;}
    [StructLayout(LayoutKind.Sequential)] private struct MinMax{public Point Reserved,MaxSize,MaxPosition,MinTrack,MaxTrack;}
    private static Win32Exception Error(string text)=>new(Marshal.GetLastPInvokeError(),text);
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_renderer?.Dispose();_renderer=null;
        if(_window!=0){DestroyWindow(_window);_window=0;}
        if(_registered)UnregisterClassW(_className,_instance);
        if(_name!=0)Marshal.FreeHGlobal(_name);GC.KeepAlive(_procedure);
    }
}