# HanimetaScraper

[English](README.md) | [中文](README.zh.md)

为里番等动画内容提供的综合元数据抓取解决方案，专为 Jellyfin 媒体服务器集成而设计。本项目提供模块化架构，包含后端服务和 Jellyfin 插件，用于从多个内容提供商提取丰富的元数据。

## ⚠️ 重要声明

本项目**仅供个人学习和教育用途**。用户有责任：

- ✅ **遵守目标网站的服务条款**
- ✅ **遵循 robots.txt 指令**
- ✅ **使用合理的请求频率避免服务器过载**
- ✅ **遵守当地法律法规**
- ❌ **不得用于商业目的或大规模数据收集**

作者对软件的误用不承担任何责任。

## 🚀 功能特性

### 多提供商支持
- **Hanime 提供商**：使用 Playwright 进行 JavaScript 启用的动态内容抓取
- **DLsite 提供商**：针对静态内容的高效 HTTP 抓取
- **可扩展架构**：轻松集成新的内容提供商

### 后端服务
- **RESTful API**：具有标准化 JSON 响应的清洁 HTTP 端点
- **双重网络客户端**：针对不同站点类型的 HTTP 和 Playwright 方法
- **反机器人保护**：内置 Cloudflare 和反机器人绕过机制
- **并发处理**：可配置的请求限制和并行处理
- **身份验证**：可选的基于令牌的 API 安全

### Jellyfin 集成
- **原生插件**：与 Jellyfin 10.8+ 媒体服务器无缝集成
- **元数据提取**：标题、描述、评分、演员、类型、图像
- **搜索功能**：支持基于 ID 和基于文本的搜索
- **图像提供商**：自动图像获取和缓存

### 生产功能
- **Docker 支持**：带健康检查的容器化部署
- **配置管理**：通过 JSON 和环境变量的灵活配置
- **全面日志记录**：具有可配置级别的结构化日志
- **错误处理**：强大的重试逻辑和优雅降级
- **性能监控**：内置指标和健康端点

## 📦 项目结构

```
HanimetaScraper/
├── ScraperBackendService/           # 核心后端服务
│   ├── Core/                       # 核心功能
│   │   ├── Abstractions/           # 接口和契约
│   │   ├── Net/                    # 网络客户端（HTTP 和 Playwright）
│   │   ├── Pipeline/               # 编排和工作流
│   │   ├── Parsing/                # HTML 和内容解析
│   │   └── Util/                   # 工具函数
│   ├── Providers/                  # 内容提供商实现
│   │   ├── DLsite/                 # DLsite 提供商
│   │   └── Hanime/                 # Hanime 提供商
│   ├── Models/                     # 数据模型和 DTO
│   └── Configuration/              # 服务配置
├── Jellyfin.Plugin.HanimeScraper/   # Hanime Jellyfin 插件
├── Jellyfin.Plugin.DLsiteScraper/   # DLsite Jellyfin 插件
└── Test/                           # 测试项目
    ├── NewScraperTest/             # 综合测试套件
    └── ScraperConsoleTest/         # 传统控制台测试
```

## 🚀 快速开始

### 先决条件
- .NET 8.0 SDK
- PowerShell（用于 Playwright 浏览器安装）

### 后端服务

1. **克隆和设置**
```bash
git clone https://github.com/your-repo/HanimetaScraper.git
cd HanimetaScraper/ScraperBackendService
dotnet restore
```

2. **安装 Playwright 浏览器**
```bash
pwsh bin/Debug/net8.0/playwright.ps1 install
```

3. **运行服务**
```bash
dotnet run
```

服务将默认在 `http://localhost:8585` 启动。

4. **验证安装**
```bash
curl http://localhost:8585/health
curl "http://localhost:8585/api/dlsite/search?title=example&max=3"
```

### Jellyfin 插件

1. **构建插件**
```bash
# 构建 DLsite 插件
cd Jellyfin.Plugin.DLsiteScraper
dotnet build -c Release

# 构建 Hanime 插件
cd ../Jellyfin.Plugin.HanimeScraper
dotnet build -c Release
```

2. **在 Jellyfin 中安装**
- 将插件文件复制到 Jellyfin 插件目录
- 重启 Jellyfin 服务器
- 在插件设置中配置后端 URL

### 测试

```bash
cd Test/NewScraperTest
dotnet run
```

选择交互式测试选项：
1. 完整测试（两个提供商）
2. 仅 DLsite 测试
3. 仅 Hanime 测试
4. 后端 API 集成测试
5. 并发负载测试

