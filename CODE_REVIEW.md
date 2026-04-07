# 前后端代码评审建议（2026-04-07）

## 总体结论
代码结构整体清晰，抽象层次（Provider/Mapper/Client）做得不错，配置页也考虑了容错与调试。
当前主要优化点集中在：

1. HTTP 资源复用与连接池管理（避免频繁创建/销毁 `HttpClient`）。
2. 重试策略与异常分类（避免无限重试与对取消请求误计失败）。
3. 前端配置页脚本复杂度（可读性与可维护性）。
4. 配置与注册逻辑去重（降低未来扩展成本）。

## 高优先级建议

### 1) 复用 HttpClient，避免每次请求创建实例
- 现状：`BaseScraperApiClient` 内部持有 `HttpClient`，但上层服务每次调用都 `using var apiClient = apiClientFactory()`，导致底层 `HttpClient` 也被反复创建与释放。
- 风险：高并发时可能造成 socket 抖动、DNS 缓存行为不可控，且统计信息被拆碎。
- 建议：
  - 改为通过 DI + `IHttpClientFactory` 或长生命周期 Typed Client。
  - Provider 侧注入单例/长生命周期 API Client，移除频繁 `Dispose`。

### 2) 为重试增加上限与抖动（jitter）
- 现状：`SearchAsync` 与 `GetMetadataInternalAsync` 在 `429` 情况会持续指数退避，但没有最大重试次数。
- 风险：后端长时间限流时，任务可能过久占用线程与资源。
- 建议：
  - 增加 `maxAttempts`（例如 5~8 次）。
  - 退避中加入 `jitter`（随机 0~20%）减少惊群效应。
  - 将策略抽成可测试的方法（便于单测）。

### 3) 指标统计口径修正
- 现状：400/404 被计入 `RecordSuccess`，`OperationCanceledException` 被计入 `RecordFailure`。
- 风险：成功率指标与真实业务成功率偏差较大。
- 建议：
  - 拆分为 `TransportSuccess` 与 `BusinessSuccess`。
  - 取消（cancellation）单独统计，不计入失败率。

## 中优先级建议

### 4) 前端配置页减少“防御式冗余逻辑”
- 现状：`TagMappingMode` 设置流程含多层 `setTimeout`、多方式回填、大量 debug 输出。
- 风险：后续维护难；出现新字段时容易复制复杂模式。
- 建议：
  - 抽象 `loadConfig -> normalize -> bindForm -> saveConfig` 四段函数。
  - 使用统一 `normalizeTagMappingMode(value)` 即可，无需多次延时校验。
  - `debugLog` 受 `EnableLogging` 或 query flag 控制，减少生产噪音。

### 5) 注册器去 switch 化，真正插件化
- 现状：`PluginServiceRegistrator.RegisterApiClientFactory` 仍按 `providerName` 写死 `switch`。
- 风险：新增 provider 仍需改核心注册器，违背“注册表驱动”的目标。
- 建议：
  - 在 `IProviderPluginConfig` 中增加 `CreateApiClientFactory(IServiceProvider)`。
  - 注册器只循环调用配置对象，彻底消除硬编码分支。

### 6) 配置访问逻辑合并
- 现状：`Plugin` 与 `BaseConfigurationManager` 都有 BackendUrl/ApiToken/Validation 逻辑。
- 风险：行为可能渐渐漂移（一个改了另一个没改）。
- 建议：
  - 保留单一配置门面（例如只走 `Plugin` 或只走 `ConfigurationManager`）。
  - 其他调用方全部依赖该门面。

## 低优先级建议

### 7) 日志安全与级别分层
- 前端已做 token 脱敏是加分项；后端建议统一封装“敏感字段清洗”并按 `Debug/Info/Error` 分层，避免长文本与 PII 泄露。

### 8) 可测试性补强
- 推荐新增测试：
  - `TagMappingMode` 序列化/反序列化兼容（数字与字符串）。
  - 429 退避策略（含最大重试与取消行为）。
  - Mapper 映射规则（Tags/Genres + Series 组合）。

## 建议落地顺序（两周内）
1. **Week 1**：先做 `HttpClient` 生命周期改造 + 重试上限（收益最大）。
2. **Week 2**：前端配置页脚本重构 + 注册器去 switch。
3. 同步补单测，最后做配置访问去重与日志收口。
