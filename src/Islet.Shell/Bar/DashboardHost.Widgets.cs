using System.Windows;
using System.Windows.Media;
using Islet.Core.Bar;
using Islet.Core.Popups;

namespace Islet.Shell.Bar;

public sealed partial class DashboardHost
{
    private sealed partial class DashboardVisual
    {
        private DashboardWidgetReader? _widgetReader;
        private readonly Dictionary<BarModule, PopupSample> _widgetSamples = [];
        private readonly HashSet<BarModule> _previousWidgets = [];

        private void StartWidgets()
        {
            if (_settings().Pause) return;
            foreach (var module in DashboardWidgetReader.Modules)
            {
                if (!_recent.TryGet(module, out var sample)) continue;
                _widgetSamples[module] = sample!;
                _previousWidgets.Add(module);
            }
            _widgetReader = new(_metrics, _previous, samples =>
            {
                for (var i = 0; i < samples.Length; i++)
                {
                    var module = DashboardWidgetReader.Modules[i];
                    var sample = samples[i];
                    if (sample.WarmingUp && _previousWidgets.Contains(module) && _recent.TryGet(module, out _)) continue;
                    _previousWidgets.Remove(module);
                    _widgetSamples[module] = sample;
                    _recent.Store(module, sample);
                }
                Draw(_scale);
            });
        }

        private void StopWidgets()
        {
            if (_widgetReader is not { } reader) return;
            Reads += reader.Reads;
            Frames += reader.Frames;
            reader.Dispose();
            _previous = reader.Cleanup;
            Cleanup.RemoveAll(task => task.IsCompleted);
            Cleanup.Add(_previous);
            _widgetReader = null;
            _widgetSamples.Clear();
            _previousWidgets.Clear();
        }

        private void Widgets(DrawingContext drawing)
        {
            const double gap = 12, height = 190;
            var contentWidth = Width - 255;
            var columns = contentWidth >= 510 ? 3 : contentWidth >= 338 ? 2 : 1;
            var width = (contentWidth - gap * (columns - 1)) / columns;
            var settings = _settings();
            var paused = settings.Pause;
            var renderer = new WidgetCardRenderer(_palette, _blue, _scale, settings.UseBits, _widgetSamples, _previousWidgets);
            var count = _actions.Usage is null ? 6 : 7;
            for (var index = 0; index < count; index++)
            {
                var x = 230 + index % columns * (width + gap);
                var y = 80 + index / columns * (height + gap);
                if (y + height <= 66 + _scroll || y >= Height - 54 + _scroll) continue;
                var card = new Rect(x, y, width, height);
                if (index == 0 && _actions.Usage is { } getUsage)
                {
                    var usage = getUsage();
                    UsageCard.Draw(drawing, card, _palette, _scale, usage);
                    foreach (var period in Enum.GetValues<UsagePeriod>())
                        Hit($"Data usage: {period}" + (period == usage.Period ? " (selected)" : ""), UsageCard.Option(card, period),
                            () => { _actions.SelectUsage?.Invoke(period); Draw(_scale); });
                    if (_actions.PinUsage is { } pinUsage)
                        Button(drawing, "Add Data usage to desktop", "+", new Rect(card.Right - 25, y + 6, 20, 20), () => Run(pinUsage));
                    continue;
                }
                var moduleIndex = count == 7 ? index - 1 : index;
                BarModule? module = moduleIndex < DashboardWidgetReader.Modules.Length ? DashboardWidgetReader.Modules[moduleIndex] : null;
                renderer.Draw(drawing, card, module, paused, _actions.DesktopWidget is null ? 0 : 20);
                if (module is { } item)
                {
                    var title = WidgetCardRenderer.WidgetName(item);
                    Hit("Open " + title + " details", new Rect(x, y + 28, width, height - 28), () => Select(title));
                }
                if (_actions.DesktopWidget is { } pin)
                {
                    var button = new Rect(card.Right - 25, y + 6, 20, 20);
                    Button(drawing, "Add " + (module is { } m ? WidgetCardRenderer.WidgetName(m) : "Combined") + " to desktop",
                        "+", button, () => Run(() => pin(module)));
                }
            }
            _bottom = 80 + ((count + columns - 1) / columns) * (height + gap) + 5;
        }

    }
}
