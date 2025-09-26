# Jellyfin Hanime 刮削器插件

[English](README.md) | [中文](README.zh.md)

Jellyfin 插件，通过连接后端刮削服务为 Hanime 内容提供元数据。该插件与 Jellyfin 的元数据系统无缝集成，为里番等成人动画内容提供丰富信息。

## 🚀 功能特性

- 🔍 **高级搜索**：按标题搜索 Hanime 内容，具有智能 ID 检测
- 📊 **丰富元数据**：提取详细元数据，包括标题、描述、评分、发布日期
- 👥 **人员信息**：演员和工作人员信息，具有角色映射
- 🖼️ **图像支持**：主要图像、背景和缩略图
- 🆔 **外部 ID 管理**：Hanime ID 跟踪，确保内容正确识别
- 🌐 **多语言支持**：处理英文和日文内容
- ⚡ **性能优化**：高效的 API 通信，支持缓存

## 📋 系统要求

- **Jellyfin**：版本 10.10.7 或更高
- **.NET 运行时**：.NET 8.0
- **后端 服务**：ScraperBackendService 运行且可访问
- **网络访问**：互联网连接用于内容获取


## ⚙️ 配置

### 插件设置

通过以下路径访问插件配置：**管理仪表板 → 插件 → Hanime 刮削器 → 设置**

| 设置 | 描述 | 默认值 | 示例 |
|------|------|--------|------|
| **后端 URL** | 刮削器后端服务的 URL | `http://127.0.0.1:8585` | `https://scraper.mydomain.com` |
| **API 令牌** | 认证令牌（可选） | `null` | `your-secret-token-123` |
| **启用日志** | 插件日志控制 | `false` | `true`（用于调试时） |

### 后端 URL 配置

后端 URL 应指向您运行的 ScraperBackendService 实例：

- **本地开发**：`http://127.0.0.1:8585`
- **Docker Compose**：`http://scraper-backend:8585`
- **远程服务器**：`https://your-scraper-domain.com`
- **自定义端口**：`http://192.168.1.100:9090`

### 认证设置

如果您的后端服务使用认证：

1. 在后端服务中配置 API 令牌
2. 在插件配置中设置相同的令牌
3. 重启 Jellyfin 以应用更改

## 🔧 使用方法

### 自动元数据检测

插件在库扫描期间自动检测 Hanime 内容：

1. **按文件名**：检测文件名中的 Hanime ID（例如，`86994.mp4`）
2. **按搜索**：未找到 ID 时按标题搜索内容
3. **按手动 ID**：在元数据编辑器中手动设置 Hanime ID

### 手动元数据刷新

1. 右键单击 Jellyfin 中的内容
2. 选择**识别**
3. 选择 **Hanime** 作为元数据提供程序
4. 输入标题或 Hanime ID 进行搜索

### 支持的 ID 格式

- **直接 ID**：`86994`、`12345`（需要4位或更多数字）
- **URL 格式**：`https://hanime1.me/watch?v=86994`
- **混合内容**：插件从各种格式中提取 ID

**注意**：为避免标题搜索时的误识别，数字 ID 必须至少包含4位数字。短数字如"123"或"99"将被视为标题的一部分而不是 ID。

## 🔍 搜索示例

### 基于文本的搜索
```
搜索词："Love Story"
结果：匹配标题的多个动画
```

### 基于 ID 的搜索
```
搜索词："86994"
结果：ID 为 86994 的特定动画
```

### 基于 URL 的搜索
```
搜索词："https://hanime1.me/watch?v=86994"
结果：从 URL 提取的特定动画
```

### 混合content搜索
```
搜索词："Episode 123"
结果：对"Episode 123"进行标题搜索（不会被当作ID 123处理）
```

## 🐛 故障排除

### 常见问题

**插件未出现**
- 验证 Jellyfin 版本（10.10.7+）
- 检查插件安装目录
- 重启 Jellyfin 服务器
- 查看 Jellyfin 日志中的错误

**未找到元数据**
- 验证后端服务正在运行
- 检查后端 URL 配置
- 测试后端连接性：`curl http://your-backend:8585/health`
- 查看 Jellyfin 中的插件日志

**认证错误**
- 验证 API 令牌与后端配置匹配
- 检查令牌头名称（默认：`X-API-Token`）
- 确保令牌正确 URL 编码

**搜索不工作**
- 检查到后端的网络连接
- 验证搜索查询格式
- 测试后端搜索：`curl "http://backend:8585/api/hanime/search?title=test"`
- 对于数字搜索，确保 ID 有4位或更多数字才会进行基于ID的搜索

### 调试模式

启用调试日志进行详细故障排除：

1. 在插件设置中将**启用日志**设置为 `true`
2. 将 Jellyfin 日志级别设置为 `Debug`
3. 重现问题
4. 检查 Jellyfin 日志获取详细信息

### 日志位置

- **Windows**：`C:\ProgramData\Jellyfin\Server\logs\`
- **Linux**：`/var/log/jellyfin/`
- **Docker**：容器日志或挂载的日志目录

## 📝 许可证

本项目根据 MIT 许可证授权 - 详情请参阅 [LICENSE](../LICENSE) 文件。

## 🤝 支持

- **问题**：通过 [GitHub Issues](https://github.com/your-repo/HanimetaScraper/issues) 报告错误
- **文档**：项目仓库中的全面指南
- **社区**：加入讨论获取帮助和功能请求

## 🔗 相关项目

- **[ScraperBackendService](../ScraperBackendService/)**：后端刮削服务
- **[DLsite 刮削器插件](../Jellyfin.Plugin.DLsiteScraper/)**：配套 DLsite 插件
- **[Jellyfin](https://jellyfin.org/)**：开源媒体服务器

---

**注意**：此插件设计用于个人使用。请尊重内容提供商的服务条款，负责任地使用。
