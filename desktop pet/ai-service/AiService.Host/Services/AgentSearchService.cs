using AiService.Host.Contracts;
using AiService.Host.Options;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiService.Host.Services;

public sealed class AgentSearchService
{
    private readonly IEnumerable<IWebSearchProvider> _providers;
    private readonly SearchDecisionService _decisionService;
    private readonly SearchOptions _options;
    private readonly LlamaGateway _llamaGateway;
    private readonly IHttpClientFactory _httpClientFactory;

    public AgentSearchService(
        IEnumerable<IWebSearchProvider> providers,
        SearchDecisionService decisionService,
        IOptions<SearchOptions> options,
        LlamaGateway llamaGateway,
        IHttpClientFactory httpClientFactory)
    {
        _providers = providers;
        _decisionService = decisionService;
        _options = options.Value;
        _llamaGateway = llamaGateway;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AgentSearchResponse> SearchAsync(AgentSearchRequest request, CancellationToken cancellationToken)
    {
        var query = (request.Query ?? string.Empty).Trim();
        var response = new AgentSearchResponse();
        if (string.IsNullOrWhiteSpace(query))
        {
            response.UsedWeb = false;
            response.DecisionReason = "empty query";
            return response;
        }

        var maxResults = NormalizeMaxResults(request.MaxResults);
        var decision = await DecideNeedWebAsync(query, cancellationToken);
        response.UsedWeb = decision.NeedWeb;

        if (!decision.NeedWeb)
        {
            response.DecisionReason = decision.Reason;
            if (request.NeedSummary)
            {
                response.Summary = await BuildNoWebSummaryAsync(query, cancellationToken);
            }

            return response;
        }

        var category = await DecideCategoryAsync(query, cancellationToken);
        var extractedQuery = await ExtractQueryByCategoryAsync(query, category, cancellationToken);
        response.DecisionReason = $"{decision.Reason}; route={category}; extracted={extractedQuery}";

        var allItems = new List<AgentSearchItem>();
        foreach (var provider in _providers)
        {
            try
            {
                var items = await provider.SearchAsync(extractedQuery, maxResults, category, cancellationToken);
                allItems.AddRange(items);
            }
            catch (Exception ex)
            {
                response.Errors.Add($"{provider.Name}: {ex.Message}");
            }
        }

        response.Items = allItems
            .Where(x => !string.IsNullOrWhiteSpace(x.Url) && !string.IsNullOrWhiteSpace(x.Title))
            .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(i => i.PublishedAt ?? DateTimeOffset.MinValue).First())
            .OrderByDescending(x => x.PublishedAt ?? DateTimeOffset.MinValue)
            .Take(maxResults)
            .ToList();

        if (request.NeedSummary)
        {
            response.Summary = await BuildWebSummaryAsync(query, category, response.Items, cancellationToken);
        }

        return response;
    }

    private int NormalizeMaxResults(int? maxResults)
    {
        var value = maxResults.GetValueOrDefault(_options.MaxResultsDefault);
        if (value <= 0)
        {
            value = _options.MaxResultsDefault;
        }

        return Math.Min(value, Math.Max(1, _options.MaxResultsMax));
    }

