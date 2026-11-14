using System.Windows;
using System.Windows.Media;
using Islet.Core.Bar;

namespace Islet.Shell.Bar;

public sealed partial class DashboardHost
{
    private sealed partial class DashboardVisual
    {
        private readonly Dictionary<(BarModule Module, BarItemStyle Style), Drawing> _orderPreviews = [];
        private (Dictionary<StripColor,string> Colors, double Scale)? _orderPreviewAppearance;

        private StripPalette? _orderPalette;
        private void DrawStripOrder(DrawingContext drawing, BarSettings settings, ref double y)
        {
            var strip = new Rect(235, y + 20, Width - 275, 38);
            if (strip.Bottom > 66 + _scroll && strip.Top < Height - 54 + _scroll)
            {
                Label(drawing, "Strip preview · select a reading to customize", strip.Left, y, 10, _dim, true);

                var appearance = (settings.StripColors, _scale);
                if (_orderPreviewAppearance != appearance)
                {
                    _orderPreviews.Clear();
                    _orderPreviewAppearance = appearance;
                    _orderPalette = new(settings.StripColors);
                }

                drawing.DrawRoundedRectangle(_orderPalette?.Get(StripColor.Background) ?? MonitorBarHost.StripBackground,
                    _orderPalette?.Pen(StripColor.Border) ?? MonitorBarHost.StripBorder, strip, 6, 6);
                var normal = settings with { GamingMode = false };

                var fullSize = BarLayout.Items(normal with { Alignment = BarAlignment.Left }, double.PositiveInfinity, batteryAvailable: true);
                var logicalWidth = fullSize.Length == 0 ? strip.Width : Math.Max(strip.Width, fullSize[^1].X + fullSize[^1].Width + 12);
                var factor = strip.Width / logicalWidth;
                var items = BarLayout.Items(normal, logicalWidth, batteryAvailable: true);
                drawing.PushClip(new RectangleGeometry(strip));
                if (items.Length == 0)
                    Label(drawing, "No strip items enabled", strip.Left, strip.Top + 11, 11, Brushes.LightGray, width: strip.Width, center: true);
                foreach (var item in items)
                {
                    var style = BarStyles.Resolve(item.Module, normal.Preference(item.Module).Style, item.Width >= 84);
                    var key = (item.Module, style);
                    if (!_orderPreviews.TryGetValue(key, out var preview))
                        _orderPreviews[key] = preview = MonitorBarHost.PreviewItem(item.Module, style, false, _scale, _orderPalette);
                    drawing.PushTransform(new MatrixTransform(factor, 0, 0, factor, strip.Left + item.X * factor, strip.Top + (strip.Height - 24 * factor) / 2));
                    drawing.DrawDrawing(preview);
                    drawing.Pop();
                    var module = item.Module;
                    Hit("Customize " + ModuleLabel(module) + " from strip preview",
                        new Rect(strip.Left+item.X*factor,strip.Top,item.Width*factor,strip.Height),
                        () => OpenModuleSettings(module));
                }
                drawing.Pop();
            }

            y += 68;
            DrawStripPlacement(drawing, settings, ref y);
            Heading(drawing, "ITEM ORDER · MOVE LEFT OR RIGHT", ref y);
            var order = settings.NormalItems;
            for (var i = 0; i < order.Length; i++)
            {
                var module = order[i];
                var name = ModuleLabel(module);
                var row = SettingsRow(drawing, name, ref y, labelInset: 35);
                if (row.IsEmpty) continue;
                DashboardIcons.Draw(drawing, name, new Point(row.Left+18,row.Top+17), _dim);
                DrawOrderArrow(drawing, row, module, name, earlier: true, enabled: i > 0);
                DrawOrderArrow(drawing, row, module, name, earlier: false, enabled: i < order.Length - 1);
            }
        }

        private static string ModuleLabel(BarModule module) => module is BarModule.Cpu or BarModule.Gpu or BarModule.Ram
            ? module.ToString().ToUpperInvariant() : module.ToString();

        private void DrawOrderArrow(DrawingContext drawing, Rect row, BarModule module, string name, bool earlier, bool enabled)
        {
            var bounds = new Rect(row.Right-(earlier?82:44),row.Top+4,34,27);
            drawing.DrawRoundedRectangle(_palette.Metrics.Surface,_palette.CardBorder,bounds,5,5);
            var arrow = earlier ? "←" : "→";
            Label(drawing,arrow,bounds.Left,bounds.Top+4,13,enabled?_text:_dim,width:bounds.Width,center:true);
            if (!enabled) return;
            var direction = earlier ? "left" : "right";
            Hit("Move "+name+" "+direction,bounds,()=>Change(
                settings=>earlier?settings.MoveItemEarlier(module):settings.MoveItemLater(module),
                $"{name} moved {direction}. Saved."));
        }

        private void DrawStripPlacement(DrawingContext drawing, BarSettings settings, ref double y)
        {
            var current = settings.TopReveal ? 2 : settings.Edge == BarEdge.Bottom ? 1 : 0;
            var placement = SettingsRow(drawing,"Screen edge",ref y);
            if (!placement.IsEmpty)
            {
                var options = new[] { "Top", "Bottom", "Hover" };
                DrawStripOptions(drawing,placement,"Screen edge",options,current,index =>
                    Change(s=>s with { Edge=index==1?BarEdge.Bottom:BarEdge.Top, RevealAtTop=index==2 },
                        $"Position set to {PositionLabels[index]}. Saved."));
            }

            var alignment = SettingsRow(drawing,"Alignment",ref y);
            if (!alignment.IsEmpty)
                DrawStripOptions(drawing,alignment,"Alignment",["Left","Center","Right"],(int)settings.Alignment,
                    index=>Change(s=>s with { Alignment=(BarAlignment)index }, "Strip alignment saved."));
        }

        private void DrawStripOptions(DrawingContext drawing, Rect row, string group, string[] options, int current, Action<int> select)
        {
            var width = Math.Min(78,(row.Width-112)/3);
            for (var i = 0; i < options.Length; i++)
            {
                var index = i;
                var bounds = new Rect(row.Right-7-(options.Length-i)*width,row.Top+4,width-3,27);
                if (i == current) DrawSettingsSelection(drawing,bounds);
                else drawing.DrawRoundedRectangle(_palette.Metrics.Surface,_palette.CardBorder,bounds,5,5);
                Label(drawing,options[i],bounds.Left,bounds.Top+5,11,i==current?SelectionText:_text,
                    width:bounds.Width,center:true);
                Hit(group+": "+options[i]+(i==current?" (selected)":""),bounds,()=>{if(index!=current)select(index);});
            }
        }
    }
}
