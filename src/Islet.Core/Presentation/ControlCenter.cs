using Islet.Core.Activities;

namespace Islet.Core.Presentation;

public sealed record ControlCenter(MediaReading Media, FocusSnapshot Focus, bool Busy = false, string? Feedback = null);
public static class ControlCenterLayout
{
    public const int FullMonitor = 480, ReturnToNotch = 481, FocusToggle = 490, FocusReset = 491;
    public const int Previous = 500, TogglePlayback = 501, Next = 502;
    public static DashboardRect Theme => new(454, 16, 62, 26);
    public static DashboardRect Monitor => new(524, 16, 68, 26);
    public static DashboardRect Media => new(24, 54, 238, 242);
    public static DashboardRect Focus => new(274, 54, 322, 54);
    public static DashboardRect Reset => new(543, 64, 43, 34);
    public static DashboardRect PreviousButton => new(76, 237, 36, 38);
    public static DashboardRect PlaybackButton => new(124, 237, 38, 38);
    public static DashboardRect NextButton => new(174, 237, 36, 38);
    public static DashboardRect Cpu => new(274, 120, 322, 30);
    public static DashboardRect Ram => new(274, 150, 322, 30);
    public static DashboardRect Network => new(274, 192, 322, 43);
    public static DashboardRect Shortcut(int index) => new(274 + index * 110, 250, 102, 46);
    public static DashboardRect ReturnButton(double width) => new(width - 194, 20, 94, 28);
    public static int HitTest(double x, double y, ControlCenter center)
    {
        if (Theme.Contains(x,y)) return DashboardLayout.ThemeCommand;
        if (Monitor.Contains(x,y)) return FullMonitor;
        if (Reset.Contains(x,y)) return FocusReset;
        if (Focus.Contains(x,y)) return FocusToggle;
        if (Cpu.Contains(x,y)) return DashboardLayout.PageCommand + (int)MonitorPage.Cpu;
        if (Ram.Contains(x,y)) return DashboardLayout.PageCommand + (int)MonitorPage.Memory;
        if (Network.Contains(x,y)) return DashboardLayout.PageCommand + (int)MonitorPage.Network;
        for (var i = 0; i < 3; i++) if (Shortcut(i).Contains(x,y))
            return DashboardLayout.PageCommand + (int)new[] { MonitorPage.Disk, MonitorPage.Apps, MonitorPage.Battery }[i];
        if (center.Busy) return 0;
        if (PreviousButton.Contains(x,y) && center.Media.Allows(MediaCommand.Previous)) return Previous;
        if (PlaybackButton.Contains(x,y) && center.Media.Allows(MediaCommand.TogglePlayback)) return TogglePlayback;
        if (NextButton.Contains(x,y) && center.Media.Allows(MediaCommand.Next)) return Next;
        return 0;
    }
}
