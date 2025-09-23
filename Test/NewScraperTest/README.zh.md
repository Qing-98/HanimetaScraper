# NewScraperTest

[English](README.md) | [中文](README.zh.md)

HanimetaScraper 后端服务和内容提供商的综合测试套件。此交互式测试应用程序通过真实场景验证 DLsite 和 Hanime 爬取提供商的功能。

## 🚀 功能特性

### 交互式测试菜单
- **完整测试套件**：两个提供商的完整验证
- **特定提供商测试**：DLsite 和 Hanime 的单独测试
- **后端 API 集成**：直接 API 端点测试
- **并发负载测试**：性能和稳定性验证
- **自定义测试场景**：用户定义输入的灵活测试

### 全面验证
- **搜索功能**：验证搜索准确性和响应格式
- **详情检索**：测试元数据提取的完整性
- **错误处理**：验证优雅的失败场景
- **性能指标**：测量响应时间和吞吐量
- **数据质量**：验证元数据格式和内容质量

### 真实世界测试
- **实时内容**：针对提供商的实际内容进行测试
- **边缘情况**：处理特殊字符、长标题和边缘场景
- **网络弹性**：在各种网络条件下测试行为
- **并发场景**：验证并发负载下的行为

## 📋 先决条件

- .NET 8 SDK
- ScraperBackendService 运行中（用于后端 API 测试）
- 互联网连接用于实时内容测试

## 🚀 快速开始

### 运行测试套件

1. **导航到测试目录**
```bash
cd Test/NewScraperTest
```

2. **运行交互式测试**
```bash
dotnet run
```

3. **从菜单中选择测试选项**
```
1. 完整测试（两个提供商）
2. 仅 DLsite 测试
3. 仅 Hanime 测试
4. 后端 API 集成测试
5. 并发负载测试
```

### 后端服务测试

对于后端 API 集成测试，确保 ScraperBackendService 正在运行：

```bash
# 在另一个终端中
cd ScraperBackendService
dotnet run
```

然后从测试菜单运行选项 4 来验证 API 端点。

## 🔧 测试类别

### 1. 完整测试套件（选项 1）
使用预定义测试用例对两个提供商进行综合测试：

**DLsite 测试用例：**
- 搜索："恋爱"（日文恋爱内容）
- 详情："RJ01402281"（特定产品 ID）
- 验证：元数据完整性、图像 URL、人员映射

**Hanime 测试用例：**
- 搜索："love"（英文关键词）
- 详情："86994"（特定内容 ID）
- 验证：标题提取、评分准确性、类型映射

### 2. 仅 DLsite 测试（选项 2）
专注于 DLsite 提供商功能的测试：

```
测试场景：
✓ 按关键词搜索日文内容
✓ 按产品 ID 检索详细元数据
✓ 验证日文字符处理
✓ 测试 RJ/VJ ID 格式解析
✓ 验证人员角色映射
✓ 检查图像 URL 有效性
```

### 3. 仅 Hanime 测试（选项 3）
专门测试 Hanime 提供商功能：

```
测试场景：
✓ 按英文关键词搜索内容
✓ 按内容 ID 检索详细元数据
✓ 验证数字 ID 解析
✓ 测试 URL 格式提取
✓ 验证评分和年份提取
✓ 检查图像去重
```

### 4. 后端 API 集成测试（选项 4）
直接测试后端服务 API 端点：

```
测试的 API 端点：
✓ GET / (服务信息)
✓ GET /health (健康检查)
✓ GET /api/dlsite/search (DLsite 搜索)
✓ GET /api/dlsite/{id} (DLsite 详情)
✓ GET /api/hanime/search (Hanime 搜索)
✓ GET /api/hanime/{id} (Hanime 详情)
```

### 5. 并发负载测试（选项 5）
并发负载下的性能和稳定性测试：

```
负载测试场景：
✓ 多个同时搜索
✓ 并发详情检索
✓ 混合提供商请求
✓ 错误率测量
✓ 响应时间分析
```

## 📊 测试输出格式

### 搜索结果显示
```
=== 搜索结果 ===
查询："love"
找到结果：12

[1] 标题：Love Story
    ID：12345
    URL：https://example.com/content/12345

[2] 标题：Love Romance
    ID：67890
    URL：https://example.com/content/67890
```

### 详情结果显示
```
=== 详情结果 ===
ID：86994
标题：内容标题
描述：内容描述...
评分：4.5/5.0
发布年份：2024
类型：恋爱，剧情
工作室：工作室名称
人员：
  - 配音演员：人员姓名
  - 导演：导演姓名
图像：
  - 主要：https://example.com/cover.jpg
  - 缩略图：找到 5 张图像
```

### 性能指标
```
=== 性能指标 ===
总请求数：50
成功：48 (96%)
失败：2 (4%)
平均响应时间：1.2 秒
最快响应：0.8 秒
最慢响应：3.1 秒
```

## 🔍 测试用例

### 预定义测试场景

