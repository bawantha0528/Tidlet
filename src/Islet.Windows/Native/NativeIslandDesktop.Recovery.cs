using System.ComponentModel;
using System.Runtime.InteropServices;
using Islet.Core.Island;
using static Islet.Windows.Native.NativeMethods;

namespace Islet.Windows.Native;

public sealed partial class NativeIslandDesktop
{
    private const uint TrayMessage = 0x8001, ForegroundMessage = 0x8002, PlacementMessage = 0x8003;
    private const int HotkeyId = 0x5449;
    private static readonly Guid DisplayPowerGuid = new("6FE69556-704A-47A0-8F24-C28D936FDA47");
    private readonly List<string> _warnings = new();
    private readonly Dictionary<string, nint> _monitorHandles = new(StringComparer.Ordinal);
    private IReadOnlyList<IslandDisplay> _displays = Array.Empty<IslandDisplay>();
    private IslandDisplay? _selectedDisplay;
    private string? _preferredDisplayId;
    private WinEvent? _eventCallback;
    private nint _foregroundHook, _locationHook, _powerRegistration;
    private uint _taskbarCreated;
    private int _eventPending;
    private bool _recoveryEnabled, _trayAdded, _hotkeyRegistered, _sessionRegistered, _observing;
    private bool _displayOff, _sessionLocked, _suspended;
    private string? _caption;
    public RecoveryStatus Recovery { get; private set; } = new(false, false, false, false, Array.Empty<string>());
    public int NotificationQueries { get; private set; }
    public int? NotificationQueryError { get; private set; }
    public int ForegroundSignals { get; private set; }

    public void EnableRecovery()
    {
        if (_recoveryEnabled) return;
        _recoveryEnabled = true;
        RefreshDisplays();
        SelectDisplay(_displays.First().Id);
        ReadPreferences();
        _taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
        if (_taskbarCreated == 0) Warn("Explorer restart notifications are unavailable.");
        TryAddTray();
        _hotkeyRegistered = RegisterHotKey(_window, HotkeyId, 0x4003, 0x49);
        if (!_hotkeyRegistered) Warn("Ctrl+Alt+I is already in use or unavailable; use the tray menu.");
        _sessionRegistered = WTSRegisterSessionNotification(_window, 0);
        if (!_sessionRegistered) Warn("Direct session-lock notifications are unavailable; Windows policy checks remain active.");
        var guid = DisplayPowerGuid;
        _powerRegistration = RegisterPowerSettingNotification(_window, ref guid, 0);
        if (_powerRegistration == 0) Warn("Display-power notifications are unavailable.");
        UpdateRecoveryStatus();
        if (!_trayAdded && !_hotkeyRegistered)
            throw new InvalidOperationException("Neither tray access nor Ctrl+Alt+I is available. Tidlet stopped so it cannot leave an unreachable window.");
    }

    private void ReadPreferences()
    {
        var animationKnown = ReadAnimationPreference(0x1042, 0, out var animation, 0);
        var contrast = new HighContrast { Size = (uint)Marshal.SizeOf<HighContrast>() };
        var contrastKnown = ReadHighContrast(0x42, contrast.Size, ref contrast, 0);
        Recovery = Recovery with { ReducedMotion = animationKnown && animation == 0,
            HighContrast = contrastKnown && (contrast.Flags & 1) != 0 };
    }

    private void UpdateRecoveryStatus() => Recovery = Recovery with
    { TrayAvailable = _trayAdded, HotkeyAvailable = _hotkeyRegistered, Warnings = _warnings.ToArray() };

    private void Warn(string message)
    {
        if (_warnings.Count < 16 && !_warnings.Contains(message, StringComparer.Ordinal)) _warnings.Add(message);
        UpdateRecoveryStatus();
    }

