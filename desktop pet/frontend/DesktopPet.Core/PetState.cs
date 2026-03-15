namespace DesktopPet.Core;

/// <summary>
/// Defines available states for the desktop pet.
/// </summary>
public enum PetState
{
    /// <summary>
    /// Default waiting state.
    /// </summary>
    Idle,

    /// <summary>
    /// Temporary state shown after interaction.
    /// </summary>
    Interact,

    /// <summary>
    /// speaking state shown when answering user.
    /// </summary>
    Speak,
}
