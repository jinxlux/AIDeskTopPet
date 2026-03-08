using AiService.Host.Contracts;
using AiService.Host.Options;
using AiService.Host.Services;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Configuration.AddJsonFile(Path.Combine("..", "config", "llm.runtime.json"), optional: true, reloadOnChange: true);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<LlmRuntimeOptions>(builder.Configuration.GetSection(LlmRuntimeOptions.SectionName));
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection(SearchOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<LlamaProcessManager>();
builder.Services.AddSingleton<LlamaGateway>();
builder.Services.AddSingleton<SearchDecisionService>();
builder.Services.AddSingleton<IWebSearchProvider, RssSearchProvider>();
builder.Services.AddSingleton<IWebSearchProvider, ApiSearchProvider>();
builder.Services.AddSingleton<IWebSearchProvider, SiteSearchProvider>();
builder.Services.AddSingleton<AgentSearchService>();
builder.Services.AddHostedService<LlamaRuntimeHostedService>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", async (LlamaProcessManager manager, CancellationToken cancellationToken) =>
{
    var status = await manager.GetStatusAsync(cancellationToken);
    return Results.Ok(status);
})
.WithSummary("Health status")
.WithDescription("Returns ai-service and local llama runtime status, including whether model process is healthy.");

app.MapPost("/admin/runtime/start", async (LlamaProcessManager manager, CancellationToken cancellationToken) =>
{
    await manager.EnsureReadyAsync(cancellationToken);
    return Results.Ok(new { started = true });
})
.WithSummary("Start runtime")
.WithDescription("Ensures llama-server process is started and healthy.");

app.MapPost("/admin/runtime/stop", async (LlamaProcessManager manager) =>
{
    await manager.StopAsync();
    return Results.Ok(new { stopped = true });
})
.WithSummary("Stop runtime")
.WithDescription("Stops llama-server process if currently running.");

app.MapGet("/v1/models", (LlamaProcessManager manager) =>
{
    var modelId = manager.Options.DefaultModelId;
    return Results.Ok(new
    {
        @object = "list",
        data = new[]
        {
            new
            {
                id = modelId,
                @object = "model",
                owned_by = "local",
            },
        },
    });
})
.WithSummary("List models")
.WithDescription("Returns the default local model id exposed by this ai-service.");

app.MapPost("/v1/chat/completions", async (ChatCompletionRequest request, LlamaGateway gateway, CancellationToken cancellationToken) =>
{
    var payload = JsonSerializer.SerializeToElement(request, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    });
    var responseJson = await gateway.ChatCompletionsAsync(payload, cancellationToken);
    return Results.Content(responseJson, "application/json");
})
.WithSummary("Chat completions")
.WithDescription("OpenAI-compatible chat endpoint proxied to local llama-server with defaults injected when fields are missing.")
.Accepts<ChatCompletionRequest>("application/json")
.Produces(StatusCodes.Status200OK, contentType: "application/json")
.WithOpenApi(operation =>
{
    operation.RequestBody ??= new OpenApiRequestBody();
    operation.RequestBody.Required = true;
    operation.RequestBody.Description = "OpenAI-compatible chat request payload.";

    var schema = new OpenApiSchema
    {
        Type = "object",
        Required = new HashSet<string> { "messages" },
        Properties = new Dictionary<string, OpenApiSchema>
        {
            ["model"] = new() { Type = "string", Nullable = true },
            ["messages"] = new()
            {
                Type = "array",
                Items = new OpenApiSchema
                {
                    Type = "object",
                    Required = new HashSet<string> { "role", "content" },
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["role"] = new() { Type = "string" },
                        ["content"] = new() { Type = "string" },
                    },
                },
            },
            ["temperature"] = new() { Type = "number", Format = "double", Nullable = true },
            ["top_p"] = new() { Type = "number", Format = "double", Nullable = true },
            ["max_tokens"] = new() { Type = "integer", Format = "int32", Nullable = true },
        },
    };

    operation.RequestBody.Content["application/json"] = new OpenApiMediaType
    {
        Schema = schema,
        Example = new OpenApiObject
        {
            ["model"] = new OpenApiString("local-model"),
            ["messages"] = new OpenApiArray
            {
                new OpenApiObject
                {
                    ["role"] = new OpenApiString("system"),
                    ["content"] = new OpenApiString("你是桌宠小狗，说话简短可爱。"),
                },
                new OpenApiObject
                {
                    ["role"] = new OpenApiString("user"),
                    ["content"] = new OpenApiString("你好呀，今天过得怎么样？"),
                },
            },
            ["temperature"] = new OpenApiDouble(0.7),
            ["top_p"] = new OpenApiDouble(0.9),
            ["max_tokens"] = new OpenApiInteger(80),
        },
    };

    operation.Responses.TryAdd("200", new OpenApiResponse { Description = "Model response in OpenAI-compatible JSON format." });
    return operation;
});

app.MapPost("/v1/agent/search", async (AgentSearchRequest request, AgentSearchService service, CancellationToken cancellationToken) =>
{
    var response = await service.SearchAsync(request, cancellationToken);
    return Results.Ok(response);
})
.WithSummary("Agent search")
.WithDescription("Decides whether web retrieval is needed. If needed, gathers results from free RSS/site sources and optionally summarizes with local model.")
.Accepts<AgentSearchRequest>("application/json")
.Produces<AgentSearchResponse>(StatusCodes.Status200OK, contentType: "application/json")
.WithOpenApi(operation =>
{
    operation.RequestBody ??= new OpenApiRequestBody();
    operation.RequestBody.Required = true;
    operation.RequestBody.Description = "Agent search request. The service decides whether to use web retrieval.";

    operation.RequestBody.Content["application/json"] = new OpenApiMediaType
    {
        Schema = new OpenApiSchema
        {
            Type = "object",
            Required = new HashSet<string> { "query" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["query"] = new() { Type = "string" },
                ["maxResults"] = new() { Type = "integer", Format = "int32", Nullable = true },
                ["needSummary"] = new() { Type = "boolean" },
            },
        },
        Example = new OpenApiObject
        {
            ["query"] = new OpenApiString("今天国内AI行业有什么新消息"),
            ["maxResults"] = new OpenApiInteger(5),
            ["needSummary"] = new OpenApiBoolean(true),
        },
    };

    operation.Responses.TryAdd("200", new OpenApiResponse
    {
        Description = "Agent decision and results. Includes used_web and decision_reason.",
    });

    return operation;
});

app.Run();


