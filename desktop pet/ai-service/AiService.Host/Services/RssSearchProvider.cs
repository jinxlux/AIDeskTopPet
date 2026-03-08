using AiService.Host.Contracts;
using AiService.Host.Options;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Xml.Linq;

namespace AiService.Host.Services;

/// <summary>
/// Retrieves and filters configured RSS feeds.
/// </summary>
public sealed class RssSearchProvider : IWebSearchProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SearchOptions _options;

    public RssSearchProvider(IHttpClientFactory httpClientFactory, IOptions<SearchOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Name => "rss";

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentSearchItem>> SearchAsync(string query, int maxResults, string category, CancellationToken cancellationToken)
    {
        var matchedResults = new List<AgentSearchItem>();
        var fallbackResults = new List<AgentSearchItem>();
        var feeds = _options.RssFeeds
            .Where(x => x.Enabled && x.ComplianceChecked && !string.IsNullOrWhiteSpace(x.Url))
            .Where(x => ShouldUseSource(x.Category, category));

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(2, _options.TimeoutSeconds)));

        foreach (var feed in feeds)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(Math.Max(2, _options.TimeoutSeconds));
                client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);

                var xml = await client.GetStringAsync(feed.Url, linkedCts.Token);
                var doc = XDocument.Parse(xml);
                var entries = doc.Descendants().Where(x => x.Name.LocalName is "item" or "entry");

                foreach (var entry in entries)
                {
                    var title = ReadFirst(entry, "title");
                    var link = ReadFirst(entry, "link");
                    var description = ReadFirst(entry, "description") ?? ReadFirst(entry, "summary");
                    var published = ReadFirst(entry, "pubDate") ?? ReadFirst(entry, "published") ?? ReadFirst(entry, "updated");

                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
                    {
                        continue;
                    }

                    var plainDescription = WebUtility.HtmlDecode(description ?? string.Empty);
                    var item = new AgentSearchItem
                    {
                        Source = $"rss:{feed.Name}",
                        Title = title.Trim(),
                        Url = link.Trim(),
                        Snippet = Truncate(plainDescription, 180),
                        PublishedAt = TryParseDate(published),
                    };

                    fallbackResults.Add(item);

                    if (IsQueryMatch(query, title, plainDescription))
                    {
                        matchedResults.Add(item);
                    }
                }
            }
            catch
            {
                // Ignore one-feed failure and continue with others.
            }
        }

        var preferMatched = matchedResults
            .OrderByDescending(x => x.PublishedAt ?? DateTimeOffset.MinValue)
            .ToList();

        if (preferMatched.Count > 0)
        {
            return preferMatched.Take(maxResults).ToList();
        }

        // News queries are often broad; fallback to latest headlines when no strict match.
        if (NormalizeCategory(category) == "news")
        {
            return fallbackResults
                .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.OrderByDescending(i => i.PublishedAt ?? DateTimeOffset.MinValue).First())
                .OrderByDescending(x => x.PublishedAt ?? DateTimeOffset.MinValue)
                .Take(maxResults)
                .ToList();
        }

        return [];
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

    private static string? ReadFirst(XElement node, string localName)
    {
        var candidate = node.Elements().FirstOrDefault(x => x.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
        if (candidate is null)
        {
            return null;
        }

        if (localName.Equals("link", StringComparison.OrdinalIgnoreCase))
        {
            var href = candidate.Attribute("href")?.Value;
            if (!string.IsNullOrWhiteSpace(href))
            {
                return href;
            }
        }

        return candidate.Value;
    }

    private static bool IsQueryMatch(string query, string title, string snippet)
    {
        var q = query.Trim();
        if (q.Length == 0)
        {
            return true;
        }

        if (title.Contains(q, StringComparison.OrdinalIgnoreCase) || snippet.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var terms = q.Split([' ', '，', ',', '。', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return terms.Any(term => term.Length >= 2 && (title.Contains(term, StringComparison.OrdinalIgnoreCase) || snippet.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    private static DateTimeOffset? TryParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        return null;
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
