namespace AiService.Host.Contracts;

/// <summary>
/// Request payload for agent search endpoint.
/// </summary>
public sealed class AgentSearchRequest
{
    /// <summary>
    /// User query text.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Max number of items to return.
    /// </summary>
    public int? MaxResults { get; set; }

    /// <summary>
    /// Whether to generate a model summary over search items.
    /// </summary>
    public bool NeedSummary { get; set; } = true;
}
