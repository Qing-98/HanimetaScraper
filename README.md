# HanimetaScraper

[English](#english) | [中文](#chinese)

<a name="chinese"></a>

## 中文简介

**HanimetaScraper** 是一个为 Jellyfin 媒体服务器提供的统一元数据解决方案，支持 **Hanime** 和 **DLsite** 内容的自动识别与信息获取。

### 📋 项目结构

```
├── ScraperBackendService/     # 后端爬虫服务（Playwright 驱动）
├── Jellyfin.Plugin.Hanimeta/  # Jellyfin 统一插件
└── Test/NewScraperTest/       # 集成测试
```

### ✨ 核心功能

- 🔍 **智能搜索** - 按标题或 ID 搜索内容
- 📊 **元数据提取** - 标题、描述、评分、演员等信息
- 🖼️ **图像管理** - 封面、背景、缩略图
- 🛡️ **反检测** - Playwright 驱动的高级反机器人功能
- ⚡ **性能优化** - 智能缓存、并发控制、速率限制

### 🚀 快速开始

#### 安装预构建包（推荐）

1. **下载发布版本** - [GitHub Releases](https://github.com/Qing-98/HanimetaScraper/releases)

2. **后端服务设置**
   ```bash
   unzip ScraperBackendService-x.x.x.zip
   cd backend
   
   # 安装 Playwright（首次）
   ./install-playwright.sh        # Linux/macOS
   # 或
   install-playwright.bat         # Windows
   
   # 启动服务
   ./start-backend.sh             # Linux/macOS
   # 或
   start-backend.bat              # Windows
   ```

3. **Jellyfin 插件安装**
   ```bash
   # 停止 Jellyfin 服务
   sudo systemctl stop jellyfin
   
   # 解压插件到插件目录
   unzip Jellyfin.Plugin.Hanimeta.zip -d /var/lib/jellyfin/plugins/
   
   # 重启 Jellyfin 服务
   sudo systemctl start jellyfin
   ```

4. **配置插件** - 在 Jellyfin 管理面板 → 插件中配置：
   - 后端服务地址：`http://127.0.0.1:8585`
   - API Token：（可选）
   - 启用日志：`false`

#### 从源代码构建

```bash
# 构建项目
dotnet build

# 安装 Playwright
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium --with-deps

# 运行后端服务
cd ScraperBackendService
dotnet run
```

### ⚙️ 配置

编辑 `appsettings.json` 调整后端参数：

```json
{
  "ServiceConfig": {
    "Port": 8585,
    "HanimeMaxConcurrentRequests": 3,
    "DlsiteMaxConcurrentRequests": 3,
    "HanimeRateLimitSeconds": 30,
    "DlsiteRateLimitSeconds": 30,
    "RequestTimeoutSeconds": 150
  }
}
```

### 📋 系统要求

- **.NET 9 Runtime** 或 SDK
- **Jellyfin 10.10.7+**
- **Playwright** - Chromium 浏览器环境
- **4GB RAM** (推荐 8GB)

### 📖 文档

- [后端服务](ScraperBackendService/README.md) - 后端服务详细文档
- [贡献指南](CONTRIBUTING.md) - 如何贡献代码
- [许可证](LICENSE) - MIT License

---

<a name="english"></a>

## English

**HanimetaScraper** is a unified metadata solution for Jellyfin media server, providing automatic content recognition and information retrieval for **Hanime** and **DLsite** content.

### 📋 Project Structure

```
├── ScraperBackendService/     # Backend scraper service (Playwright-driven)
├── Jellyfin.Plugin.Hanimeta/  # Jellyfin unified plugin
└── Test/NewScraperTest/       # Integration tests
```

### ✨ Features

- 🔍 **Smart Search** - Search content by title or ID
- 📊 **Metadata Extraction** - Title, description, rating, cast, etc.
- 🖼️ **Image Management** - Cover, backdrop, thumbnail images
- 🛡️ **Anti-Detection** - Playwright-driven advanced anti-bot functionality
- ⚡ **Performance** - Smart caching, concurrency control, rate limiting

### 🚀 Quick Start

#### Install Prebuilt Package (Recommended)

1. **Download Release** - [GitHub Releases](https://github.com/Qing-98/HanimetaScraper/releases)

2. **Backend Service Setup**
   ```bash
   unzip ScraperBackendService-x.x.x.zip
   cd backend
   
   # Install Playwright (first time)
   ./install-playwright.sh        # Linux/macOS
   # or
   install-playwright.bat         # Windows
   
   # Start service
   ./start-backend.sh             # Linux/macOS
   # or
   start-backend.bat              # Windows
   ```

3. **Jellyfin Plugin Installation**
   ```bash
   # Stop Jellyfin service
   sudo systemctl stop jellyfin
   
   # Extract plugin to plugins directory
   unzip Jellyfin.Plugin.Hanimeta.zip -d /var/lib/jellyfin/plugins/
   
   # Restart Jellyfin service
   sudo systemctl start jellyfin
   ```

4. **Configure Plugin** - In Jellyfin Dashboard → Plugins:
   - Backend Service URL：`http://127.0.0.1:8585`
   - API Token：(Optional)
   - Enable Logging：`false`

#### Build from Source

```bash
# Build project
dotnet build

# Install Playwright
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium --with-deps

# Run backend service
cd ScraperBackendService
dotnet run
```

### ⚙️ Configuration

Edit `appsettings.json` to adjust backend parameters:

```json
{
  "ServiceConfig": {
    "Port": 8585,
    "HanimeMaxConcurrentRequests": 3,
    "DlsiteMaxConcurrentRequests": 3,
    "HanimeRateLimitSeconds": 30,
    "DlsiteRateLimitSeconds": 30,
    "RequestTimeoutSeconds": 150
  }
}
```

### 📋 System Requirements

- **.NET 9 Runtime** or SDK
- **Jellyfin 10.10.7+**
- **Playwright** - Chromium browser environment
- **4GB RAM** (8GB recommended)

### 📖 Documentation

- [Backend Service](ScraperBackendService/README.md) - Backend service details
- [Contributing](CONTRIBUTING.md) - How to contribute
- [License](LICENSE) - MIT License

---

**Repository**: [HanimetaScraper](https://github.com/Qing-98/HanimetaScraper)