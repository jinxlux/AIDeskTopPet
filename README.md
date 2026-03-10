# AIDeskTopPet

一个基于 **C# WPF** 的桌宠项目，支持本地大模型对话（`llama.cpp`）、基础联网检索（新闻/天气/宠物），并附带图片处理工具链（去背景、视频拆帧）。

## 项目路径

当前仓库主目录：

`D:\githubProject\DesktopPetAiAgent\AIDeskTopPet\desktop pet`

## 目录结构

```text
AIDeskTopPet/
├─ desktop pet/
│  ├─ frontend/                # WPF 桌宠主程序（DesktopPet.sln）
│  ├─ ai-service/              # 本地 LLM 服务（.NET + llama.cpp）
│  ├─ tools/                   # 辅助工具（Python）
│  ├─ prephotos/               # 待处理图片/视频输入
│  └─ afterphotos/             # 工具处理后的输出
└─ README.md
```

## 功能概览

- 桌宠动画（idle/interact 帧序列）
- 桌宠聊天 UI（与本地 AI 服务通信）
- OpenAI 兼容对话接口：`/v1/chat/completions`
- Agent 检索接口：`/v1/agent/search`
  - 先判断是否需要联网
  - 需要联网时按类别路由（news/weather/pet）
  - 聚合 RSS/API 结果并可选总结
- 工具链
  - 批量抠图（`tools/batch-cutout`）
  - 视频拆帧 + 可选去背景（`tools/video2frames`）

## 环境要求

- Windows 10/11
- .NET SDK 8.0+
- Visual Studio 2022（推荐，WPF 开发）
- Python 3.10+（仅工具链需要）

## 1. 启动桌宠（Frontend）

```powershell
cd "D:\githubProject\DesktopPetAiAgent\AIDeskTopPet\desktop pet\frontend"
dotnet build DesktopPet.sln
```

运行方式（任选其一）：

1. 用 Visual Studio 打开 `DesktopPet.sln`，启动 `DesktopPet.App`
2. 命令行运行 `DesktopPet.App`

## 2. 启动 AI 服务（ai-service，可选手动）

```powershell
cd "D:\githubProject\DesktopPetAiAgent\AIDeskTopPet\desktop pet\ai-service\AiService.Host"
dotnet run
```

默认情况下，不需要先手动启动 ai-service。

当你启动桌宠并打开 AI 对话时，桌宠会自动触发 ai-service（以及其底层 llama runtime）启动。

手动启动主要用于独立调试接口或查看 Swagger。

默认可通过 Swagger 测试接口（Development 环境下）。

### 核心接口

- `GET /health`
- `POST /admin/runtime/start`
- `POST /admin/runtime/stop`
- `GET /v1/models`
- `POST /v1/chat/completions`
- `POST /v1/agent/search`

## 3. 本地模型与运行时说明

`ai-service` 通过 `llama-server.exe` 启动本地 GGUF 模型。

关键位置：

- Runtime：`desktop pet/ai-service/runtime/`
- Models：`desktop pet/ai-service/models/`
- 配置：
  - `desktop pet/ai-service/AiService.Host/appsettings.json`
  - `desktop pet/ai-service/config/llm.runtime.json`

### 模型文件不放 GitHub

由于 GGUF 模型文件体积较大（通常数 GB），本仓库 **不提交模型文件**。

请自行下载模型后放到以下目录：

- 目录：`desktop pet/ai-service/models/`
- 当前默认文件名（按现有配置）：`qwen2.5-7b-instruct-q4_k_m.gguf`
- 示例路径：
  `\desktop pet\ai-service\models\qwen2.5-7b-instruct-q4_k_m.gguf`

若你使用其他模型，请同步修改：

- `desktop pet/ai-service/AiService.Host/appsettings.json` 的 `LlmRuntime:ModelPath`

建议使用相对路径，方便他人 clone 后直接运行。

## 4. 联网检索说明（/v1/agent/search）

当前支持：

- 新闻：Wikinews RSS
- 天气：Open-Meteo（地名解析 + forecast）
- 宠物：Wikimedia OpenSearch（Dog/Cat API 可作为补充）

注意：

- 不是所有问题都联网，服务会先做判定。
- 仅会访问配置里 `Enabled=true` 且 `ComplianceChecked=true` 的来源。

示例请求：

```json
{
  "query": "上海天气怎么样",
  "maxResults": 5,
  "needSummary": true
}
```

## 5. 工具链（tools）

### 5.1 批量抠图

目录：`desktop pet/tools/batch-cutout`

示例：

```powershell
python batch_cutout.py --input "D:\...\prephotos" --output "D:\...\afterphotos"
```

### 5.2 视频拆帧（可选去背景）

目录：`desktop pet/tools/video2frames`

示例：

```powershell
python video_pipeline.py --input "D:\...\prephotos\video.mp4" --output "D:\...\afterphotos" --cutbg
```

## 常见问题

### 1) 新闻查询返回空

优先检查：

- `Search.RssFeeds` 是否启用
- 网络是否可访问对应 RSS
- 是否运行的是最新构建版本

### 2) 构建报文件被占用（AiService.Host.exe/.dll locked）

说明有旧进程正在运行。先停止运行中的 `AiService.Host` 或关闭 VS 调试会话，再重新构建。

### 3) 宠物 API 没结果

部分第三方 API 可能限流或返回 403。可优先使用 Wikimedia OpenSearch 作为稳定兜底来源。联网搜索如果是中国内网，可能部分搜索不可用，效果不好

## 工具使用

代码大部分通过codex辅助生成。人工主要是审核和给定计划框架

## 许可证

见仓库根目录 `LICENSE`。


