# HanimetaScraper

[中文](#chinese) | [English](#english)

---

<a name="chinese"></a>

## 中文

为 Jellyfin 提供的 Hanime 和 DLsite 元数据解决方案。

### 📋 项目结构

```
├── ScraperBackendService/     # 后端服务（Playwright）
├── Jellyfin.Plugin.Hanimeta/  # Jellyfin 插件
└── Test/NewScraperTest/       # 测试
```

### ✨ 功能

- 🔍 智能搜索 - 标题/ID 搜索
- 📊 元数据提取 - 标题、描述、评分、演员
- 🖼️ 图像管理 - 封面、背景、缩略图
- 🛡️ 反检测 - Playwright 反机器人
- ⚡ 性能优化 - 缓存、并发控制、速率限制

### 🚀 快速开始

#### 1. 安装后端服务

**预构建包（推荐）：**

1. 下载 [Releases](https://github.com/Qing-98/HanimetaScraper/releases)
2. 安装后端服务：
   ```bash
   unzip ScraperBackendService-x.x.x.zip && cd backend
   ./install-playwright.sh  # 或 install-playwright.bat (Windows)
   ./start-backend.sh       # 或 start-backend.bat (Windows)
   ```

**源码构建：**

```bash
dotnet build
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium --with-deps
cd ScraperBackendService && dotnet run
```

#### 2. 安装 Jellyfin 插件

**方式 A: 自动安装（推荐）**

1. 打开 Jellyfin 管理面板
2. 导航到 **控制台** → **插件** → **存储库**
3. 点击 **+** 添加新存储库
4. 输入以下信息：
   - **存储库名称**: `Hanimeta Official`
   - **存储库 URL**: `https://raw.githubusercontent.com/Qing-98/HanimetaScraper/main/repository.json`
5. 点击 **保存**
6. 返回 **插件** → **目录**
7. 在列表中找到 **Hanimeta** 插件并点击 **安装**
8. 重启 Jellyfin

**方式 B: 手动安装**

如果自动安装失败，可以手动下载并安装：

```bash
# 下载最新版本
wget https://github.com/Qing-98/HanimetaScraper/releases/latest/download/Jellyfin.Plugin.Hanimeta.zip

# 停止 Jellyfin
sudo systemctl stop jellyfin

# 解压到插件目录
unzip Jellyfin.Plugin.Hanimeta.zip -d /var/lib/jellyfin/plugins/Hanimeta/

# 启动 Jellyfin
sudo systemctl start jellyfin
```

#### 3. 配置插件

在 Jellyfin 管理面板中：

1. 导航到 **控制台** → **插件** → **Hanimeta**
2. 配置以下项：
   - **后端地址**: `http://127.0.0.1:8585`（如果后端在远程服务器，请修改为相应地址）
   - **API Token**: （可选，如果后端配置了认证）
   - **启用日志**: 建议开启以便排查问题
3. 点击 **保存**

#### 4. 测试配置

1. 在媒体库中添加 Hanime 或 DLsite 内容
2. 右键点击媒体文件 → **识别** → 选择 **Hanimeta** 提供商
3. 搜索并匹配元数据

### 🔄 自动更新

通过插件仓库安装后，Jellyfin 会自动检测并提示新版本更新。你也可以手动检查更新：

1. 导航到 **控制台** → **插件** → **已安装**
2. 如果有新版本，会显示 **更新可用**
3. 点击 **更新** 并重启 Jellyfin

### ⚙️ 配置

编辑 `ScraperBackendService/appsettings.json`：

```json
{
  "ServiceConfig": {
    "Port": 8585,
    "MaxConcurrentRequests": 3,
    "RateLimitSeconds": 20,
    "RequestTimeoutSeconds": 180
  }
}
```

**配置说明：**

| 配置项 | 描述 | 默认值 | 推荐范围 |
|--------|------|--------|----------|
| `Port` | HTTP 监听端口 | 8585 | 1024-65535 |
| `Host` | 监听地址 | "0.0.0.0" | 127.0.0.1（本地）/0.0.0.0（所有） |
| `AuthToken` | API 认证令牌 | "" | 强随机字符串（生产环境必须） |
| `MaxConcurrentRequests` | 全局并发槽位数 | 3 | 1-10 |
| `RateLimitSeconds` | 全局速率限制（秒） | 20 | 10-60 |
| `RequestTimeoutSeconds` | 请求超时（秒） | 180 | 90-300 |

> **注意**：并发和速率限制对所有提供商（Hanime、DLsite）统一生效。

**性能调优：**

```json
// 个人使用
{ "MaxConcurrentRequests": 3, "RateLimitSeconds": 20 }

// 多用户
{ "MaxConcurrentRequests": 10, "RateLimitSeconds": 20 }

// 保守（避免封禁）
{ "MaxConcurrentRequests": 1, "RateLimitSeconds": 60 }
```

### 📋 系统要求

- .NET 9 Runtime/SDK
- Jellyfin 10.10.7+
- Playwright - Chromium（约 100MB）
- 4GB RAM（推荐 8GB）
- Linux/Windows/macOS

### 🆘 故障排除

**Playwright 未找到：**
```bash
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium --with-deps
```

**后端无法启动：**
```bash
dotnet --version  # 检查 .NET
playwright install chromium  # 重装 Playwright
```

**插件未加载：**
1. 验证后端运行：`http://127.0.0.1:8585`
2. 检查插件配置
3. 查看 Jellyfin 日志

### 📖 文档

- [后端服务详细文档](ScraperBackendService/README.md)
- [贡献指南](CONTRIBUTING.md)
- [MIT License](LICENSE)

### 📞 支持

- [Issues](https://github.com/Qing-98/HanimetaScraper/issues)
- [Discussions](https://github.com/Qing-98/HanimetaScraper/discussions)

---

<a name="english"></a>

## English

Unified metadata solution for Jellyfin supporting Hanime and DLsite content.

### 📋 Structure

```
├── ScraperBackendService/     # Backend service (Playwright)
├── Jellyfin.Plugin.Hanimeta/  # Jellyfin plugin
└── Test/NewScraperTest/       # Tests
```

### ✨ Features

- 🔍 Smart search - Title/ID search
- 📊 Metadata extraction - Title, description, rating, cast
- 🖼️ Image management - Cover, backdrop, thumbnails
- 🛡️ Anti-detection - Playwright anti-bot
- ⚡ Performance - Caching, concurrency, rate limiting

### 🚀 Quick Start

#### 1. Install Backend Service

**Prebuilt Package (Recommended):**

1. Download [Releases](https://github.com/Qing-98/HanimetaScraper/releases)
2. Install backend:
   ```bash
   unzip ScraperBackendService-x.x.x.zip && cd backend
   ./install-playwright.sh  # or install-playwright.bat (Windows)
   ./start-backend.sh       # or start-backend.bat (Windows)
   ```

**Build from Source:**

```bash
dotnet build
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium --with-deps
cd ScraperBackendService && dotnet run
```

#### 2. Install Jellyfin Plugin

**Method A: Automatic Installation (Recommended)**

1. Open Jellyfin Dashboard
2. Navigate to **Dashboard** → **Plugins** → **Repositories**
3. Click **+** to add a new repository
4. Enter the following information:
   - **Repository Name**: `Hanimeta Official`
   - **Repository URL**: `https://raw.githubusercontent.com/Qing-98/HanimetaScraper/main/repository.json`
5. Click **Save**
6. Go to **Plugins** → **Catalog**
7. Find **Hanimeta** in the list and click **Install**
8. Restart Jellyfin

**Method B: Manual Installation**

If automatic installation fails, you can install manually:

```bash
# Download latest release
wget https://github.com/Qing-98/HanimetaScraper/releases/latest/download/Jellyfin.Plugin.Hanimeta.zip

# Stop Jellyfin
sudo systemctl stop jellyfin

# Extract to plugins directory
unzip Jellyfin.Plugin.Hanimeta.zip -d /var/lib/jellyfin/plugins/Hanimeta/

# Start Jellyfin
sudo systemctl start jellyfin
```

#### 3. Configure Plugin

In Jellyfin Dashboard:

1. Navigate to **Dashboard** → **Plugins** → **Hanimeta**
2. Configure the following:
   - **Backend URL**: `http://127.0.0.1:8585` (change if backend is on remote server)
   - **API Token**: (optional, if backend requires authentication)
   - **Enable Logging**: Recommended for troubleshooting
3. Click **Save**

#### 4. Test Configuration

1. Add Hanime or DLsite content to your library
2. Right-click media file → **Identify** → Select **Hanimeta** provider
3. Search and match metadata

### 🔄 Automatic Updates

After installing via plugin repository, Jellyfin will automatically detect and notify you of new version updates. You can also manually check for updates:

1. Navigate to **Dashboard** → **Plugins** → **Installed**
2. If a new version is available, you'll see **Update Available**
3. Click **Update** and restart Jellyfin

### ⚙️ Configuration

编辑 `ScraperBackendService/appsettings.json`：

```json
{
  "ServiceConfig": {
    "Port": 8585,
    "MaxConcurrentRequests": 3,
    "RateLimitSeconds": 20,
    "RequestTimeoutSeconds": 180
  }
}
```

**Configuration Options:**

| Setting | Description | Default | Recommended |
|---------|-------------|---------|-------------|
| `Port` | HTTP listening port | 8585 | 1024-65535 |
| `Host` | Listening address | "0.0.0.0" | 127.0.0.1 (local)/0.0.0.0 (all) |
| `AuthToken` | API auth token | "" | Strong random string (production required) |
| `MaxConcurrentRequests` | Global concurrency slots | 3 | 1-10 |
| `RateLimitSeconds` | Global rate limit (seconds) | 20 | 10-60 |
| `RequestTimeoutSeconds` | Request timeout (seconds) | 180 | 90-300 |

> **Note**: Concurrency and rate limit apply globally to all providers (Hanime, DLsite).

**Performance Tuning:**

```json
// Personal use
{ "MaxConcurrentRequests": 3, "RateLimitSeconds": 20 }

// Multi-user
{ "MaxConcurrentRequests": 10, "RateLimitSeconds": 20 }

// Conservative (avoid blocking)
{ "MaxConcurrentRequests": 1, "RateLimitSeconds": 60 }
```

### 📋 系统要求

- .NET 9 Runtime/SDK
- Jellyfin 10.10.7+
- Playwright - Chromium（约 100MB）
- 4GB RAM（推荐 8GB）
- Linux/Windows/macOS

### 🆘 故障排除

**Playwright 未找到：**
```bash
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium --with-deps
```

**后端无法启动：**
```bash
dotnet --version  # 检查 .NET
playwright install chromium  # 重装 Playwright
```

**插件未加载：**
1. 验证后端运行：`http://127.0.0.1:8585`
2. 检查插件配置
3. 查看 Jellyfin 日志

### 📖 文档

- [后端服务详细文档](ScraperBackendService/README.md)
- [贡献指南](CONTRIBUTING.md)
- [MIT License](LICENSE)

### 📞 支持

- [Issues](https://github.com/Qing-98/HanimetaScraper/issues)
- [Discussions](https://github.com/Qing-98/HanimetaScraper/discussions)

---

**Repository**: [HanimetaScraper](https://github.com/Qing-98/HanimetaScraper) | **License**: MIT