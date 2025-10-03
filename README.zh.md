# HanimetaScraper

基于 .NET 8 的 Jellyfin 元数据刮削解决方案，支持 Hanime 和 DLsite 内容。

## 项目结构

### 后端服务
- **ScraperBackendService** - 核心刮削后端服务，提供 REST API

### Jellyfin 插件
- **Jellyfin.Plugin.Hanimeta.HanimeScraper** - Hanime 元数据提供插件
- **Jellyfin.Plugin.Hanimeta.DLsiteScraper** - DLsite 元数据提供插件
- **Jellyfin.Plugin.Hanimeta.Common** - 插件共享库

### 测试工具
- **NewScraperTest** - 后端服务测试套件

## 功能特性

- 🔍 **智能搜索** - 按标题或 ID 搜索内容
- 📊 **丰富元数据** - 标题、描述、评分、发布日期、人员信息
- 🖼️ **图像支持** - 封面、背景、缩略图
- 🎌 **多语言** - 支持中文、日文内容
- ⚡ **高性能** - 并发处理、缓存、重试机制
- 🛡️ **反检测** - 处理 Cloudflare 等反爬虫机制

## 架构

```
Jellyfin 插件 → HTTP API → ScraperBackendService → 网站爬虫
```

后端服务提供统一 API，插件通过 HTTP 请求获取元数据，支持缓存和并发控制。

## 配置

### 后端服务配置

主要配置项（appsettings.json）：
```json
{
  "ServiceConfig": {
    "Port": 8585,
    "AuthToken": "",
    "MaxConcurrentRequests": 10,
    "RequestTimeoutSeconds": 60
  }
}
```

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

## 许可证

MIT License