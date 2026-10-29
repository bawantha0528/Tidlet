using System.Text.Json;

namespace Islet.Core.Settings;

public sealed record SettingsEnvelope
{
    public int EnvelopeVersion { get; init; } = 1;
    public string ExportedUtc { get; init; } = DateTime.UtcNow.ToString("O");
    public string Product { get; init; } = "Tidlet Monitor";
    public SettingsDocument Settings { get; init; } = new();

    public static string Export(SettingsDocument settings)
    {
        var envelope = new SettingsEnvelope
        {
            EnvelopeVersion = 1,
            ExportedUtc = DateTime.UtcNow.ToString("O"),
            Settings = settings.ValidateAndSanitize()
        };
        return JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
    }

    public static (bool Success, SettingsDocument? Settings, string? Error) TryImport(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (false, null, "Import content is empty.");

        if (json.Length > 512 * 1024)
            return (false, null, "Import payload exceeds the 512 KB safety limit.");

        try
        {
            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Settings", out var settingsProp))
            {
                // Direct SettingsDocument import fallback
                var direct = JsonSerializer.Deserialize<SettingsDocument>(json);
                if (direct is not null && direct.HasSupportedSchema)
                    return (true, direct.ValidateAndSanitize(), null);
                return (false, null, "JSON does not contain a supported Settings schema.");
            }

            var envelope = JsonSerializer.Deserialize<SettingsEnvelope>(json);
            if (envelope?.Settings is null)
                return (false, null, "Failed to parse Settings from envelope.");

            if (!envelope.Settings.HasSupportedSchema)
                return (false, null, $"Unsupported schema version {envelope.Settings.SchemaVersion}.");

            return (true, envelope.Settings.ValidateAndSanitize(), null);
        }
        catch (JsonException ex)
        {
            return (false, null, $"Malformed JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, null, $"Import failed: {ex.Message}");
        }
    }
}
