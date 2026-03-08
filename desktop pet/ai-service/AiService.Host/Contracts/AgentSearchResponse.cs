namespace AiService.Host.Contracts;

/// <summary>
/// Response payload for agent search endpoint.
/// </summary>
public sealed class AgentSearchResponse
{
    /// <summary>
    /// Whether web retrieval was used.
    /// </summary>
    public bool UsedWeb { get; set; }

    /// <summary>
    /// Decision reason for using or skipping web retrieval.
    /// </summary>
    public string DecisionReason { get; set; } = string.Empty;

    /// <summary>
    /// Search items merged from providers.
    /// </summary>
    public List<AgentSearchItem> Items { get; set; } = [];

    /// <summary>
    /// Optional model-generated summary.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Provider-level failures.
    /// </summary>
    public List<string> Errors { get; set; } = [];
}

/// <summary>
/// One web search result item.
/// </summary>
public sealed class AgentSearchItem
{
    /// <summary>
    /// Source provider name.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Result title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Result URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Result snippet.
    /// </summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>
    /// Optional publish time.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }
}
