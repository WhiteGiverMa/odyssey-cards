# Scripts/UI — 程序化 UI、预览与交互

## Scope

Godot Control 层。UI 负责显示、输入、动画与异步选择；规则落 Combat/Core/Card，不在 UI 里结算游戏逻辑。

## Map

| 区域 | 文件 | 注意 |
|------|------|------|
| 战斗 UI 编排 | `CombatUI.cs` | 生命周期、事件连线、字段 |
| 战斗布局 | `CombatUI.Layout.cs` | BuildLayout + Create*，程序化控件 |
| 战斗刷新 | `CombatUI.Refresh.cs` | Pull 刷新，不轮询规则 |
| 战斗选择 | `CombatUI.Selection.cs` | 手牌/目标/攻击/发现状态机 |
| 手牌 | `HandUI.cs`, `CardUI.cs` | 手动布局；CardUI 支持 `[Tool]` 预览 |
| 棋盘 | `BoardUI.cs` | `[Tool]` 预览，槽位原地刷新 |
| 敌方身份卡 | `EnemyIdentityCard.cs` | 意图、英雄攻击绿框、旧意图桥接 |
| 异步选牌 | `DiscoverUI.cs`, `RewardUI.cs`, `CardSelectionScreen.cs` | `TaskCompletionSource` 一次性屏幕 |
| Roguelike UI | `EventUI.cs`, `ShopUI.cs`, `RestSiteUI.cs` | 存在但流程未完全接线 |
| 弹窗/导航 | `MobileDialogHost.cs`, `SubmenuStack.cs`, `PauseMenu.cs` | 模态栈 + 设置页 |
| 视觉反馈 | `ArrowRenderer.cs`, `EffectBar.cs`, `CardAnimation.cs`, `CardFlyVfx.cs` | 动画/箭头/浮字 |
| 编辑器预览 | `Scenes/CardPreview.tscn`, `BoardPreview.tscn`, `CombatPreview.tscn` | `#if TOOLS` 零发布开销 |

## Conventions

- UI 文本一律 `Localization.T()` + `zh.yaml`/`en.yaml`。
- 根 UI 订阅 `GameManager.LanguageChanged`；`_ExitTree` 必须取消订阅。
- HotkeyManager 绑定：`PushPressedBinding` 与 `RemovePressedBinding` 配对。
- CombatUI partial 只按职责拆：核心/Layout/Refresh/Selection，不新增“杂项 partial”。
- 手动布局控件用 `Position`，不要让 Container 覆盖位置。
- 异步 UI：创建 → 入场保护 → await → `SetResult` → `QueueFree`。
- 新模态优先考虑 `MobileDialogHost` + `MobileInputRouter`，避免每屏自建输入拦截。

## Godot Traps

- `MouseFilter=Ignore` 不会收到 `_Input()`；拖拽必须 `_Process` 轮询全局 Input。
- 覆盖层要么 `Ignore` 穿透，要么 `Stop` 接管，不要半吊子。
- 同一栈帧不要 `QueueFree` 旧节点后立刻 `AddChild` 新节点；deferred 批处理。
- `[Tool]` 会实例化嵌套 Control；嵌套类要无参构造，值类型字段用 `default`。
- 嵌套 partial class 的 `signal +=` 不可靠；用 `Connect` 或 C# event。

## Anti-Patterns

- 禁止 UI 里直接改战斗规则状态，走 CombatManager/拆分系统。
- 禁止硬编码中文 UI 文本。
- 禁止非 Container 父控件用 `Offset*`。
- 禁止 DragLayer 同时持有多张卡。
- `CardAnimation.cs` 当前有 `async void` 债务；新代码不要复制。
