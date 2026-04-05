namespace DesktopPet.Core.Services;

/// <summary>
/// Provides cute idle thought phrases and avoids immediate repetition.
/// </summary>
public sealed class ThoughtPhraseProvider
{
    private static readonly IReadOnlyList<string> DefaultPhrases =
    [
        "今天吃什么呢？",
        "要不要先休息一下？",
        "我在这里安静陪你。",
        "要不要一起发会儿呆？",
        "今天也会是好日子。",
        "先喝口水也不错。",
        "我在等你看我一眼。",
        "阳光好的时候心情也会变好。",
    ];

    private readonly IReadOnlyList<string> _phrases;
    private readonly Queue<string> _recent = new();
    private readonly int _recentWindow;

    /// <summary>
    /// Initializes a thought phrase provider.
    /// </summary>
    public ThoughtPhraseProvider(IEnumerable<string>? phrases = null, int recentWindow = 2)
    {
        var normalized = phrases?.Where(static text => !string.IsNullOrWhiteSpace(text)).ToArray() ?? [];
        _phrases = normalized.Length == 0 ? DefaultPhrases : normalized;
        _recentWindow = Math.Max(0, recentWindow);
    }

    /// <summary>
    /// Returns one random phrase while trying to avoid recent repeats.
    /// </summary>
    public string GetRandomPhrase(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return GetRandomPhrase(random, null);
    }

    /// <summary>
    /// Returns one random phrase from the provided override list while trying to avoid recent repeats.
    /// </summary>
    public string GetRandomPhrase(Random random, IEnumerable<string>? overridePhrases)
    {
        ArgumentNullException.ThrowIfNull(random);

        var source = overridePhrases?.Where(static text => !string.IsNullOrWhiteSpace(text)).ToArray();
        var phrases = source is { Length: > 0 } ? source : _phrases.ToArray();

        if (phrases.Length == 1)
        {
            return phrases[0];
        }

        var candidates = phrases.Where(phrase => !_recent.Contains(phrase)).ToArray();
        if (candidates.Length == 0)
        {
            candidates = phrases.ToArray();
        }

        var selected = candidates[random.Next(candidates.Length)];
        Remember(selected);
        return selected;
    }

    private void Remember(string phrase)
    {
        if (_recentWindow == 0)
        {
            return;
        }

        _recent.Enqueue(phrase);
        while (_recent.Count > _recentWindow)
        {
            _recent.Dequeue();
        }
    }
}
