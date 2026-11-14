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
        private static readonly int[] FpsTargets = [30, 60, 120, 144, 165, 240, 360];
        private static readonly int[] ThresholdLevels = [70, 75, 80, 85, 90, 95];
        private static readonly string[] PositionLabels = ["Top", "Bottom", "Top · reveal on hover"];
        private sealed record SettingOption(string Label, Action Apply, string? Swatch = null);
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

        private static string StripColorLabel(StripColor role) => role switch
        {
            StripColor.Track => "Meter track",
            StripColor.Battery => "Battery fill",
            StripColor.Upload => "Upload dot",
            StripColor.Download => "Download dot",
            _ => role.ToString()
        };

        private void SaveStripColor(StripColor role)
        {
            var value = _entry.Trim();
            if (value.Length > 0 && !BarSettings.IsStripColor(value))
            {
                _status = "Enter a hex color such as #0A84FF, or leave it blank to restore the default.";
                _editing = "Strip " + StripColorLabel(role);
                Draw(_scale);
                return;
            }

            ApplyStripColor(role, value.Length == 0 ? null : value);
        }

        private void ApplyStripColor(StripColor role, string? color)
        {
            Change(settings =>
            {
                var colors = new Dictionary<StripColor, string>(settings.StripColors);
                if (color is null) colors.Remove(role);
                else colors[role] = color.ToUpperInvariant();
                return settings with { StripColors = colors };
            }, color is null ? "Default strip color restored." : "Strip color saved.");
        }

        private static readonly (string Name, string Hex)[] StripSwatches =
        [
            ("Matte black", "#141414"), ("Slate", "#343A46"), ("Soft white", "#F4F5F7"),
            ("Blue", "#0A84FF"), ("Cyan", "#36C5F0"), ("Green", "#30D158"),
            ("Amber", "#FF9F0A"), ("Red", "#FF453A"), ("Violet", "#A78BFA")
        ];

        private static string DefaultStripSwatch(StripColor role) => role switch
        {
            StripColor.Background => "#141414",
            StripColor.Border => "#343434",
            StripColor.Track => "#393C40",
            StripColor.Label => "#B1B1B1",
            StripColor.Battery => "#30D158",
            StripColor.Upload => "#F14A47",
            StripColor.Warning => "#FF9F0A",
            StripColor.Critical => "#FF453A",
            StripColor.Download => "#2C99EA",
            StripColor.Meter => "#0A84FF",
            _ => "#FFFFFF"
        };

        private static Brush SwatchBrush(string hex) =>
            PopupPalette.Solid(int.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

        private void StripColorPicker(DrawingContext drawing, BarSettings settings, StripColor role, ref double y)
        {
            var name = "Strip " + StripColorLabel(role);
            var color = settings.StripColors.GetValueOrDefault(role);
            if (_editing == name)
            {
                Choice(drawing, name, "", ref y, () => Edit(name, color ?? ""));
                return;
            }

            var row = SettingsRow(drawing, name, ref y);
            if (row.IsEmpty) return;
            var width = Math.Min(190, row.Width * .47);
            var button = new Rect(row.Right-width-7, row.Top+4, width, row.Height-8);
            drawing.DrawRoundedRectangle(_palette.Metrics.Surface, _palette.CardBorder, button, 6, 6);
            drawing.DrawRoundedRectangle(SwatchBrush(color ?? DefaultStripSwatch(role)), _palette.CardBorder,
                new Rect(button.Left+6,button.Top+5,18,17), 4, 4);
            Label(drawing, color ?? "Default", button.Left+31, button.Top+5, 11, _text, width:width-51);
            Label(drawing, "⌄", button.Right-18, button.Top+4, 11, _dim);
            Hit(name + ": " + (color ?? "Default") + " · Choose color", row, () =>
            {
                var anchor = new Rect(row.Right-Math.Max(width,145)-7, button.Top-_scroll, Math.Max(width,145), button.Height);
                var options = StripSwatches.Select(swatch =>
                    new SettingOption(swatch.Name, () => ApplyStripColor(role, swatch.Hex), swatch.Hex)).ToList();
                options.Insert(0, new("Default", () => ApplyStripColor(role, null), DefaultStripSwatch(role)));
                options.Add(new("Custom hex…", () => Edit(name, color ?? "")));
                var current = color is null ? "Default" : StripSwatches
                    .FirstOrDefault(swatch => string.Equals(swatch.Hex, color, StringComparison.OrdinalIgnoreCase)).Name ?? "Custom hex…";
                _settingsMenu = new(name, current, anchor, options.ToArray());
                Draw(_scale);
                FocusMenuOption(Math.Max(0, options.FindIndex(option => option.Label == current)));
            });
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
            if (settings.Edge == BarEdge.Bottom)
                Dropdown(drawing, "Mode", settings.Mode, Enum.GetValues<BarMode>(), value => value.ToString(),
                    value => Change(s => s with { Mode = value }, $"Mode set to {value}. Saved."), ref y);
            Dropdown(drawing, "Monitors", settings.Monitors, new[] { MonitorSelection.Primary, MonitorSelection.All },
                value => value switch { MonitorSelection.Primary => "Primary monitor", MonitorSelection.All => "All monitors", _ => "Selected monitor" },
                value => Change(s => s with { Monitors = value }, "Monitor selection saved."), ref y);
            Dropdown(drawing, "Bar refresh", settings.IntervalSeconds, RefreshIntervals, Seconds,
                value => Change(s => s with { IntervalSeconds = value }, $"Bar refresh set to {Seconds(value)}. Saved."), ref y);

            Heading(drawing, "STRIP COLORS", ref y);
            foreach (var role in Enum.GetValues<StripColor>()) StripColorPicker(drawing, settings, role, ref y);
            if (settings.StripColors.Count > 0)
                Choice(drawing, "Reset strip colors", "Restore defaults", ref y,
                    () => Change(s => s with { StripColors = [] }, "Default strip colors restored."));

            Heading(drawing, "READINGS", ref y);
            Dropdown(drawing, "Transfer units", settings.UseBits, new[] { false, true }, value => value ? "Bits / second" : "Bytes / second",
                value => Change(s => s with { UseBits = value }, "Transfer units saved."), ref y);
            Dropdown(drawing, "Temperature", settings.Fahrenheit, new[] { false, true }, value => value ? "Fahrenheit (°F)" : "Celsius (°C)",
                value => Change(s => s with { Fahrenheit = value }, "Temperature units saved."), ref y);
            Toggle(drawing, "Pause monitoring", settings.Pause, ref y,
                () => Change(s => s with { Pause = !s.Pause }, settings.Pause ? "Monitoring resumed. Saved." : "Monitoring paused. Saved."));

            Choice(drawing, "Gaming", "Open gaming settings", ref y, () => Select(GamingPage));

            Heading(drawing, "SUSTAINED ALERTS", ref y);
            Toggle(drawing, "Windows notifications", settings.SustainedAlerts, ref y,
                () => Change(s => s with { SustainedAlerts = !s.SustainedAlerts }));
            Dropdown(drawing, "CPU and RAM usage", settings.SustainedUsagePercent,
                new[] { 70, 75, 80, 85, 90, 95, 99 }, value => $"{value}%",
                value => Change(s => s with { SustainedUsagePercent = value }), ref y);
            Dropdown(drawing, "Low battery while unplugged", settings.SustainedBatteryPercent,
                new[] { 5, 10, 15, 20, 25, 30, 35, 40 }, value => $"{value}%",
                value => Change(s => s with { SustainedBatteryPercent = value }), ref y);
            Dropdown(drawing, "Time above or below limit", settings.SustainedSeconds,
                new[] { 30, 60, 120, 300 }, value => value < 60 ? $"{value} seconds" : $"{value / 60} min",
                value => Change(s => s with { SustainedSeconds = value }), ref y);

            Heading(drawing, "MODULES", ref y);
            foreach (var item in BarSettings.ConfigurableModules)
                Choice(drawing, item.ToString(), settings.Items.Contains(item) ? "In strip" : "Hidden", ref y, () => OpenModuleSettings(item));
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

        private Rect SettingsRow(DrawingContext drawing, string name, ref double y, double labelInset = 12)
        {
            var row = new Rect(235, y - 3, Width - 275, 35);
            y += 41;
            if (row.Bottom <= 66 + _scroll || row.Top >= Height - 54 + _scroll) return Rect.Empty;
            drawing.DrawRoundedRectangle(_surface, _palette.CardBorder, row, 6, 6);
            Label(drawing, name, row.Left + labelInset, row.Top + 9, 12, _text, width: row.Width * .5 - labelInset);
            return row;
        }

        private void DrawSettingsMenu(DrawingContext drawing)
        {
            if (_settingsMenu is not { } menu) return;
            var optionHeight = menu.Options.Any(option => option.Swatch is not null) ? 25.0 : 29.0;
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
                var textInset = 9;
                if (option.Swatch is { } hex)
                {
                    drawing.DrawRoundedRectangle(SwatchBrush(hex), _palette.CardBorder,
                        new Rect(item.Left+9,item.Top+(item.Height-17)/2,17,17), 4, 4);
                    textInset = 33;
                }
                Label(drawing, option.Label, item.Left + textInset, item.Top + 6, 12, brush, selected, item.Width - textInset - 23);
                if (selected) Label(drawing, "✓", item.Right - 20, item.Top + 6, 12, brush);
                Hit(MenuOptionName(option.Label, selected), item, () =>
                {
                    _settingsMenu = null;
                    option.Apply();
                    if (_editing is null) FocusSettingsRow(menu.Name);
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
