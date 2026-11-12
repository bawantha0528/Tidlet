using System.Windows;
using System.Windows.Media;
using Islet.Core.Bar;

namespace Islet.Shell.Bar;

public sealed partial class DashboardHost
{
    private sealed partial class DashboardVisual
    {
        private readonly Dictionary<(BarModule Module, BarItemStyle Style), Drawing> _orderPreviews = [];
        private (DashboardPalette Palette, double Scale)? _orderPreviewAppearance;

        private void DrawStripOrder(DrawingContext drawing, BarSettings settings, ref double y)
        {
            var strip = new Rect(235, y + 20, Width - 275, 38);
            if (strip.Bottom > 66 + _scroll && strip.Top < Height - 54 + _scroll)
            {
                Label(drawing, "Strip preview · sample readings", strip.Left, y, 10, _dim, true);
                drawing.DrawRoundedRectangle(_palette.Metrics.Surface, _palette.CardBorder, strip, 6, 6);
                var appearance = (_palette, _scale);
                if (_orderPreviewAppearance != appearance)
                {
                    _orderPreviews.Clear();
                    _orderPreviewAppearance = appearance;
                }

                var normal = settings with { GamingMode = false };
                var light = _palette == DashboardPalette.For(BarTheme.Light);
                var fullSize = BarLayout.Items(normal with { Alignment = BarAlignment.Left }, double.PositiveInfinity, batteryAvailable: true);
                var logicalWidth = fullSize.Length == 0 ? strip.Width : Math.Max(strip.Width, fullSize[^1].X + fullSize[^1].Width + 12);
                var factor = strip.Width / logicalWidth;
                var items = BarLayout.Items(normal, logicalWidth, batteryAvailable: true);
                drawing.PushClip(new RectangleGeometry(strip));
                if (items.Length == 0)
                    Label(drawing, "No strip items enabled", strip.Left, strip.Top + 11, 11, _dim, width: strip.Width, center: true);
                foreach (var item in items)
                {
                    var style = BarStyles.Resolve(item.Module, normal.Preference(item.Module).Style, item.Width >= 84);
                    var key = (item.Module, style);
                    if (!_orderPreviews.TryGetValue(key, out var preview))
                        _orderPreviews[key] = preview = MonitorBarHost.PreviewItem(item.Module, style, light, _scale);
                    drawing.PushTransform(new MatrixTransform(factor, 0, 0, factor, strip.Left + item.X * factor, strip.Top + (strip.Height - 24 * factor) / 2));
                    drawing.DrawDrawing(preview);
                    drawing.Pop();
                }
                drawing.Pop();
            }

            y += 68;
            var order = settings.NormalItems;
            for (var i = 0; i < order.Length; i++)
            {
                var module = order[i];
                var name = module is BarModule.Cpu or BarModule.Gpu or BarModule.Ram
                    ? module.ToString().ToUpperInvariant() : module.ToString();
                var row = SettingsRow(drawing, name, ref y);
                if (row.IsEmpty) continue;
                if (i > 0)
                    DrawPageButton(drawing, $"{name} ←", new Rect(row.Right - 164, row.Top + 4, 74, 27), false,
                        () => Change(s => s.MoveItemEarlier(module), $"{name} moved left. Saved."), pinned: false);
                if (i < order.Length - 1)
                    DrawPageButton(drawing, $"{name} →", new Rect(row.Right - 82, row.Top + 4, 74, 27), false,
                        () => Change(s => s.MoveItemLater(module), $"{name} moved right. Saved."), pinned: false);
            }
        }
    }
}
