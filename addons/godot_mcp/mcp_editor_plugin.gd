@tool
extends EditorPlugin

## 空壳编辑器插件 — 仅用于在 Godot 插件列表中注册 Godot MCP。
## 实际运行时逻辑由 autoload McpInteractionServer 处理。

func _enter_tree() -> void:
	pass

func _exit_tree() -> void:
	pass
