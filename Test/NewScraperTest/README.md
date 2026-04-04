# NewScraperTest

[中文](#中文) | [English](#english)

---

## 中文

`NewScraperTest` 是用于验证 `ScraperBackendService` API 的控制台测试工具。

### 运行前提

- .NET 9 SDK
- 已启动后端服务（默认 `http://localhost:8585`）

### 启动

```bash
cd Test/NewScraperTest
dotnet run
```

程序启动后会询问：

- Backend URL（默认 `http://localhost:8585`）
- API token（可空）

### 交互菜单

- `1` Full coverage suite
- `2` Core service suite
- `3` DLsite suite
- `4` Hanime suite
- `5` Cache suite
- `6` Redirect suite
- `7` Concurrency suite
- `8` Mechanism suite (slot/cache)
- `0` Exit

### 命令行模式

可直接运行指定套件：

```bash
dotnet run full
dotnet run core
dotnet run dlsite
dotnet run hanime
dotnet run cache
dotnet run redirect
dotnet run mechanism
dotnet run concurrent
```

---

## English

`NewScraperTest` is a console test runner for validating `ScraperBackendService` APIs.

### Prerequisites

- .NET 9 SDK
- Backend service running (default `http://localhost:8585`)

### Start

```bash
cd Test/NewScraperTest
dotnet run
```

At startup, it asks for:

- Backend URL (default `http://localhost:8585`)
- API token (optional)

### Interactive Menu

- `1` Full coverage suite
- `2` Core service suite
- `3` DLsite suite
- `4` Hanime suite
- `5` Cache suite
- `6` Redirect suite
- `7` Concurrency suite
- `8` Mechanism suite (slot/cache)
- `0` Exit

### CLI Mode

Run a specific suite directly:

```bash
dotnet run full
dotnet run core
dotnet run dlsite
dotnet run hanime
dotnet run cache
dotnet run redirect
dotnet run mechanism
dotnet run concurrent