    private NotifyIcon TrayData() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIcon>(), Window = _window, Id = 1, Flags = 1 | 2 | 4 | 0x80,
        CallbackMessage = TrayMessage, Icon = LoadIconW(0, 32512), Tip = "Tidlet Monitor — CPU\nClick to open · Right-click for options",
        Info = string.Empty, InfoTitle = string.Empty, Version = 4
    };

    private void TryAddTray()
    {
        var data = TrayData();
        if (data.Icon == 0) { Warn("The tray icon image is unavailable."); return; }
        _trayAdded = Shell_NotifyIconW(0, ref data);
        if (_trayAdded && !Shell_NotifyIconW(4, ref data))
        {
            Shell_NotifyIconW(2, ref data);
            _trayAdded = false;
        }
        if (!_trayAdded) Warn("The notification area is unavailable; use Ctrl+Alt+I.");
        UpdateRecoveryStatus();
    }

    private void RefreshDisplays()
    {
        var displays = new List<(IslandDisplay Display, nint Handle, bool Primary)>();
        Exception? failure = null;
        MonitorEnum callback = (nint monitor, nint dc, ref Rect rect, nint data) =>
        {
            try
            {
                var info = new MonitorInfoEx { Size = (uint)Marshal.SizeOf<MonitorInfoEx>(), Device = string.Empty };
                if (!GetMonitorInfoExW(monitor, ref info)) throw Error("Read monitor information");
                if (string.IsNullOrWhiteSpace(info.Device) || info.Monitor.Right <= info.Monitor.Left || info.Monitor.Bottom <= info.Monitor.Top) return true;
                var dpi = monitor == MonitorFromWindow(_window, 2) ? GetDpiForWindow(_window) : 96;
                displays.Add((new(info.Device, Convert(info.Monitor), Convert(info.Work), dpi == 0 ? 96 : dpi), monitor, (info.Flags & 1) != 0));
                return true;
            }
            catch (Exception ex) { failure = ex; return false; }
        };
        var result = EnumDisplayMonitors(0, 0, callback, 0);
        GC.KeepAlive(callback);
        if (failure is not null) throw new InvalidOperationException("Windows could not enumerate displays.", failure);
        if (!result || displays.Count == 0) throw new InvalidOperationException("Windows did not provide an active display.");
        var ordered = displays.OrderByDescending(d => d.Primary).ThenBy(d => d.Display.Bounds.X).ToArray();
        _monitorHandles.Clear();
        foreach (var item in ordered) _monitorHandles[item.Display.Id] = item.Handle;
        _displays = ordered.Select(d => d.Display).ToArray();
    }

    public IReadOnlyList<IslandDisplay> ReadDisplays() => _displays;

    public void SelectDisplay(string id)
    {
        _preferredDisplayId = id;
        var target = _displays.FirstOrDefault(d => d.Id == id) ?? _displays.First();
        var handle = _monitorHandles[target.Id];
        if (MonitorFromWindow(_window, 2) != handle)
        {
            if (!SetWindowPos(_window, -1, target.Bounds.X, target.Bounds.Y, 1, 1, 0x10)) throw Error("Move island to selected display");
        }
        var dpi = GetDpiForWindow(_window);
        _selectedDisplay = target with { Dpi = dpi == 0 ? target.Dpi : dpi };
    }

    public PointerSnapshot ReadPointer()
    {
        if (!GetCursorPos(out var point)) return new(false, 0, 0, false, false);
        var primaryButton = GetSystemMetrics(23) != 0 ? 2 : 1;
        return new(true, point.X, point.Y, GetAsyncKeyState(primaryButton) < 0, GetAsyncKeyState(0x1B) < 0);
    }

    public void SetClickThrough(bool enabled)
    {
        var current = IntPtr.Size == 8 ? GetWindowLongPtrW(_window, -20).ToInt64() : GetWindowLongW(_window, -20);
        var error = Marshal.GetLastPInvokeError();
        if (current == 0 && error != 0) throw new Win32Exception(error, "Read input style");
        var desired = enabled ? current | 0x20L : current & ~0x20L;
        if (desired == current) return;
        var previous = IntPtr.Size == 8 ? SetWindowLongPtrW(_window, -20, new nint(desired)).ToInt64()
            : SetWindowLongW(_window, -20, unchecked((int)desired));
        error = Marshal.GetLastPInvokeError();
        if (previous == 0 && error != 0) throw new Win32Exception(error, "Change island input style");
        if (!SetWindowPos(_window, 0, 0, 0, 0, 0, 0x37)) throw Error("Refresh island input style");
    }

    public DesktopSuppression ReadSuppression()
    {
        if (_displayOff || _sessionLocked || _suspended)
            return new(false, true, _suspended ? "Windows is suspending." : _displayOff ? "The display is off." : "This session is locked.");
        NotificationQueries++;
        var result = SHQueryUserNotificationState(out var state);
        NotificationQueryError = result < 0 ? result : null;
        if (result >= 0 && state is 1 or 2 or 3 or 4)
            return new(true, false, "Windows is keeping this desktop quiet or a fullscreen application is active.");
        var foreground = GetForegroundWindow();
        if (foreground == 0 || foreground == _window || foreground == GetShellWindow() || foreground == GetDesktopWindow())
            return new(false, false, null);
        if (!GetWindowRect(foreground, out var rect)) return new(false, false, null);
        var monitor = MonitorFromWindow(foreground, 2);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfoW(monitor, ref info)) return new(false, false, null);
        var style = IntPtr.Size == 8 ? GetWindowLongPtrW(foreground, -16).ToInt64() : GetWindowLongW(foreground, -16);
        var fullscreen = FullscreenGeometry.CoversMonitor(Convert(rect), Convert(info.Monitor),
            (style & 0x00C00000) != 0, IsIconic(foreground), false);
        return new(fullscreen, false, fullscreen ? "A borderless fullscreen window is active." : null);
    }

    public void ObserveForeground(bool enabled)
    {
        if (!_recoveryEnabled || enabled == _observing) return;
        _observing = enabled;
        if (enabled)
        {
            _eventCallback ??= OnWinEvent;
            _foregroundHook = SetWinEventHook(3, 3, 0, _eventCallback, 0, 0, 2);
            _locationHook = SetWinEventHook(0x800B, 0x800B, 0, _eventCallback, 0, 0, 2);
            if (_foregroundHook == 0 || _locationHook == 0) Warn("Some fullscreen events are unavailable; periodic checks remain active.");
        }
        else
        {
            if (_foregroundHook != 0) UnhookWinEvent(_foregroundHook);
            if (_locationHook != 0) UnhookWinEvent(_locationHook);
            _foregroundHook = _locationHook = 0;
        }
    }

    private void OnWinEvent(nint hook, uint eventId, nint hwnd, int objectId, int childId, uint thread, uint time)
    {
        try
        {
            if (!_observing || _window == 0) return;
            if (eventId == 0x800B && (objectId != 0 || childId != 0 || hwnd != GetForegroundWindow())) return;
            if (Interlocked.Exchange(ref _eventPending, 1) == 0 && !PostMessageW(_window, ForegroundMessage, 0, 0))
                Interlocked.Exchange(ref _eventPending, 0);
        }
        catch { /* One-second reconciliation remains available; never unwind a native callback. */ }
    }

    public int OpenMenu(IReadOnlyList<IslandMenuItem> items)
    {
        var menu = CreatePopupMenu();
        if (menu == 0) throw new InvalidOperationException("Windows could not create the options menu.");
        var previousForeground = GetForegroundWindow();
        try
        {
            foreach (var item in items)
            {
                var flags = item.Id == 0 ? 0x800u : (item.Checked ? 8u : 0) | (item.Enabled ? 0u : 1u);
                if (!AppendMenuW(menu, flags, (nuint)item.Id, item.Id == 0 ? null : item.Label))
                    throw new InvalidOperationException("Windows could not add an options menu item.");
            }
            if (!GetCursorPos(out var point)) return 0;
            // Explicit user menu invocation, never ambient transitions.
            SetForegroundWindow(_window);
            var selected = TrackPopupMenuEx(menu, 0x100 | 0x80 | 0x2, point.X, point.Y, _window, 0);
            PostMessageW(_window, 0, 0, 0);
            return (int)selected;
        }
        finally
        {
            DestroyMenu(menu);
            if (GetForegroundWindow() == _window && previousForeground != 0) SetForegroundWindow(previousForeground);
        }
    }

    private bool HandleShellMessage(nint hwnd, uint message, nuint wParam, nint lParam, out nint result)
    {
        result = 0;
        if (!_recoveryEnabled) return false;
        if (_taskbarCreated != 0 && message == _taskbarCreated)
        { _trayAdded = false; TryAddTray(); return true; }
        switch (message)
        {
            case TrayMessage:
                var action = (uint)(lParam.ToInt64() & 0xFFFF);
                if (action is 0x7B or 0x401) Input?.Invoke(new(IslandInputKind.ContextMenu));
                else if (action == 0x400) Input?.Invoke(new(IslandInputKind.Toggle));
                return true;
            case 0x312:
                if ((int)wParam == HotkeyId) Input?.Invoke(new(IslandInputKind.Toggle));
                return true;
            case 0x201: Input?.Invoke(new(IslandInputKind.LeftClick)); return true;
            case 0x7B: Input?.Invoke(new(IslandInputKind.ContextMenu)); return true;
            case ForegroundMessage:
                Interlocked.Exchange(ref _eventPending, 0);
                ForegroundSignals++;
                Input?.Invoke(new(IslandInputKind.PowerChanged));
                return true;
            case 0x2E0:
                if (lParam != 0)
                {
                    var suggested = Marshal.PtrToStructure<Rect>(lParam);
                    if (!SetWindowPos(_window, -1, suggested.Left, suggested.Top,
                        Math.Max(1, suggested.Right - suggested.Left), Math.Max(1, suggested.Bottom - suggested.Top), 0x10))
                        throw Error("Apply changed DPI bounds");
                }
                if (_selectedDisplay is not null) _selectedDisplay = _selectedDisplay with { Dpi = (uint)(wParam & 0xFFFF) };
                PostMessageW(_window, PlacementMessage, 0, 0);
                return true;
            case 0x7E: // WM_DISPLAYCHANGE
                RefreshDisplays();
                SelectDisplay(_preferredDisplayId ?? _displays.First().Id);
                Input?.Invoke(new(IslandInputKind.DisplayChanged));
                return true;
            case 0x1A: // WM_SETTINGCHANGE, including work area and accessibility.
                ReadPreferences();
                RefreshDisplays();
                SelectDisplay(_preferredDisplayId ?? _displays.First().Id);
                Input?.Invoke(new(IslandInputKind.DisplayChanged));
                return true;
            case PlacementMessage:
                Input?.Invoke(new(IslandInputKind.DpiChanged));
                return true;
            case 0x2B1: // WM_WTSSESSION_CHANGE
                if (wParam == 7) _sessionLocked = true;
                if (wParam == 8) _sessionLocked = false;
                Input?.Invoke(new(IslandInputKind.PowerChanged));
                return true;
            case 0x218: // WM_POWERBROADCAST
                if (wParam == 4) _suspended = true;
                if (wParam is 7 or 18) _suspended = false;
                if (wParam == 0x8013 && lParam != 0 && Marshal.PtrToStructure<Guid>(lParam) == DisplayPowerGuid
                    && Marshal.ReadInt32(lParam, 16) == 4)
                    _displayOff = Marshal.ReadInt32(lParam, 20) == 0;
                Input?.Invoke(new(IslandInputKind.PowerChanged));
                result = 1;
                return true;
        }
        return false;
    }

    private void CleanupRecovery()
    {
        if (!_recoveryEnabled) return;
        ObserveForeground(false);
        if (_trayAdded) { var data = TrayData(); Shell_NotifyIconW(2, ref data); _trayAdded = false; }
        if (_hotkeyRegistered) { UnregisterHotKey(_window, HotkeyId); _hotkeyRegistered = false; }
        if (_sessionRegistered) { WTSUnRegisterSessionNotification(_window); _sessionRegistered = false; }
        if (_powerRegistration != 0) { UnregisterPowerSettingNotification(_powerRegistration); _powerRegistration = 0; }
        _recoveryEnabled = false;
        GC.KeepAlive(_eventCallback);
    }
}
