# 刮削器后端服务

[English](README.md) | [中文](README.zh.md)

为里番等动画元数据提取而设计的综合网络爬虫服务，支持多个提供商。使用 .NET 8 构建，专为与 Jellyfin 媒体服务器集成而设计。

## 🚀 功能特性

### 多提供商支持
- **Hanime 提供商**：基于 Playwright 的动态 JavaScript 内容爬取
- **DLsite 提供商**：高效静态内容提取的基于 HTTP 的爬取
- **可扩展架构**：轻松添加新的内容提供商

### 高级爬取功能
- **双重网络客户端**：针对不同内容类型的 HTTP 和 Playwright 方法
- **反机器人保护**：内置处理 Cloudflare 和其他反机器人措施的机制
- **上下文管理**：智能浏览器上下文重用和轮换
- **并发处理**：可配置的并发请求处理
- **重试逻辑**：强大的错误处理和重试机制

### 全面的元数据提取
- **基本信息**：标题、描述、ID、评分、发布日期
- **媒体资产**：主要图像、背景、缩略图，具有自动去重
- **人员信息**：演员和工作人员，具有角色映射（日文 → 英文）
- **分类信息**：类型、工作室、系列信息
- 
### 生产就绪功能
- **RESTful API**：具有标准化响应格式的清洁 HTTP API
- **身份验证**：可选的基于令牌的身份验证
- **配置**：通过 appsettings.json 和环境变量的灵活配置
- **日志记录**：具有可配置级别的全面日志记录
- **健康检查**：内置健康监控端点
- **超时管理**：可配置的请求超时
- **速率限制**：并发请求限制

## 📦 安装

### 先决条件
- .NET 8 SDK
- PowerShell（用于 Playwright 浏览器安装）

### 快速开始

1. **克隆仓库**
```bash
git clone https://github.com/your-repo/HanimetaScraper.git
cd HanimetaScraper/ScraperBackendService
```

2. **安装依赖项**
```bash
dotnet restore
```

3. **安装 Playwright 浏览器**
```bash
pwsh bin/Debug/net8.0/playwright.ps1 install
```

4. **运行服务**
```bash
dotnet run
```

服务将默认在 `http://localhost:8585` 启动。

## ⚙️ 配置

### appsettings.json
```json
{
  "ServiceConfig": {
    "Port": 8585,
    "Host": "0.0.0.0",
    "AuthToken": null,
    "TokenHeaderName": "X-API-Token",
    "EnableDetailedLogging": false,
    "MaxConcurrentRequests": 10,
    "RequestTimeoutSeconds": 60
  }
}
```

### 环境变量
- `SCRAPER_PORT`：覆盖监听端口
- `SCRAPER_AUTH_TOKEN`：设置认证令牌

### 配置选项

| 设置 | 描述 | 默认值 | 示例 |
|------|------|--------|------|
| `Port` | HTTP 监听端口 | 8585 | 9090 |
| `Host` | 监听地址 | "0.0.0.0" | "127.0.0.1" |
| `AuthToken` | API 认证令牌 | null | "secret-token-123" |
| `TokenHeaderName` | 认证头名称 | "X-API-Token" | "Authorization" |
| `EnableDetailedLogging` | 调试日志 | false | true |
| `MaxConcurrentRequests` | 并发限制 | 10 | 20 |
| `RequestTimeoutSeconds` | 请求超时 | 60 | 120 |

## 🌐 API 参考

### 基础 URL
```
http://localhost:8585
```

### 身份验证
当配置了 `AuthToken` 时，在请求头中包含它：
```
X-API-Token: your-secret-token
```

### 端点

#### 服务信息
```http
GET /
```
返回服务元数据和健康状态。

**响应：**
```json
{
  "success": true,
  "data": {
    "service": "ScraperBackendService",
    "version": "2.0.0",
    "providers": ["Hanime", "DLsite"],
    "authEnabled": false,
    "timestamp": "2024-01-15T10:30:00.000Z"
  }
}
```

#### 健康检查
```http
GET /health
```
返回服务健康状态。

#### Hanime 内容搜索
```http
GET /api/hanime/search?title={title}&max={max}
```

**参数：**
- `title`（必需）：搜索关键词或短语
- `max`（可选）：最大结果数（默认：12，最大：50）

**示例：**
```bash
curl "http://localhost:8585/api/hanime/search?title=Love&max=5"
```

#### Hanime 内容详情
```http
GET /api/hanime/{id}
```

**参数：**
- `id`：Hanime 内容 ID（数字）

**示例：**
```bash
curl "http://localhost:8585/api/hanime/12345"
```

#### DLsite 内容搜索
```http
GET /api/dlsite/search?title={title}&max={max}
```

**参数：**
- `title`（必需）：搜索关键词（支持日文）
- `max`（可选）：最大结果数（默认：12，最大：50）

**示例：**
```bash
curl "http://localhost:8585/api/dlsite/search?title=恋爱&max=5"
```

#### DLsite 内容详情
```http
GET /api/dlsite/{id}
```

**参数：**
- `id`：DLsite 产品 ID（例如，"RJ123456"）

**示例：**
```bash
curl "http://localhost:8585/api/dlsite/RJ123456"
```

### 响应格式

所有 API 响应都遵循这种标准格式：

