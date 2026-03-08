using System.Text.Json;
using DesktopPet.Core.Models;
using DesktopPet.Core.Services;

namespace DesktopPet.Infrastructure;

/// <summary>
/// Persists settings in JSON format on local disk.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _settingsFilePath;

    /// <summary>
    /// Initializes a settings service instance.
    /// </summary>
    /// <param name="settingsFilePath">Target JSON file path.</param>
    public JsonSettingsService(string settingsFilePath)
    {
        _settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
            ? throw new ArgumentException("Settings file path is required.", nameof(settingsFilePath))
            : settingsFilePath;
    }

    /// <inheritdoc />
    public PetSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new PetSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<PetSettings>(json, SerializerOptions) ?? new PetSettings();
        }
        catch
        {
            // Fall back to defaults so corrupted files do not block startup.
            return new PetSettings();
        }
    }

    /// <inheritdoc />
    public void Save(PetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_settingsFilePath, json);
    }
}
