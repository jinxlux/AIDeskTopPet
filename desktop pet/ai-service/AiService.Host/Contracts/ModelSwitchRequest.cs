namespace AiService.Host.Contracts;

/// <summary>
/// Request payload used to switch the local GGUF model.
/// </summary>
public sealed class ModelSwitchRequest
{
    /// <summary>
    /// Gets or sets the model path relative to the ai-service models directory.
    /// </summary>
    public string RelativeModelPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional exposed model id override.
    /// </summary>
    public string? DefaultModelId { get; set; }
}
