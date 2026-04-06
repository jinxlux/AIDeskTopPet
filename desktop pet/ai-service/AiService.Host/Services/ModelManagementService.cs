using AiService.Host.Contracts;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AiService.Host.Services;

/// <summary>
/// Provides local GGUF model discovery and persisted model switching.
/// </summary>
public sealed class ModelManagementService
{
    private static readonly Regex QuantizationSuffixRegex = new("[-_](q\\d+.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly LlamaProcessManager _processManager;
    private readonly string _contentRoot;
    private readonly string _modelsRoot;
    private readonly string _runtimeConfigPath;

    /// <summary>
    /// Initializes model management service.
    /// </summary>
    public ModelManagementService(LlamaProcessManager processManager, IWebHostEnvironment environment)
    {
        _processManager = processManager;
        _contentRoot = environment.ContentRootPath;
        _modelsRoot = Path.GetFullPath(Path.Combine(_contentRoot, "..", "models"));
        _runtimeConfigPath = Path.GetFullPath(Path.Combine(_contentRoot, "..", "config", "llm.runtime.json"));
    }

    /// <summary>
    /// Lists all locally available GGUF models under the models directory.
    /// </summary>
    public ModelCatalogResponse GetModels()
    {
        Directory.CreateDirectory(_modelsRoot);

        var currentFullPath = Path.GetFullPath(_processManager.ResolveConfiguredPath(_processManager.Options.ModelPath));
        var items = Directory
            .EnumerateFiles(_modelsRoot, "*.gguf", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var relativePath = Path.GetRelativePath(_modelsRoot, path).Replace('\\', '/');
                var info = new FileInfo(path);
                var modelId = BuildModelId(Path.GetFileNameWithoutExtension(path));
                return new LocalModelInfo
                {
                    ModelId = modelId,
                    FileName = info.Name,
                    RelativeModelPath = relativePath,
                    FullPath = path,
                    SizeBytes = info.Length,
                    LastWriteTimeUtc = info.LastWriteTimeUtc,
                    IsCurrent = string.Equals(path, currentFullPath, StringComparison.OrdinalIgnoreCase),
                };
            })
            .ToArray();

        return new ModelCatalogResponse
        {
            ModelsRoot = _modelsRoot,
            CurrentModelPath = currentFullPath,
            CurrentModelId = _processManager.Options.DefaultModelId,
            Items = items,
        };
    }

    /// <summary>
    /// Returns current configured model information.
    /// </summary>
    public CurrentModelResponse GetCurrentModel()
    {
        var fullPath = Path.GetFullPath(_processManager.ResolveConfiguredPath(_processManager.Options.ModelPath));
        return new CurrentModelResponse
        {
            ModelId = _processManager.Options.DefaultModelId,
            ConfiguredModelPath = _processManager.Options.ModelPath,
            FullPath = fullPath,
            Exists = File.Exists(fullPath),
            ProcessRunning = _processManager.IsProcessRunning,
        };
    }

    /// <summary>
    /// Switches the configured model and optionally restarts the runtime if needed.
    /// </summary>
    public async Task<ModelSwitchResponse> SwitchModelAsync(ModelSwitchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RelativeModelPath))
        {
            throw new InvalidOperationException("RelativeModelPath 不能为空。");
        }

        var fullPath = ResolveModelFullPath(request.RelativeModelPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("目标模型文件不存在。", fullPath);
        }

        var relativeToHost = Path.GetRelativePath(_contentRoot, fullPath).Replace('\\', '/');
        var modelId = string.IsNullOrWhiteSpace(request.DefaultModelId)
            ? BuildModelId(Path.GetFileNameWithoutExtension(fullPath))
            : request.DefaultModelId.Trim();

        var wasRunning = _processManager.IsProcessRunning;
        await _processManager.StopAsync();

        _processManager.Options.ModelPath = relativeToHost;
        _processManager.Options.DefaultModelId = modelId;
        PersistRuntimeSelection(relativeToHost, modelId);

        if (wasRunning)
        {
            await _processManager.EnsureReadyAsync(cancellationToken);
        }

        return new ModelSwitchResponse
        {
            Switched = true,
            Restarted = wasRunning,
            ModelId = modelId,
            ConfiguredModelPath = relativeToHost,
            FullPath = fullPath,
        };
    }

    private string ResolveModelFullPath(string relativeModelPath)
    {
        var normalized = relativeModelPath.Replace('/', Path.DirectorySeparatorChar).Trim();
        var fullPath = Path.GetFullPath(Path.Combine(_modelsRoot, normalized));
        if (!fullPath.StartsWith(_modelsRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("模型路径超出了 ai-service/models 目录范围。");
        }

        return fullPath;
    }

    private void PersistRuntimeSelection(string modelPath, string modelId)
    {
        JsonNode rootNode;
        if (File.Exists(_runtimeConfigPath))
        {
            rootNode = JsonNode.Parse(File.ReadAllText(_runtimeConfigPath)) ?? new JsonObject();
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_runtimeConfigPath)!);
            rootNode = new JsonObject();
        }

        var rootObject = rootNode.AsObject();
        if (rootObject["LlmRuntime"] is not JsonObject runtimeObject)
        {
            runtimeObject = new JsonObject();
            rootObject["LlmRuntime"] = runtimeObject;
        }

        runtimeObject["ModelPath"] = modelPath;
        runtimeObject["DefaultModelId"] = modelId;

        File.WriteAllText(_runtimeConfigPath, rootObject.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
    }

    private static string BuildModelId(string fileNameWithoutExtension)
    {
        var normalized = fileNameWithoutExtension.Trim().ToLowerInvariant();
        normalized = QuantizationSuffixRegex.Replace(normalized, string.Empty);
        normalized = normalized.Trim('-', '_');
        return string.IsNullOrWhiteSpace(normalized) ? "local-model" : normalized;
    }
}

public sealed class ModelCatalogResponse
{
    public string ModelsRoot { get; set; } = string.Empty;
    public string CurrentModelPath { get; set; } = string.Empty;
    public string CurrentModelId { get; set; } = string.Empty;
    public IReadOnlyList<LocalModelInfo> Items { get; set; } = [];
}

public sealed class LocalModelInfo
{
    public string ModelId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string RelativeModelPath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class CurrentModelResponse
{
    public string ModelId { get; set; } = string.Empty;
    public string ConfiguredModelPath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public bool ProcessRunning { get; set; }
}

public sealed class ModelSwitchResponse
{
    public bool Switched { get; set; }
    public bool Restarted { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string ConfiguredModelPath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
}
