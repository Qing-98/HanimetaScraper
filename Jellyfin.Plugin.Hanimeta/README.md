# Jellyfin.Plugin.Hanimeta

[中文](#中文) | [English](#english)

---

## 中文

`Jellyfin.Plugin.Hanimeta` 是 Jellyfin 插件层，用于向后端 `ScraperBackendService` 请求 Hanime / DLsite 元数据。

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
