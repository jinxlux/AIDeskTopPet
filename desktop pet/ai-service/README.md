# Ai Service (Reusable Local LLM Host)

This service wraps `llama.cpp` (`llama-server.exe`) and exposes OpenAI-compatible endpoint for reuse across projects.

## Folder layout

- `ai-service/runtime/llama-server.exe`
- `ai-service/models/qwen2.5-3b-instruct-q4_k_m.gguf`
- `ai-service/config/llm.runtime.json`
- `ai-service/AiService.Host/` (this .NET host)

## Run

```powershell
cd "D:\ai项目\desktop pet\ai-service\AiService.Host"
dotnet run
```

## Endpoints

- `GET /health`
- `POST /admin/runtime/start`
- `POST /admin/runtime/stop`
- `GET /v1/models`
- `POST /v1/chat/completions`

## Example request

```json
{
  "messages": [
    {"role":"system", "content":"You are a helpful desktop pet assistant."},
    {"role":"user", "content":"你好，今天心情怎么样？"}
  ]
}
```

If model/runtime files are missing, service returns clear file-not-found errors.
