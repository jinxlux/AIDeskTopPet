using AiService.Host.Contracts;
using AiService.Host.Options;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.RegularExpressions;

namespace AiService.Host.Services;

/// <summary>
/// Scrapes configured site-search pages with template URLs.
/// </summary>
public sealed class SiteSearchProvider : IWebSearchProvider
{
    private static readonly Regex AnchorRegex = new(
        "<a\\b[^>]*href=\\\"(?<href>[^\\\"]+)\\\"[^>]*>(?<title>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SearchOptions _options;

    public SiteSearchProvider(IHttpClientFactory httpClientFactory, IOptions<SearchOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Name => "site-search";

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentSearchItem>> SearchAsync(string query, int maxResults, string category, CancellationToken cancellationToken)
    {
        var results = new List<AgentSearchItem>();
        var sources = _options.SiteSearches
            .Where(x => !IsApiSource(x.Name))
            .Where(x => x.Enabled && x.ComplianceChecked && !string.IsNullOrWhiteSpace(x.UrlTemplate))
            .Where(x => ShouldUseSource(x.Category, category));

        foreach (var source in sources)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(Math.Max(2, _options.TimeoutSeconds));
                client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);

                var url = source.UrlTemplate.Replace("{query}", WebUtility.UrlEncode(query), StringComparison.OrdinalIgnoreCase);
                var html = await client.GetStringAsync(url, cancellationToken);

                foreach (Match match in AnchorRegex.Matches(html))
                {
                    var href = WebUtility.HtmlDecode(match.Groups["href"].Value.Trim());
                    var rawTitle = match.Groups["title"].Value;
                    var title = CleanupHtml(rawTitle);

                    if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    if (!Uri.TryCreate(href, UriKind.Absolute, out var absUri))
                    {
                        continue;
                    }

                    if (!IsQueryMatch(query, title))
                    {
                        continue;
                    }

                    results.Add(new AgentSearchItem
                    {
                        Source = $"site:{source.Name}",
                        Title = Truncate(title, 120),
                        Url = absUri.ToString(),
                        Snippet = string.Empty,
                    });

                    if (results.Count >= maxResults * 3)
                    {
                        break;
                    }
                }
            }
            catch
            {
                // Ignore one-source failure and continue.
            }
        }

        return results.Take(maxResults).ToList();
    }

    private static bool IsApiSource(string name)
    {
        var n = (name ?? string.Empty).Trim().ToLowerInvariant();
        return n.StartsWith("open-meteo", StringComparison.Ordinal)
            || n.StartsWith("the-dog-api", StringComparison.Ordinal)
            || n.StartsWith("the-cat-api", StringComparison.Ordinal);
    }

    private static bool ShouldUseSource(string sourceCategory, string requestedCategory)
    {
        var src = NormalizeCategory(sourceCategory);
        var req = NormalizeCategory(requestedCategory);

        if (req == "general")
        {
            return true;
        }

        return src == req || src == "general";
    }

    private static string NormalizeCategory(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "news" or "weather" or "pet" ? normalized : "general";
    }

    private static bool IsQueryMatch(string query, string title)
    {
        var q = query.Trim();
        if (q.Length == 0)
        {
            return true;
        }

        if (title.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var terms = q.Split([' ', '，', ',', '。', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return terms.Any(term => term.Length >= 2 && title.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string CleanupHtml(string text)
    {
        var withoutTags = Regex.Replace(text, "<.*?>", string.Empty);
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return decoded.Replace("\n", " ").Replace("\r", " ").Trim();
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "...";
    }
}
