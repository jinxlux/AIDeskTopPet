using AiService.Host.Options;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace AiService.Host.Services;

/// <summary>
/// Manages local llama.cpp process lifecycle and runtime health checks.
/// </summary>
public sealed class LlamaProcessManager : IDisposable
{
    private readonly ILogger<LlamaProcessManager> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _contentRoot;
    private Process? _process;

    /// <summary>
    /// Initializes a process manager.
    /// </summary>
    public LlamaProcessManager(
        IOptions<LlmRuntimeOptions> options,
        ILogger<LlamaProcessManager> logger,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment environment)
    {
        Options = options.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _contentRoot = environment.ContentRootPath;
    }

    /// <summary>
    /// Gets current runtime options.
    /// </summary>
    public LlmRuntimeOptions Options { get; }

    /// <summary>
    /// Gets whether llama-server process is currently running.
    /// </summary>
    public bool IsProcessRunning => _process is { HasExited: false };

    /// <summary>
    /// Gets local llama server base URL.
    /// </summary>
    public string BaseUrl => $"http://{Options.Host}:{Options.Port}";

    /// <summary>
    /// Ensures local runtime process is ready to accept requests.
    /// </summary>
    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (!Options.Enabled)
        {
            throw new InvalidOperationException("LlmRuntime is disabled by configuration.");
        }

        if (await IsHealthyAsync(cancellationToken))
        {
            return;
        }

        StartProcess();

        var timeout = TimeSpan.FromSeconds(Math.Max(5, Options.StartupTimeoutSeconds));
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            if (await IsHealthyAsync(cancellationToken))
            {
                return;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException($"Local llama runtime did not become healthy within {timeout.TotalSeconds} seconds.");
    }

    /// <summary>
    /// Stops local runtime process if running.
    /// </summary>
    public Task StopAsync()
    {
        if (_process is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop llama runtime process cleanly.");
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets combined process and health status.
    /// </summary>
    public async Task<object> GetStatusAsync(CancellationToken cancellationToken)
    {
        var healthy = await IsHealthyAsync(cancellationToken);
        return new
        {
            enabled = Options.Enabled,
            healthy,
            processRunning = IsProcessRunning,
            baseUrl = BaseUrl,
            model = ResolvePath(Options.ModelPath),
            server = ResolvePath(Options.LlamaServerPath),
        };
    }

    /// <summary>
    /// Resolves a configured relative or absolute path against the host content root.
    /// </summary>
    public string ResolveConfiguredPath(string configuredPath)
        => ResolvePath(configuredPath);

    /// <summary>
    /// Releases managed resources.
    /// </summary>
    public void Dispose()
    {
        _ = StopAsync();
    }

    private void StartProcess()
    {
        if (_process is { HasExited: false })
        {
            return;
        }

        var serverPath = ResolvePath(Options.LlamaServerPath);
        var modelPath = ResolvePath(Options.ModelPath);

        if (!File.Exists(serverPath))
        {
            throw new FileNotFoundException("llama-server executable not found.", serverPath);
        }

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("GGUF model file not found.", modelPath);
        }

        var arguments = BuildArguments(modelPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = serverPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(serverPath) ?? _contentRoot,
        };

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                _logger.LogInformation("llama-server: {Line}", eventArgs.Data);
            }
        };
        _process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                _logger.LogWarning("llama-server: {Line}", eventArgs.Data);
            }
        };

        _logger.LogInformation("Starting llama runtime: {FileName} {Arguments}", startInfo.FileName, startInfo.Arguments);
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    private string BuildArguments(string modelPath)
    {
        var args = new List<string>
        {
            "-m", Quote(modelPath),
            "--host", Quote(Options.Host),
            "--port", Options.Port.ToString(),
            "-c", Options.ContextSize.ToString(),
            "-t", Options.Threads.ToString(),
        };

        foreach (var arg in Options.ExtraArgs)
        {
            if (!string.IsNullOrWhiteSpace(arg))
            {
                args.Add(arg);
            }
        }

        return string.Join(' ', args);
    }

    private static string Quote(string value)
    {
        return value.Contains(' ') ? $"\"{value}\"" : value;
    }

    private string ResolvePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(_contentRoot, configuredPath));
    }

    private async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            using var response = await client.GetAsync(BaseUrl + Options.HealthPath, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
