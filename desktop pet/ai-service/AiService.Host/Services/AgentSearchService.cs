using AiService.Host.Contracts;
using AiService.Host.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AiService.Host.Services;

/// <summary>
/// Orchestrates decision, retrieval, routing, deduplication, and optional summary for agent search.
/// </summary>
public sealed class AgentSearchService
{
    private readonly IEnumerable<IWebSearchProvider> _providers;
    private readonly SearchDecisionService _decisionService;
    private readonly SearchOptions _options;
    private readonly LlamaGateway _llamaGateway;

    public AgentSearchService(
        IEnumerable<IWebSearchProvider> providers,
        SearchDecisionService decisionService,
        IOptions<SearchOptions> options,
        LlamaGateway llamaGateway)
    {
        _providers = providers;
        _decisionService = decisionService;
        _options = options.Value;
        _llamaGateway = llamaGateway;
    }

    /// <summary>
    /// Executes agent search flow with web-use decision and source routing.
    /// </summary>
    /// <param name="request">Agent search request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Agent search response.</returns>
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
        response.DecisionReason = $"{decision.Reason}; route={category}";

        var allItems = new List<AgentSearchItem>();
        foreach (var provider in _providers)
        {
            try
            {
                var items = await provider.SearchAsync(query, maxResults, category, cancellationToken);
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
            response.Summary = await BuildWebSummaryAsync(query, response.Items, cancellationToken);
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

            if (content.Contains("weather"))
            {
                return "weather";
            }

            if (content.Contains("pet"))
            {
                return "pet";
            }

            if (content.Contains("news"))
            {
                return "news";
            }

            return "general";
        }
        catch
        {
            return "general";
        }
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

        if (weatherKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return "weather";
        }

        if (petKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return "pet";
        }

        if (newsKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return "news";
        }

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

    private async Task<string> BuildWebSummaryAsync(string query, IReadOnlyList<AgentSearchItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return "未检索到可用结果。";
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

