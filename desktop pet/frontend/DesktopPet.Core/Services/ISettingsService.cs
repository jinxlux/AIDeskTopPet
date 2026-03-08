using DesktopPet.Core.Models;

namespace DesktopPet.Core.Services;

/// <summary>
/// Provides persistence operations for application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads settings from storage, or returns defaults.
    /// </summary>
    /// <returns>A valid <see cref="PetSettings"/> object.</returns>
    PetSettings Load();

    /// <summary>
    /// Saves settings into storage.
    /// </summary>
    /// <param name="settings">Settings to persist.</param>
    void Save(PetSettings settings);
}
