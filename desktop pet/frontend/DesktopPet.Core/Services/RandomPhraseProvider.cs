namespace DesktopPet.Core.Services;

/// <summary>
/// Provides random short phrases for interaction feedback.
/// </summary>
public sealed class RandomPhraseProvider
{
    private static readonly IReadOnlyList<string> DefaultPhrases =
    [
        "汪~",
        "摸摸头！",
        "我在这里。",
        "今天也要开心。",
    ];

    private readonly IReadOnlyList<string> _phrases;

    /// <summary>
    /// Initializes a phrase provider.
    /// </summary>
    /// <param name="phrases">Optional custom phrase set.</param>
    public RandomPhraseProvider(IEnumerable<string>? phrases = null)
    {
        var normalized = phrases?.Where(static text => !string.IsNullOrWhiteSpace(text)).ToArray() ?? [];
        _phrases = normalized.Length == 0 ? DefaultPhrases : normalized;
    }

    /// <summary>
    /// Returns a random phrase.
    /// </summary>
    /// <param name="random">Random generator used to select phrase index.</param>
    /// <returns>A non-empty phrase.</returns>
    public string GetRandomPhrase(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return _phrases[random.Next(_phrases.Count)];
    }
}