    private async Task<(bool NeedWeb, string Reason)> DecideNeedWebAsync(string query, CancellationToken cancellationToken)
    {
        var rule = _decisionService.DecideByRule(query);
        if (!rule.IsAmbiguous)
        {
            return (rule.NeedWeb, rule.Reason);
        }

        if (!_options.EnableModelDecision)
        {
            return (false, "rule ambiguous and model decision disabled");
        }

        try
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                messages = new[]
                {
                    new { role = "system", content = "你是联网决策器。只输出 NEED_WEB 或 NO_WEB。若查询涉及最新信息、新闻、价格、实时动态，输出 NEED_WEB；否则输出 NO_WEB。" },
                    new { role = "user", content = query },
                },
                temperature = 0,
                top_p = 0.1,
                max_tokens = 8,
            });

            var json = await _llamaGateway.ChatCompletionsAsync(payload, cancellationToken);
            var content = ParseAssistantContent(json).ToUpperInvariant();
            if (content.Contains("NEED_WEB"))
            {
                return (true, "model-assisted decision: NEED_WEB");
            }

            return (false, "model-assisted decision: NO_WEB");
        }
        catch
        {
            return (false, "model-assisted decision failed; fallback to no-web");
        }
    }

    private async Task<string> DecideCategoryAsync(string query, CancellationToken cancellationToken)
    {
        var byRule = DecideCategoryByRule(query);
        if (byRule != "general")
        {
            return byRule;
        }

        if (!_options.EnableModelDecision)
        {
            return "general";
        }

        try
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                messages = new[]
                {
                    new { role = "system", content = "你是检索路由器。只输出一个标签：news、weather、pet、general。不要输出其他内容。" },
                    new { role = "user", content = query },
                },
                temperature = 0,
                top_p = 0.1,
                max_tokens = 6,
            });

            var json = await _llamaGateway.ChatCompletionsAsync(payload, cancellationToken);
            var content = ParseAssistantContent(json).Trim().ToLowerInvariant();

            if (content.Contains("weather")) return "weather";
            if (content.Contains("pet")) return "pet";
            if (content.Contains("news")) return "news";
            return "general";
        }
        catch
        {
            return "general";
        }
    }

    private async Task<string> ExtractQueryByCategoryAsync(string query, string category, CancellationToken cancellationToken)
    {
        var byRule = TryRuleExtractQuery(query, category);
        if (!string.IsNullOrWhiteSpace(byRule) && !IsWeakExtracted(byRule, category))
        {
            return byRule;
        }

        if (!_options.EnableModelDecision)
        {
            return string.IsNullOrWhiteSpace(byRule) ? query : byRule;
        }

        try
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "你是检索词提取器。根据类别提取最适合搜索的短词，只输出 JSON：{\"value\":\"...\"}。weather 提取地名；pet 提取宠物品种/关键词；news 提取新闻主题词。禁止输出‘有什么/怎么样/推荐一下’等空泛词。"
                    },
                    new { role = "user", content = $"category={category}\nquery={query}" },
                },
                temperature = 0,
                top_p = 0.1,
                max_tokens = 40,
            });

            var json = await _llamaGateway.ChatCompletionsAsync(payload, cancellationToken);
            var content = ParseAssistantContent(json);
            var extracted = ParseJsonValue(content);
            if (!string.IsNullOrWhiteSpace(extracted) && !IsWeakExtracted(extracted, category))
            {
                return extracted;
            }

            return string.IsNullOrWhiteSpace(byRule) ? query : byRule;
        }
        catch
        {
            return string.IsNullOrWhiteSpace(byRule) ? query : byRule;
        }
    }

    private static string TryRuleExtractQuery(string query, string category)
    {
        var text = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (category == "weather")
        {
            var cnMatch = Regex.Match(text, "(?:今天|明天|后天|现在|目前)?(?<v>[\\u4e00-\\u9fa5]{2,12})(?:天气|气温|温度|降雨|预报)");
            if (cnMatch.Success)
            {
                return cnMatch.Groups["v"].Value.Trim();
            }

            var cleaned = text;
            foreach (var n in new[] { "天气", "气温", "温度", "降雨", "预报", "怎么样", "如何", "多少", "吗", "？", "?", "请问", "明天", "今天", "后天", "适合出去玩", "出去玩", "适合" })
            {
                cleaned = cleaned.Replace(n, string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? text : cleaned;
        }

        if (category == "pet")
        {
            var match = Regex.Match(text, "(?<v>[\\u4e00-\\u9fa5A-Za-z]{2,12})(?:犬|狗|猫|品种)");
            if (match.Success)
            {
                var value = match.Groups["v"].Value.Trim();
                foreach (var n in new[] { "推荐", "适合", "上班族", "新手", "养", "给我", "一个", "的" })
                {
                    value = value.Replace(n, string.Empty, StringComparison.OrdinalIgnoreCase);
                }

                value = value.Trim();
                if (!string.IsNullOrWhiteSpace(value) && value.Length >= 2)
                {
                    return value;
                }
            }

            // No concrete breed found: fallback to generic taxonomy terms.
            if (text.Contains("狗", StringComparison.OrdinalIgnoreCase) || text.Contains("犬", StringComparison.OrdinalIgnoreCase))
            {
                return "dog breed";
            }

            if (text.Contains("猫", StringComparison.OrdinalIgnoreCase))
            {
                return "cat breed";
            }

            var cleaned = text;
            foreach (var n in new[] { "推荐", "适合", "新手", "上班族", "养", "宠物", "狗", "猫", "品种", "给我", "一个", "请问", "吗", "？", "?", "怎么", "如何" })
            {
                cleaned = cleaned.Replace(n, string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? text : cleaned;
        }

        if (category == "news")
        {
            if (text.Contains("国际", StringComparison.OrdinalIgnoreCase)) return "国际";
            if (text.Contains("国内", StringComparison.OrdinalIgnoreCase)) return "国内";
            if (text.Contains("科技", StringComparison.OrdinalIgnoreCase)) return "科技";
            if (text.Contains("财经", StringComparison.OrdinalIgnoreCase)) return "财经";

            var cleaned = text;
            foreach (var n in new[] { "今天", "最新", "今日", "刚刚", "新闻", "资讯", "热点", "有什么", "有啥", "news", "latest", "breaking" })
            {
                cleaned = cleaned.Replace(n, string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? "今日要闻" : cleaned;
        }

        return text;
    }

    private static bool IsWeakExtracted(string value, string category)
    {
        var v = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(v)) return true;

        string[] weakWords = ["有什么", "有啥", "怎么样", "如何", "推荐一下", "帮我", "请问", "一下", "news", "weather", "pet", "的狗狗", "的猫猫", "狗狗", "猫猫", "宠物"];
        if (weakWords.Any(w => string.Equals(v, w, StringComparison.OrdinalIgnoreCase))) return true;

        if ((category == "news" || category == "weather" || category == "pet") && v.Length < 2) return true;
        return false;
    }

    private static string ParseJsonValue(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
            {
                return (value.GetString() ?? string.Empty).Trim();
            }
        }
        catch
        {
            var match = Regex.Match(content, "\\{[\\s\\S]*\\}");
            if (match.Success)
            {
                try
                {
                    using var doc2 = JsonDocument.Parse(match.Value);
                    if (doc2.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        return (value.GetString() ?? string.Empty).Trim();
                    }
                }
                catch
                {
                }
            }
        }

        return string.Empty;
    }

    private static string DecideCategoryByRule(string query)
    {
        var text = query.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return "general";
        }

        string[] weatherKeywords = ["天气", "气温", "降雨", "台风", "温度", "weather", "forecast", "temperature"];
        string[] petKeywords = ["宠物", "狗", "猫", "犬", "dog", "cat", "breed", "品种", "领养", "adoption", "pet"];
        string[] newsKeywords = ["新闻", "最新", "今日", "时事", "news", "latest", "breaking"];

        if (weatherKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase))) return "weather";
        if (petKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase))) return "pet";
        if (newsKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase))) return "news";
        return "general";
    }

    private async Task<string> BuildNoWebSummaryAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                messages = new[]
                {
                    new { role = "system", content = "你是一个简洁助手。" },
                    new { role = "user", content = query },
                },
                temperature = 0.6,
                top_p = 0.9,
                max_tokens = 180,
            });

            var json = await _llamaGateway.ChatCompletionsAsync(payload, cancellationToken);
            return ParseAssistantContent(json);
        }
        catch
        {
            return "未联网检索；本地总结暂时不可用。";
        }
    }

    private async Task<string> BuildWebSummaryAsync(string query, string category, IReadOnlyList<AgentSearchItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return "未检索到可用结果。";
        }

        if (category == "news")
        {
            return await BuildNewsLayeredSummaryAsync(query, items, cancellationToken);
        }

        var lines = items.Take(8).Select((x, i) => $"[{i + 1}] 标题: {x.Title}\n来源: {x.Source}\n链接: {x.Url}\n摘要: {x.Snippet}");
        var sourceContext = string.Join("\n\n", lines);

        try
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                messages = new[]
                {
                    new { role = "system", content = "根据给定检索结果进行简明总结，避免编造，优先引用有信息量的结果。" },
                    new { role = "user", content = $"用户问题: {query}\n\n检索结果:\n{sourceContext}" },
                },
                temperature = 0.4,
                top_p = 0.9,
                max_tokens = 260,
            });

            var json = await _llamaGateway.ChatCompletionsAsync(payload, cancellationToken);
            return ParseAssistantContent(json);
        }
        catch
        {
            return "检索成功，但总结阶段失败。";
        }
    }

    private async Task<string> BuildNewsLayeredSummaryAsync(string query, IReadOnlyList<AgentSearchItem> items, CancellationToken cancellationToken)
    {
        var topN = Math.Max(1, _options.NewsSummaryArticleCount);
        var candidates = items.Take(topN).ToList();

        var perArticleBriefs = new List<string>();
        foreach (var item in candidates)
        {
            var content = await TryFetchArticleTextAsync(item.Url, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                content = item.Snippet;
            }

            var limited = LimitText(content, Math.Max(300, _options.NewsSummaryArticleMaxChars));
            var brief = await SummarizeSingleArticleAsync(item.Title, limited, cancellationToken);
            if (!string.IsNullOrWhiteSpace(brief))
            {
                perArticleBriefs.Add($"标题: {item.Title}\n链接: {item.Url}\n要点: {brief}");
            }
        }

        if (perArticleBriefs.Count == 0)
        {
            return "检索到新闻，但正文抓取或摘要失败。";
        }

        var context = string.Join("\n\n", perArticleBriefs);
        try
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                messages = new[]
                {
                    new { role = "system", content = "你是新闻总结助手。基于已提供的新闻要点，输出简洁中文总结，分点说明，避免编造。" },
                    new { role = "user", content = $"用户问题: {query}\n\n新闻要点:\n{context}" },
                },
                temperature = 0.3,
                top_p = 0.9,
                max_tokens = 260,
            });

            var json = await _llamaGateway.ChatCompletionsAsync(payload, cancellationToken);
            return ParseAssistantContent(json);
        }
        catch
        {
            return string.Join("\n", perArticleBriefs.Take(3));
        }
    }

    private async Task<string> SummarizeSingleArticleAsync(string title, string articleText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(articleText))
        {
            return string.Empty;
        }

        try
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                messages = new[]
                {
                    new { role = "system", content = "你是新闻压缩器。请将文章压缩成2-3条中文要点，总字数控制在120字内。" },
                    new { role = "user", content = $"标题: {title}\n正文:\n{articleText}" },
                },
                temperature = 0.2,
                top_p = 0.9,
                max_tokens = Math.Max(60, _options.NewsSummaryPerArticleMaxTokens),
            });

            var json = await _llamaGateway.ChatCompletionsAsync(payload, cancellationToken);
            return ParseAssistantContent(json);
        }
        catch
        {
            return LimitText(articleText, 120);
        }
    }

    private async Task<string> TryFetchArticleTextAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(Math.Max(3, _options.TimeoutSeconds));
            client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);

            var html = await client.GetStringAsync(url, cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            html = Regex.Replace(html, "<script[\\s\\S]*?</script>", " ", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, "<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
            var text = Regex.Replace(html, "<[^>]+>", " ");
            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, "\\s+", " ").Trim();
            return text;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string LimitText(string input, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var value = input.Trim();
        return value.Length <= maxChars ? value : value[..maxChars];
    }

    private static string ParseAssistantContent(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var first = choices[0];
        if (!first.TryGetProperty("message", out var message))
        {
            return string.Empty;
        }

        if (!message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        return content.GetString()?.Trim() ?? string.Empty;
    }
}


