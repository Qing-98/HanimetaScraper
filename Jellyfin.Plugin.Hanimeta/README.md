# Jellyfin.Plugin.Hanimeta

[中文](#中文) | [English](#english)

---

## 中文

`Jellyfin.Plugin.Hanimeta` 是 Jellyfin 插件层，用于向后端 `ScraperBackendService` 请求 Hanime / DLsite 元数据。

### 兼容版本与安装

- 当前插件针对 Jellyfin **10.11.x / .NET 9** 编译；Jellyfin 10.8.x 和 10.10.x 无法加载此 DLL，需要先升级服务器。
- 停止 Jellyfin，移除插件目录中的旧版 Hanimeta DLL，再安装新版本并重启。若插件仍显示禁用，请在后台重新启用并检查服务器启动日志。
- `Skipping disabled plugin` 只表示插件当前被禁用，实际加载失败原因需查看它之前的日志。

### 按编号刮削

Hanime 支持 `260626`、`[260626]`、`[260626] 标题.mp4`、`【260626】标题.mkv` 和 `https://hanime1.me/watch?v=260626`。DLsite 支持 `RJ` / `VJ` 编号及包含编号的文件名。自动刮削和手动识别都会优先使用编号；也可直接填写 Jellyfin 的 Hanime / DLsite 外部 ID 字段。

若后端报告验证或 blocked 页面、网络错误，先从后端主机检查站点访问和验证状态后重试。这类失败不会再作为“内容不存在”写入缓存；升级 Jellyfin 本身无法解决上游站点访问问题。

### 插件配置项

在 Jellyfin 插件页面可设置：

- `Backend URL`：后端地址（默认 `http://127.0.0.1:8585`）
- `API Token`：后端启用认证时填写
- `Enable Logging`：插件调试日志开关
- `Tag Mapping Mode`：标签写入模式（`Tags` / `Genres`）

### 配置要求

- 后端必须可从 Jellyfin 所在主机访问
- 若后端 `AuthToken` 非空，插件 `API Token` 必须一致
- 后端地址建议去掉尾部 `/`

---

## English

`Jellyfin.Plugin.Hanimeta` is the Jellyfin plugin layer that requests Hanime / DLsite metadata from `ScraperBackendService`.

### Compatibility and Installation

- The current plugin targets Jellyfin **10.11.x / .NET 9**. Jellyfin 10.8.x and 10.10.x cannot load this DLL; upgrade the server first.
- Stop Jellyfin, remove the old Hanimeta DLL from the plugin directory, install the new version, and restart. If it remains disabled, enable it in the dashboard and inspect the server startup logs.
- `Skipping disabled plugin` describes the current plugin state; check earlier log entries for the actual loading failure.

### ID Lookup

Hanime accepts `260626`, `[260626]`, `[260626] Title.mp4`, `【260626】Title.mkv`, and `https://hanime1.me/watch?v=260626`. DLsite accepts `RJ` / `VJ` IDs and filenames containing them. Automatic metadata retrieval and manual identification prefer IDs over title searches. You can also set the Hanime / DLsite external ID in Jellyfin.

If the backend reports a verification or blocked page, or a network error, check site access and verification from the backend host before retrying. These failures are no longer cached as missing content. Upgrading Jellyfin does not resolve upstream site access failures.

### Plugin Settings

Configurable fields in Jellyfin plugin page:

- `Backend URL`: backend service URL (default `http://127.0.0.1:8585`)
- `API Token`: required when backend authentication is enabled
- `Enable Logging`: plugin troubleshooting log switch
- `Tag Mapping Mode`: metadata mapping mode (`Tags` / `Genres`)

### Configuration Requirements

- Backend must be reachable from the Jellyfin host
- If backend `AuthToken` is set, plugin `API Token` must match
- Backend URL should not end with `/`