## ⚙️ 配置

### 后端服务

环境变量：
- `SCRAPER_PORT`：服务监听端口（默认：8585）
- `SCRAPER_AUTH_TOKEN`：API 认证令牌
- `SCRAPER_LOG_LEVEL`：日志详细程度

配置文件（`appsettings.json`）：
```json
{
  "ServiceConfig": {
    "Port": 8585,
    "Host": "0.0.0.0",
    "AuthToken": null,
    "EnableDetailedLogging": false,
    "MaxConcurrentRequests": 10,
    "RequestTimeoutSeconds": 60
  }
}
```

### Jellyfin 插件

两个插件共享通用配置：
- **后端 URL**：后端服务 URL（默认：http://localhost:8585）
- **API 令牌**：可选的认证令牌
- **启用日志**：插件特定的日志控制

## 📋 API 参考

### 基础端点

| 方法 | 端点 | 描述 |
|------|------|------|
| GET | `/` | 服务信息 |
| GET | `/health` | 健康检查 |

### DLsite 提供商

| 方法 | 端点 | 描述 |
|------|------|------|
| GET | `/api/dlsite/search?title={query}&max={limit}` | 搜索内容 |
| GET | `/api/dlsite/{id}` | 获取内容详情 |

### Hanime 提供商

| 方法 | 端点 | 描述 |
|------|------|------|
| GET | `/api/hanime/search?title={query}&max={limit}` | 搜索内容 |
| GET | `/api/hanime/{id}` | 获取内容详情 |

### 响应格式

```json
{
  "success": true,
  "data": {
    "id": "12345",
    "title": "内容标题",
    "description": "内容描述...",
    "rating": 4.5,
    "year": 2024,
    "genres": ["恋爱", "喜剧"],
    "studios": ["工作室名称"],
    "people": [
      {
        "name": "人员姓名",
        "type": "Actor",
        "role": "配音演员"
      }
    ],
    "primary": "https://example.com/cover.jpg",
    "thumbnails": ["https://example.com/thumb1.jpg"]
  }
}
```

## 🐳 Docker 部署

### 使用 Docker Compose

```yaml
version: '3.8'
services:
  scraper-backend:
    build: ./ScraperBackendService
    ports:
      - "8585:8585"
    environment:
      - SCRAPER_PORT=8585
      - SCRAPER_AUTH_TOKEN=your-secret-token
    volumes:
      - ./logs:/app/logs
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8585/health"]
      interval: 30s
      timeout: 10s
      retries: 3
```

### 独立 Docker

```bash
# 构建镜像
docker build -t hanime-scraper ./ScraperBackendService

# 运行容器
docker run -d \
  --name scraper-backend \
  -p 8585:8585 \
  -e SCRAPER_AUTH_TOKEN=your-token \
  hanime-scraper
```

## 🔧 开发

### 添加新提供商

1. 实现 `IMediaProvider` 接口
2. 在依赖注入中注册
3. 添加 API 端点
4. 更新文档

### 代码质量

- StyleCop 分析器用于代码样式
- 启用可空引用类型
- 全面的 XML 文档
- 单元和集成测试

### 架构优势

- 现代 .NET 8 架构
- 全程依赖注入
- 基于接口的可测试设计
- 全面的错误处理
- 生产就绪的日志和监控


## 📊 性能

### 优化功能
- HTTP 客户端连接池
- 高效的浏览器上下文管理
- 图像 URL 规范化
- 可配置的并发请求限制
- 响应缓存功能

### 监控
- 内置健康检查
- 结构化 JSON 日志
- 性能指标跟踪
- 全面的错误分类

## 🤝 贡献

1. Fork 仓库
2. 创建功能分支
3. 遵循编码标准
4. 添加全面测试
5. 更新文档
6. 提交拉取请求

## 📝 许可证

本项目根据 MIT 许可证授权 - 详情请参阅 [LICENSE](LICENSE) 文件。

## 🙏 致谢

- **Jellyfin 社区**：优秀的媒体服务器平台
- **Playwright 团队**：强大的浏览器自动化框架
- **Microsoft**：.NET 生态系统和开发工具

## 📞 支持

- **问题**：通过 GitHub Issues 报告错误
- **讨论**：社区帮助和想法
- **文档**：项目目录中的全面指南

---

**注意**：本项目设计用于教育和个人用途。请尊重被抓取网站的服务条款，负责任地使用。