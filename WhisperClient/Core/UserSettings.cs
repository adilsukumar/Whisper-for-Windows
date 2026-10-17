using System.Text.Json;
using System.IO;

namespace WhisperClient.Core;

public sealed record UserSettings
{
    public const int CurrentSchema = 1;
    public int SchemaVersion { get; init; } = CurrentSchema;
    public bool SpeakResponses { get; init; } = true;
    public bool IncludeScreenForVoice { get; init; }
    public bool ReducedMotion { get; init; }
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string SettingsPath { get; }

    public SettingsStore(string? path = null) => SettingsPath = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Whisper", "settings.json");

    public (UserSettings Settings, string? Warning) Load()
    {
        if (!File.Exists(SettingsPath)) return (new(), null);
        try
        {
            if (new FileInfo(SettingsPath).Length > 64 * 1024)
                return (new(), "Settings file is too large. Safe defaults are active; the original file is preserved.");
            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsPath), JsonOptions);
            if (settings is null || settings.SchemaVersion != UserSettings.CurrentSchema)
                return (new(), "Unsupported settings version. Safe defaults are active; the original file is preserved.");
            return (settings, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return (new(), "Settings could not be read. Safe defaults are active; the original file is preserved.");
        }
    }

    public void Save(UserSettings settings)
    {
        if (settings.SchemaVersion != UserSettings.CurrentSchema)
            throw new InvalidOperationException("Unsupported settings version.");
        if (File.Exists(SettingsPath))
        {
            // Never overwrite a future version, even if this older client uses defaults.
            try
            {
                using var existing = JsonDocument.Parse(File.ReadAllText(SettingsPath));
                if (existing.RootElement.ValueKind == JsonValueKind.Object &&
                    existing.RootElement.TryGetProperty(nameof(UserSettings.SchemaVersion), out var version) &&
                    version.ValueKind == JsonValueKind.Number &&
                    version.TryGetInt32(out var number) && number > UserSettings.CurrentSchema)
                    throw new InvalidOperationException("A newer app created these settings. Upgrade before saving.");
            }
            catch (JsonException) { /* Explicit user save may replace invalid JSON, preserving a backup below. */ }
        }
        var directory = Path.GetDirectoryName(Path.GetFullPath(SettingsPath))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $"settings-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, JsonOptions));
            if (File.Exists(SettingsPath)) File.Copy(SettingsPath, SettingsPath + ".bak", true);
            File.Move(temporary, SettingsPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
