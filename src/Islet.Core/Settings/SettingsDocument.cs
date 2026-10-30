using System.Text.Json.Serialization;

namespace Islet.Core.Settings;

public enum IslandStyle { Island, Notch, Bar }
public enum IslandEdge { Top, Bottom, Left, Right }
public enum ThemeMode { System, Light, Dark }
public enum ExpansionTrigger { Click, Hover }

public sealed record AlertSettings
{
    public bool Enabled { get; init; } = true;
    public double BatteryLowThresholdPercent { get; init; } = 20.0;
    public double PingSpikeThresholdMs { get; init; } = 200.0;
    public int CooldownSeconds { get; init; } = 60;
}

public sealed record ProfileRule
{
    public string Name { get; init; } = "Default";
    public IslandStyle Style { get; init; } = IslandStyle.Island;
    public bool SuppressAlerts { get; init; } = false;
    public string? TriggerProcessName { get; init; }
}

public sealed record SettingsDocument
{
    public int SchemaVersion { get; init; } = 1;
    public IslandStyle Style { get; init; } = IslandStyle.Island;
    public IslandEdge Edge { get; init; } = IslandEdge.Top;
    public ThemeMode Theme { get; init; } = ThemeMode.System;
    public string AccentColorHex { get; init; } = "#3B82F6";
    public double Opacity { get; init; } = 0.98;
    public double CornerRadius { get; init; } = 18.0;
    public int TransitionDurationMs { get; init; } = 250;
    public bool ReducedMotion { get; init; } = false;
    public string CollapsedMetric { get; init; } = "cpu";
    public ExpansionTrigger ExpansionTrigger { get; init; } = ExpansionTrigger.Click;
    public List<string> VisibleWidgets { get; init; } = new() { "cpu", "ram", "network", "battery" };
    public string? SelectedMonitorId { get; init; }
    public string HotkeyChord { get; init; } = "Ctrl+Alt+I";
    public bool DoNotDisturb { get; init; } = false;
    public AlertSettings Alerts { get; init; } = new();
    public List<ProfileRule> Profiles { get; init; } = new()
    {
        new() { Name = "Work", Style = IslandStyle.Island, SuppressAlerts = false },
        new() { Name = "Gaming", Style = IslandStyle.Bar, SuppressAlerts = true },
        new() { Name = "Meeting", Style = IslandStyle.Island, SuppressAlerts = true }
    };

    public bool HasSupportedSchema =>
        SchemaVersion == 1
        && Enum.IsDefined(Style)
        && Enum.IsDefined(Edge)
        && Enum.IsDefined(Theme)
        && Enum.IsDefined(ExpansionTrigger);

        public bool Equals(SettingsDocument? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return SchemaVersion == other.SchemaVersion
            && Style == other.Style
            && Edge == other.Edge
            && Theme == other.Theme
            && string.Equals(AccentColorHex, other.AccentColorHex, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(Opacity - other.Opacity) < 0.0001
            && Math.Abs(CornerRadius - other.CornerRadius) < 0.0001
            && TransitionDurationMs == other.TransitionDurationMs
            && ReducedMotion == other.ReducedMotion
            && string.Equals(CollapsedMetric, other.CollapsedMetric, StringComparison.OrdinalIgnoreCase)
            && ExpansionTrigger == other.ExpansionTrigger
            && string.Equals(SelectedMonitorId, other.SelectedMonitorId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(HotkeyChord, other.HotkeyChord, StringComparison.OrdinalIgnoreCase)
            && DoNotDisturb == other.DoNotDisturb
            && Equals(Alerts, other.Alerts)
            && (VisibleWidgets == other.VisibleWidgets || VisibleWidgets.SequenceEqual(other.VisibleWidgets, StringComparer.OrdinalIgnoreCase))
            && (Profiles == other.Profiles || Profiles.SequenceEqual(other.Profiles));
    }

    public override int GetHashCode() => HashCode.Combine(SchemaVersion, Style, Edge, Theme);
    public SettingsDocument ValidateAndSanitize()
    {
        var clampedOpacity = Math.Clamp(double.IsFinite(Opacity) ? Opacity : 0.98, 0.3, 1.0);
        var clampedRadius = Math.Clamp(double.IsFinite(CornerRadius) ? CornerRadius : 18.0, 0.0, 32.0);
        var clampedDuration = Math.Clamp(TransitionDurationMs, 200, 300);

        var widgets = VisibleWidgets?
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string> { "cpu", "ram", "network", "battery" };

        var chord = string.IsNullOrWhiteSpace(HotkeyChord) ? "Ctrl+Alt+I" : HotkeyChord.Trim();
        var hex = string.IsNullOrWhiteSpace(AccentColorHex) || !AccentColorHex.StartsWith('#') ? "#3B82F6" : AccentColorHex.Trim();

        return this with
        {
            Opacity = clampedOpacity,
            CornerRadius = clampedRadius,
            TransitionDurationMs = clampedDuration,
            VisibleWidgets = widgets,
            HotkeyChord = chord,
            AccentColorHex = hex
        };
    }
}
