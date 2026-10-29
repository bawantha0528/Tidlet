using System.ComponentModel;
using System.Runtime.InteropServices;
using Islet.Core.Island;
using static Islet.Windows.Native.NativeMethods;

namespace Islet.Windows.Native;

public sealed partial class NativeIslandDesktop : IInteractiveIslandDesktop
{
    private readonly WindowProc _procedure;
    private readonly DibRenderer _renderer = new();
    private readonly HashSet<IslandTimer> _timers = new();
    private readonly string _className = "TidletMonitor." + Guid.NewGuid().ToString("N");
    private nint _window, _instance, _classNameBuffer;
    private bool _registered, _disposed;
    public string? Failure { get; private set; }
    public int RenderCount => _renderer.FrameCount;
    public int ActiveTimerCount => _timers.Count;
    public static void ShowStartupError(string reason) => MessageBoxW(0, "Tidlet Monitor could not start. " + reason, "Tidlet Monitor", 0x10);
    public event Action<IslandInput>? Input;

    public NativeIslandDesktop() => _procedure = WindowProcedure;

    public void Create()
    {
        _instance = GetModuleHandleW(null);
        if (_instance == 0) throw Error("Locate application module");
        _classNameBuffer = Marshal.StringToHGlobalUni(_className);
        var cls = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Instance = _instance,
            Proc = Marshal.GetFunctionPointerForDelegate(_procedure), ClassName = _classNameBuffer };
        if (RegisterClassExW(ref cls) == 0) throw Error("Register island window");
        _registered = true;
        _window = CreateWindowExW(0x08000000 | 0x80 | 0x20 | 0x80000, _className, "Tidlet Monitor", 0x80000000,
            0, 0, 1, 1, 0, 0, _instance, 0);
        if (_window == 0) throw Error("Create island window");
        var monitor = MonitorFromWindow(0, 1);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfoW(monitor, ref info)) throw Error("Read primary display");
        if (!SetWindowPos(_window, -1, info.Monitor.Left, info.Monitor.Top, 1, 1, 0x10))
            throw Error("Place native island");
    }

    public IslandDisplay ReadDisplay() => _recoveryEnabled && _selectedDisplay is not null ? _selectedDisplay : ReadMinimalDisplay();

    private IslandDisplay ReadMinimalDisplay()
    {
        var monitor = MonitorFromWindow(_window, 2);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfoW(monitor, ref info)) throw Error("Read island display");
        var dpi = GetDpiForWindow(_window);
        if (dpi == 0) throw new InvalidOperationException("Windows did not provide display DPI.");
        return new("primary", Convert(info.Monitor), Convert(info.Work), dpi);
    }

    public void Render(IslandFrame frame)
    {
        _renderer.Draw(_window, frame);
        var caption = "Tidlet Monitor — " + frame.Label + ". " + frame.Explanation;
        if (_recoveryEnabled && caption != _caption)
        {
            if (!SetWindowTextW(_window, caption)) Warn("The accessible window caption could not be refreshed.");
            _caption = caption;
        }
    }
    public void SaveBitmap(string path) => _renderer.SaveBitmap(path);

    public void Show(bool visible)
    {
        // First call may honor the launcher's hidden STARTUPINFO. A second SW_SHOWNOACTIVATE
        // explicitly sets the intended visible state, without activating the island.
        ShowWindow(_window, visible ? 4 : 0);
        if (visible && !IsWindowVisible(_window)) ShowWindow(_window, 4);
    }

    public void SetTimer(IslandTimer timer, int? milliseconds)
    {
        if (milliseconds is null)
        {
            if (_timers.Remove(timer)) KillTimer(_window, (nuint)timer);
            return;
        }
        if (milliseconds < 10) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        if (NativeMethods.SetTimer(_window, (nuint)timer, (uint)milliseconds.Value, 0) == 0)
            throw Error("Schedule island update");
        _timers.Add(timer);
    }

    public NativeIslandStatus Observe()
    {
        var style = IntPtr.Size == 8 ? GetWindowLongPtrW(_window, -20).ToInt64() : GetWindowLongW(_window, -20);
        var error = Marshal.GetLastPInvokeError();
        if (style == 0 && error != 0) throw new Win32Exception(error, "Read island styles");
        if (!GetWindowRect(_window, out var rect)) throw Error("Read island bounds");
        return new(IsWindowVisible(_window), (style & 0x80000) != 0, (style & 0x08000000) != 0,
            (style & 0x80) != 0, (style & 0x20) != 0, GetForegroundWindow() == _window,
            GetDpiForWindow(_window), Convert(rect), _timers.Count, _renderer.FrameCount);
    }

    public void Close()
    {
        foreach (var timer in _timers.ToArray()) SetTimer(timer, null);
        if (_window != 0 && !DestroyWindow(_window)) throw Error("Close island window");
        _window = 0;
    }

    public int Run()
    {
        while (true)
        {
            var result = GetMessageW(out var message, 0, 0, 0);
            if (result == -1) throw Error("Read desktop messages");
            if (result == 0) return Failure is null ? 0 : 1;
            TranslateMessage(ref message);
            DispatchMessageW(ref message);
        }
    }

    private nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        try
        {
            if (HandleShellMessage(hwnd, message, wParam, lParam, out var handledResult)) return handledResult;
            switch (message)
            {
                case 0x113:
                    if (_timers.Contains((IslandTimer)wParam)) Input?.Invoke(new(IslandInputKind.Timer, (int)wParam));
                    return 0;
                case 0x10: Input?.Invoke(new(IslandInputKind.Close)); return 0;
                case 0x2: CleanupRecovery(); PostQuitMessage(Failure is null ? 0 : 1); return 0;
                case 0x21: return 3; // MA_NOACTIVATE
                case 0x14: return 1; // Entire layered surface is supplied by UpdateLayeredWindow.
            }
            return DefWindowProcW(hwnd, message, wParam, lParam);
        }
        catch (Exception ex)
        {
            // Never unwind a managed exception across a reverse P/Invoke boundary.
            Failure = ex.Message;
            PostQuitMessage(1);
            return 0;
        }
    }

    private static PixelRect Convert(Rect r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    private static Win32Exception Error(string action) => new(Marshal.GetLastPInvokeError(), action);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CleanupRecovery();
        if (_window != 0)
        {
            foreach (var timer in _timers.ToArray()) SetTimer(timer, null);
            DestroyWindow(_window);
            _window = 0;
        }
        _renderer.Dispose();
        if (_registered) UnregisterClassW(_className, _instance);
        if (_classNameBuffer != 0) Marshal.FreeHGlobal(_classNameBuffer);
        _classNameBuffer = 0;
        GC.KeepAlive(_procedure);
    }
}
