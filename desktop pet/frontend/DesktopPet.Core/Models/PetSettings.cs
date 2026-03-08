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
}
