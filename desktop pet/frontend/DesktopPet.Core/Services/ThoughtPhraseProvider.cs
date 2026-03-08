namespace DesktopPet.Core.Services;

/// <summary>
/// Provides cute idle thought phrases and avoids immediate repetition.
/// </summary>
public sealed class ThoughtPhraseProvider
{
    private static readonly IReadOnlyList<string> DefaultPhrases =
    [
        "今天吃什么呢？",
        "你开心我就开心。",
        "要不要一起发呆？",
        "我在练习打坐。",
        "今天也要元气满满。",
        "小狗也会想你。",
        "风有点舒服呀。",
        "我想要摸摸头。",
        "你在忙什么呀？",
        "要不要喝口水？",
        "我在等你的夸夸。",
        "今天也会是好日子。",
        "想出去晒太阳。",
        "我在认真可爱。",
    ];

    private readonly IReadOnlyList<string> _phrases;
    private readonly Queue<string> _recent = new();
    private readonly int _recentWindow;

    /// <summary>
    /// Initializes a thought phrase provider.
    /// </summary>
    /// <param name="phrases">Optional custom thought phrase list.</param>
    /// <param name="recentWindow">Number of recent phrases to avoid repeating.</param>
    public ThoughtPhraseProvider(IEnumerable<string>? phrases = null, int recentWindow = 2)
    {
        var normalized = phrases?.Where(static text => !string.IsNullOrWhiteSpace(text)).ToArray() ?? [];
        _phrases = normalized.Length == 0 ? DefaultPhrases : normalized;
        _recentWindow = Math.Max(0, recentWindow);
    }

    /// <summary>
    /// Returns one random phrase while trying to avoid recent repeats.
    /// </summary>
    /// <param name="random">Random generator used to select phrase index.</param>
    /// <returns>A non-empty phrase.</returns>
    public string GetRandomPhrase(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (_phrases.Count == 1)
        {
            return _phrases[0];
        }

        var candidates = _phrases.Where(phrase => !_recent.Contains(phrase)).ToArray();
        if (candidates.Length == 0)
        {
            candidates = _phrases.ToArray();
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
