using System.Text.Json;

namespace MagicMouse.Core.Settings;

public sealed record AppProfile(string ExecutablePath, string DisplayName, bool UseCustomSettings, SettingsOverrides? Overrides = null);

public sealed class SettingsOverrides
{
    public Dictionary<string, double> Numbers { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, bool> Toggles { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Choices { get; set; } = new(StringComparer.Ordinal);
}

public static class ProfileResolver
{
    public static UserSettings EffectiveSettings(UserSettings global, AppProfile? profile)
    {
        var result = new UserSettings
        {
            Numbers = new(global.Numbers, StringComparer.Ordinal),
            Toggles = new(global.Toggles, StringComparer.Ordinal),
            Choices = new(global.Choices, StringComparer.Ordinal)
        };
        if (profile is not { UseCustomSettings: true, Overrides: not null }) return result;
        foreach (var pair in profile.Overrides.Numbers) result.Numbers[pair.Key] = pair.Value;
        foreach (var pair in profile.Overrides.Toggles) result.Toggles[pair.Key] = pair.Value;
        foreach (var pair in profile.Overrides.Choices) result.Choices[pair.Key] = pair.Value;
        return result;
    }
    public static AppProfile? Resolve(string? foregroundExecutablePath, IEnumerable<AppProfile> profiles)
    {
        if (string.IsNullOrWhiteSpace(foregroundExecutablePath)) return null;
        try
        {
            var foregroundPath = Path.GetFullPath(foregroundExecutablePath);
            return profiles.FirstOrDefault(profile => string.Equals(
                Path.GetFullPath(profile.ExecutablePath), foregroundPath, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}

public sealed class UserSettings
{
    public UserSettings Validate()
    {
        if (SettingsSchemaVersion != 1) throw new InvalidDataException("Esta versión de configuración no es compatible.");
        if (Numbers is null || Toggles is null || Choices is null || Profiles is null)
            throw new InvalidDataException("El archivo de configuración está incompleto.");
        if (Numbers.Any(pair => !double.IsFinite(pair.Value))) throw new InvalidDataException("Hay un valor numérico no válido.");
        if (Choices.Any(pair => pair.Value is null)) throw new InvalidDataException("Hay una opción sin valor.");
        foreach (var profile in Profiles)
        {
            if (profile is null || string.IsNullOrWhiteSpace(profile.ExecutablePath) || string.IsNullOrWhiteSpace(profile.DisplayName))
                throw new InvalidDataException("Hay un perfil de aplicación no válido.");
            if (profile.Overrides is { } overrides && (overrides.Numbers is null || overrides.Toggles is null || overrides.Choices is null || overrides.Numbers.Any(pair => !double.IsFinite(pair.Value)) || overrides.Choices.Any(pair => pair.Value is null)))
                throw new InvalidDataException("Los ajustes del perfil no son válidos.");
        }
        return this;
    }
    public int SettingsSchemaVersion { get; set; } = 1;
    public Dictionary<string, double> Numbers { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, bool> Toggles { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Choices { get; set; } = new(StringComparer.Ordinal);
    public List<AppProfile> Profiles { get; set; } = [];
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    public string FilePath { get; }

    public SettingsStore(string? filePath = null) => FilePath = filePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MagicMouseForWindows", "settings.json");

    public async Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath)) return new UserSettings();
        await using var stream = File.OpenRead(FilePath);
        return (await JsonSerializer.DeserializeAsync<UserSettings>(stream, JsonOptions, cancellationToken)
               ?? throw new InvalidDataException("El archivo de configuración está vacío.")).Validate();
    }

    public async Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = FilePath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        finally { _saveGate.Release(); }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            if (File.Exists(FilePath)) File.Delete(FilePath);
            var emptyJson = JsonSerializer.Serialize(new UserSettings(), JsonOptions);
            await File.WriteAllTextAsync(FilePath + ".tmp", emptyJson, cancellationToken);
            File.Move(FilePath + ".tmp", FilePath, overwrite: true);
        }
        finally { _saveGate.Release(); }
    }
}
