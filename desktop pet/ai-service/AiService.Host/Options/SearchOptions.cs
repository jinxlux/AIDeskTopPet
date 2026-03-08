namespace AiService.Host.Options;

/// <summary>
/// Configures free web search sources and retrieval safeguards.
/// </summary>
public sealed class SearchOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Search";

    /// <summary>
    /// Request timeout in seconds for each provider call.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 8;

    /// <summary>
    /// Default max number of search items to return.
    /// </summary>
    public int MaxResultsDefault { get; set; } = 5;

    /// <summary>
    /// Hard cap of max results.
    /// </summary>
    public int MaxResultsMax { get; set; } = 10;

    /// <summary>
    /// User-Agent used by outbound fetches.
    /// </summary>
    public string UserAgent { get; set; } = "DesktopPet-AiService/1.0";

    /// <summary>
    /// Whether LLM-assisted decision is enabled when rules are ambiguous.
    /// </summary>
    public bool EnableModelDecision { get; set; } = true;

    /// <summary>
    /// RSS feed sources.
    /// </summary>
    public List<RssFeedOptions> RssFeeds { get; set; } = [];

    /// <summary>
    /// Site-search scraping sources.
    /// </summary>
    public List<SiteSearchOptions> SiteSearches { get; set; } = [];
}

/// <summary>
/// One RSS source option.
/// </summary>
public sealed class RssFeedOptions
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool ComplianceChecked { get; set; } = false;

    /// <summary>
    /// Source category for routing. Supported: news, weather, pet, general.
    /// </summary>
    public string Category { get; set; } = "news";
}

/// <summary>
/// One site-search source option.
/// </summary>
public sealed class SiteSearchOptions
{
    public string Name { get; set; } = string.Empty;
    public string UrlTemplate { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool ComplianceChecked { get; set; } = false;

    /// <summary>
    /// Source category for routing. Supported: news, weather, pet, general.
    /// </summary>
    public string Category { get; set; } = "general";
}
