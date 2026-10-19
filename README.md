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

推荐使用已发布的 Docker 镜像；支持 Linux x86-64（`amd64`）和 ARM64，Docker 自动选择架构。部署机器只需安装 Docker 与 Compose，不需要安装 .NET、Chromium 或导入镜像文件。

下载本仓库的 [compose.yaml](compose.yaml) 到一个空目录，然后在该目录运行：

```bash
docker compose up -d
```

默认镜像：`ghcr.io/qing-98/hanimetascraper-backend:latest`。首次发布成功且镜像包设为 Public 后，其他用户无需登录即可拉取。检查 `http://部署机器IP:8585/health`；在 Jellyfin 插件中将 `Backend URL` 设为 `http://部署机器IP:8585`，该地址必须能从 Jellyfin 所在环境访问。

如需修改端口或设置 API Token，将 [.env.example](.env.example) 复制为与 `compose.yaml` 同目录的 `.env` 并修改对应值；启用 Token 后，也要填写插件中的 `API Token`。Cookie 自动保存在 Docker 卷中。

更新：

```bash
docker compose pull
docker compose up -d
```

#### 维护者发布镜像

[Publish Container](.github/workflows/publish-container.yml) 使用 GitHub Actions 自带的 `GITHUB_TOKEN` 发布到 GHCR，不需要 Docker Hub 账户或额外配置个人 Token。工作流在原生 x86-64 / ARM64 机器上分别构建，检查后端启动以及 Chromium 默认多进程和 JavaScript，再发布包含两种架构的镜像。

- `main` 的后端或容器构建文件更新时自动发布，也可在 Actions 中手动运行。
- 推送 `backend-v*.*.*` 标签会同时发布对应版本；`container-*` 标签用于单独发布容器，例如 `container-20261020`。
- 镜像提供 `latest`、对应版本及 `sha-完整提交ID` 标签。固定版本可在 `.env` 中修改 `HANIMETA_IMAGE`。
- 第一次成功发布后，仓库所有者需要在 GitHub 的 Packages 中打开 `hanimetascraper-backend` → Package settings → Change visibility，将其改为 **Public**，其他客户端才能匿名下载。镜像包默认是 Private，仓库公开不会自动改变镜像可见性。

#### 源码运行

1. 安装 .NET 9 SDK/Runtime
2. 安装 Playwright 运行依赖：
   - `dotnet tool install --global Microsoft.Playwright.CLI`
   - `playwright install chromium --with-deps`
3. 启动后端：
   - `cd ScraperBackendService`
   - `dotnet run`

默认监听：`http://0.0.0.0:8585`

### 本地构建 Docker（Linux amd64 / ARM64）

在仓库根目录构建和运行：

```bash
docker build --platform linux/amd64 -f ScraperBackendService/Dockerfile -t hanimeta-backend:local .
docker run -d --init --name hanimeta-backend --shm-size=1g -p 8585:8585 --mount type=volume,source=hanimeta-cookies,target=/app/Data/Cookies hanimeta-backend:local
```

镜像包含 .NET 9、与项目 Playwright 版本匹配的 Chromium 及系统依赖，使用普通用户运行。Cookie 保存在具名卷中；容器默认关闭系统 Chrome 优先选择和人工验证窗口。健康检查：`http://localhost:8585/health`。

这里的 `amd64` / `x86-64` 指 Intel/AMD 64 位设备；不支持 32 位 x86（`linux/386`）。

ARM64 设备构建时将 `--platform linux/amd64` 改为 `--platform linux/arm64`。在 x86-64 主机上通过 QEMU 运行 ARM64 镜像时，Chromium 的子进程可能崩溃；此模拟场景可在 `docker run` 中添加以下参数：

```bash
-e ServiceConfig__BrowserLaunchArgs__0=--single-process \
-e ServiceConfig__BrowserLaunchArgs__1=--no-zygote \
-e ServiceConfig__BrowserLaunchArgs__2=--in-process-gpu \
-e ServiceConfig__BrowserJavaScriptEnabled=false
```

`BrowserLaunchArgs` 默认是空数组，`BrowserJavaScriptEnabled` 默认开启。上述 QEMU 配置使用单进程并只解析静态元数据，避免广告脚本拖慢模拟浏览器；原生 ARM64 设备保留默认的多进程和 JavaScript 设置。

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

