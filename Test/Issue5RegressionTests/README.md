# Issue #5 regression tests

使用 .NET 9 SDK 运行 / Run with the .NET 9 SDK:

```bash
dotnet test Test/Issue5RegressionTests/Issue5RegressionTests.csproj -c Release
```

测试使用本地模拟后端和 HTML fixture，不需要启动 Jellyfin、安装浏览器或访问 Hanime。覆盖自动刮削与手动识别的编号优先策略、标题搜索回退，以及验证页面、网络错误和取消请求的错误传播。

These tests use a local fake backend and HTML fixtures. They require no Jellyfin server, browser installation, or Hanime access. Coverage includes ID precedence in automatic metadata retrieval and manual identification, title search fallback, and propagation of verification, network, and cancellation failures.
