namespace AiService.Host.Options;

/// <summary>
/// Defines runtime options for the local llama.cpp service.
/// </summary>
public sealed class LlmRuntimeOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "LlmRuntime";

    /// <summary>
    /// Gets or sets whether local runtime is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to auto-start runtime at service startup.
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Gets or sets the host for llama server binding.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the port for llama server binding.
    /// </summary>
    public int Port { get; set; } = 18080;

    /// <summary>
    /// Gets or sets the HTTP health check path.
    /// </summary>
    public string HealthPath { get; set; } = "/health";

    /// <summary>
    /// Gets or sets startup timeout in seconds.
    /// </summary>
    public int StartupTimeoutSeconds { get; set; } = 90;

    /// <summary>
    /// Gets or sets relative or absolute llama-server executable path.
    /// </summary>
    public string LlamaServerPath { get; set; } = "../runtime/llama-server.exe";

    /// <summary>
    /// Gets or sets relative or absolute GGUF model path.
    /// </summary>
    public string ModelPath { get; set; } = "../models/qwen2.5-3b-instruct-q4_k_m.gguf";

    /// <summary>
    /// Gets or sets default exposed model id for OpenAI-compatible endpoint.
    /// </summary>
    public string DefaultModelId { get; set; } = "qwen2.5-3b-instruct";

    /// <summary>
    /// Gets or sets context window size.
    /// </summary>
    public int ContextSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets CPU thread count used by llama.cpp.
    /// </summary>
    public int Threads { get; set; } = 8;

    /// <summary>
    /// Gets or sets default temperature for generated responses.
    /// </summary>
    public double DefaultTemperature { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets default top-p.
    /// </summary>
    public double DefaultTopP { get; set; } = 0.9;

    /// <summary>
    /// Gets or sets default max output tokens.
    /// </summary>
    public int DefaultMaxTokens { get; set; } = 160;

    /// <summary>
    /// Gets or sets additional raw arguments for llama-server.
    /// </summary>
    public string[] ExtraArgs { get; set; } = [];
}