当前插件要求 Jellyfin **10.11.x / .NET 9**；安装与编号识别说明见 [插件文档](Jellyfin.Plugin.Hanimeta/README.md)。

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

Use the published Docker image for Linux x86-64 (`amd64`) and ARM64. Docker selects the architecture automatically. Deployment needs Docker and Compose; .NET, Chromium, and image archive imports are included or unnecessary.

Download [compose.yaml](compose.yaml) into an empty directory, then run there:

```bash
docker compose up -d
```

Default image: `ghcr.io/qing-98/hanimetascraper-backend:latest`. Anonymous pulls work after the first successful publication and the package is made Public. Check `http://DEPLOYMENT_HOST:8585/health` and set the Jellyfin plugin's `Backend URL` to `http://DEPLOYMENT_HOST:8585`, using an address reachable from Jellyfin's environment.

To change the port or set an API token, copy [.env.example](.env.example) to `.env` beside `compose.yaml` and edit it. Set the same token in the plugin's `API Token` field. Cookies persist in a Docker volume.

Update with:

```bash
docker compose pull
docker compose up -d
```

#### Publishing images (maintainers)

[Publish Container](.github/workflows/publish-container.yml) publishes to GHCR using the automatic GitHub Actions `GITHUB_TOKEN`. No Docker Hub account or personal token is required. Separate native x86-64 and ARM64 runners build and check backend startup plus Chromium with its default multiple processes and JavaScript, then publish one image containing both architectures.

- Changes to backend or container build files on `main` publish automatically. Manual runs are also available under Actions.
- `backend-v*.*.*` tags publish the matching version. `container-*` tags publish containers independently, for example `container-20261020`.
- Tags include `latest`, the matching version, and `sha-FULL_COMMIT_ID`. Set `HANIMETA_IMAGE` in `.env` to pin a version.
- After the first successful publication, the repository owner must open GitHub Packages → `hanimetascraper-backend` → Package settings → Change visibility and choose **Public** for anonymous client pulls. Packages start Private, even for public repositories.

#### Running from source

1. Install .NET 9 SDK/Runtime
2. Install Playwright runtime dependencies:
   - `dotnet tool install --global Microsoft.Playwright.CLI`
   - `playwright install chromium --with-deps`
3. Start backend:
   - `cd ScraperBackendService`
   - `dotnet run`

Default listen URL: `http://0.0.0.0:8585`

### Building Docker Locally (Linux amd64 / ARM64)

Build and run from the repository root:

```bash
docker build --platform linux/amd64 -f ScraperBackendService/Dockerfile -t hanimeta-backend:local .
docker run -d --init --name hanimeta-backend --shm-size=1g -p 8585:8585 --mount type=volume,source=hanimeta-cookies,target=/app/Data/Cookies hanimeta-backend:local
```

The image includes .NET 9, Chromium matching the project's Playwright version, and browser system dependencies. It runs as a non-root user and stores cookies in the named volume. System Chrome preference and interactive verification windows are disabled by default. Health endpoint: `http://localhost:8585/health`.

`amd64` / `x86-64` means 64-bit Intel/AMD hardware. 32-bit x86 (`linux/386`) is not supported.

For ARM64 hardware, build with `--platform linux/arm64`. When running that image through QEMU on an x86-64 host, Chromium subprocesses can crash. Add these options to `docker run` for that emulated environment:

```bash
-e ServiceConfig__BrowserLaunchArgs__0=--single-process \
-e ServiceConfig__BrowserLaunchArgs__1=--no-zygote \
-e ServiceConfig__BrowserLaunchArgs__2=--in-process-gpu \
-e ServiceConfig__BrowserJavaScriptEnabled=false
```

`BrowserLaunchArgs` defaults to an empty array and `BrowserJavaScriptEnabled` defaults to true. This QEMU configuration uses a single process and static metadata to avoid slow advertising scripts. Native ARM64 deployments retain the default multi-process and JavaScript settings.

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

The current plugin requires Jellyfin **10.11.x / .NET 9**. See the [plugin documentation](Jellyfin.Plugin.Hanimeta/README.md) for installation and ID lookup.

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
