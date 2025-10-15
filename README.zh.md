# HanimetaScraper

基于 .NET 8 的 Jellyfin 元数据刮削解决方案，支持 Hanime 和 DLsite 内容，具备高级速率限制和反检测功能。

## 项目结构

### 后端服务
- **ScraperBackendService** - 核心刮削后端服务，提供带速率限制和缓存的 REST API

### Jellyfin 插件
- **Jellyfin.Plugin.Hanimeta.HanimeScraper** - Hanime 元数据提供插件
- **Jellyfin.Plugin.Hanimeta.DLsiteScraper** - DLsite 元数据提供插件
- **Jellyfin.Plugin.Hanimeta.Common** - 插件共享库

### 测试工具
- **NewScraperTest** - 后端服务测试套件

## 功能特性

### 核心功能
- 🔍 **智能搜索** - 按标题或 ID 搜索内容
- 📊 **丰富元数据** - 标题、描述、评分、发布日期、人员信息
- 🖼️ **图像支持** - 封面、背景、缩略图
- 🎌 **多语言** - 支持中文、日文内容
- ⚡ **高性能** - 并发处理、智能缓存、重试机制

### 高级功能
- 🛡️ **反检测** - 处理 Cloudflare 等反爬虫机制
- ⏱️ **速率限制** - 每槽位速率限制，防止 IP 封禁
- 🔄 **请求队列** - 等待可用槽位而非立即失败
- 💾 **智能缓存** - 内存缓存配合 LRU 淘汰策略
- 📝 **结构化日志** - 完善的日志系统，支持多级详细度
- ⚙️ **灵活配置** - 精细控制并发和速率限制

## 架构

```
Jellyfin 插件 → HTTP API (3分钟超时) → ScraperBackendService (150秒超时) → 网站爬虫
                                                      ↓
                                          并发控制 (3个槽位)
                                                      ↓
                                          速率限制 (每槽30秒)
                                                      ↓
                                          提供商访问 (Hanime/DLsite)
```

后端服务提供统一 API，包含:
- **并发控制**: 限制每个提供商的同时请求数
- **速率限制**: 强制同一槽位的请求间隔时间
- **智能缓存**: 减少重复请求
- **请求队列**: 最多等待 15 秒以获取可用槽位

## 快速开始

### 1. 后端服务设置

```bash
cd ScraperBackendService
dotnet run
```

服务将在 `http://0.0.0.0:8585` 启动

### 2. 插件安装

1. 复制插件 DLL 到 Jellyfin 插件目录:
   - Hanime: `Jellyfin.Plugin.Hanimeta.HanimeScraper.dll`
   - DLsite: `Jellyfin.Plugin.Hanimeta.DLsiteScraper.dll`
   - Common: `Jellyfin.Plugin.Hanimeta.Common.dll`

2. 重启 Jellyfin

3. 在插件设置中配置后端 URL:
   - **管理面板 → 插件 → [插件名称] → 设置**
   - 设置 **后端服务地址**: `http://127.0.0.1:8585` (或你的服务器 IP)

### 3. 使用

在 Jellyfin 中扫描媒体库，插件将自动从后端服务获取元数据。

## 配置

### 后端服务配置

主要配置项（appsettings.json）：

```json
{
  "ServiceConfig": {
    "Port": 8585,
    "Host": "0.0.0.0",
    "AuthToken": "",
    "HanimeMaxConcurrentRequests": 3,
    "DlsiteMaxConcurrentRequests": 3,
    "HanimeRateLimitSeconds": 30,
    "DlsiteRateLimitSeconds": 30,
    "RequestTimeoutSeconds": 150
  }
}
```

**配置说明:**

| 配置项 | 描述 | 默认值 | 推荐范围 |
|--------|------|--------|----------|
| **Port** | HTTP 监听端口 | 8585 | 1024-65535 |
| **Host** | 监听地址 | "0.0.0.0" | "127.0.0.1" (本地) / "0.0.0.0" (全部) |
| **AuthToken** | API 认证令牌 | 空 | 强随机字符串 |
| **HanimeMaxConcurrentRequests** | Hanime 并发槽位数 | 3 | 1-10 |
| **DlsiteMaxConcurrentRequests** | DLsite 并发槽位数 | 3 | 1-10 |
| **HanimeRateLimitSeconds** | Hanime 速率限制 (每槽) | 20 | 10-60 |
| **DlsiteRateLimitSeconds** | DLsite 速率限制 (每槽) | 20 | 10-60 |
| **RequestTimeoutSeconds** | 请求超时 | 150 | 90-300 |

**速率限制说明:**

- **并发槽位**: 限制可同时执行的请求数量
- **速率限制**: 强制同一槽位连续请求的最小间隔
- **请求队列**: 请求最多等待 15 秒以获取槽位，否则返回 429

**配置场景示例:**

