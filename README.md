# HanimetaScraper

> **[English](README.en.md) | 中文**

基于 .NET 8 的 Jellyfin 元数据刮削解决方案，支持 Hanime 和 DLsite 内容，具备**Playwright 驱动的高级反检测**和速率限制功能。

## 🚀 快速开始

### 📦 预构建发布包安装 (推荐给大多数用户)

使用我们预构建的发布包是最简单的入门方式：

#### Windows 用户
1. **下载最新发布版本**：访问 [GitHub Releases](https://github.com/Qing-98/HanimetaScraper/releases)
2. **解压后端服务**：
   - 下载 `ScraperBackendService-x.x.x.zip`
   - 解压到你希望的位置（如 `C:\HanimetaScraper\`）
3. **设置 Playwright 浏览器**（首次使用）：
   ```batch
   # 进入解压目录
   cd backend
   install-playwright.bat
   ```
4. **启动后端服务**：
   ```batch
   start-backend.bat
   ```
5. **安装 Jellyfin 插件**：
   - 下载 `Jellyfin.Plugin.Hanimeta.DLsiteScraper.zip` 和 `Jellyfin.Plugin.Hanimeta.HanimeScraper.zip`
   - 停止 Jellyfin 服务
   - 将插件文件解压到 `C:\ProgramData\Jellyfin\Server\plugins\`
   - 重启 Jellyfin 服务
6. **配置插件**：在 Jellyfin 管理面板 → 插件 中配置

#### Linux/macOS 用户
1. **下载最新发布版本**：访问 [GitHub Releases](https://github.com/Qing-98/HanimetaScraper/releases)
2. **解压后端服务**：
   ```bash
   unzip ScraperBackendService-x.x.x.zip
   cd backend
   ```
3. **设置 Playwright 浏览器**（首次使用）：
   ```bash
   chmod +x install-playwright.sh
   ./install-playwright.sh
   ```
4. **启动后端服务**：
   ```bash
   chmod +x start-backend.sh
   ./start-backend.sh
   ```
5. **安装 Jellyfin 插件**：
   ```bash
   # 停止 Jellyfin 服务
   sudo systemctl stop jellyfin
   
   # 解压插件到 Jellyfin 目录
   unzip Jellyfin.Plugin.Hanimeta.DLsiteScraper.zip -d /var/lib/jellyfin/plugins/Jellyfin.Plugin.Hanimeta.DLsiteScraper/
   unzip Jellyfin.Plugin.Hanimeta.HanimeScraper.zip -d /var/lib/jellyfin/plugins/Jellyfin.Plugin.Hanimeta.HanimeScraper/
   
   # 重启 Jellyfin 服务
   sudo systemctl start jellyfin
   ```
6. **配置插件**：在 Jellyfin 管理面板 → 插件 中配置

#### 插件配置（所有平台）
安装插件后，在 Jellyfin 中进行配置：
1. 打开 Jellyfin 管理面板
2. 导航到 **管理面板 → 插件**
3. 找到 "DLsite Scraper" 和 "Hanime Scraper" 插件
4. 点击每个插件的 **设置** 并配置：
   - **后端服务地址**: `http://127.0.0.1:8585`
   - **API Token**: (如果后端未设置则留空)
   - **启用日志**: `false` (调试时设为 `true`)
   - **标签映射模式**: `Tags` 或 `Genres`

### 🛠️ 从源代码构建 (开发者使用)

#### Windows 用户 (推荐路径)
```batch
# 一键安装向导 (以管理员身份运行)
# 自动安装 .NET 8 SDK 和 Playwright 浏览器
scripts\install-wizard.bat

# 或使用 PowerShell 管理脚本
.\scripts\manage.ps1 build
.\scripts\manage.ps1 setup-playwright    # 安装 Playwright 浏览器
.\scripts\manage.ps1 start
.\scripts\manage.ps1 install
```

📋 **[完整 Windows 指南](WINDOWS_README.md)** - Windows 用户详细说明

#### Linux/macOS 用户
```bash
# 快速设置脚本 (包含 Playwright 设置)
./scripts/quick-start.sh

# 手动设置
cd ScraperBackendService
dotnet run
```

## ⚠️ 重要：Playwright 要求

此解决方案使用 **Microsoft Playwright** 提供高级反机器人功能：
- **自动浏览器安装** (~100MB Chromium 下载)
- **系统依赖** (由安装脚本处理)
- **运行后端服务前需要首次设置**

**快速 Playwright 设置：**
```powershell
# Windows
.\scripts\manage.ps1 setup-playwright

# Linux/macOS
./scripts/quick-start.sh setup-playwright
```

## 项目结构

### 后端服务
- **ScraperBackendService** - 核心刮削后端服务，带有 **Playwright 浏览器自动化**

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

### 高级反检测功能 (Playwright 驱动)
- 🛡️ **高级反检测** - 基于 Playwright 的浏览器自动化，支持隐身配置
- 🌐 **Cloudflare 绕过** - 自动挑战检测和解决
- 🎭 **浏览器指纹随机化** - 动态 User Agent 和视口管理
- 🔄 **会话管理** - 持久浏览器上下文提高成功率
- 🚫 **请求拦截** - 智能资源阻断提升性能

### 性能与可靠性
- ⏱️ **速率限制** - 每槽位速率限制，防止 IP 封禁
- 🔄 **请求队列** - 等待可用槽位而非立即失败
- 💾 **智能缓存** - 内存缓存配合 LRU 淘汰策略
- 📝 **结构化日志** - 完善的日志系统，支持多级详细度
- ⚙️ **灵活配置** - 精细控制并发和速率限制

### Windows 集成
- 🖥️ **一键安装器** - 自动安装向导，包含 .NET SDK 和 Playwright 安装
- 🔧 **PowerShell 管理** - 高级管理脚本，支持 Playwright 设置
- 🎯 **Visual Studio 集成** - 完整的 VS Code 和 Visual Studio 2022 支持
- 📁 **桌面快捷方式** - 方便访问后端服务和管理工具

## 架构

```
Jellyfin 插件 → HTTP API (3分钟超时) → ScraperBackendService (150秒超时) → Playwright 浏览器 → 网站爬虫
                                                      ↓                                    ↓
                                          并发控制 (3个槽位)                    反检测功能
                                                      ↓                                    ↓
                                          速率限制 (每槽30秒)                  隐身浏览器配置
                                                      ↓                                    ↓
                                          提供商访问 (Hanime/DLsite)           Cloudflare 绕过
```

后端服务提供统一 API，包含：
- **并发控制**: 限制每个提供商的同时请求数
- **速率限制**: 强制同一槽位的请求间隔时间
- **智能缓存**: 减少重复请求
- **请求队列**: 最多等待 15 秒以获取可用槽位
- **Playwright 集成**: 用于反机器人功能的高级浏览器自动化

## 安装

### 📦 使用预构建发布包 (推荐)

这是大多数用户最简单的安装方法：

1. **下载发布文件**
   - 访问 [GitHub Releases](https://github.com/Qing-98/HanimetaScraper/releases)
   - 下载最新发布文件：
     - `ScraperBackendService-x.x.x.zip` - 后端服务
     - `Jellyfin.Plugin.Hanimeta.DLsiteScraper.zip` - DLsite 插件
     - `Jellyfin.Plugin.Hanimeta.HanimeScraper.zip` - Hanime 插件

2. **后端服务设置**
   ```bash
   # 解压后端服务
   unzip ScraperBackendService-x.x.x.zip
   cd backend
   
   # 安装 Playwright 浏览器 (首次使用)
   # Windows: 运行 install-playwright.bat
   # Linux/macOS: 运行 ./install-playwright.sh
   
   # 启动服务
   # Windows: 运行 start-backend.bat
   # Linux/macOS: 运行 ./start-backend.sh
   ```

3. **插件安装**
   ```bash
   # 停止 Jellyfin
   sudo systemctl stop jellyfin  # Linux
   # 或在 Windows 上停止 Jellyfin 服务
   
   # 解压插件到 Jellyfin 插件目录
   # Linux: /var/lib/jellyfin/plugins/
   # Windows: C:\ProgramData\Jellyfin\Server\plugins\
   # macOS: ~/Library/Application Support/jellyfin/plugins/
   
   unzip Jellyfin.Plugin.Hanimeta.DLsiteScraper.zip -d [JELLYFIN_PLUGINS_DIR]/Jellyfin.Plugin.Hanimeta.DLsiteScraper/
   unzip Jellyfin.Plugin.Hanimeta.HanimeScraper.zip -d [JELLYFIN_PLUGINS_DIR]/Jellyfin.Plugin.Hanimeta.HanimeScraper/
   
   # 重启 Jellyfin
   sudo systemctl start jellyfin  # Linux
   ```

4. **配置插件**
   - 打开 Jellyfin 管理面板
   - 进入 管理面板 → 插件
   - 配置每个刮削器插件：
     - 后端服务地址: `http://127.0.0.1:8585`
     - API Token: (如果后端未设置则留空)
     - 启用日志: `false` (调试时为 true)

### 🛠️ 从源代码构建

#### 🖥️ Windows (推荐路径)

##### 选项 1: 一键安装器 (包含 Playwright 设置)
1. 下载最新版本
2. 右键点击 `scripts\install-wizard.bat` 并选择 "以管理员身份运行"
3. 按照交互式安装向导操作 (自动安装 .NET 8 SDK 和 Playwright)

##### 选项 2: PowerShell 管理
```powershell
# 检查状态和获取帮助
.\scripts\manage.ps1 help
.\scripts\manage.ps1 status

# 完整设置，包括 Playwright
.\scripts\manage.ps1 build           # 构建解决方案并设置 Playwright
.\scripts\manage.ps1 setup-playwright # 手动 Playwright 设置
.\scripts\manage.ps1 start           # 启动后端服务
.\scripts\manage.ps1 install         # 安装 Jellyfin 插件
```

#### 🐧 Linux/macOS

##### 自动化设置 (包含 Playwright)
```bash
# 一条命令完成包含 Playwright 的完整设置
./scripts/quick-start.sh

# 或分步执行
./scripts/quick-start.sh build       # 包含 Playwright 设置
./scripts/quick-start.sh start
./scripts/quick-start.sh install
```

##### 手动设置
1. **安装 .NET 8 SDK 和 Playwright**
```bash
# 安装 .NET 8
sudo apt install dotnet-sdk-8.0  # Ubuntu/Debian
# 或从 Microsoft 下载

# 安装 Playwright
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium --with-deps
```

2. **后端服务设置**
```bash
cd ScraperBackendService
dotnet run
```

3. **插件安装**
```bash
# 复制插件 DLL 到 Jellyfin 插件目录
# Linux: /var/lib/jellyfin/plugins/
# macOS: ~/Library/Application Support/jellyfin/plugins/
```

### 插件配置

1. 打开 Jellyfin 管理面板
2. 导航到 **管理面板 → 插件**
3. 找到安装的刮削器并点击 **设置**

#### 配置选项
- **后端服务地址**: `http://127.0.0.1:8585` (如为远程则调整 IP)
- **API Token**: 必须与后端 `AuthToken` 匹配 (如果设置了)
- **启用日志**: `false` (调试时设为 `true`)
- **标签映射模式**: `Tags` 或 `Genres`

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

**Playwright 性能影响:**
- **内存**: 每个浏览器实例额外 ~50-100MB
- **启动时间**: 首次请求增加 2-5 秒
- **CPU**: 主动刮削时中等，空闲时很低

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

**正常情况** (Playwright + 速率限制):
```
请求 → 等待槽位 → 速率限制等待 → Playwright 浏览器 → 刮削 → 缓存 → 响应
耗时: ~40-65秒 ⏱️ (包含浏览器启动)
```

**最坏情况** (全部等待 + 浏览器):
```
请求 → 等待槽位15秒 → 速率限制30秒 → 浏览器启动5秒 → 刮削60秒 → 响应
耗时: ~110秒 🐌
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

## 管理和监控

### Windows 管理
```powershell
# 服务管理
.\scripts\manage.ps1 start       # 启动后端服务
.\scripts\manage.ps1 stop        # 停止后端服务
.\scripts\manage.ps1 status      # 检查服务状态 (包含 Playwright)

# Playwright 管理
.\scripts\manage.ps1 setup-playwright  # 安装/更新 Playwright 浏览器

# 插件管理
.\scripts\manage.ps1 install     # 安装 Jellyfin 插件
.\scripts\manage.ps1 uninstall   # 移除插件

# 开发
.\scripts\manage.ps1 build       # 构建解决方案 (包含 Playwright 检查)
.\scripts\manage.ps1 test        # 运行测试
.\scripts\manage.ps1 logs        # 查看日志
```

### 跨平台脚本
```bash
# Linux/macOS
./scripts/quick-start.sh [command]

# 可用命令: all, build, start, install, config, examples, setup-playwright
```

## 日志系统

后端服务提供结构化日志，支持多级详细度:

**总是可见 (LogAlways):**
- 用户操作 (搜索/查询开始)
- 操作结果 (成功/失败/结果数)
- 速率限制等待
- 服务状态
- **Playwright 浏览器事件**

**信息级别 (LogInformation):**
- 缓存操作
- 内部流程状态
- **浏览器启动/关闭**

**调试级别 (LogDebug):**
- 槽位分配详情
- 内存管理
- 性能指标
- **详细 Playwright 操作**

**日志输出示例:**

```
12:34:56 [HanimeDetail] Query: '12345'
12:34:57 [HanimeDetail] 正在启动 Playwright 浏览器...
12:34:59 [HanimeDetail] 浏览器就绪，正在导航...
12:35:01 [HanimeDetail] Waiting 25s (rate limit)
12:35:26 [HanimeDetail] ✅ Found
```

## API 端点

### 基础
- `GET /` - 服务信息 (包含 Playwright 状态)
- `GET /health` - 健康检查 (包含浏览器健康状态)
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

### Playwright 相关问题

#### Playwright 未安装
**症状:** "Playwright executable not found" 错误

**解决方案:**
```powershell
# Windows
.\scripts\manage.ps1 setup-playwright

# Linux/macOS
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium --with-deps
```

#### 浏览器启动失败
**症状:** "Failed to launch browser" 错误

**解决方案:**
1. 检查可用内存 (每个浏览器需要 ~100MB)
2. 验证系统依赖:
   ```bash
   # Linux
   sudo apt update && sudo apt install -y libnss3 libatk1.0-0 libdrm2 libxcomposite1
   ```
3. 重启后端服务

#### 浏览器进程挂起
**症状:** 请求超时，浏览器进程仍然存在

**解决方案:**
1. 重启后端服务 (自动清理浏览器)
2. 手动结束浏览器进程:
   ```bash
   # Linux/macOS
   pkill -f chromium
   
   # Windows
   taskkill /f /im chrome.exe
   ```

### 响应时间过长

**症状:** 请求需要 60+ 秒

**解决方案:**
1. 检查缓存命中率: `GET /cache/stats`
2. 减少速率限制: `HanimeRateLimitSeconds: 15`
3. 增加并发槽位: `HanimeMaxConcurrentRequests: 5`
4. **监控浏览器启动时间** (首次请求需要更长时间)

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
3. **验证 Playwright 是否工作**: 检查浏览器启动日志

### Windows 特定问题

**PowerShell 执行策略:**
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**插件安装失败:**
```batch
# 以管理员身份运行
scripts\install-wizard.bat

# 或手动复制文件
.\scripts\manage.ps1 install -Force
```

**后端服务无法启动:**
```batch
# 检查 .NET 安装
dotnet --version

# 检查 Playwright 设置
.\scripts\manage.ps1 setup-playwright

# 使用安装向导修复所有问题
scripts\install-wizard.bat
```

## 开发

### Visual Studio Code (Windows/Linux/macOS)
1. 在 VS Code 中打开项目文件夹
2. 安装推荐扩展 (自动提示)
3. 按 **F5** 开始调试
4. 使用 **Ctrl+Shift+P** → `Tasks: Run Task` 进行构建操作

### Visual Studio 2022 (Windows)
1. 打开 `HanimetaScraper.sln`
2. 将 `ScraperBackendService` 设为启动项目
3. 按 **F5** 开始调试

### 命令行
```bash
# 还原依赖
dotnet restore

# 构建解决方案
dotnet build

# 设置 Playwright (开发必需)
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium

# 运行后端服务
cd ScraperBackendService
dotnet run

# 运行测试
cd Test/NewScraperTest
dotnet run
```

## 系统要求

### 后端服务
- **.NET 8 Runtime** 或 SDK
- **Playwright 依赖**:
  - Chromium 浏览器 (~100MB, 自动安装)
  - 系统库 (Linux: libnss3, libatk1.0-0 等)
- **4GB RAM 最低** (高负载 + 浏览器推荐 8GB)
- **网络连接** 用于内容刮削和浏览器安装
- **Linux/Windows/macOS** 支持

### Jellyfin 插件
- **Jellyfin 10.10.7** 或更高版本
- **后端服务** 运行且可访问 (已设置 Playwright)
- **网络连接** 到后端服务

## 发布和部署

### 自动发布 (GitHub Actions)
```bash
# 更新版本并创建发布
.\scripts\update-version.sh 1.1.0  # Linux/macOS
.\scripts\update-version.bat 1.1.0  # Windows

git add .
git commit -m "chore: bump version to v1.1.0"
git tag v1.1.0
git push origin main --tags
```

### 手动包创建
```powershell
# Windows (包含 Playwright 设置脚本)
.\scripts\manage.ps1 release -Version "1.1.0"

# Linux/macOS  
./scripts/quick-start.sh package
```

## 文档

- **[安装指南](INSTALL.md)** - 详细安装说明
- **[Windows 指南](WINDOWS_README.md)** - Windows 特定文档
- **[贡献指南](CONTRIBUTING.md)** - 开发和贡献指南
- **[安全策略](SECURITY.md)** - 安全指南和报告
- **[后端 README](ScraperBackendService/README.md)** - 后端服务文档

## 许可证

MIT License

## 贡献

欢迎贡献! 请阅读 [贡献指南](CONTRIBUTING.md) 了解行为准则和提交 Pull Request 的流程。

**贡献者注意**: 确保在开发环境中设置了 Playwright:
```bash
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium
```

## 支持

如有问题或功能请求，请使用 [GitHub Issues](https://github.com/Qing-98/HanimetaScraper/issues) 页面。

如有一般问题和讨论，请访问 [GitHub Discussions](https://github.com/Qing-98/HanimetaScraper/discussions)。

## ⚠️ 重要说明

### 首次设置
1. **首次使用前必须安装 Playwright 浏览器**
2. **允许防火墙通过浏览器下载** (~100MB 下载)
3. **浏览器进程带来的额外内存需求**

### 性能考虑
- 浏览器启动为首次请求增加 2-5 秒
- 每个浏览器实例使用 ~50-100MB 内存
- 浏览器缓存随时间增长 (~50-200MB)

### 反检测功能
- 高级指纹随机化
- 自动挑战检测和处理
- 持久浏览器会话提高成功率
- 智能资源阻断增强性能

---

**为 Jellyfin 社区用 ❤️ 制作**  
**由 Microsoft Playwright 驱动，提供高级反检测** 🎭