using System.Globalization;
using Islet.Core.Island;
using Islet.Core.Presentation;
using Islet.Core.Settings;

namespace Islet.Windows.Native;

internal sealed unsafe partial class DibRenderer
{
    private int _background = 0x141A25, _panel, _border, _text, _muted, _accent, _warm;
    private double _s;
    private int _w, _h;
    private void SetPalette(IslandFrame frame)
    {
        var light = frame.Dashboard?.Theme == ThemeMode.Light;
        _background = light ? 0xF4F4F5 : 0x0D0C0F;
        _panel = light ? 0xFFFFFF : 0x080809;
        _border = light ? 0xE2E2E5 : 0x28272C;
        _text = light ? 0x202124 : 0xF7F7F9;
        _muted = light ? 0x707075 : 0x95939C;
        _accent = 0x3B82F6;
        if (frame.Dashboard?.Accent is { Length: 7 } hex && hex[0] == '#'
            && int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var accent))
        {
            var red = (accent >> 16) & 255; var green = (accent >> 8) & 255; var blue = accent & 255;
            // The user's new palette explicitly excludes green, including saved accents from the old theme.
            if (!(green > red && green > blue)) _accent = accent;
        }
        _warm = light ? 0xE64B58 : 0xF05B67;
        if (frame.HighContrast) { _background = _panel = 0; _text = _muted = _border = 0xFFFFFF; _accent = 0xFFFF00; _warm = 0xFFFFFF; }
    }
    private void Label(string value, double x, double y, double width, double size = 11, int? color = null, int weight = 400, double height = 22)
        => Text(value, x * _s, y * _s, (x + width) * _s, (y + height) * _s, size * _s, weight, color ?? _text, _w, _h);
    private void Stroke(double x, double y, double right, double bottom, int color)
        => Line((int)(x * _s), (int)(y * _s), (int)(right * _s), (int)(bottom * _s), color, _w, _h);
    private void Block(DashboardRect r, int color, double radius = 10, int alpha = 255)
    {
        var left = r.X * _s; var top = r.Y * _s; var right = (r.X + r.Width) * _s; var bottom = (r.Y + r.Height) * _s;
        var rr = Math.Max(0, Math.Min(radius * _s, Math.Min(right - left, bottom - top) / 2));
        for (var y = Math.Max(_clipTop, (int)top); y < Math.Min(Math.Min(_h,_clipBottom), bottom); y++)
            for (var x = Math.Max(0, (int)left); x < Math.Min(_w, right); x++)
            {
                var dx = Math.Max(Math.Max(left + rr - x - 0.5, x + 0.5 - right + rr), 0);
                var dy = Math.Max(Math.Max(top + rr - y - 0.5, y + 0.5 - bottom + rr), 0);
                var coverage = rr == 0 ? 1 : Math.Clamp(rr + 0.5 - Math.Sqrt(dx * dx + dy * dy), 0, 1);
                var index = y * _output!.Width + x;
                if (coverage > 0 && (uint)_output.Pixels[index] >> 24 != 0)
                    _output.Pixels[index] = Over(_output.Pixels[index], color, (int)(alpha * coverage));
            }
    }
    private void Ring(double x, double y, double radius, double? percent, int color)
    {
        var cx = x * _s; var cy = y * _s; var r = radius * _s; var thickness = 4 * _s;
        for (var row = Math.Max(_clipTop, (int)(cy - r - 1)); row < Math.Min(Math.Min(_h,_clipBottom), cy + r + 1); row++)
            for (var col = Math.Max(0, (int)(cx - r - 1)); col < Math.Min(_w, cx + r + 1); col++)
            {
                var dx = col + 0.5 - cx; var dy = row + 0.5 - cy;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                var coverage = Math.Min(Math.Clamp(r + 0.5 - distance, 0, 1), Math.Clamp(distance - r + thickness + 0.5, 0, 1));
                if (coverage <= 0) continue;
                var angle = (Math.Atan2(dy, dx) + Math.PI / 2 + Math.PI * 2) % (Math.PI * 2);
                var active = MonitorFormat.ValidPercent(percent) && angle / (Math.PI * 2) < percent / 100;
                var index = row * _output!.Width + col;
                if ((uint)_output.Pixels[index] >> 24 != 0) _output.Pixels[index] = Over(_output.Pixels[index], active ? color : _border, (int)(255 * coverage));
            }
    }
    private static int CardColor(MonitorPage page) => page switch
    {
        MonitorPage.Cpu => 0x0A84FF,
        MonitorPage.Memory => 0xFF9F0A,
        MonitorPage.Network => 0x64D2FF,
        MonitorPage.Disk => 0xAF52DE,
        MonitorPage.Battery => 0x30D158,
        MonitorPage.Gpu => 0x40C8E0,
        MonitorPage.Apps => 0x5E5CE6,
        _ => 0x0A84FF
    };
    private void DrawDashboard(IslandFrame frame, int width, int height)
    {
        _s = frame.Scale; _w = width; _h = height;
        var w = width / _s; var h = height / _s;
        var dashboard = frame.Dashboard!;
        if (dashboard.Surface == MonitorSurface.Application) { DrawApplicationDashboard(dashboard,w,h); return; }
        if (dashboard.Surface == MonitorSurface.Module) { DrawModulePanel(dashboard,w,h); return; }
        if (dashboard.Surface == MonitorSurface.Strip) { DrawMonitorStrip(dashboard,w,h); return; }
        if (frame.State == IslandState.Collapsed || h < 70)
        {
            var cpu = dashboard.Cards.FirstOrDefault(c => c.Page == MonitorPage.Cpu);
            var ram = dashboard.Cards.FirstOrDefault(c => c.Page == MonitorPage.Memory);
            var net = dashboard.Cards.FirstOrDefault(c => c.Page == MonitorPage.Network);
            var bat = dashboard.Cards.FirstOrDefault(c => c.Page == MonitorPage.Battery);
            var inset = frame.Style == IslandStyle.Notch ? 34.0 : 18.0;

            if (w >= 260)
            {
                Dot(inset * _s, h / 2.0 * _s, 3.5 * _s, 0x0A84FF, _w, _h);
                var curX = inset + 12.0;
                var midY = (h - 22) / 2.0;

                // CPU
                Dot((curX + 3) * _s, (h / 2.0) * _s, 2.5 * _s, 0x0A84FF, _w, _h);
                Label("CPU", curX + 10, midY, 26, 8, _muted, 700);
                var cpuVal = cpu?.Value is "Unavailable" or null ? "—" : cpu.Value;
                Label(cpuVal, curX + 36, midY, 36, 11, _text, 700);
                curX += 74.0;
                Stroke(curX, midY + 3, curX, midY + 17, _border);
                curX += 8.0;

                // RAM
                Dot((curX + 3) * _s, (h / 2.0) * _s, 2.5 * _s, 0xFF9F0A, _w, _h);
                Label("RAM", curX + 10, midY, 28, 8, _muted, 700);
                var ramVal = ram?.Value is "Unavailable" or null ? "—" : ram.Value;
                Label(ramVal, curX + 38, midY, 36, 11, _text, 700);
                curX += 76.0;

                // Net / Bat if room
                if (curX + 70 <= w - inset)
                {
                    Stroke(curX, midY + 3, curX, midY + 17, _border);
                    curX += 8.0;
                    if (net is not null && !net.Absent && curX + 72 <= w - inset)
                    {
                        var up = net.Details.ElementAtOrDefault(1)?.Value ?? "0 KB/s";
                        var down = net.Details.ElementAtOrDefault(0)?.Value ?? net.Value;
                        Dot((curX + 3) * _s, (h / 2.0 - 4) * _s, 2 * _s, 0xFF375F, _w, _h);
                        Label(up, curX + 8, midY - 4, 52, 8, _muted, 500);
                        Dot((curX + 3) * _s, (h / 2.0 + 4) * _s, 2 * _s, 0x64D2FF, _w, _h);
                        Label(down, curX + 8, midY + 4, 52, 8, _text, 600);
                        curX += 62.0;
                    }
                    else if (bat is not null && !bat.Absent)
                    {
                        Dot((curX + 3) * _s, (h / 2.0) * _s, 2.5 * _s, 0x30D158, _w, _h);
                        Label("BAT", curX + 10, midY, 26, 8, _muted, 700);
                        Label(bat.Value, curX + 36, midY, 36, 11, 0x30D158, 700);
                    }
                }

                if (frame.Style == IslandStyle.Bar)
                    Label("TIDLET", w - 65, midY, 50, 9, _muted, 700);
                return;
            }

            Dot(inset * _s, height / 2.0, 3 * _s, _accent, width, height);
            Label(frame.Label, inset + 14, (h - 22) / 2, w - inset - 28, 13, weight: 600);
            if (frame.Style == IslandStyle.Bar) Label("TIDLET", w - 75, (h - 20) / 2, 60, 9, _muted, 600);
            return;
        }
        if (frame.State == IslandState.Hover || h < 235)
        {
            var cpu = dashboard.Cards.FirstOrDefault(c => c.Page == MonitorPage.Cpu);
            var ram = dashboard.Cards.FirstOrDefault(c => c.Page == MonitorPage.Memory);
            var net = dashboard.Cards.FirstOrDefault(c => c.Page == MonitorPage.Network);
            var bat = dashboard.Cards.FirstOrDefault(c => c.Page == MonitorPage.Battery);

            Icon("layout-dashboard", 22, 12, 16, 0x0A84FF);
            Label("Tidlet", 44, 9, 70, 13, _text, 700);
            Dot(95 * _s, 19 * _s, 2.5 * _s, 0x30D158, _w, _h);
            Label("LIVE", 102, 10, 40, 8, 0x30D158, 700);
            Right("Open full dashboard  ↗", w - 180, 10, 156, 10, 0x0A84FF, 600);

            var margin = 20.0;
            var gap = 8.0;
            var count = bat is not null && !bat.Absent ? 4 : 3;
            var cardW = (w - margin * 2 - gap * (count - 1)) / count;
            var cardH = 50.0;
            var cardY = 32.0;

            // 1. CPU
            var box0 = new DashboardRect(margin, cardY, cardW, cardH);
            Block(box0, 0x0C2038, 8);
            Stroke(box0.X, box0.Y, box0.X + box0.Width, box0.Y, 0x1A4274);
            Icon("cpu", box0.X + 8, box0.Y + 8, 14, 0x0A84FF);
            Label("CPU", box0.X + 26, box0.Y + 7, box0.Width - 30, 8, _muted, 700);
            Label(cpu?.Value ?? "—", box0.X + 8, box0.Y + 24, box0.Width - 16, 14, _text, 700);

            // 2. RAM
            var box1 = new DashboardRect(margin + (cardW + gap), cardY, cardW, cardH);
            Block(box1, 0x2A1A08, 8);
            Stroke(box1.X, box1.Y, box1.X + box1.Width, box1.Y, 0x5E3912);
            Icon("memory-stick", box1.X + 8, box1.Y + 8, 14, 0xFF9F0A);
            Label("RAM", box1.X + 26, box1.Y + 7, box1.Width - 30, 8, _muted, 700);
            Label(ram?.Value ?? "—", box1.X + 8, box1.Y + 24, box1.Width - 16, 14, _text, 700);

            // 3. NET
            var box2 = new DashboardRect(margin + (cardW + gap) * 2, cardY, cardW, cardH);
            Block(box2, 0x08222E, 8);
            Stroke(box2.X, box2.Y, box2.X + box2.Width, box2.Y, 0x124860);
            Icon("globe", box2.X + 8, box2.Y + 8, 14, 0x64D2FF);
            Label("NET", box2.X + 26, box2.Y + 7, box2.Width - 30, 8, _muted, 700);
            Label(net?.Value ?? "—", box2.X + 8, box2.Y + 24, box2.Width - 16, 14, _text, 700);

            // 4. BAT
            if (count == 4)
            {
                var box3 = new DashboardRect(margin + (cardW + gap) * 3, cardY, cardW, cardH);
                Block(box3, 0x082613, 8);
                Stroke(box3.X, box3.Y, box3.X + box3.Width, box3.Y, 0x145228);
                Icon("battery", box3.X + 8, box3.Y + 8, 14, 0x30D158);
                Label("BAT", box3.X + 26, box3.Y + 7, box3.Width - 30, 8, _muted, 700);
                Label(bat?.Value ?? "—", box3.X + 8, box3.Y + 24, box3.Width - 16, 14, 0x30D158, 700);
            }

            if (h >= 110)
                Center("Click to open dashboard  ·  Esc to collapse", 0, h - 22, w, 9, _muted, 500, 16);
            return;
        }
        if (dashboard.ControlCenter is { } center) { DrawControlCenter(dashboard, center); return; }
        if (dashboard.CanReturnToNotch)
        {
            var back = ControlCenterLayout.ReturnButton(w); Block(back, _panel, 8);
            Label("←  Notch", back.X + 11, back.Y + 3, back.Width - 16, 10);
        }
        Icon("layout-dashboard", 28, 14, 20, 0x0A84FF);
        Label("Tidlet", 56, 10, 160, 22, weight: 700, height: 32);
        Label("Your system, at a glance", 132, 20, w - 270, 11, _muted);
        var theme = DashboardLayout.ThemeButton(w);
        Block(theme, _border, 8);
        Label(dashboard.Theme == ThemeMode.Light ? "Dark  ◐" : "Light  ◑", theme.X + 10, theme.Y + 3, theme.Width - 16, 10, weight: 500);
        for (var i = 0; i < DashboardLayout.PageNames.Length; i++)
        {
            var tab = DashboardLayout.Tab(i, w);
            var isSel = (int)dashboard.Page == i;
            var tabColor = CardColor((MonitorPage)i);
            if (isSel)
            {
                Block(tab, tabColor, 8);
                Icon(MetricIcon((MonitorPage)i), tab.X + 8, tab.Y + 6, 14, 0xFFFFFF);
                Label(DashboardLayout.PageNames[i], tab.X + 26, tab.Y + 4, tab.Width - 30, 11, 0xFFFFFF, 600);
            }
            else
            {
                Block(tab, _border, 8, 40);
                Icon(MetricIcon((MonitorPage)i), tab.X + 8, tab.Y + 6, 14, _muted);
                Label(DashboardLayout.PageNames[i], tab.X + 26, tab.Y + 4, tab.Width - 30, 11, _muted, 400);
            }
        }
        if (dashboard.Page == MonitorPage.Overview)
        {
            var cards = dashboard.Cards.Where(c => !c.Absent && c.Page != MonitorPage.Gpu).Take(6).ToArray();
            for (var i = 0; i < cards.Length; i++) DrawOverviewCard(cards[i], DashboardLayout.Card(i, cards.Length, w, h));
        }
        else
        {
            var card = dashboard.Cards.FirstOrDefault(c => c.Page == dashboard.Page);
            if (card is not null) DrawDetail(card, dashboard, w, h);
        }
        Dot(32 * _s, (h - 20) * _s, 2.5 * _s, _accent, _w, _h);
        Label("Live while open", 42, h - 31, 160, 10, _muted);
        Label("Select a module for details   ·   Esc to collapse", Math.Max(205, w - 330), h - 31, 302, 10, _muted);
    }
    private void DrawOverviewCard(MonitorCard card, DashboardRect box)
    {
        var accent = CardColor(card.Page);
        Block(box, _border, 14);
        Block(new(box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2), _panel, 13);
        var x = box.X + 16; var y = box.Y; var inner = box.Width - 32;
        Icon(MetricIcon(card.Page), x, y + 10, 16, accent);
        Label(card.Title, x + 24, y + 8, inner - 48, 12, weight: 600);
        Label("↗", box.X + box.Width - 28, y + 8, 18, 12, accent);
        if (card.Page == MonitorPage.Apps)
        {
            Label("WORKING SET", x, y + 34, inner, 8, _muted, 600);
            if (card.Details.Count == 0) Label("Waiting for accessible processes", x, y + 76, inner, 11, _muted);
            for (var i = 0; i < Math.Min(5, card.Details.Count); i++)
            {
                Icon("app-window", x, y + 54 + i * 25 + 2, 12, _muted);
                Label(card.Details[i].Label, x + 16, y + 54 + i * 25, (inner - 16) * 0.54, 10);
                Right(card.Details[i].Value, x + (inner - 16) * 0.54 + 16, y + 54 + i * 25, (inner - 16) * 0.46, 10, _text, 500);
            }
            return;
        }
        if (card.Percent is { } percent && MonitorFormat.ValidPercent(percent))
        {
            var rx = x + 30; var ry = y + 72;
            Ring(rx, ry, 28, percent, accent);
            Center(card.Value, rx - 25, ry - 10, 50, 13, _text, 700, 20);
            Label(card.Subtitle, x + 72, y + 49, inner - 72, 10, _muted);
            if (card.Details.Count > 0)
            {
                var line = card.Page == MonitorPage.Cpu && card.Details.Count > 1
                    ? card.Details[1].Value + " idle"
                    : card.Details[0].Value;
                Label(line, x + 72, y + 69, inner - 72, 15, accent, 600);
            }
        }
        else
        {
            Label(card.Value, x, y + 38, inner, card.Value.Length > 15 ? 18 : 22, accent, 700, 32);
            Label(card.Subtitle, x, y + 75, inner, 10, _muted);
        }
        if (box.Height >= 170)
        {
            Plot(card.History, card.SecondaryHistory, new(x, y + 110, inner, box.Height - 151),
                card.Page is MonitorPage.Cpu or MonitorPage.Memory or MonitorPage.Battery ? 100 : null, accent);
            if (card.Page == MonitorPage.Battery && card.Details.Count > 1)
            {
                var health = card.Details.FirstOrDefault(d => d.Label == "Health")?.Value;
                var cycles = card.Details.FirstOrDefault(d => d.Label == "Cycle count")?.Value;
                if (!string.IsNullOrEmpty(health) && health != "Not reported")
                {
                    var healthText = cycles is not null ? $"Health: {health}  ·  {cycles} cycles" : $"Health: {health}";
                    Label(healthText, x, y + box.Height - 34, inner, 10, 0x30D158, 600);
                }
                else if (card.Details.Count > 1) DrawRow(card.Details[1], x, y + box.Height - 34, inner, 10);
            }
            else if (card.Details.Count > 1) DrawRow(card.Details[1], x, y + box.Height - 34, inner, 10);
            else Label(card.Status, x, y + box.Height - 34, inner, 10, _muted);
        }
    }
    private void DrawRow(DetailRow row, double x, double y, double width, double size = 11)
    {
        Label(row.Label, x, y, width * 0.56, size, _muted);
        Text(row.Value, (x + width * 0.56) * _s, y * _s, (x + width) * _s, (y + 22) * _s,
            size * _s, 500, _text, _w, _h, true);
    }
    private void DrawDetail(MonitorCard card, MonitorDashboard dashboard, double width, double height)
    {
        var accent = CardColor(card.Page);
        var mainWidth = (width - 68) * 0.64; var x = 28.0; var y = 116.0; var bottom = height - 42;
        Block(new(x, y, mainWidth, bottom - y), _panel, 14);
        Icon(MetricIcon(card.Page), x + 20, y + 14, 18, accent);
        Label(card.Title, x + 44, y + 11, mainWidth - 64, 16, weight: 700);
        Label(card.Subtitle, x + 20, y + 41, mainWidth - 40, 11, _muted);
        if (MonitorFormat.ValidPercent(card.Percent))
        {
            var rx = x + 60; var ry = y + 112;
            Ring(rx, ry, 36, card.Percent, accent);
            Center(card.Value, rx - 35, ry - 12, 70, 17, _text, 700, 24);
            Label(card.Page == MonitorPage.Battery ? "Charge level" : "In use right now", x + 113, y + 90, mainWidth - 145, 12, _muted);
            Label(card.Value, x + 113, y + 112, mainWidth - 145, 26, accent, 700, 38);
        }
        else Label(card.Value, x + 20, y + 77, mainWidth - 40, 28, accent, 700, 42);
        var chartY = y + 171; var chartHeight = Math.Max(38, Math.Min(70, (bottom - y) * 0.18));
        if (card.Page != MonitorPage.Apps)
        {
            Label(card.HistoryLabel, x + 20, chartY - 22, mainWidth - 40, 8, _muted, 600);
            Plot(card.History, card.SecondaryHistory, new(x + 20, chartY, mainWidth - 40, chartHeight), card.Page is MonitorPage.Cpu or MonitorPage.Memory or MonitorPage.Battery or MonitorPage.Gpu ? 100 : null, accent);
        }
        var detailY = card.Page == MonitorPage.Apps ? y + 152 : chartY + chartHeight + 12;
        for (var i = 0; i < card.Details.Count; i++)
        {
            if (detailY + i * 24 + 22 > bottom - 37) break;
            DrawRow(card.Details[i], x + 20, detailY + i * 24, mainWidth - 40);
        }
        Label(card.Status, x + 20, bottom - 35, mainWidth - 40, 10, _muted);
        var railX = x + mainWidth + 12; var railWidth = width - railX - 28;
        Label("COMBINED MODULES", railX + 12, y, railWidth - 20, 8, _muted, 600);
        var railCards = dashboard.Cards.Where(c => !c.Absent && c.Page != MonitorPage.Apps).ToArray();
        var rowHeight = Math.Min(62, (bottom - y - 27) / Math.Max(1, railCards.Length));
        for (var i = 0; i < railCards.Length; i++)
        {
            var item = railCards[i]; var ry = y + 27 + i * rowHeight;
            var itemColor = CardColor(item.Page);
            Block(new(railX, ry, railWidth, rowHeight - 6), _panel, 10);
            Icon(MetricIcon(item.Page), railX + 12, ry + 12, 14, itemColor);
            Label(item.Title, railX + 32, ry + 6, railWidth - 45, 10, _muted);
            Label(item.Value, railX + 32, ry + 24, railWidth - 45, 14, item.Percent.HasValue ? itemColor : _text, 600);
        }
    }
    private void Plot(IReadOnlyList<double?> primary, IReadOnlyList<double?>? secondary, DashboardRect r, double? fixedMax, int? plotColor = null)
    {
        if (r.Height < 2 || r.Width < 2) return;
        var max = fixedMax ?? Math.Max(1, primary.Concat(secondary ?? []).Where(v => v.HasValue && double.IsFinite(v.Value) && v >= 0).Select(v => v!.Value).DefaultIfEmpty(1).Max() * 1.15);
        for (var i = 0; i < 3; i++) Stroke(r.X, r.Y + i * r.Height / 2, r.X + r.Width, r.Y + i * r.Height / 2, _border);
        if (!primary.Any(v => v.HasValue && double.IsFinite(v.Value)) && !(secondary?.Any(v => v.HasValue && double.IsFinite(v.Value)) ?? false))
        { Label("Waiting for valid readings", r.X + 8, r.Y + Math.Max(0, r.Height / 2 - 11), r.Width - 16, 10, _muted); return; }
        void Series(IReadOnlyList<double?> values, int color, bool fill)
        {
            (double X, double Y)? prior = null;
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] is not { } v || !double.IsFinite(v) || v < 0) { prior = null; continue; }
                var x = r.X + r.Width * (60 - values.Count + i) / 59;
                var y = r.Y + r.Height * (1 - Math.Clamp(v / max, 0, 1));
                if (prior is { } p)
                {
                    if (fill)
                    {
                        var from = Math.Max(0, (int)(p.X * _s)); var to = Math.Min(_w - 1, (int)(x * _s));
                        for (var col = from; col <= to; col++)
                        {
                            var t = x == p.X ? 1 : (col / _s - p.X) / (x - p.X);
                            var yy = p.Y + (y - p.Y) * t;
                            for (var row = Math.Max(_clipTop, (int)(yy * _s)); row < Math.Min(Math.Min(_h,_clipBottom), (r.Y + r.Height) * _s); row++)
                            {
                                var idx = row * _output!.Width + col;
                                if ((uint)_output.Pixels[idx] >> 24 != 0) _output.Pixels[idx] = Over(_output.Pixels[idx], color, 25);
                            }
                        }
                    }
                    Stroke(p.X, p.Y, x, y, color);
                }
                Dot(x * _s, y * _s, 0.9 * _s, color, _w, _h);
                prior = (x, y);
            }
        }
        Series(primary, plotColor ?? _accent, true);
        if (secondary is not null) Series(secondary, _warm, false);
    }
}