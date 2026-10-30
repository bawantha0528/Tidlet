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
        for (var y = Math.Max(0, (int)top); y < Math.Min(_h, bottom); y++)
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
        for (var row = Math.Max(0, (int)(cy - r - 1)); row < Math.Min(_h, cy + r + 1); row++)
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
    private void DrawDashboard(IslandFrame frame, int width, int height)
    {
        _s = frame.Scale; _w = width; _h = height;
        var w = width / _s; var h = height / _s;
        var dashboard = frame.Dashboard!;
        if (frame.State == IslandState.Collapsed || h < 70)
        {
            var inset = frame.Style == IslandStyle.Notch ? 34 : 22;
            Dot(inset * _s, height / 2.0, 3 * _s, _accent, width, height);
            Label(frame.Label, inset + 14, (h - 22) / 2, w - inset - 28, 13, weight: 600);
            if (frame.Style == IslandStyle.Bar) Label("TIDLET", w - 75, (h - 20) / 2, 60, 9, _muted, 600);
            return;
        }
        if (frame.State == IslandState.Hover || h < 235)
        {
            Label("TIDLET", 28, 12, 70, 9, _accent, 600);
            Label(frame.Label, 28, 34, w - 55, 23, weight: 600, height: 34);
            Label("Open your system at a glance  →", 28, 69, w - 55, 10, _muted);
            return;
        }
        if (dashboard.ControlCenter is { } center) { DrawControlCenter(dashboard, center); return; }
        if (dashboard.CanReturnToNotch)
        {
            var back = ControlCenterLayout.ReturnButton(w); Block(back, _panel, 8);
            Label("←  Notch", back.X + 11, back.Y + 3, back.Width - 16, 10);
        }
        Label("Tidlet", 28, 13, 190, 24, weight: 600, height: 35);
        Label("Your system, at a glance", 103, 24, w - 240, 11, _muted);
        var theme = DashboardLayout.ThemeButton(w);
        Block(theme, _border, 8);
        Label(dashboard.Theme == ThemeMode.Light ? "Dark  ◐" : "Light  ◑", theme.X + 10, theme.Y + 3, theme.Width - 16, 10, weight: 500);
        for (var i = 0; i < DashboardLayout.PageNames.Length; i++)
        {
            var tab = DashboardLayout.Tab(i, w);
            if ((int)dashboard.Page == i) Block(tab, _accent, 8, 26);
            Label(DashboardLayout.PageNames[i], tab.X + 11, tab.Y + 4, tab.Width - 17, 11,
                (int)dashboard.Page == i ? _accent : _muted, (int)dashboard.Page == i ? 600 : 400);
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
        Block(box, _border, 12);
        Block(new(box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2), _panel, 11);
        var x = box.X + 16; var y = box.Y; var inner = box.Width - 32;
        Label(card.Title, x, y + 10, inner - 20, 12, weight: 600);
        Label("↗", box.X + box.Width - 30, y + 10, 18, 12, _muted);
        if (card.Page == MonitorPage.Apps)
        {
            Label("WORKING SET", x, y + 34, inner, 8, _muted, 600);
            if (card.Details.Count == 0) Label("Waiting for accessible processes", x, y + 76, inner, 11, _muted);
            for (var i = 0; i < Math.Min(5, card.Details.Count); i++) DrawRow(card.Details[i], x, y + 54 + i * 25, inner, 10);
            return;
        }
        if (card.Percent is { } percent && MonitorFormat.ValidPercent(percent))
        {
            Ring(x + 30, y + 72, 29, percent, _accent);
            Label(card.Value, x + 9, y + 60, 45, 13, weight: 600);
            Label(card.Subtitle, x + 73, y + 49, inner - 73, 10, _muted);
            if (card.Details.Count > 0) Label(card.Page == MonitorPage.Cpu && card.Details.Count > 1 ? card.Details[1].Value + " idle" : card.Details[0].Value, x + 73, y + 69, inner - 73, 15, weight: 600);
        }
        else
        {
            Label(card.Value, x, y + 38, inner, card.Value.Length > 15 ? 19 : 24, weight: 600, height: 34);
            Label(card.Subtitle, x, y + 75, inner, 10, _muted);
        }
        if (box.Height >= 170)
        {
            Plot(card.History, card.SecondaryHistory, new(x, y + 110, inner, box.Height - 151), card.Page is MonitorPage.Cpu or MonitorPage.Memory or MonitorPage.Battery ? 100 : null);
            if (card.Details.Count > 1) DrawRow(card.Details[1], x, y + box.Height - 34, inner, 10);
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
        var mainWidth = (width - 68) * 0.64; var x = 28.0; var y = 116.0; var bottom = height - 42;
        Block(new(x, y, mainWidth, bottom - y), _panel, 12);
        Label(card.Title, x + 20, y + 13, mainWidth - 40, 15, weight: 600);
        Label(card.Subtitle, x + 20, y + 41, mainWidth - 40, 11, _muted);
        if (MonitorFormat.ValidPercent(card.Percent))
        {
            Ring(x + 60, y + 112, 36, card.Percent, _accent);
            Label(card.Value, x + 34, y + 100, 58, 17, weight: 600);
            Label(card.Page == MonitorPage.Battery ? "Charge level" : "In use right now", x + 113, y + 90, mainWidth - 145, 12, _muted);
            Label(card.Value, x + 113, y + 112, mainWidth - 145, 26, weight: 600, height: 38);
        }
        else Label(card.Value, x + 20, y + 77, mainWidth - 40, 28, weight: 600, height: 42);
        var chartY = y + 171; var chartHeight = Math.Max(38, Math.Min(70, (bottom - y) * 0.18));
        if (card.Page != MonitorPage.Apps)
        {
            Label(card.HistoryLabel, x + 20, chartY - 22, mainWidth - 40, 8, _muted, 600);
            Plot(card.History, card.SecondaryHistory, new(x + 20, chartY, mainWidth - 40, chartHeight), card.Page is MonitorPage.Cpu or MonitorPage.Memory or MonitorPage.Battery or MonitorPage.Gpu ? 100 : null);
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
            Block(new(railX, ry, railWidth, rowHeight - 6), _panel, 9);
            Label(item.Title, railX + 13, ry + 5, railWidth - 26, 10, _muted);
            Label(item.Value, railX + 13, ry + 24, railWidth - 26, 14, item.Percent.HasValue ? _accent : _text, 600);
        }
    }
    private void Plot(IReadOnlyList<double?> primary, IReadOnlyList<double?>? secondary, DashboardRect r, double? fixedMax)
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
                            for (var row = Math.Max(0, (int)(yy * _s)); row < Math.Min(_h, (r.Y + r.Height) * _s); row++)
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
        Series(primary, _accent, true);
        if (secondary is not null) Series(secondary, _warm, false);
    }
}