# HanimetaScraper

[中文](#中文) | [English](#english)

---

## 中文

HanimetaScraper 是一个为 Jellyfin 提供元数据的项目，包含：

- `ScraperBackendService`：后端抓取服务（Hanime / DLsite）
- `Jellyfin.Plugin.Hanimeta`：Jellyfin 插件（前端接入层）
- `Test/NewScraperTest`：后端接口测试工具

### 项目结构

```text
HanimetaScraper/
├─ ScraperBackendService/
├─ Jellyfin.Plugin.Hanimeta/
└─ Test/NewScraperTest/
```

### 快速部署（后端）

1. 安装 .NET 9 SDK/Runtime
2. 安装 Playwright 运行依赖：
   - `dotnet tool install --global Microsoft.Playwright.CLI`
   - `playwright install chromium --with-deps`
3. 启动后端：
   - `cd ScraperBackendService`
   - `dotnet run`

默认监听：`http://0.0.0.0:8585`

### 后端关键设置

配置文件：`ScraperBackendService/appsettings.json`

核心项：

- `Port` / `Host`
- `AuthToken` / `TokenHeaderName`
- `MaxConcurrentRequests` / `RateLimitSeconds`
- `RequestTimeoutSeconds`
- `ChallengeAutoWaitSeconds` / `ChallengeAutoWaitSlowSeconds`
- `EnableManualChallengeResolution`
- `EnableCookiePersistence`
- `CacheTtlMinutes` / `CacheNullResultTtlMinutes` / `CacheSizeLimitEntries`

环境变量覆盖：

- `SCRAPER_PORT`
- `SCRAPER_AUTH_TOKEN`

### Jellyfin 插件配置（前端）

在 Jellyfin 后台打开插件配置页，至少设置：

- `Backend URL`（默认 `http://127.0.0.1:8585`）
- `API Token`（当后端启用 `AuthToken` 时必填）
- `Enable Logging`（排查问题时建议开启）
- `Tag Mapping Mode`（Tags / Genres）

### 后端 API（总览）

- `GET /`
- `GET /health`
- `GET /cache/stats`
- `DELETE /cache/clear`
- `DELETE /cache/{provider}/{id}`
- `GET /api/hanime/search?title={query}&max={limit}`
- `GET /api/hanime/{id}`
- `GET /api/dlsite/search?title={query}&max={limit}`
- `GET /api/dlsite/{id}`
- `GET /r/dlsite/{id}`

> 认证说明：当 `AuthToken` 非空时，`/api/*` 和 `/cache/*` 需要在 `TokenHeaderName` 头中携带 Token。

### 子文档

- 后端说明：`ScraperBackendService/README.md`
- 插件说明：`Jellyfin.Plugin.Hanimeta/README.md`
- 测试说明：`Test/NewScraperTest/README.md`

---

## English

HanimetaScraper provides Jellyfin metadata through:

- `ScraperBackendService`: backend scraper service (Hanime / DLsite)
- `Jellyfin.Plugin.Hanimeta`: Jellyfin plugin (frontend integration layer)
- `Test/NewScraperTest`: backend API test runner

### Structure

```text
HanimetaScraper/
├─ ScraperBackendService/
├─ Jellyfin.Plugin.Hanimeta/
└─ Test/NewScraperTest/
```

### Quick Deployment (Backend)

1. Install .NET 9 SDK/Runtime
2. Install Playwright runtime dependencies:
   - `dotnet tool install --global Microsoft.Playwright.CLI`
   - `playwright install chromium --with-deps`
3. Start backend:
   - `cd ScraperBackendService`
   - `dotnet run`

Default listen URL: `http://0.0.0.0:8585`

### Backend Key Settings

Config file: `ScraperBackendService/appsettings.json`

Core options:

- `Port` / `Host`
- `AuthToken` / `TokenHeaderName`
- `MaxConcurrentRequests` / `RateLimitSeconds`
- `RequestTimeoutSeconds`
- `ChallengeAutoWaitSeconds` / `ChallengeAutoWaitSlowSeconds`
- `EnableManualChallengeResolution`
- `EnableCookiePersistence`
- `CacheTtlMinutes` / `CacheNullResultTtlMinutes` / `CacheSizeLimitEntries`

Environment overrides:

- `SCRAPER_PORT`
- `SCRAPER_AUTH_TOKEN`

### Jellyfin Plugin Configuration (Frontend)

In Jellyfin plugin settings, configure at least:

- `Backend URL` (default `http://127.0.0.1:8585`)
- `API Token` (required when backend `AuthToken` is enabled)
- `Enable Logging` (recommended for troubleshooting)
- `Tag Mapping Mode` (Tags / Genres)

### Backend API (Overview)

- `GET /`
- `GET /health`
- `GET /cache/stats`
- `DELETE /cache/clear`
- `DELETE /cache/{provider}/{id}`
- `GET /api/hanime/search?title={query}&max={limit}`
- `GET /api/hanime/{id}`
- `GET /api/dlsite/search?title={query}&max={limit}`
- `GET /api/dlsite/{id}`
- `GET /r/dlsite/{id}`

> Auth note: when `AuthToken` is set, `/api/*` and `/cache/*` require token in the `TokenHeaderName` header.

### Sub Documents

- Backend: `ScraperBackendService/README.md`
- Plugin: `Jellyfin.Plugin.Hanimeta/README.md`
- Tests: `Test/NewScraperTest/README.md`