**成功响应：**
```json
{
  "success": true,
  "data": { ... },
  "message": "可选的成功消息",
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

**错误响应：**
```json
{
  "success": false,
  "error": "错误描述",
  "message": "可选的错误详情",
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

### 元数据模式

```json
{
  "id": "12345",
  "title": "内容标题",
  "originalTitle": "原语言标题",
  "description": "内容描述...",
  "rating": 4.5,
  "releaseDate": "2024-01-15T00:00:00Z",
  "year": 2024,
  "studios": ["工作室名称"],
  "genres": ["恋爱", "喜剧"],
  "series": ["系列名称"],
  "people": [
    {
      "name": "人员姓名",
      "type": "Actor",
      "role": "配音演员"
    }
  ],
  "primary": "https://example.com/cover.jpg",
  "backdrop": "https://example.com/backdrop.jpg",
  "thumbnails": [
    "https://example.com/thumb1.jpg",
    "https://example.com/thumb2.jpg"
  ],
  "sourceUrls": ["https://source-site.com/content/12345"]
}
```

## 🔧 开发

### 项目结构
```
ScraperBackendService/
├── Core/                    # 核心功能
│   ├── Abstractions/       # 接口和契约
│   ├── Net/                # 网络客户端
│   ├── Parsing/            # HTML/内容解析
│   ├── Pipeline/           # 编排逻辑
│   ├── Routing/            # URL 和 ID 处理
│   ├── Normalize/          # 数据规范化
│   └── Util/               # 工具函数
├── Providers/              # 内容提供商实现
│   ├── DLsite/            # DLsite 提供商
│   └── Hanime/            # Hanime 提供商
├── Models/                 # 数据模型
├── Configuration/          # 配置类
├── Middleware/             # HTTP 中间件
├── Extensions/             # 服务扩展
└── Program.cs             # 应用程序入口点
```

### 添加新提供商

1. **实现 IMediaProvider 接口：**
```csharp
public class MyProvider : IMediaProvider
{
    public string Name => "MyProvider";
    
    public bool TryParseId(string input, out string id) { ... }
    public string BuildDetailUrlById(string id) { ... }
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(...) { ... }
    public async Task<HanimeMetadata?> FetchDetailAsync(...) { ... }
}
```

2. **在 ServiceCollectionExtensions 中注册：**
```csharp
services.AddScoped<MyProvider>(sp => 
{
    var networkClient = sp.GetRequiredService<HttpNetworkClient>();
    var logger = sp.GetRequiredService<ILogger<MyProvider>>();
    return new MyProvider(networkClient, logger);
});
```

3. **在 Program.cs 中添加 API 端点：**
```csharp
app.MapGet("/api/myprovider/search", async (...) => { ... });
app.MapGet("/api/myprovider/{id}", async (...) => { ... });
```

### 测试

使用测试项目进行开发和验证：

```bash
cd Test/NewScraperTest
dotnet run
```

从交互式测试选项中选择：
1. 完整测试（两个提供商）
2. 仅 DLsite 测试
3. 仅 Hanime 测试
4. 后端 API 集成测试
5. 并发负载测试


## 🔗 Jellyfin 集成

此服务设计用于通过自定义元数据插件与 Jellyfin 媒体服务器配合工作。插件通过 REST API 与此后端服务通信。

### 插件配置
1. 安装配套的 Jellyfin 插件
2. 在插件设置中配置后端服务 URL
3. 如果启用，设置认证令牌
4. 启用您要使用的提供商

## 📝 日志记录

服务提供全面的日志记录：

- **Information**：基本操作流程
- **Warning**：可恢复的错误和异常情况
- **Error**：不可恢复的错误
- **Debug**：详细的操作信息（当启用 DetailedLogging 时）

在 `appsettings.json` 中的日志配置：
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "ScraperBackendService": "Debug"
    }
  }
}
```

## 🔐 安全

### 身份验证
- API 端点的基于令牌的身份验证
- 可配置的令牌头名称
- 公共端点：`/`、`/health`
- 受保护的端点：`/api/*`


## 🚨 故障排除

### 常见问题

**Playwright 浏览器安装**
```bash
# 手动安装浏览器
pwsh bin/Debug/net8.0/playwright.ps1 install chromium

# 安装系统依赖项（Linux）
pwsh bin/Debug/net8.0/playwright.ps1 install-deps
```

**权限问题（Linux）**
```bash
# 将用户添加到适当的组
sudo usermod -a -G audio,video $USER

# 安装额外的依赖项
sudo apt-get install libnss3 libatk-bridge2.0-0 libdrm2 libxkbcommon0 libxss1 libasound2
```

**内存问题**
- 在配置中减少 `MaxConcurrentRequests`
- 增加系统内存或交换空间
- 使用 `--disable-dev-shm-usage` 浏览器标志（已配置）

**网络问题**
- 检查防火墙设置
- 验证目标站点是否可访问
- 如果在企业防火墙后，配置代理设置

### 调试模式

启用详细日志记录：
```json
{
  "ServiceConfig": {
    "EnableDetailedLogging": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

## 📄 许可证

本项目根据 MIT 许可证授权 - 详情请参阅 LICENSE 文件。

## 🤝 贡献

1. Fork 仓库
2. 创建功能分支
3. 进行更改
4. 为新功能添加测试
5. 确保所有测试通过
6. 提交拉取请求

## 📞 支持

对于问题和疑问：
1. 查看故障排除部分
2. 搜索现有的 GitHub 问题
3. 创建包含详细信息的新问题
4. 包含日志和配置（删除敏感数据）