**DLsite 测试数据：**
```csharp
// 搜索测试用例
{ Query: "恋爱", ExpectedMin: 5, Type: "Romance" },
{ Query: "ボイス", ExpectedMin: 10, Type: "Voice" },
{ Query: "同人", ExpectedMin: 20, Type: "Doujin" }

// 详情测试用例
{ ID: "RJ01402281", ExpectedTitle: true, ExpectedImages: true },
{ ID: "VJ123456", ExpectedPersonnel: true, ExpectedGenres: true }
```

**Hanime 测试数据：**
```csharp
// 搜索测试用例
{ Query: "love", ExpectedMin: 8, Type: "Romance" },
{ Query: "school", ExpectedMin: 15, Type: "School" },
{ Query: "fantasy", ExpectedMin: 10, Type: "Fantasy" }

// 详情测试用例
{ ID: "86994", ExpectedRating: true, ExpectedYear: true },
{ ID: "12345", ExpectedGenres: true, ExpectedStudios: true }
```

## 🛠️ 自定义

### 添加自定义测试用例

在 `QuickTest.cs` 中编辑测试配置：

```csharp
// 添加新的搜索测试
var customSearchTests = new[]
{
    new { Query = "your-search-term", Provider = "dlsite", MinResults = 5 },
    new { Query = "your-hanime-search", Provider = "hanime", MinResults = 3 }
};

// 添加新的详情测试
var customDetailTests = new[]
{
    new { ID = "RJ123456", Provider = "dlsite", ValidateImages = true },
    new { ID = "54321", Provider = "hanime", ValidateRating = true }
};
```

### 自定义验证逻辑

实现自定义验证函数：

```csharp
public static bool ValidateCustomMetadata(HanimeMetadata metadata)
{
    // 自定义验证逻辑
    return metadata.Title?.Length > 5 &&
           metadata.Rating > 0 &&
           metadata.Genres?.Any() == true;
}
```

## 🐛 故障排除

### 常见测试失败

**网络连接问题**
```
错误：无法连接到提供商
解决方案：检查互联网连接和提供商站点可访问性
```

**后端服务未运行**
```
错误：连接被拒绝到 localhost:8585
解决方案：在运行 API 测试之前启动 ScraperBackendService
```

**测试数据过时**
```
错误：未找到预期内容
解决方案：使用当前有效内容更新测试 ID
```


### 调试模式

启用详细输出进行详细调试：

```csharp
// 在 Program.cs 中，设置调试标志
const bool DEBUG_MODE = true;

if (DEBUG_MODE)
{
    Console.WriteLine($"请求 URL: {url}");
    Console.WriteLine($"响应头: {headers}");
    Console.WriteLine($"响应体: {body}");
}
```

## 📝 测试报告

### 生成测试报告

测试套件可以生成详细报告：

```bash
# 运行并生成报告
dotnet run -- --generate-report

# 指定输出格式
dotnet run -- --report-format json
dotnet run -- --report-format xml
```

### 报告内容

测试报告包括：
- 测试执行摘要
- 单独测试结果
- 性能指标
- 错误详情和堆栈跟踪
- 环境信息
- 时间戳和持续时间

## 🔧 开发

### 添加新测试类型

1. **在 QuickTest.cs 中创建测试方法：**
```csharp
public static async Task RunCustomTest()
{
    Console.WriteLine("运行自定义测试...");
    // 测试实现
}
```

2. **在 Program.cs 中添加菜单选项：**
```csharp
Console.WriteLine("6. 自定义测试场景");
// 处理菜单选择
```

3. **实现验证逻辑：**
```csharp
private static bool ValidateCustomResult(object result)
{
    // 自定义验证
    return result != null;
}
```

### 测试数据管理

测试数据通过配置文件和常量管理：

```csharp
// 测试配置
public static class TestConfig
{
    public const string DEFAULT_BACKEND_URL = "http://localhost:8585";
    public const int DEFAULT_TIMEOUT = 30000;
    public const int MAX_CONCURRENT_REQUESTS = 10;
}
```

## 📊 性能基准测试

### 基准测试类别

1. **响应时间基准测试**
   - 搜索操作延迟
   - 详情检索速度
   - API 端点响应时间

2. **吞吐量基准测试**
   - 每秒请求数
   - 并发请求处理
   - 提供商比较指标

3. **可靠性基准测试**
   - 随时间的成功率
   - 错误恢复测试
   - 网络弹性验证

## 🤝 贡献

### 添加测试用例

1. 确定新的测试场景
2. 实现测试方法
3. 添加验证逻辑
4. 更新文档
5. 提交拉取请求

### 测试指南

- 使用真实的测试数据
- 包括正面和负面测试用例
- 验证所有元数据字段
- 测试错误处理场景
- 测量性能影响

## 📄 许可证

本项目根据 MIT 许可证授权 - 详情请参阅 LICENSE 文件。

## 🔗 相关项目

- **[ScraperBackendService](../../ScraperBackendService/)**：正在测试的后端服务
- **[Jellyfin 插件](../../Jellyfin.Plugin.*)**：插件集成
- **[主项目](../../)**：完整的 HanimetaScraper 解决方案

---

**注意**：此测试套件设计用于开发和验证目的。运行测试时请确保遵守目标网站的服务条款。