# Scripts/Infrastructure — 控制台、输入、生命周期与开发工具

## Scope

Godot Autoload 与全域服务层。ChatScreen 控制台、多层输入栈、移动端触控、场景树安全、EvalGateway、OdysseyInput 常量。Commands/ 是 ChatScreen 的命令组子系，不单独建 AGENTS.md。

## Map

| 区域 | 文件 | 注意 |
|------|------|------|
| 控制台 | `ChatScreen.cs`, `ChatScreenEngine.cs`, `ChatScreenCommand.cs`, `CommandResult.cs` | 薄壳 UI → 纯 C# 引擎 → 抽象命令组；`/help` 自动生成 |
| 命令组 | `Commands/`（8 个） | 继承 `ChatScreenCommand`，在 `RegisterAllCommands()` 注册；新增命令不需改 ChatScreen |
| 键盘输入 | `InputManager.cs`（物理键→逻辑动作）、`HotkeyManager.cs`（动作→回调栈） | 三层栈：物理键→OdysseyInput 常量→HotkeyManager 绑定；必配 Push/RemovePressedBinding；`AddBlockingScreen` 拦截 |
| 输入常量 | `OdysseyInput.cs` | 逻辑动作枚举唯一真源；禁止新功能直接写 `Key.` 枚举 |
| 移动端 | `MobileInputRouter.cs`（触控路由+模态栈）、`MobileInputHelper.cs`（旧辅助） | Router 只过滤 zone；Godot 原生 Control 信号仍可能触发——不能把 PushModalLayer 当全局屏蔽 |
| 树安全 | `SceneLifecycleGuard.cs` | `CallDeferredSafe` 防 QueueFree/场景切换竞态；批量重建用 deferred 边界 |
| C# 求值 | `EvalGateway.cs` | DEBUG only Autoload；godot-mcp 的 `game_eval_csharp`/`game_eval_csharp_snapshot` 后端 |
| 补全/队列 | `FixedSizedQueue.cs`, `CompletionCandidate.cs` | ChatScreen 历史自动补全 |

## Antoload 清单

| 名称 | 文件 | 用途 |
|------|------|------|
| `ChatScreen` | `ChatScreen.cs` | 控制台 |
| `MobileInputHelper` | `MobileInputHelper.cs` | 旧触控（非战斗 UI 用） |
| `MobileInputRouter` | `MobileInputRouter.cs` | 移动端触控路由 |
| `InputManager` | `InputManager.cs` | 键位映射 |
| `HotkeyManager` | `HotkeyManager.cs` | 回调分发 |
| `EvalGateway` | `EvalGateway.cs` | DEBUG only C# 求值 |

## Conventions

- 新键盘功能：`OdysseyInput` 常量 → `HotkeyManager.PushPressedBinding` → `_ExitTree` 中 `RemovePressedBinding`。
- ChatScreen 命令：写 `ChatScreenCommand` 子类 → `ChatScreenEngine.RegisterAllCommands()` 注册。元数据自动生成 `/help`。
- 移动端：同一控件同一动作一条主触控路径；手动 hit-test 检查 `IsVisibleInTree()`；Tab 切页同步维护 `Visible` + `MouseFilter`。
- 树操作：`CallDeferredSafe` 或 explicit deferred 批处理；同一栈帧不要 `QueueFree` + `AddChild`。

## Anti-Patterns

- 禁止新功能写 `if (Input.IsKeyPressed(Key.X))`——必须走 `OdysseyInput` + `HotkeyManager`.
- 禁止把 `MobileInputRouter.PushModalLayer` 当全局输入屏蔽——原生 Control 信号仍然透传。
- 禁止 `_Input()` 手动 hit-test 只看控件 `Visible`——必须 `IsVisibleInTree()`。
- 禁止 EvalGateway 绕过 `#if DEBUG` 在生产环境暴露。

## Commands/ 子系统

`Commands/` 下 9 个命令组 + `ChatScreenCommand` 抽象基类。每个命令组是一组逻辑相关的 ChatScreen 命令。

| 组 | 文件 | 典型命令 |
|----|------|----------|
| 伤害/战斗 | `DamageCommands.cs`, `CombatCommands.cs` | `/damage`, `/end`, `/fight` |
| 资源 | `ResourceCommands.cs` | `/draw`, `/mana`, `/heal`, `/armor` |
| 卡牌 | `CardCommands.cs` | `/play`, `/token`, `/summon_player` |
| 藏品 | `RelicCommands.cs` | `/addrelic` |
| 表情 | `EmoteCommands.cs` | `/emote` |
| QA | `QaCommands.cs` | `/qa_*` |
| 工具 | `UtilityCommands.cs` | `/version`, `/help` |
| 主题预览 | `ThemeCommands.cs` | `/theme_preview` |

新增命令组：建 `<Name>Commands.cs`（继承 `ChatScreenCommand`），在 `ChatScreenEngine.RegisterAllCommands()` 中注册。
