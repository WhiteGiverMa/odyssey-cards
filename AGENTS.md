# OdysseyCards — Godot 4.6 C# · 类炉石 Roguelite 卡牌

**Branch:** `main` · **Commit:** `394871f` · **Updated:** 2026-06-03
**Scale:** 78 `.cs` (Scripts/) · ~20,100 行 · 32 `.tres` 卡牌 · 4 `.tscn` 场景

## Start

- 本文件是公共版。本地规则见 `AGENTS_LOCAL.md`（gitignored）。
- 工作语言中文。代码注释中文。子代理调度中文。
- 新增功能后 → 更新本文件。

## Map

```
Scripts/
├── Core/ (25)      # CardData, GameManager(Autoload), DamageResolver, Keyword, SaveDataManager…
├── UI/ (18)        # CombatUI, BoardUI, HandUI, CardUI, CollectionUI, MapUI, DiscoverUI, RewardUI…
├── Card/ (10)      # Card, Minion, Spell, Hero, Weapon, ActiveDomain, StatusEffect (纯 C#，不继承 Node)
├── Character/ (5)  # Player, CommanderCore, Deck, CombatDeckState
├── Combat/ (4)     # CombatManager(1740+行), Board, EnemyUnit, GameState (纯 C#)
├── AI/ (7)         # IntentAI, EnemyRegistry, MechanicalRoachBrain, ZhangLang, ShanHu…
├── Roguelike/ (3)  # EventSelector, RoomData, GameRunState
├── Localization/ (5)# YAML 多语言 (LocalStr, ConcatLocalStr, ILocalizable, YamlParser)
└── Infrastructure/ (1) # DevConsole (Autoload)
Resources/Cards/    # 32 卡牌 .tres (法术15 + 随从11 + 领域6)
Resources/Localization/ # zh.yaml / en.yaml
Scenes/             # Main.tscn, Combat.tscn, Collection.tscn, Map.tscn
```

## Commands

```bash
dotnet build                  # 调试构建
dotnet build -c Release       # 发布构建
dotnet format OdysseyCards.sln --verify-no-changes  # CI 格式检查
dotnet format OdysseyCards.sln # 自动格式化
dotnet test                   # xunit (4 测试文件, tests/csharp/)
```

## Autoload

| 名称 | 路径 | 用途 |
|------|------|------|
| `GameManager` | `Scripts/Core/GameManager.cs` | 全局状态、卡牌注册表、语言切换、跨战斗持久化 |
| `UIScaler` | `Scripts/UI/UIScaler.cs` | UI 缩放，基准 1152×648 |
| `DevConsole` | `Scripts/Infrastructure/DevConsole.cs` | 开发者控制台，`` ` `` 呼出 |

## WHERE TO LOOK

| 做什么 | 位置 | 注意 |
|--------|------|------|
| 卡牌数据 | `Core/CardData.cs` | Godot Resource, `[Export]` |
| 卡牌运行时 | `Card/Card.cs` → `Minion.cs` | **纯 C#，不继承 Node** |
| 战斗主循环 | `Combat/CombatManager.cs` | 1740+行，PlayMinion/PlaySpell/MinionAttack/EndPlayerTurn/ExecuteEnemyTurn |
| 棋盘 | `Combat/Board.cs` | 2×5 槽位，嘲讽检测，C# event |
| 回合/法力 | `Combat/GameState.cs` | 纯 C#，法力上限 10，CombatPhase 枚举 |
| 敌方单位 | `Combat/EnemyUnit.cs` | 纯 C#，Hero 身体 + EnemyEncounter 大脑 |
| 英雄/护甲 | `Card/Hero.cs` | 纯 C#，包装 CommanderCore，Heal/GainArmor/WeaponSlot |
| 武器 | `Card/Weapon.cs` / `WeaponSkill.cs` | 纯 C#，被动/主动技能 |
| 伤害计算 | `Core/DamageResolver.cs` | 三阶段：ADDITIVE→MULTIPLICATIVE→CAPPING，Clamp≥0 |
| 敌人 AI | `AI/IntentAI.cs` / `AI/EnemyRegistry.cs` | 多种敌人 + 多种 Brain |
| UI 总控 | `UI/CombatUI.cs` | 程序化布局，5 种 SelectionMode + 攻击拖拽状态机 |
| 手牌 | `UI/HandUI.cs` | 风扇交叠，Control 手动布局（非 Container） |
| 卡牌交互 | `UI/CardUI.cs` | 点击/拖拽状态机，`_Process` 轮询 |
| 棋盘 UI | `UI/BoardUI.cs` | 2×5 BoardSlot，OnSlotRightClicked |
| 收藏 | `UI/CollectionUI.cs` / `CardGrid.cs` | 浏览/编辑/分页/过滤 |
| 地图 | `UI/MapUI.cs` | Roguelike 路线选择 |
| 发现/奖励 | `UI/DiscoverUI.cs` / `RewardUI.cs` | N选1/M，TaskCompletionSource 异步 |
| 效果图标 | `UI/EffectBar.cs` | Emoji+层数，CanvasLayer 独立渲染 |
| 箭头 | `UI/ArrowRenderer.cs` | `_Draw()` 攻击/意图箭头 |
| 暂停 | `UI/PauseMenu.cs` | ESC 触发全屏覆盖，内嵌设置 |
| 存档 | `Core/SaveDataManager.cs` | user://save.json 持久化 |
| 本地化 | `Localization/Localization.cs` | YAML 加载，DirAccess→硬编码回退 |
| 控制台 | `Infrastructure/DevConsole.cs` | `` ` `` 呼出，AI 可调 |

