using System.Windows.Media;
using Islet.Core.Bar;

namespace Islet.Shell.Bar;

public sealed partial class DashboardHost
{
    public const string GamingPage = "Gaming";

    private sealed partial class DashboardVisual
    {
        private static readonly int[] GamingRefreshIntervals = [0, 1, 2, 3, 5];

        private void Gaming(DrawingContext drawing)
        {
            var settings = _settings();
            double y = 84;
            Heading(drawing, "GAMING MODE", ref y);
            Toggle(drawing, "Gaming Mode", settings.GamingMode, ref y, ToggleGamingMode);
            Toggle(drawing, "Auto Gaming Mode", settings.GamingModeAuto, ref y,
                () => Change(s => s with { GamingModeAuto = !s.GamingModeAuto }, "Automatic activation saved."));
            Dropdown(drawing, "Strip refresh", settings.GamingRefreshSeconds ?? 0, GamingRefreshIntervals,
                value => value == 0 ? "Use normal strip rate" : Seconds(value),
                value => Change(s => s with { GamingRefreshSeconds = value == 0 ? null : value }, "Gaming refresh saved."), ref y);

            Heading(drawing, "FPS & USAGE", ref y);
            Dropdown(drawing, "FPS target", settings.FpsTarget, FpsTargets, value => $"{value} fps",
                value => Change(s => s with { FpsTarget = value }, $"FPS target set to {value} fps. Saved."), ref y);
            Dropdown(drawing, "Warning threshold", settings.ThresholdPercent, ThresholdLevels, value => $"{value}%",
                value => Change(s => s with { ThresholdPercent = value }, $"Usage warning set to {value}%. Applies to both layouts."), ref y);
            Label(drawing, "PresentMon measures FPS for the foreground app when the strip is visible.", 247, y, 11, _dim, width: Width - 285);
            y += 32;

            Heading(drawing, "GAMING STRIP ITEMS", ref y);
            foreach (var item in BarSettings.GamingModules)
            {
                var shown = settings.GamingItems.Contains(item);
                var label = item switch
                {
                    BarModule.FrameRate => "FPS and frame time",
                    BarModule.Cpu => "CPU",
                    BarModule.Gpu => "GPU",
                    BarModule.Ram => "RAM",
                    _ => item.ToString()
                };
                Toggle(drawing, label, shown, ref y, () =>
                {
                    if (shown && settings.GamingItems.Count == 1)
                    {
                        _status = "Keep at least one item in the Gaming strip.";
                        Draw(_scale);
                        return;
                    }

                    Change(s => s.WithGamingItemVisibility(item, !shown), $"Gaming strip items saved.");
                });
            }
            Choice(drawing, "Gaming strip layout", "Restore defaults", ref y,
                () => Change(s => s with { GamingStripItems = null }, "Default Gaming strip items restored."));
            _bottom = y + 30;
        }
    }
}
