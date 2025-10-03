# ScraperBackendService

HanimetaScraper 的核心后端服务，提供 REST API 用于元数据刮削。

## 功能特性

- **多提供商支持** - Hanime、DLsite
- **RESTful API** - 标准化 JSON 响应
- **反机器人保护** - Playwright 浏览器自动化
- **并发控制** - 可配置的 Provider 访问限制
- **缓存机制** - 内存缓存减少重复请求
- **身份验证** - 可选 API Token 认证

## API 端点

### 基础
- `GET /` - 服务信息
- `GET /health` - 健康检查
- `GET /cache/stats` - 缓存统计
- `DELETE /cache/clear` - 清空缓存
- `DELETE /cache/{provider}/{id}` - 删除特定缓存

### Hanime
- `GET /api/hanime/search?title={query}&max={limit}` - 搜索
- `GET /api/hanime/{id}` - 获取详情

### DLsite  
- `GET /api/dlsite/search?title={query}&max={limit}` - 搜索
- `GET /api/dlsite/{id}` - 获取详情

## 配置

### 主要配置项（appsettings.json）

```json
{
  "ServiceConfig": {
    "Port": 8585,
    "Host": "0.0.0.0",
    "AuthToken": "",
    "TokenHeaderName": "X-API-Token",
    "HanimeMaxConcurrentRequests": 3,
    "DlsiteMaxConcurrentRequests": 3,
    "RequestTimeoutSeconds": 60,
    "EnableAggressiveMemoryOptimization": true
  }
}
```

### 配置选项详解

| 配置项 | 描述 | 默认值 | 推荐范围 |
|--------|------|--------|----------|
| **Port** | HTTP 监听端口 | 8585 | 1024-65535 |
| **Host** | 监听地址 | "0.0.0.0" | "127.0.0.1"（本地）/"0.0.0.0"（所有接口） |
| **AuthToken** | API 认证令牌 | 空字符串 | 强随机字符串（生产环境必须设置） |
| **TokenHeaderName** | 认证头名称 | "X-API-Token" | 自定义头名称 |
| **HanimeMaxConcurrentRequests** | Hanime 并发限制 | 3 | 1-15 |
| **DlsiteMaxConcurrentRequests** | DLsite 并发限制 | 3 | 1-15 |
| **RequestTimeoutSeconds** | 请求超时时间（秒） | 60 | 30-300 |
| **EnableAggressiveMemoryOptimization** | 启用激进内存优化 | true | true/false |

### 并发控制说明

**Provider 并发限制** - 统一控制对各提供商网站的访问：
- `HanimeMaxConcurrentRequests` - 限制对 Hanime 网站的同时访问数
- `DlsiteMaxConcurrentRequests` - 限制对 DLsite 网站的同时访问数
- 包括搜索请求、详情获取、直接 ID 查询等所有操作
- 超过限制时返回 429 状态码，前端会自动重试

### 环境变量覆盖

以下环境变量可覆盖配置文件设置：

| 环境变量 | 对应配置 | 示例 |
|----------|----------|------|
| **SCRAPER_PORT** | Port | `8080` |
| **SCRAPER_AUTH_TOKEN** | AuthToken | `your-secret-token-here` |

### 缓存配置

缓存系统自动配置，主要参数：
- **缓存容量**：100 个条目
- **成功结果 TTL**：2 分钟
- **失败结果 TTL**：2 分钟
- **驱逐策略**：LRU（最近最少使用）

### 性能调优建议

**低负载环境（个人使用）：**
```json
{
  "HanimeMaxConcurrentRequests": 3,
  "DlsiteMaxConcurrentRequests": 3
}
```

**高负载环境（多用户）：**
```json
{
  "HanimeMaxConcurrentRequests": 10,
  "DlsiteMaxConcurrentRequests": 10
}
```

**保守设置（避免被封）：**
```json
{
  "HanimeMaxConcurrentRequests": 1,
  "DlsiteMaxConcurrentRequests": 1
}
```

## 技术栈

- **.NET 8** - 现代 C# Runtime
- **ASP.NET Core** - Web API 框架
- **Playwright** - 浏览器自动化
- **HtmlAgilityPack** - HTML 解析