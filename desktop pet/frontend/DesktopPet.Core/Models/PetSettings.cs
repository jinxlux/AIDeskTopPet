namespace DesktopPet.Core.Models;

/// <summary>
/// Represents persisted user settings.
/// </summary>
public sealed class PetSettings
{
    /// <summary>
    /// Gets or sets window left coordinate.
    /// </summary>
    public double Left { get; set; } = 80;

    /// <summary>
    /// Gets or sets window top coordinate.
    /// </summary>
    public double Top { get; set; } = 80;

    /// <summary>
    /// Gets or sets whether rendering prioritizes smooth playback over visual quality.
    /// </summary>
    public bool PreferSmoothRendering { get; set; } = true;

    /// <summary>
    /// Gets or sets current runtime character id.
    /// </summary>
    public string CurrentCharacterId { get; set; } = "dog_default";

    /// <summary>
    /// Gets or sets the preferred AI model id selected from the frontend.
    /// </summary>
    public string PreferredModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the preferred AI model path relative to ai-service/models.
    /// </summary>
    public string PreferredModelRelativePath { get; set; } = string.Empty;
}

