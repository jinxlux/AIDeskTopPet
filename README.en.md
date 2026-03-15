# AIDeskTopPet

[中文](./README.md) | [English](./README.en.md)

A desktop pet project built with **C# WPF**. It supports local LLM chat via `llama.cpp`, basic web retrieval (news/weather/pet), and includes an image toolchain (background removal, video-to-frames).

## Project Root

Main code directory:

`\desktop pet`

## Structure

```text
AIDeskTopPet/
├─ desktop pet/
│  ├─ frontend/                # WPF desktop pet app (DesktopPet.sln)
│  ├─ ai-service/              # Local LLM service (.NET + llama.cpp)
│  ├─ tools/                   # Helper tools (Python)
│  ├─ prephotos/               # Inputs: images/videos
│  └─ afterphotos/             # Outputs: processed results
└─ README.md
```

## Features

- Pet animations (idle/interact frame sequences)
- Chat UI (communicates with local AI service)
- OpenAI-compatible endpoint: `/v1/chat/completions`
- Agent retrieval endpoint: `/v1/agent/search`
  - Decide if web access is needed
  - Route by category (news/weather/pet)
  - Aggregate RSS/API results with optional summary
- Toolchain
  - Batch cutout (`tools/batch-cutout`)
  - Video to frames + optional cutout (`tools/video2frames`)

## Requirements

- Windows 10/11
- .NET SDK 8.0+
- Visual Studio 2022 (recommended for WPF)
- Python 3.10+ (tools only)

## 1. Run Desktop Pet (Frontend)

```powershell
cd "{YOUR_PATH}\desktop pet\frontend"
dotnet build DesktopPet.sln
```

Run options:

1. Open `DesktopPet.sln` in Visual Studio and run `DesktopPet.App`.
2. Run `DesktopPet.App` from command line.

## 2. Run AI Service (ai-service, optional manual)

```powershell
cd "{YOUR_PATH}\desktop pet\ai-service\AiService.Host"
dotnet run
```

By default, you do not need to start ai-service manually.

When you launch the pet app and open AI chat, the pet will auto-start ai-service (and its llama runtime).

Manual start is mainly for standalone API debugging or Swagger.

Swagger is available in Development by default.

### Core Endpoints

- `GET /health`
- `POST /admin/runtime/start`
- `POST /admin/runtime/stop`
- `GET /v1/models`
- `POST /v1/chat/completions`
- `POST /v1/agent/search`

## 3. Local Model & Runtime

`ai-service` starts local GGUF models via `llama-server.exe`.

Key locations:

- Runtime: `desktop pet/ai-service/runtime/`
- Models: `desktop pet/ai-service/models/`
- Config:
  - `desktop pet/ai-service/AiService.Host/appsettings.json`
  - `desktop pet/ai-service/config/llm.runtime.json`

### Do Not Commit Model Files

GGUF models are large (often multiple GB). This repository **does not** include model files.

Download your model and place it here:

- Folder: `desktop pet/ai-service/models/`
- Default filename (current config): `qwen2.5-7b-instruct-q4_k_m.gguf`
- Example path:
  `\desktop pet\ai-service\models\qwen2.5-7b-instruct-q4_k_m.gguf`

If you use a different model, update:

- `LlmRuntime:ModelPath` in `desktop pet/ai-service/AiService.Host/appsettings.json`

Relative paths are recommended for easier cloning and setup.

## 4. Web Retrieval (/v1/agent/search)

Currently supported:

- News: Wikinews RSS
- Weather: Open-Meteo (geocoding + forecast)
- Pet: Wikimedia OpenSearch (Dog/Cat API as backup)

Notes:

- Not all queries require web access; a decision step runs first.
- Only sources marked `Enabled=true` and `ComplianceChecked=true` are used.

Example:

```json
{
  "query": "How is the weather in Shanghai",
  "maxResults": 5,
  "needSummary": true
}
```

## 5. Tools

### 5.1 Batch Cutout

Path: `desktop pet/tools/batch-cutout`

Example:

```powershell
python batch_cutout.py --input "D:\...\prephotos" --output "D:\...\afterphotos"
```

### 5.2 Video to Frames (Optional Cutout)

Path: `desktop pet/tools/video2frames`

Example:

```powershell
python video_pipeline.py --input "D:\...\prephotos\video.mp4" --output "D:\...\afterphotos" --cutbg
```

## FAQ

### 1) News returns empty

Check first:

- `Search.RssFeeds` enabled
- Network access to the RSS source
- You are running the latest build

### 2) Build error: file is locked (AiService.Host.exe/.dll locked)

An old process is still running. Stop `AiService.Host` or close the VS debug session, then rebuild.

### 3) Pet API returns no results

Some third-party APIs may rate-limit or return 403. Wikimedia OpenSearch is the recommended fallback. In mainland China, some sources may be inaccessible.

## Tooling Note

Most code was generated with codex assistance. Human input focuses on review and planning.

## License

See root `LICENSE`.
