using System.Text.Json.Serialization;

namespace AiService.Host.Contracts;

/// <summary>
/// Represents one OpenAI-compatible chat completion request.
/// </summary>
public sealed class ChatCompletionRequest
{
    /// <summary>
    /// Gets or sets model id. Optional for this service.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets ordered chat messages.
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets sampling temperature.
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Gets or sets nucleus sampling top_p.
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    /// <summary>
    /// Gets or sets max output tokens.
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }
}

/// <summary>
/// Represents one chat message item.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>
    /// Gets or sets message role, such as system/user/assistant.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets message text content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