## Architecture Rules

### 纯 C# 核心
- `Card`, `Minion`, `Spell`, `Hero`, `Weapon`, `Board`, `GameState`, `EnemyUnit` — **禁止**调用 Godot API。
- 数据与渲染分离：`Card` = 纯数据，`CardUI` = Godot Control 包装。
- `CombatManager` 是唯一跨两层的中介。

### 伤害管线
- 三阶段可插拔：`ADDITIVE`(+防御力调整等) → `MULTIPLICATIVE`(×脆弱/抵抗) → `CAPPING`(伤害上限，如固璋) → `Clamp(0)`。
- 每个阶段挂载 0..N 个 `IDamageModifier`。
- 意图伤害预览走 `DamageResolver.ResolvePreviewDamage()`。
- 护甲吸收在 DamageResolver **之前**——有护甲时不走伤害管线。
- Cap 阶段：伤害上限（如 Intangible≤1）；Multiplicative 阶段：条件性倍率（如有易伤→×0.5）。

### 意图系统
- `EnemyIntent` 是 struct：`DamageCalc`(Func lambda 延迟计算) + `TargetSelector`(Func lambda 动态选目标) + 显示文本。
- 意图不存静态数值——每次查询重算，保证战场变化时意图显示实时更新。
- `CombatManager.OnCombatStateChanged` 事件：Board/Minion 状态变更 → UI 刷新意图。
- 意图执行动画期间 `CombatManager.IsEnemyTurnAnimating` 冻结 UI 不刷新。
- `DamageCalc` 为 lambda 延迟求值，`IsEnemyTurnAnimating` 冻结动画期间 UI 不刷新。

### 多敌人架构
- 每个敌人是独立 actor：自己的 HP、MoveState 链、Intent。
- `EnemyUnit` = `Hero`(身体) + `EnemyEncounter`(大脑)，不是共享血条包装器。
- 跨敌人协同用被动/状态效果监听事件，不让 Encounter 变上帝对象。
- 敌方随从意图≥`DefaultAttackMinionBrain`，不降级为"无意图自动攻击"。
- 协同通过被动/事件监听，不通过上帝对象指挥。

### 领域系统
- 领域：打出时挂 `ActiveDomain` 到 `Hero.ActiveDomains`，长期行为在战斗事件点触发。
- 领域不是"消耗性法术"——打出后的效果持续性触发，非一次性结算。
- Counter 叠加：多次打出同一领域 = 多层 counter，每触发一次消耗一层。

### UI 交互
- 双交互模式（手牌/攻击均适用）：点击选中→第二击目标 / 按住拖拽→松手打出。`DragThreshold=10f` 区分。
- 手牌：`HandUI` 手动 Control 布局，风扇交叠 `OVERLAP_FACTOR=0.85`，悬停上浮放大，相邻推开衰减。
- 攻击：拖拽不 reparent 随从到 DragLayer——留在原位，视觉仅靠 `ArrowRenderer`。
- 出牌区域判定：Y 轴阈值（屏幕高度 75%），自适应拖拽起始位置调整阈值。
- 目标选择：无目标卡牌 → 松手即出；有目标卡牌 → 瞄准线+准星→第二击确认。
- 取消：右键 / 拖回底部 / 松手在无效区域。

### 异步 UI（Discover/Reward）
- `TaskCompletionSource<T>` 驱动：创建 UI → 等待选择 → `SetResult` → `QueueFree`。
- 屏幕即一次性：选择完成即销毁。
- 入场 350ms 防误触保护。

