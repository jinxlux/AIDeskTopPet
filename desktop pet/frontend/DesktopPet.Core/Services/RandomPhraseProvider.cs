namespace DesktopPet.Core.Services;

/// <summary>
/// Provides random short phrases for interaction feedback.
/// </summary>
public sealed class RandomPhraseProvider
{
    private static readonly IReadOnlyList<string> DefaultPhrases =
    [
        "我在这里。",
        "今天也要开心。",
        "来互动一下吧。",
        "我会陪着你。",
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
    public string GetRandomPhrase(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return _phrases[random.Next(_phrases.Count)];
    }

    /// <summary>
    /// Returns a random phrase from the provided override list, or falls back to the provider phrases.
    /// </summary>
    public string GetRandomPhrase(Random random, IEnumerable<string>? overridePhrases)
    {
        ArgumentNullException.ThrowIfNull(random);
        var normalized = overridePhrases?.Where(static text => !string.IsNullOrWhiteSpace(text)).ToArray() ?? [];
        var source = normalized.Length == 0 ? _phrases : normalized;
        return source[random.Next(source.Count)];
    }
}
