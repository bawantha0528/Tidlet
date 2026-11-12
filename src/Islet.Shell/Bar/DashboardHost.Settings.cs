using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Islet.Core.Bar;

namespace Islet.Shell.Bar;

public sealed partial class DashboardHost
{
    private sealed partial class DashboardVisual
    {
        private static readonly int[] RefreshIntervals = [1, 2, 3, 5];
        private static readonly int[] OpacityLevels = [20, 30, 40, 50, 60, 70, 80, 90, 100];
        private static readonly int[] FpsTargets = [30, 60, 120, 144, 165, 240, 360];
        private static readonly int[] ThresholdLevels = [70, 75, 80, 85, 90, 95];
        private static readonly string[] PositionLabels = ["Top", "Bottom", "Top · reveal on hover"];
        private sealed record SettingOption(string Label, Action Apply);
        private sealed record SettingsMenu(string Name, string Current, Rect Anchor, SettingOption[] Options);
        private SettingsMenu? _settingsMenu;
        private string? _accentValue;
        private Brush? _customAccent;
        private Brush SelectionText { get; set; } = Brushes.White;

        private void RefreshAccent(string value)
        {
            if (string.Equals(_accentValue, value, StringComparison.OrdinalIgnoreCase)) return;
            _accentValue = value;
            _customAccent = null;
            SelectionText = Brushes.White;
            if (string.Equals(value, "#0A84FF", StringComparison.OrdinalIgnoreCase) ||
                !uint.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)) return;
            _customAccent = PopupPalette.Solid((int)rgb);
            static double Linear(double component)
            {
                component /= 255;
                return component <= .04045 ? component / 12.92 : Math.Pow((component + .055) / 1.055, 2.4);
            }
            var luminance = .2126 * Linear(rgb >> 16) + .7152 * Linear((rgb >> 8) & 255) + .0722 * Linear(rgb & 255);
            if (luminance > .179) SelectionText = Brushes.Black;
        }

        private void DrawSettingsSelection(DrawingContext drawing, Rect bounds)
        {
            if (_customAccent is null) DashboardPalette.DrawSelection(drawing, bounds);
            else drawing.DrawRoundedRectangle(_customAccent, null, bounds, 7, 7);
        }

        private void Settings(DrawingContext drawing)
        {
            var settings = _settings();
            double y = 85;
            if (_settingsModule is { } module)
            {
                Heading(drawing, module.ToString().ToUpperInvariant() + " SETTINGS", ref y);
                ModuleSettings(drawing, settings, module, ref y);
                Choice(drawing, "All settings", "Back", ref y, () => Select("Settings"));
                _bottom = y + 30;
                return;
            }

            Heading(drawing, "APPEARANCE", ref y);
            Dropdown(drawing, "Theme", settings.Theme, Enum.GetValues<BarTheme>(), value => value.ToString(),
                value => Change(s => s with { Theme = value }, $"Theme set to {value}. Saved."), ref y);
            Choice(drawing, "Accent color", settings.Accent + "  ·  Edit", ref y, () => Edit("Accent color", settings.Accent));
            Heading(drawing, "MONITOR STRIP", ref y);
            DrawStripOrder(drawing, settings, ref y);
            var position = settings.TopReveal ? PositionLabels[2] : settings.Edge == BarEdge.Bottom ? PositionLabels[1] : PositionLabels[0];
            Dropdown(drawing, "Position", position, PositionLabels, value => value, value =>
                Change(s => s with
                {
                    Edge = value == PositionLabels[1] ? BarEdge.Bottom : BarEdge.Top,
                    RevealAtTop = value == PositionLabels[2],
                    Mode = value == PositionLabels[2] ? BarMode.Overlay : s.Mode
                }, $"Position set to {value}. Saved."), ref y);
            Dropdown(drawing, "Mode", settings.TopReveal ? BarMode.Overlay : settings.Mode, Enum.GetValues<BarMode>(),
                value => value.ToString(), value => Change(s => s with
                {
                    Mode = value,
                    RevealAtTop = value == BarMode.Overlay && s.RevealAtTop
                }, value == BarMode.Reserve && settings.TopReveal
                    ? "Reserve mode enabled. Top hover reveal turned off. Saved."
                    : $"Mode set to {value}. Saved."), ref y);
            Dropdown(drawing, "Alignment", settings.Alignment, Enum.GetValues<BarAlignment>(), value => value.ToString(),
                value => Change(s => s with { Alignment = value }, $"Alignment set to {value}. Saved."), ref y);
            Dropdown(drawing, "Monitors", settings.Monitors, new[] { MonitorSelection.Primary, MonitorSelection.All },
                value => value switch { MonitorSelection.Primary => "Primary monitor", MonitorSelection.All => "All monitors", _ => "Selected monitor" },
                value => Change(s => s with { Monitors = value }, "Monitor selection saved."), ref y);
            Dropdown(drawing, "Opacity", (int)Math.Round(settings.Opacity * 100), OpacityLevels, value => $"{value}%",
                value => Change(s => s with { Opacity = value / 100.0 }, $"Opacity set to {value}%. Saved."), ref y);
            Dropdown(drawing, "Bar refresh", settings.IntervalSeconds, RefreshIntervals, Seconds,
                value => Change(s => s with { IntervalSeconds = value }, $"Bar refresh set to {Seconds(value)}. Saved."), ref y);

            Heading(drawing, "READINGS", ref y);
            Dropdown(drawing, "Transfer units", settings.UseBits, new[] { false, true }, value => value ? "Bits / second" : "Bytes / second",
                value => Change(s => s with { UseBits = value }, "Transfer units saved."), ref y);
            Dropdown(drawing, "Temperature", settings.Fahrenheit, new[] { false, true }, value => value ? "Fahrenheit (°F)" : "Celsius (°C)",
                value => Change(s => s with { Fahrenheit = value }, "Temperature units saved."), ref y);
            Toggle(drawing, "Pause monitoring", settings.Pause, ref y,
                () => Change(s => s with { Pause = !s.Pause }, settings.Pause ? "Monitoring resumed. Saved." : "Monitoring paused. Saved."));

            Choice(drawing, "Gaming", "Open gaming settings  ›", ref y, () => Select(GamingPage));

            Heading(drawing, "MODULES", ref y);
            foreach (var item in BarSettings.ConfigurableModules)
                Choice(drawing, item.ToString(), settings.Items.Contains(item) ? "In strip  ›" : "Hidden  ›", ref y, () => OpenModuleSettings(item));
            Heading(drawing, "IMPORT & EXPORT", ref y);
            Choice(drawing, "Export settings", "Save local JSON", ref y, () => Run(_actions.ExportSettings));
            Choice(drawing, "Import settings", "Enter local file path", ref y, () => Edit("Import settings", ""));
            Choice(drawing, "Compatibility report", "Export local JSON", ref y, () => Run(_actions.CompatibilityReport));
            _bottom = y + 30;
        }

