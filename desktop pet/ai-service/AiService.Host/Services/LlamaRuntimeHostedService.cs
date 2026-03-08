namespace AiService.Host.Services;

/// <summary>
/// Auto-starts local llama runtime when service host starts.
/// </summary>
public sealed class LlamaRuntimeHostedService : IHostedService
{
    private readonly LlamaProcessManager _processManager;
    private readonly ILogger<LlamaRuntimeHostedService> _logger;

    /// <summary>
    /// Initializes a hosted service instance.
    /// </summary>
    public LlamaRuntimeHostedService(LlamaProcessManager processManager, ILogger<LlamaRuntimeHostedService> logger)
    {
        _processManager = processManager;
        _logger = logger;
    }

    /// <summary>
    /// Starts runtime warm-up on host startup if enabled.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_processManager.Options.Enabled || !_processManager.Options.AutoStart)
        {
            return;
        }

        try
        {
            await _processManager.EnsureReadyAsync(cancellationToken);
            _logger.LogInformation("Local llama runtime is ready.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local llama runtime auto-start failed. Service will continue and retry on first request.");
        }
    }

    /// <summary>
    /// Stops runtime process on host shutdown.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _processManager.StopAsync();
    }
}