| 场景 | 槽位数 | 速率限制 | 行为 |
|------|--------|----------|------|
| **激进** | 10 | 10秒 | 快速但有风险 (可能触发封禁) |
| **平衡** | 3 | 30秒 | 良好平衡 (推荐) |
| **保守** | 1 | 60秒 | 最慢但最安全 |

### 插件配置选项

每个插件支持以下配置项：

| 配置项 | 描述 | 默认值 | 示例 |
|--------|------|--------|------|
| **后端服务地址** | ScraperBackendService 的 URL | `http://127.0.0.1:8585` | `https://scraper.mydomain.com` |
| **API Token** | 后端服务认证令牌（可选） | 空 | `your-secret-token-123` |
| **启用日志** | 插件调试日志控制 | `false` | `true`（调试时使用） |
| **标签映射模式** | 标签写入位置选择 | `Tags` | `Tags` 或 `Genres` |

**标签映射模式说明：**
- **Tags 模式**：Series + Content Tags → Jellyfin Tags 字段，后端 Genres → Jellyfin Genres 字段
- **Genres 模式**：Series + Content Tags → Jellyfin Genres 字段（与后端 Genres 合并）

配置路径：**管理面板 → 插件 → [插件名称] → 设置**

## 性能调优

### 响应时间优化

**最佳情况** (缓存命中):
```
请求 → 缓存命中 → 响应
耗时: ~1ms ✅
```

**正常情况** (速率限制):
```
请求 → 等待槽位 → 速率限制等待 → 刮削 → 缓存 → 响应
耗时: ~35-60秒 ⏱️
```

**最坏情况** (全部等待):
```
请求 → 等待槽位15秒 → 速率限制30秒 → 刮削60秒 → 响应
耗时: ~105秒 🐌
```

### 配置建议

**个人使用** (低流量):
```json
{
  "HanimeMaxConcurrentRequests": 3,
  "HanimeRateLimitSeconds": 20
}
```

**多用户** (高流量):
```json
{
  "HanimeMaxConcurrentRequests": 5,
  "HanimeRateLimitSeconds": 30
}
```

**保守配置** (避免封禁):
```json
{
  "HanimeMaxConcurrentRequests": 1,
  "HanimeRateLimitSeconds": 60
}
```

### 禁用速率限制 (不推荐)

如需禁用速率限制 (用于测试或私有实例):

```json
{
  "HanimeRateLimitSeconds": 0,
  "DlsiteRateLimitSeconds": 0
}
```

⚠️ **警告**: 禁用速率限制可能导致目标网站封禁 IP。

## 日志系统

后端服务提供结构化日志，支持多级详细度:

**总是可见 (LogAlways):**
- 用户操作 (搜索/查询开始)
- 操作结果 (成功/失败/结果数)
- 速率限制等待
- 服务状态

**信息级别 (LogInformation):**
- 缓存操作
- 内部流程状态

**调试级别 (LogDebug):**
- 槽位分配详情
- 内存管理
- 性能指标

**日志输出示例:**

```
12:34:56 [HanimeDetail] Query: '12345'
12:34:57 [HanimeDetail] Waiting 25s (rate limit)
12:35:22 [HanimeDetail] ✅ Found
```

## API 端点

### 基础
- `GET /` - 服务信息
- `GET /health` - 健康检查
- `GET /cache/stats` - 缓存统计
- `DELETE /cache/clear` - 清空缓存
- `DELETE /cache/{provider}/{id}` - 删除特定缓存

### Hanime
- `GET /api/hanime/search?title={query}&max={limit}` - 标题搜索
- `GET /api/hanime/{id}` - ID 查询详情

### DLsite  
- `GET /api/dlsite/search?title={query}&max={limit}` - 标题搜索
- `GET /api/dlsite/{id}` - ID 查询详情

## 故障排除

### 响应时间过长

**症状:** 请求需要 60+ 秒

**解决方案:**
1. 检查缓存命中率: `GET /cache/stats`
2. 减少速率限制: `HanimeRateLimitSeconds: 15`
3. 增加并发槽位: `HanimeMaxConcurrentRequests: 5`

### 频繁 429 错误

**症状:** 大量 "Service busy" 消息

**解决方案:**
1. 增加并发槽位: `HanimeMaxConcurrentRequests: 5`
2. 增加后端超时: `RequestTimeoutSeconds: 180`

### IP 被封禁

**症状:** 请求失败，出现 Cloudflare 挑战

**解决方案:**
1. 增加速率限制: `HanimeRateLimitSeconds: 45`
2. 减少并发槽位: `HanimeMaxConcurrentRequests: 2`
3. 启用激进内存优化

## 文档

- **后端 README**: [ScraperBackendService/README.md](ScraperBackendService/README.md)

## 许可证

MIT License

## 贡献

欢迎贡献! 请随时提交 Pull Request。

## 支持

如有问题或功能请求，请使用 [GitHub Issues](https://github.com/Qing-98/HanimetaScraper/issues) 页面。