        private void OpenModuleSettings(BarModule module)
        {
            Select("Settings");
            _settingsModule = module;
            Draw(_scale);
        }

        private void ModuleSettings(DrawingContext drawing, BarSettings settings, BarModule module, ref double y)
        {
            var preference = settings.Preference(module);
            var shown = settings.Items.Contains(module);
            Toggle(drawing, "Show in monitor strip", shown, ref y,
                () => Change(s => s.WithItemVisibility(module, !shown), $"{module} {(shown ? "hidden from" : "shown in")} the strip. Saved."));
            var cost = module == BarModule.Sensors ? "High · hardware-dependent"
                : module is BarModule.Gpu or BarModule.Cpu or BarModule.Ram ? "Medium" : "Low";
            Label(drawing, "Sampling cost · " + cost, 247, y, 11, _dim, width: Width - 285);
            y += 26;
            Dropdown(drawing, "Details refresh", preference.UpdateSeconds, RefreshIntervals, Seconds,
                value => Preference(module, p => p with { UpdateSeconds = value }, $"Details refresh set to {Seconds(value)}. Saved."), ref y);
            Dropdown(drawing, "Bar style", preference.Style, BarStyles.Options(module), StyleName,
                value => Preference(module, p => p with { Style = value }, "Bar style saved."), ref y);
            Toggle(drawing, "Open popup on click", preference.PopupEnabled, ref y,
                () => Preference(module, p => p with { PopupEnabled = !p.PopupEnabled }, preference.PopupEnabled ? "Click-to-open popup disabled. Saved." : "Click-to-open popup enabled. Saved."));
            if (Array.IndexOf(settings.NormalItems, module) > 0)
                Choice(drawing, "Move earlier in strip", "Move ↑", ref y, () => Change(s => s.MoveItemEarlier(module), $"{module} moved earlier. Saved."));
        }

        private void Preference(BarModule module, Func<ModulePreference, ModulePreference> update, string feedback) =>
            Change(s => s with { Modules = new(s.Modules) { [module] = update(s.Preference(module)) } }, feedback);

        private static string Seconds(int value) => value == 1 ? "1 second" : $"{value} seconds";