### Godot UI 陷阱
- `MouseFilter` 默认 `Stop`——任何覆盖父控件的子控件必须显式设 `Ignore`，否则父控件事件被拦截。
- `HBoxContainer` 等容器布局引擎覆盖手动 `Position`——脱离容器的内容用 `CanvasLayer` 或 `GetTree().Root.AddChild()`。
- 嵌套 partial class 的 `signal +=` 在 Godot Mono 中不可靠——用 `Connect(SignalName, Callable.From(...))` 或 `_Notification` override。
- `MouseFilter=Ignore` 完全阻断 `_Input()`——拖拽追踪必须 `_Process` 轮询 `Input` 全局状态。
- 非 Container 父控件禁用 `Offset*` 属性——位置全由 `Position` 控制。

### 树操作安全
- 避免同一调用栈内 `QueueFree` 旧节点 + `AddChild` 新节点。
- 批量重建时应：缓存目标状态 → deferred 边界统一重建。
- 原则：主线程直接操作，否则 `CallDeferred`。

## Conventions

### 命名
- 接口：`I` 前缀 `ICommander`, `IDamageSource`
- private 字段：`_camelCase`
- public：`PascalCase`
- 局部/参数：`camelCase`
- 命名空间：`file_scoped` (`namespace X;`)，新文件必须。`using` 在 namespace 外。

### 事件/信号
- **不用** `[Signal]`。全部 C# `event Action<...>`。
- 触发：`?.Invoke()`，订阅：`+=`，取消：`-=`。

### UI 刷新
- Pull 模式：`CombatUI.RefreshAll()` → `BoardUI.RefreshBoard` / `HandUI.RefreshHand`。
- HandUI 刷新手牌 = 销毁重建（QueueFree 全部 CardUI 后重建）。
- BoardUI = 属性更新（`BoardSlot.UpdateDisplay` 原地刷新）。

### 资源加载
- `GD.Load<T>(path)` / `ResourceLoader.Exists(path)`。
- `[Export] PackedScene` → `Instantiate<T>()`。
- `GetNode<Type>(path)` / `GetNodeOrNull<Type>(path)`。

### 本地化
- 入口：`GameManager.SetLanguage()` → `Localization.SetLanguage()`。
- 事件：`GameManager.LanguageChanged`，所有场景订阅刷新。
- 查找：`Localization.T("key", "默认值")`。支持 `{key}` 占位符。
- 卡牌翻译：`Card.GetLocalizedName()` / `GetLocalizedDescription()` → YAML `cards.{id}.name` → `CardData.CardName`。
- **新增 UI 内容 checklist**：
  1. 所有文本用 `Localization.T("key", "默认中文")`
  2. `zh.yaml` + `en.yaml` 添加 key
  3. 根节点订阅 `GameManager.LanguageChanged`，回调中刷新
  4. `OnLanguageChanged` 加 `IsInsideTree()` 守卫
- **新增语言**：同步更新 `Localization.TryLoadTranslationsViaDirAccess()` 回退列表。

### 注释
- XML doc 中文。日志：`GD.Print("[ClassName] 消息")`。

## Export Build

- Godot 4 `.pck` 中 `DirAccess.Open("res://...")` 枚举目录失败。所有 DirAccess 使用点已有硬编码回退。
- `GameManager.cs`：`CardResourcePaths[32]` 硬编码卡牌路径。**新增卡牌必须同步更新此数组**。
- `Localization.cs`：`TryLoadTranslationsViaDirAccess()` + 已知文件回退。
- `export_presets.cfg`：`include_filter="*.yaml,*.yml"` 强制跟踪 YAML（原始文件无 `.import`）。
- 导出前删除 `user://save.json`（`%APPDATA%/OdysseyCards/save.json`）确保干净初始化。

## DevConsole

AI 调用：`game_call_method(nodePath="/root/DevConsole", method="DevCommand", args=["/damage 10"])`

| 命令 | 效果 |
|------|------|
| `/damage N` | 对敌方英雄造成 N 点伤害 |
| `/damage_enemy N` | 同上（显式） |
| `/damage_self N` | 对自己造成 N 点伤害 |
| `/damage_eslot X N` | 对敌方槽位 X 随从造成 N 点伤害 |
| `/damage_pslot X N` | 对己方槽位 X 随从造成 N 点伤害 |
| `/damage_all N` | 对所有敌方随从造成 N 点伤害 |
| `/damage -c N` | 点击模式：隐藏控制台→点击目标造成伤害→右键取消 |
| `/draw N` | 抽 N 张牌 |
| `/mana N` | 获得 N 点法力 |
| `/heal N` | 恢复 N 点生命值 |
| `/armor N` | 获得 N 点护甲 |
| `/end` | 强制结束回合 |
| `/refresh` | 刷新战斗 UI |
| `/help` | 显示帮助 |
| `/clear` | 清空输出 |

