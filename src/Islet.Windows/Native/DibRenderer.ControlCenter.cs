using Islet.Core.Activities;
using Islet.Core.Presentation;
using Islet.Core.Settings;

namespace Islet.Windows.Native;

internal sealed unsafe partial class DibRenderer
{
    private void DrawControlCenter(MonitorDashboard dashboard, ControlCenter center)
    {
        Label("◈", 28, 16, 24, 17, _accent, 600);
        Label("TIDLET", 57, 18, 115, 10, _muted, 600);
        Label("YOUR SPACE TO FOCUS", 188, 18, 210, 9, _muted);
        CenterButton(ControlCenterLayout.Theme, dashboard.Theme == ThemeMode.Light ? "Dark  ◐" : "Light  ◑", 10);
        CenterButton(ControlCenterLayout.Monitor, "Monitor  ↗", 10);
        var box = ControlCenterLayout.Media;
        Block(box, _border, 16); Block(new(box.X+1,box.Y+1,box.Width-2,box.Height-2), _panel, 15);
        Block(new(40,72,46,46), _border, 12);
        Label("♫", 52, 76, 30, 25, _muted, height: 35);
        Label(center.Media.Title, 98, 72, 148, 13, weight:600);
        Label(center.Media.Subtitle, 98, 96, 148, 10, _muted);
        Label(center.Media.State == MediaState.Available ? (center.Media.Playing ? "NOW PLAYING" : "PAUSED") : "SYSTEM MEDIA", 42, 142, 200, 8, _muted, 600);
        var position = center.Media.Position; var duration = center.Media.Duration;
        Block(new(42,176,202,3), _border, 1);
        if (position.HasValue && duration is { TotalSeconds: > 0 })
            Block(new(42,176,202 * Math.Clamp(position.Value.TotalSeconds / duration.Value.TotalSeconds,0,1),3), _accent,1);
        Label(position.HasValue ? MediaTime(position.Value) : "--:--", 42, 182, 94, 9, _muted);
        Text(duration.HasValue ? MediaTime(duration.Value) : "--:--", 170*_s,182*_s,244*_s,204*_s,9*_s,400,_muted,_w,_h,true);
        Label(center.Feedback ?? (center.Media.State == MediaState.Unavailable ? center.Media.Subtitle : "Controls follow your active player"),42,207,202,9,_muted);
        MediaButton(ControlCenterLayout.PreviousButton, "◀", center.Media.Allows(MediaCommand.Previous) && !center.Busy);
        MediaButton(ControlCenterLayout.PlaybackButton, center.Media.Playing ? "Ⅱ" : "▶", center.Media.Allows(MediaCommand.TogglePlayback) && !center.Busy, true);
        MediaButton(ControlCenterLayout.NextButton, "▶", center.Media.Allows(MediaCommand.Next) && !center.Busy);
        Block(ControlCenterLayout.Focus,_panel,14);
        Label("◷", 287, 65, 25, 19, _accent);
        Label("Focus timer", 316, 61, 125, 12, weight:600);
        var instruction = center.Focus.State switch { FocusState.Running => "Click to pause", FocusState.Paused => "Click to resume", FocusState.Complete => "Done · click to restart", _ => "Click to start · 25 minutes" };
        Label(instruction,316,81,160,9,_muted);
        Label(center.Focus.Display,480,65,60,17,weight:600);
        Label("↺",554,68,29,19,_muted);
        Block(new(274,120,322,60),_panel,14);
        CenterMetric(dashboard,MonitorPage.Cpu,"CPU",126,_accent);
        CenterMetric(dashboard,MonitorPage.Memory,"RAM",153,_warm);
        Block(ControlCenterLayout.Network,_panel,14);
        Label("Network",287,202,75,11,_muted,600);
        var network = dashboard.Cards.FirstOrDefault(c=>c.Page == MonitorPage.Network);
        var rates = network?.Details;
        Label(rates is { Count: >= 2 } ? "↓ " + rates[0].Value + "   ↑ " + rates[1].Value : network?.Value ?? "Unavailable",367,202,215,10);
        CenterButton(ControlCenterLayout.Shortcut(0),"◫  Disk",11);
        CenterButton(ControlCenterLayout.Shortcut(1),"▤  Apps",11);
        var battery = dashboard.Cards.FirstOrDefault(c=>c.Page == MonitorPage.Battery);
        CenterButton(ControlCenterLayout.Shortcut(2),battery is { Absent:false } ? "▰  " + battery.Value : "▰  Power",11);
    }
    private void CenterButton(DashboardRect box,string text,double size)
    { Block(box,_panel,12); Label(text,box.X+12,box.Y+(box.Height-22)/2,box.Width-20,size,weight:500); }
    private void MediaButton(DashboardRect box,string text,bool enabled,bool main=false)
    {
        if(main) Block(box, enabled ? _text : _border,19);
        Label(text,box.X+11,box.Y+7,box.Width-14,17,main&&enabled ? _panel : enabled ? _text : _muted,600);
    }
    private void CenterMetric(MonitorDashboard dashboard, MonitorPage page,string label,double y,int color)
    {
        var card = dashboard.Cards.FirstOrDefault(c=>c.Page == page);
        Label(label,287,y,40,10,_muted,600);
        Block(new(334,y+8,195,5),_border,2);
        if (MonitorFormat.ValidPercent(card?.Percent)) Block(new(334,y+8,195*card!.Percent!.Value/100,5),color,2);
        Label(card?.Percent is { } p && MonitorFormat.ValidPercent(p) ? $"{p:0}%" : "N/A",543,y,48,10,weight:600);
    }
    private static string MediaTime(TimeSpan time) => time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{(int)time.TotalMinutes}:{time.Seconds:00}";
}