        private void Dropdown<T>(DrawingContext drawing, string name, T current, IEnumerable<T> values,
            Func<T, string> label, Action<T> apply, ref double y)
        {
            var row = SettingsRow(drawing, name, ref y);
            if (row.IsEmpty) return;
            var width = Math.Min(210, row.Width * .48);
            var button = new Rect(row.Right - width - 7, row.Top + 4, width, row.Height - 8);
            drawing.DrawRoundedRectangle(_palette.Metrics.Surface, _palette.CardBorder, button, 5, 5);
            var value = label(current);
            Label(drawing, value, button.Left + 9, button.Top + 5, 11, _text, true, width - 31);
            var arrow = new Point(button.Right - 13, button.Top + button.Height / 2);
            var chevron = new Pen(_dim, 1.25);
            drawing.DrawLine(chevron, new Point(arrow.X - 3, arrow.Y - 1), new Point(arrow.X, arrow.Y + 2));
            drawing.DrawLine(chevron, new Point(arrow.X, arrow.Y + 2), new Point(arrow.X + 3, arrow.Y - 1));
            Hit(name + ": " + value, row, () =>
            {
                var anchor = button;
                anchor.Offset(0, -_scroll);
                _editing = null;
                _settingsMenu = new(name, value, anchor, values.Select(option =>
                    new SettingOption(label(option), () => { if (!EqualityComparer<T>.Default.Equals(current, option)) apply(option); else Draw(_scale); })).ToArray());
                Draw(_scale);
                FocusMenuOption(Math.Max(0, Array.FindIndex(_settingsMenu.Options, option => option.Label == value)));
            });
        }

        private void Toggle(DrawingContext drawing, string name, bool enabled, ref double y, Action apply)
        {
            var row = SettingsRow(drawing, name, ref y);
            if (row.IsEmpty) return;
            var toggle = new Rect(row.Right - 49, row.Top + 8, 37, 19);
            DrawTogglePill(drawing, toggle, enabled);
            var value = enabled ? "On" : "Off";
            Label(drawing, value, toggle.Left - 49, row.Top + 9, 11, _dim, width: 39, right: true);
            Hit(name + ": " + value, row, apply);
        }

        private Rect SettingsRow(DrawingContext drawing, string name, ref double y)
        {
            var row = new Rect(235, y - 3, Width - 275, 35);
            y += 41;
            if (row.Bottom <= 66 + _scroll || row.Top >= Height - 54 + _scroll) return Rect.Empty;
            drawing.DrawRoundedRectangle(_surface, _palette.CardBorder, row, 6, 6);
            Label(drawing, name, row.Left + 12, row.Top + 9, 12, _text, width: row.Width * .5 - 12);
            return row;
        }

        private void DrawSettingsMenu(DrawingContext drawing)
        {
            if (_settingsMenu is not { } menu) return;
            const double optionHeight = 29;
            var height = optionHeight * menu.Options.Length + 10;
            var top = menu.Anchor.Bottom + 4;
            if (top + height > Height - 55) top = Math.Max(67, menu.Anchor.Top - height - 4);
            var bounds = new Rect(menu.Anchor.X, top, menu.Anchor.Width, height);
            // Replace the tab and hit targets while the menu is open.
            foreach (var button in _buttons) RemoveVisualChild(button);
            _buttons.Clear();
            Hit("Dismiss " + menu.Name + " choices", new Rect(0, 0, Width, Height), CloseSettingsMenu, pinned: true);
            _buttons[0].Focusable = false;
            drawing.DrawRoundedRectangle(_surface, new Pen(_blue, 1), bounds, 7, 7);
            for (int i = 0; i < menu.Options.Length; i++)
            {
                var option = menu.Options[i];
                var item = new Rect(bounds.Left + 5, top + 5 + i * optionHeight, bounds.Width - 10, optionHeight);
                var selected = option.Label == menu.Current;
                if (selected) DrawSettingsSelection(drawing, item);
                var brush = selected ? SelectionText : _text;
                Label(drawing, option.Label, item.Left + 9, item.Top + 6, 12, brush, selected, item.Width - 32);
                if (selected) Label(drawing, "✓", item.Right - 20, item.Top + 6, 12, brush);
                Hit(MenuOptionName(option.Label, selected), item, () =>
                {
                    _settingsMenu = null;
                    option.Apply();
                    FocusSettingsRow(menu.Name);
                }, pinned: true);
            }
        }

        private static string MenuOptionName(string label, bool selected) => selected ? label + " (selected)" : label;

        private bool HandleSettingsMenuKey(KeyEventArgs e)
        {
            if (_settingsMenu is not { } menu) return false;
            if (e.Key == Key.Escape) CloseSettingsMenu();
            else if (e.Key is Key.Up or Key.Down or Key.Tab or Key.Home or Key.End)
            {
                var index = _buttons.FindIndex(button => button.IsKeyboardFocused) - 1;
                var step = e.Key == Key.Up || e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1;
                var count = menu.Options.Length;
                FocusMenuOption(e.Key == Key.Home ? 0 : e.Key == Key.End ? count - 1 : (Math.Max(0, index) + step + count) % count);
            }
            else return false;
            e.Handled = true;
            return true;
        }

        private void FocusMenuOption(int index) => _buttons[index + 1].Focus();

        private void CloseSettingsMenu()
        {
            if (_settingsMenu is not { } menu) return;
            _settingsMenu = null;
            Draw(_scale);
            FocusSettingsRow(menu.Name);
        }

        private void FocusSettingsRow(string name) =>
            _buttons.Find(button => button.Label.StartsWith(name + ": ", StringComparison.Ordinal))?.Focus();
    }
}
