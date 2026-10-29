namespace Islet.Core.Settings;

public enum IslandStyle { Island, Notch, Bar }
public enum ThemeMode { System, Light, Dark }

/// <summary>M0 schema foundation only. Persistence/customization belong to M3.</summary>
public sealed record SettingsDocument
{
    public int SchemaVersion { get; init; } = ProductInfo.SettingsSchemaVersion;
    public IslandStyle Style { get; init; } = IslandStyle.Island;
    public ThemeMode Theme { get; init; } = ThemeMode.System;

    public bool HasSupportedSchema =>
        SchemaVersion == ProductInfo.SettingsSchemaVersion
        && Enum.IsDefined(Style) && Enum.IsDefined(Theme);
}
