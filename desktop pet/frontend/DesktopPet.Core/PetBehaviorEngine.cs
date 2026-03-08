namespace DesktopPet.Core;

/// <summary>
/// Handles interaction state transitions and cooldown checks.
/// </summary>
public sealed class PetBehaviorEngine
{
    private readonly TimeSpan _interactDuration;
    private readonly TimeSpan _cooldown;
    private DateTimeOffset _interactEndAt = DateTimeOffset.MinValue;
    private DateTimeOffset _nextInteractAllowedAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a behavior engine.
    /// </summary>
    /// <param name="interactDuration">Duration for the interact state.</param>
    /// <param name="cooldown">Cooldown between interactions.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when duration values are negative.</exception>
    public PetBehaviorEngine(TimeSpan interactDuration, TimeSpan cooldown)
    {
        if (interactDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interactDuration));
        }

        if (cooldown < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cooldown));
        }

        _interactDuration = interactDuration;
        _cooldown = cooldown;
    }

    /// <summary>
    /// Gets the current state.
    /// </summary>
    public PetState CurrentState { get; private set; } = PetState.Idle;

    /// <summary>
    /// Attempts to enter interact state.
    /// </summary>
    /// <param name="now">Current time used for deterministic transitions.</param>
    /// <returns><c>true</c> when interaction is accepted; otherwise <c>false</c>.</returns>
    public bool TryInteract(DateTimeOffset now)
    {
        if (now < _nextInteractAllowedAt)
        {
            return false;
        }

        CurrentState = PetState.Interact;
        _interactEndAt = now + _interactDuration;
        _nextInteractAllowedAt = now + _cooldown;
        return true;
    }

    /// <summary>
    /// Progresses internal timers and exits interact state when needed.
    /// </summary>
    /// <param name="now">Current time used for deterministic transitions.</param>
    /// <returns><c>true</c> if state changed; otherwise <c>false</c>.</returns>
    public bool Tick(DateTimeOffset now)
    {
        if (CurrentState == PetState.Interact && now >= _interactEndAt)
        {
            CurrentState = PetState.Idle;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Forces state back to idle after an interaction animation has completed.
    /// </summary>
    public void ForceIdle()
    {
        CurrentState = PetState.Idle;
    }
}
