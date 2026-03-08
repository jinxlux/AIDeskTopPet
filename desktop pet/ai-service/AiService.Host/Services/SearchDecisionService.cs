namespace AiService.Host.Services;

/// <summary>
/// Decides whether a query should trigger web retrieval.
/// </summary>
public sealed class SearchDecisionService
{
    private static readonly string[] MustWebKeywords =
    [
        "最新", "今天", "实时", "新闻", "价格", "汇率", "股价", "热搜", "刚刚", "现在", "明天", "近期",
        "天气", "气温", "降雨", "台风", "温度", "预报",
        "品种", "领养", "宠物医院", "犬种", "猫种",
        "latest", "today", "news", "price", "stock", "rate", "current", "real-time", "breaking", "tomorrow",
        "weather", "forecast", "temperature", "rain", "typhoon",
        "breed", "adoption", "pet shelter",
        "搜索", "搜一下", "网上", "查一下", "帮我查", "web", "internet",
    ];

    private static readonly string[] NoWebKeywords =
    [
        "你好", "在吗", "你是谁", "讲个笑话", "闲聊", "陪我聊", "开心吗",
        "hello", "hi", "who are you", "joke", "chat",
    ];

    /// <summary>
    /// Returns rule-based decision and reason.
    /// </summary>
    /// <param name="query">User query.</param>
    /// <returns>Tuple of (need web, decision reason, is ambiguous).</returns>
    public (bool NeedWeb, string Reason, bool IsAmbiguous) DecideByRule(string query)
    {
        var text = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return (false, "empty query", false);
        }

        if (MustWebKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return (true, "matched time-sensitive/search keyword", false);
        }

        if (NoWebKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return (false, "matched casual/no-web keyword", false);
        }

        return (false, "rule ambiguous", true);
    }
}