## MCP Testing

- godot-mcp 拖拽交互**无法验证**：`game_click` 合成事件 vs OS 真实鼠标不一致。
- 可验证：`/damage`、`/draw`、`/mana`、`/end`、`/armor` + `game_get_logs` / `game_get_ui`。
- 需人类肉眼：UI 位置/大小、拖拽交互、动画、字体。
- 路径硬编码：`G:\dev\godot-mcp-fc-a\build\scripts\`。

## Anti-Patterns

- **禁止** 对 `Card`/`Minion`/`Hero`/`Board`/`GameState`/`EnemyUnit` 调 Godot API。
- **禁止** 直接操作 `Board.PlayerSlots[index]` → 用 `PlaceMinion`/`RemoveMinion`。
- **禁止** `async void`。
- **禁止** 混淆 `Player._core` 和 `CombatManager._playerCore`。
- **禁止** `DamageResolver` 传 null source 不检查 → NRE。
- **禁止** Hero 有护甲时假设 DamageResolver 生效 → 护甲绕过。
- **禁止** UI 硬编码中文字符串 → 必须 `Localization.T()` + YAML key。
- **禁止** `MouseFilter=Ignore` 控件用 `_Input()` → 轮询 `_Process`。
- **禁止** 非 Container 父控件用 `Offset*` → 只 `Position`。
- **禁止** DragLayer 同时持有多张卡 → 先归还旧卡再取新卡。
- **禁止** 用 `GetGlobalMousePosition()` 替代 `InputEventMouseButton.GlobalPosition` 做点击初始坐标。
- **禁止** 新增卡牌不同步 `CardResourcePaths[]` → 导出无法加载。
- **禁止** 新增语言不同步 `TryLoadTranslationsViaDirAccess()` 回退。
- **禁止** 同一栈帧内 `QueueFree` + `AddChild` 混用 → 批处理 deferred 重建。

## Unique Styles

- 程序化 UI：CombatUI 子组件纯代码，Combat.tscn 仅布局容器。
- 双层 CommanderCore：Player 和 CombatManager 各持一份，`internal Deck setter` 共享牌堆。
- 手动法力同步：GameState 和 CommanderCore 各维护法力，CombatManager 手动 SetMana。
- 意图动态计算：lambda 延迟求值，不缓存静态数值。
- 风扇手牌：Control 手动 LayoutChildren，`OVERLAP_FACTOR=0.85`，`BASE_SCALE=0.85`。
- 双交互：点击选中+拖拽松手，`DragThreshold=10f` 区分。
- 攻击双交互：箭头追随+松手攻击/右键取消，`AttackDragThreshold=10f`。
- 导出回退：DirAccess 优先 + 硬编码回退。
- 关键词检测：比较运行时 vs `CardData` 基线区分"自带"vs"授予"。

## State

- ✅ 本地化全场景接线。10 处硬编码已修复 + 12 YAML key。
- ✅ 导出兼容：DirAccess 回退 + YAML include_filter。
- ✅ 卡牌选中/拖拽已修复：多选归位、点击跟随、拖拽追踪。
- ✅ 攻击双交互 + 右键取消。HandleAttackDrop NRE 已修复。
- ✅ 多敌人 AI：IntentAI + MechanicalRoachBrain + DefaultAttackMinionBrain + EnemyRegistry。
- ✅ 暂停 ESC 全屏覆盖。`IsInsideTree()` 守卫。
- ⚠️ `Spell.cs` 从未实例化（死代码）。
- ⚠️ `EventSelector` 未接线（RewardUI 自包含洗牌）。
- ⚠️ 英雄技能未实现（`IHeroPower` 空接口）。
- ⚠️ 手牌无上限 + 无疲劳。
- ⚠️ `CardEffectData.GetDescription()` 26 debug 字符串未本地化（低优）。
- ⚠️ DevConsole ~40 帮助文本未本地化（低优）。
- `.godot/` 删除后编辑器重生成 UID，异常先查 .tscn 引用。
- `.cs` ↔ `.uid` 配对，重命名/移动同步处理。
- 端口冲突→旧 Godot 进程残留→手动 Stop-Process。
