# Scripts/Localization — YAML 多语言系统

## Scope

5 个文件的紧凑工具模块，但 **32 个外部文件引用**——UI 层最大消费者（20 incoming）。YAML 翻译 + C# 类型安全的本地化字符串包装。不做 UI，不做资源扫描。

## Map

| 文件 | 职责 | 注意 |
|------|------|------|
| `Localization.cs` | 入口：`T(key, fallback)`、`SetLanguage()`、`LanguageChanged` 事件 | DirAccess 扫描优先，硬编码回退必须同步 |
| `YamlParser.cs` | 自定义 YAML 解析器 | **tab 缩进致命 bug**——见下 |
| `LocalStr.cs` | 类型安全翻译字符串（单参数 `{0}`） | 不可变 struct |
| `ConcatLocalStr.cs` | 多段翻译拼接 | |
| `ILocalizable.cs` | 标记接口——可本地化的对象 | GameManager.LanguageChanged 后自动刷新 |

## YAML 缩进陷阱（CRITICAL）

`YamlParser.GetIndentLevel` 把 **tab 算作 2 空格**，与父级 2 空格缩进歧义导致子节点错位到 root——整段翻译失效。

**新增翻译时**：
- YAML 用 **2 空格递增**（root 0 → level1 2 → level2 4）
- **禁止 tab**
- `Resources/Localization/zh.yaml` 和 `en.yaml` 同时更新
- 键命名惯例：`cards.{id}.name/description`、`ui.*`、`hero_power.*`、`keyword.*`

## API

```csharp
Localization.T("key", "默认值")          // 返回翻译字符串
Localization.T("key.{param}", "默认{param}").Replace("{param}", val)  // 占位符
```

## 新增语言 Checklist

1. 在 `Resources/Localization/` 建对应 `.yaml`（如 `jp.yaml`）
2. 在 `Localization.TryLoadTranslationsViaDirAccess()` 同步硬编码回退
3. 在 `GameManager.SetLanguage()` 中注册语言选项
4. 所有 UI 组件订阅 `GameManager.LanguageChanged`；`_ExitTree` 取消订阅
5. 导出包不走 DirAccess 时硬编码回退必须覆盖所有语言

## Anti-Patterns

- 禁止 UI 硬编码中文字符串——必须 `Localization.T()` + YAML key。
- 禁止 YAML 文件中用 tab 缩进。
- 禁止新增卡牌后只依赖 DirAccess 扫描——必须同步 `CardResourcePaths[]` 硬编码回退。
- 禁止 `LanguageChanged` 回调中没有 `IsInsideTree()` 检查。
