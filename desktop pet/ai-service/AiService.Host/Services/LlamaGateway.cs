using AiService.Host.Options;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiService.Host.Services;

/// <summary>
/// Proxies OpenAI-compatible chat completion requests to local llama runtime.
/// </summary>
public sealed class LlamaGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LlamaProcessManager _processManager;
    private readonly LlmRuntimeOptions _options;

    /// <summary>
    /// Initializes a gateway.
    /// </summary>
    public LlamaGateway(IHttpClientFactory httpClientFactory, LlamaProcessManager processManager, IOptions<LlmRuntimeOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _processManager = processManager;
        _options = options.Value;
    }

    /// <summary>
    /// Sends one chat completion request to local llama runtime.
    /// </summary>
    public async Task<string> ChatCompletionsAsync(JsonElement inputPayload, CancellationToken cancellationToken)
    {
        await _processManager.EnsureReadyAsync(cancellationToken);

        var patchedPayload = BuildPayloadWithDefaults(inputPayload);
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(2);

        using var request = new HttpRequestMessage(HttpMethod.Post, _processManager.BaseUrl + "/v1/chat/completions")
        {
            Content = new StringContent(patchedPayload, Encoding.UTF8, "application/json"),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Llama runtime request failed: {(int)response.StatusCode} {responseBody}");
        }

        return responseBody;
    }

    private string BuildPayloadWithDefaults(JsonElement payload)
    {
        var node = JsonNode.Parse(payload.GetRawText())?.AsObject() ?? new JsonObject();

        if (node["model"] is null)
        {
            node["model"] = _options.DefaultModelId;
        }

        if (node["temperature"] is null)
        {
            node["temperature"] = _options.DefaultTemperature;
        }

        if (node["top_p"] is null)
        {
            node["top_p"] = _options.DefaultTopP;
        }

        if (node["max_tokens"] is null)
        {
            node["max_tokens"] = _options.DefaultMaxTokens;
        }

        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}
