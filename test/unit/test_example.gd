extends GutTest

## 示例测试 — 验证 GUT 框架正常工作
## 所有测试方法必须以 `test_` 开头

func test_assert_true() -> void:
	assert_true(true, "true 应该为 true")

func test_assert_eq() -> void:
	assert_eq(1 + 1, 2, "1+1 应该等于 2")

func test_assert_ne() -> void:
	assert_ne(42, 0, "42 不应该等于 0")

## 注意：GUT 是 GDScript 原生测试框架。
## 本项目是 C# 项目，GUT 主要用于测试 GDScript 辅助脚本
## （如 mcp_interaction_server.gd、godot_operations.gd 等）。
##
## C# 代码的单元测试建议使用：
## - gdUnit4Net (NuGet: gdUnit4.api) — 最成熟的 C# Godot 测试框架
## - xUnit / NUnit 配合 Godot 无头模式
##
## 通过 game_eval (godot-mcp) 也可以在 GDScript 测试中调用 C# 方法：
##   var result = get_tree().root.get_node("/root/GameManager").SomeMethod()
