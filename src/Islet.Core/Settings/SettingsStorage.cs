using System.Text.Json;

namespace Islet.Core.Settings;

public interface ISettingsStore
{
    SettingsDocument Load();
    void Save(SettingsDocument settings);
    string? LastWarning { get; }
}

public sealed class FileSettingsStore : ISettingsStore
{
    private readonly string _directory;
    private readonly string _settingsPath;
    private readonly string _backupPath;
    private readonly string _corruptPath;

    public string? LastWarning { get; private set; }

    public FileSettingsStore(string? baseDirectory = null)
    {
        _directory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IsletMonitor");

        _settingsPath = Path.Combine(_directory, "settings.json");
        _backupPath = Path.Combine(_directory, "settings.backup.json");
        _corruptPath = Path.Combine(_directory, "settings.corrupt.json");
    }

    public SettingsDocument Load()
    {
        LastWarning = null;
        if (!File.Exists(_settingsPath))
        {
            if (File.Exists(_backupPath))
                return TryLoadFile(_backupPath, isBackup: true);
            return new SettingsDocument();
        }

        return TryLoadFile(_settingsPath, isBackup: false);
    }

    private SettingsDocument TryLoadFile(string path, bool isBackup)
    {
        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return RecoverCorrupt(path, "Settings file was empty.");

            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("SchemaVersion", out var versionProp) && versionProp.GetInt32() > 1)
            {
                LastWarning = $"Settings schema version {versionProp.GetInt32()} is newer than supported version 1. Using safe defaults without overwriting.";
                return new SettingsDocument();
            }

            var settings = JsonSerializer.Deserialize<SettingsDocument>(json);
            if (settings is null || !settings.HasSupportedSchema)
                return RecoverCorrupt(path, "Settings schema validation failed.");

            return settings.ValidateAndSanitize();
        }
        catch (JsonException ex)
        {
            return RecoverCorrupt(path, $"Malformed JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return RecoverCorrupt(path, $"Read failure: {ex.Message}");
        }
    }

    private SettingsDocument RecoverCorrupt(string failedPath, string reason)
    {
        LastWarning = $"Corrupt settings detected ({reason}). Preserved copy and loaded backup/defaults.";
        try
        {
            if (File.Exists(failedPath))
                File.Copy(failedPath, _corruptPath, overwrite: true);
        }
        catch { }

        if (File.Exists(_backupPath) && failedPath != _backupPath)
        {
            try
            {
                var backupJson = File.ReadAllText(_backupPath);
                var backup = JsonSerializer.Deserialize<SettingsDocument>(backupJson);
                if (backup is not null && backup.HasSupportedSchema)
                    return backup.ValidateAndSanitize();
            }
            catch { }
        }

        return new SettingsDocument();
    }

    public void Save(SettingsDocument settings)
    {
        var sanitized = settings.ValidateAndSanitize();
        Directory.CreateDirectory(_directory);

        var json = JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { WriteIndented = true });
        var tempPath = Path.Combine(_directory, $"settings.{Guid.NewGuid():N}.tmp");

        File.WriteAllText(tempPath, json);

        // Atomic swap & backup preservation
        if (File.Exists(_settingsPath))
        {
            try { File.Copy(_settingsPath, _backupPath, overwrite: true); }
            catch { }
        }

        File.Move(tempPath, _settingsPath, overwrite: true);
    }
}
