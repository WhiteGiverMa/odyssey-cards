# Godot MCP — OdysseyCards

本目录包含 Godot MCP 集成所需的项目本地文件。

## 文件说明

| 文件 | 来源 | 说明 |
|------|------|------|
| `plugin.cfg` | 项目本地维护 | Godot 插件注册 |
| `mcp_editor_plugin.gd` | 项目本地维护 | 空壳编辑器插件 |
| `godot_operations.gd` | fork vendor | 无头模式操作脚本 |
| `mcp_interaction_server.gd` | fork vendor | TCP 运行时交互服务器 (autoload) |

## 同步 .gd 文件

运行 fork 仓库中的 `scripts/sync-downstream.ps1`：

```powershell
cd G:\dev\godot-mcp
.\scripts\sync-downstream.ps1
```

## Autoload

`mcp_interaction_server.gd` 已在 `project.godot` 中注册为 autoload `McpInteractionServer`。

## MCP 配置

可选的运行时参数见 `res://config/mcp_server.json`。
