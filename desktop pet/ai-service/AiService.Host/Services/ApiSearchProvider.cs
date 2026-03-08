using AiService.Host.Contracts;
using AiService.Host.Options;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiService.Host.Services;

/// <summary>
/// Handles JSON API-style search sources.
/// </summary>
public sealed class ApiSearchProvider : IWebSearchProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SearchOptions _options;

    public ApiSearchProvider(IHttpClientFactory httpClientFactory, IOptions<SearchOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Name => "api-search";

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentSearchItem>> SearchAsync(string query, int maxResults, string category, CancellationToken cancellationToken)
    {
        var sources = _options.SiteSearches
            .Where(x => x.Enabled && x.ComplianceChecked && !string.IsNullOrWhiteSpace(x.UrlTemplate))
            .Where(x => IsApiSource(x.Name))
            .Where(x => ShouldUseSource(x.Category, category));

        var results = new List<AgentSearchItem>();
        foreach (var source in sources)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(Math.Max(2, _options.TimeoutSeconds));
                client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);

                var sourceQuery = NormalizeQueryForSource(query, source.Name, category);
                var url = source.UrlTemplate.Replace("{query}", WebUtility.UrlEncode(sourceQuery), StringComparison.OrdinalIgnoreCase);

                if (source.Name.StartsWith("open-meteo", StringComparison.OrdinalIgnoreCase))
                {
                    var weatherItems = await SearchOpenMeteoWithForecastAsync(client, source.Name, url, maxResults, cancellationToken);
                    results.AddRange(weatherItems);
                    continue;
                }

                var json = await client.GetStringAsync(url, cancellationToken);
                results.AddRange(ParseSourceResults(source.Name, url, json, maxResults));
            }
            catch
            {
                // Ignore one-source failure and continue.
            }
        }

        return results
            .Where(x => !string.IsNullOrWhiteSpace(x.Title) && !string.IsNullOrWhiteSpace(x.Url))
            .Take(maxResults)
            .ToList();
    }

    private async Task<IReadOnlyList<AgentSearchItem>> SearchOpenMeteoWithForecastAsync(HttpClient client, string sourceName, string geocodeUrl, int maxResults, CancellationToken cancellationToken)
    {
        var geocodeJson = await client.GetStringAsync(geocodeUrl, cancellationToken);
        using var geocodeDoc = JsonDocument.Parse(geocodeJson);

        var items = new List<AgentSearchItem>();
        if (!geocodeDoc.RootElement.TryGetProperty("results", out var places) || places.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (var place in places.EnumerateArray())
        {
            if (items.Count >= maxResults)
            {
                break;
            }

            var placeName = TryGetString(place, "name");
            var country = TryGetString(place, "country");
            var admin1 = TryGetString(place, "admin1");
            var lat = TryGetNumber(place, "latitude");
            var lon = TryGetNumber(place, "longitude");

            if (string.IsNullOrWhiteSpace(placeName) || lat is null || lon is null)
            {
                continue;
            }

            var forecastUrl = BuildForecastUrl(lat.Value, lon.Value);
            string snippet;
            try
            {
                var forecastJson = await client.GetStringAsync(forecastUrl, cancellationToken);
                snippet = BuildForecastSnippet(forecastJson, lat.Value, lon.Value);
            }
            catch
            {
                snippet = $"坐标: {lat.Value.ToString(CultureInfo.InvariantCulture)}, {lon.Value.ToString(CultureInfo.InvariantCulture)}";
            }

            var title = string.IsNullOrWhiteSpace(admin1)
                ? $"{placeName} ({country})"
                : $"{placeName}, {admin1} ({country})";

            items.Add(new AgentSearchItem
            {
                Source = $"api:{sourceName}",
                Title = title,
                Url = forecastUrl,
                Snippet = snippet,
            });
        }

        return items;
    }

    private static string BuildForecastUrl(double lat, double lon)
    {
        var latText = lat.ToString(CultureInfo.InvariantCulture);
        var lonText = lon.ToString(CultureInfo.InvariantCulture);
        return $"https://api.open-meteo.com/v1/forecast?latitude={latText}&longitude={lonText}&current=temperature_2m,weather_code,wind_speed_10m&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto&forecast_days=1";
    }

    private static string BuildForecastSnippet(string forecastJson, double lat, double lon)
    {
        using var doc = JsonDocument.Parse(forecastJson);
        var root = doc.RootElement;

        var currentTemp = root.TryGetProperty("current", out var current) ? TryGetNumber(current, "temperature_2m") : null;
        var weatherCode = root.TryGetProperty("current", out current) ? TryGetInt(current, "weather_code") : null;
        var wind = root.TryGetProperty("current", out current) ? TryGetNumber(current, "wind_speed_10m") : null;

        double? tMax = null;
        double? tMin = null;
        double? rainProb = null;
        if (root.TryGetProperty("daily", out var daily))
        {
            tMax = TryGetFirstArrayNumber(daily, "temperature_2m_max");
            tMin = TryGetFirstArrayNumber(daily, "temperature_2m_min");
            rainProb = TryGetFirstArrayNumber(daily, "precipitation_probability_max");
        }

        var parts = new List<string>();
        if (currentTemp is not null) parts.Add($"当前 {currentTemp:0.#}°C");
        if (weatherCode is not null) parts.Add($"天气 {WeatherCodeToText(weatherCode.Value)}");
        if (wind is not null) parts.Add($"风速 {wind:0.#} km/h");
        if (tMax is not null || tMin is not null) parts.Add($"今日 {tMin:0.#}~{tMax:0.#}°C");
        if (rainProb is not null) parts.Add($"降水概率 {rainProb:0.#}%");

        if (parts.Count == 0)
        {
            return $"坐标: {lat.ToString(CultureInfo.InvariantCulture)}, {lon.ToString(CultureInfo.InvariantCulture)}";
        }

        return string.Join("; ", parts);
    }

    private static string WeatherCodeToText(int code)
    {
        return code switch
        {
            0 => "晴",
            1 or 2 => "多云",
            3 => "阴",
            45 or 48 => "雾",
            51 or 53 or 55 => "毛毛雨",
            56 or 57 => "冻毛毛雨",
            61 or 63 or 65 => "雨",
            66 or 67 => "冻雨",
            71 or 73 or 75 or 77 => "雪",
            80 or 81 or 82 => "阵雨",
            85 or 86 => "阵雪",
            95 => "雷暴",
            96 or 99 => "雷暴伴冰雹",
            _ => $"代码{code}",
        };
    }

    private static string NormalizeQueryForSource(string query, string sourceName, string category)
    {
        var q = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(q))
        {
            return q;
        }

        var n = sourceName.Trim().ToLowerInvariant();
        if (n.StartsWith("open-meteo", StringComparison.Ordinal) || category.Equals("weather", StringComparison.OrdinalIgnoreCase))
        {
            return CleanupWeatherQuery(q);
        }

        if (n.StartsWith("the-dog-api", StringComparison.Ordinal)
            || n.StartsWith("the-cat-api", StringComparison.Ordinal)
            || n.StartsWith("wikimedia-opensearch", StringComparison.Ordinal)
            || category.Equals("pet", StringComparison.OrdinalIgnoreCase))
        {
            return CleanupPetQuery(q);
        }

        return q;
    }

    private static string CleanupWeatherQuery(string q)
    {
        var cleaned = q;
        string[] noise = ["天气", "气温", "温度", "降雨", "预报", "怎么样", "如何", "多少", "吗", "？", "?", "请问", "明天", "今天", "后天"];
        foreach (var n in noise)
        {
            cleaned = cleaned.Replace(n, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? q : cleaned;
    }

    private static string CleanupPetQuery(string q)
    {
        var cleaned = q;
        string[] noise = ["品种", "推荐", "适合", "新手", "养", "宠物", "狗", "猫", "请问", "吗", "？", "?", "怎么", "如何"];
        foreach (var n in noise)
        {
            cleaned = cleaned.Replace(n, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? q : cleaned;
    }

    private static bool IsApiSource(string name)
    {
        var n = (name ?? string.Empty).Trim().ToLowerInvariant();
        return n.StartsWith("open-meteo", StringComparison.Ordinal)
            || n.StartsWith("the-dog-api", StringComparison.Ordinal)
            || n.StartsWith("the-cat-api", StringComparison.Ordinal)
            || n.StartsWith("wikimedia-opensearch", StringComparison.Ordinal);
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

    private static IReadOnlyList<AgentSearchItem> ParseSourceResults(string sourceName, string url, string json, int maxResults)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var name = sourceName.ToLowerInvariant();

        if (name.StartsWith("the-dog-api", StringComparison.Ordinal) || name.StartsWith("the-cat-api", StringComparison.Ordinal))
        {
            return ParseBreedApi(root, sourceName, url, maxResults);
        }

        if (name.StartsWith("wikimedia-opensearch", StringComparison.Ordinal))
        {
            return ParseWikiOpenSearch(root, sourceName, maxResults);
        }

        return [];
    }

    private static IReadOnlyList<AgentSearchItem> ParseBreedApi(JsonElement root, string sourceName, string requestUrl, int maxResults)
    {
        var items = new List<AgentSearchItem>();
        if (root.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (var breed in root.EnumerateArray())
        {
            var name = TryGetString(breed, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var temperament = TryGetString(breed, "temperament");
            var origin = TryGetString(breed, "origin");
            var life = TryGetString(breed, "life_span");
            var wiki = TryGetString(breed, "wikipedia_url");

            var snippetParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(temperament)) snippetParts.Add($"性格: {temperament}");
            if (!string.IsNullOrWhiteSpace(origin)) snippetParts.Add($"起源: {origin}");
            if (!string.IsNullOrWhiteSpace(life)) snippetParts.Add($"寿命: {life}");

            items.Add(new AgentSearchItem
            {
                Source = $"api:{sourceName}",
                Title = name,
                Url = string.IsNullOrWhiteSpace(wiki) ? requestUrl : wiki,
                Snippet = string.Join("; ", snippetParts),
            });

            if (items.Count >= maxResults)
            {
                break;
            }
        }

        return items;
    }

    private static IReadOnlyList<AgentSearchItem> ParseWikiOpenSearch(JsonElement root, string sourceName, int maxResults)
    {
        var items = new List<AgentSearchItem>();
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 4)
        {
            return items;
        }

        var titles = root[1];
        var snippets = root[2];
        var links = root[3];
        if (titles.ValueKind != JsonValueKind.Array || links.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        var len = Math.Min(titles.GetArrayLength(), links.GetArrayLength());
        for (var i = 0; i < len && items.Count < maxResults; i++)
        {
            var title = titles[i].GetString();
            var url = links[i].GetString();
            var snippet = snippets.ValueKind == JsonValueKind.Array && i < snippets.GetArrayLength()
                ? snippets[i].GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            items.Add(new AgentSearchItem
            {
                Source = $"api:{sourceName}",
                Title = title,
                Url = url,
                Snippet = snippet,
            });
        }

        return items;
    }

    private static string? TryGetString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static double? TryGetNumber(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.GetDouble();
    }

    private static int? TryGetInt(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.GetInt32();
    }

    private static double? TryGetFirstArrayNumber(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
        {
            return null;
        }

        var first = value[0];
        return first.ValueKind == JsonValueKind.Number ? first.GetDouble() : null;
    }
}
