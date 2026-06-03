# OdysseyCards — Godot 4.6 C# · 类炉石传说Roguelite卡牌游戏

**Branch:** main · **Latest Commit:** 394871f
**Generated:** 2026-06-03 · **Updated:** 2026-06-03

## OVERVIEW

类炉石传说的回合制卡牌对战 Roguelite 游戏。2×5 随从棋盘，法力水晶系统（初始 1 费，每回合 +1，上限 10），7 种关键词。Godot 4.6 + C# (.NET 8.0, Godot.NET.Sdk/4.6.2)。78 个 .cs 文件，约 20,100 行。

**当前状态：能玩MVP** — 完整回合制战斗循环可运行：主菜单→起手→出牌/施法/随从攻击→结束回合→敌人AI行动→胜负弹窗→卡牌收藏→地图路线→存档系统。

**卡牌类型**：随从 (Minion)、法术 (Spell)、领域 (Domain)、武器 (Weapon) 及武器技能 (WeaponSkill)、活性领域 (ActiveDomain)、状态效果 (StatusEffect)。

## STRUCTURE

```
Scripts/
├── Core/ (25)           # CardData, DamageResolver, GameManager(Autoload), Keyword, SaveDataManager, GameSaveData…
├── UI/ (18)             # CombatUI, BoardUI, HandUI, CardUI, CollectionUI, MapUI, PauseMenu, DiscoverUI, RewardUI…
├── Card/ (10)           # Card(纯C#), Minion, Spell, Hero(纯C#), Weapon, ActiveDomain, StatusEffect, WeaponSkill…
├── Character/ (5)       # Player, CommanderCore, Deck, CombatDeckState, ICommander
├── Combat/ (4)          # CombatManager(1740+行), Board(纯C#), EnemyUnit(纯C#), GameState(纯C#)
├── AI/ (7)              # IntentAI, EnemyRegistry, MechanicalRoachBrain, ZhangLang, ShanHu, DefaultAttackMinionBrain
├── Roguelike/ (3)       # EventSelector, RoomData, GameRunState
├── Localization/ (5)    # YAML-based 多语言系统（LocalStr/ConcatLocalStr/ILocalizable/YamlParser）
└── Infrastructure/ (1)  # DevConsole (Autoload) — 开发者控制台
Resources/Cards/         # 32 张卡牌 .tres（法术15 + 随从11 + 领域6）
Resources/Localization/  # zh.yaml / en.yaml 翻译文件
Scenes/                  # Main.tscn, Combat.tscn, Collection.tscn, Map.tscn（4 个场景）
```

## WHERE TO LOOK

| Task | Location | Notes |
|------|----------|-------|
| 卡牌数据定义 | `Scripts/Core/CardData.cs` | Godot Resource, [Export] 字段 |
| 卡牌运行时 | `Scripts/Card/Card.cs` → Minion/Spell | **纯 C#，不继承 Node** |
| 战斗核心循环 | `Scripts/Combat/CombatManager.cs` | 1740+行，PlayMinion/PlaySpell/MinionAttack/EndPlayerTurn/ExecuteEnemyTurn |
| 棋盘管理 | `Scripts/Combat/Board.cs` | 2×5 槽位，嘲讽检测，C# event 而非 Godot signal |
| 回合/法力 | `Scripts/Combat/GameState.cs` | 纯 C#，法力上限10，CombatPhase枚举控制流转 |
| 敌人单位 | `Scripts/Combat/EnemyUnit.cs` | 纯 C#，敌方棋盘单位包装器 |
| 英雄/护甲 | `Scripts/Card/Hero.cs` | 纯 C#，包装 CommanderCore，Heal/GainArmor/WeaponSlot |
| 武器系统 | `Scripts/Card/Weapon.cs` / `WeaponSkill.cs` | 纯 C#，武器装备与技能 |
| 伤害计算 | `Scripts/Core/DamageResolver.cs` | 三阶段管线 ADDITIVE→MULTIPLICATIVE→CAPPING，Clamp 最小 0 |
| 敌人 AI | `Scripts/AI/IntentAI.cs` / `EnemyRegistry.cs` | 多种敌人类型 + 多种 AI Brain（尖塔式意图、近战蟑螂等） |
| 战后奖励 | `Scripts/Roguelike/EventSelector.cs` | Fisher-Yates 洗牌 + RewardUI |
| UI 编排 | `Scripts/UI/CombatUI.cs` | 程序化布局，5种选择模式 + 攻击拖拽状态机 |
| 手牌布局 | `Scripts/UI/HandUI.cs` | STS2 风格手动 Control 风扇交叠布局（非 HBoxContainer） |
| 卡牌交互 | `Scripts/UI/CardUI.cs` | 拖拽/点击选中状态机，_Process 轮询追踪（非 _Input） |
| 箭头渲染 | `Scripts/UI/ArrowRenderer.cs` | 攻击目标指示箭头 |
| 卡牌收藏 | `Scripts/UI/CollectionUI.cs` / `CardGrid.cs` | 卡牌浏览、卡组编辑、分页、稀有度过滤 |
| 地图路线 | `Scripts/UI/MapUI.cs` | Roguelike 路线选择 |
| UI 缩放 | `Scripts/UI/UIScaler.cs` | Autoload 单例，基准 1152×648 |
| 暂停菜单 | `Scripts/UI/PauseMenu.cs` | ESC/按钮触发，全屏覆盖，内嵌设置（语言切换） |
| 存档系统 | `Scripts/Core/SaveDataManager.cs` | 游戏进度持久化（user://save.json） |
| 全局状态 | `Scripts/Core/GameManager.cs` | Autoload，跨战斗持久化，卡牌注册表 + DirAccess 导出回退 |
| 本地化 | `Scripts/Localization/Localization.cs` | YAML 加载，DirAccess 优先 + 硬编码回退 |
| 开发者控制台 | `Scripts/Infrastructure/DevConsole.cs` | Autoload，`` ` `` 键呼出，AI可调用 |

## SCENES

| 场景 | 路径 | 说明 |
|------|------|------|
| 主菜单 | `Scenes/Main.tscn` | 入口场景，含 MainMenu 逻辑 |
| 战斗 | `Scenes/Combat.tscn` | 战斗场景，程序化 UI 布局（仅提供布局容器） |
| 收藏 | `Scenes/Collection.tscn` | 卡牌收藏与卡组编辑 |
| 地图 | `Scenes/Map.tscn` | Roguelike 路线选择 |

## DEV CONSOLE (开发者控制台)

`DevConsole` 是 Autoload 单例，在所有场景中可用。

### 人类使用
- 按反引号键 `` ` `` 呼出/隐藏控制台
- 在底部输入栏输入命令，回车执行
- 按 `Escape` 关闭控制台

### AI 调用（godot-mcp）
```
game_call_method(nodePath="/root/DevConsole", method="DevCommand", args=["/damage 10"])
```

### 可用命令

| 命令 | 参数 | 效果 | 用途 |
|------|------|------|------|
| `/damage N` | 伤害值 | 对敌方英雄造成 N 点伤害 | 快速验证胜负/游戏结束弹窗 |
| `/damage_enemy N` | 伤害值 | 对敌方英雄造成 N 点伤害（显式） | 同上 |
| `/damage_self N` | 伤害值 | 对己方英雄造成 N 点伤害 | 测试失败弹窗 |
| `/damage_eslot X N` | 槽位0-4, 伤害值 | 对敌方槽位 X 随从造成 N 点伤害 | 验证亡语/随从死亡 |
| `/damage_pslot X N` | 槽位0-4, 伤害值 | 对己方槽位 X 随从造成 N 点伤害 | 测试己方亡语 |
| `/damage_all N` | 伤害值 | 对所有敌方随从造成 N 点伤害 | AOE 测试 |
| `/damage -c N` | 伤害值 | 点击模式：控制台隐藏→点击目标造成 N 点伤害→右键取消 | 精细目标选择 |
| `/draw N` | 数量 | 抽 N 张牌 | 补手牌，搜索特定卡牌 |
| `/mana N` | 数量 | 获得 N 点法力 | 解除费用限制测试高费操作 |
| `/heal N` | 数量 | 恢复 N 点生命值 | 恢复玩家血量 |
| `/armor N` | 数量 | 获得 N 点护甲 | 测试护甲机制 |
| `/end` | — | 强制结束回合 | 跳过操作直接进入敌方回合 |
| `/refresh` | — | 刷新战斗 UI | 强制刷新界面 |
| `/help` | — | 显示帮助 | 查看所有命令 |
| `/clear` | — | 清空输出 | 清空控制台 |

### AI 测试典型流程
```
# 场景：验证游戏结束弹窗
DevCommand("/damage 19")  # Cultist 20→1 HP
DevCommand("/damage 1")   # 触发胜利弹窗

# 场景：验证亡语抽牌
DevCommand("/draw 10")  # 找联树侦察犬
DevCommand("/mana 5")   # 获取法力
# → 打出联树侦察犬 → 让它死亡 → 检查手牌数
```

## MCP TESTING GUIDANCE

### 可验证 vs 不可验证

godot-mcp 的 `game_click` 使用合成事件（`InputEventMouseButton`），而 `GetGlobalMousePosition()` 返回真实 OS 鼠标位置，`Input.IsMouseButtonPressed()` 也依赖 OS 层。**拖拽交互无法通过 MCP 验证**——需用户在实际游戏中肉眼验证。

### AI 可独立验证的测试场景
| 场景 | 方法 |
|------|------|
| 敌人 AI 执行 | 点击 EndTurnButton → `game_get_logs` 检查伤害 |
| 胜负判定 + 弹窗 | `/damage N` 击杀敌人 → `game_get_logs` 检查 |
| 法力水晶增减 | `/mana N` → `game_get_logs` 检查 |
| 抽牌 | `/draw N` → `game_get_ui` 检查手牌数 |
| 护甲机制 | `/armor N` → `game_get_ui` 检查护甲显示 |
| 回合流转 | `/end` → `game_get_logs` 检查回合数 |

### 需要人类视觉验证的场景
| 场景 | 原因 |
|------|------|
| UI 控件位置/大小 | 无视觉模型 |
| 卡牌拖拽/选中交互 | MCP 合成事件与 OS 鼠标状态不一致 |
| 动画效果 | godot-mcp 无法感知动画状态 |
| 文字显示/字体 | 无法截图验证 |

## CONVENTIONS

### 命名空间与 using
- 命名空间：统一使用 `file_scoped` (`namespace X;`)，新文件必须遵守
- `using` 放在 namespace **外部**
- `System.*` using 优先排序

### 命名规则
| 符号 | 规则 | 示例 |
|------|------|------|
| 接口 | `I` 前缀 + PascalCase | `ICommander`, `IDamageSource` |
| private 字段 | `_camelCase` | `_playerCore`, `_boardUI` |
| public 成员 | PascalCase | `CurrentHealth`, `PlayerSlots` |
| 局部/参数 | camelCase | `slotIndex`, `card` |

### 信号/事件
- **不使用 `[Signal]` 属性**。全部使用 C# `event Action<...>`
- 触发：`?.Invoke()`，订阅：`+=`，取消：`-=`
- UI 按钮事件使用 `Button.Pressed += Handler`

### UI 更新模式
- **Pull 模式**：`CombatUI.RefreshAll()` → BoardUI.RefreshBoard / HandUI.RefreshHand
- 无 `_Process` 轮询（拖拽追踪除外）
- HandUI 刷新手牌使用**销毁重建**（QueueFree 全部 CardUI 后重新创建）
- BoardUI 使用**属性更新**（BoardSlot.UpdateDisplay 原地刷新）

### 资源加载
- `GD.Load<T>(path)` 加载资源
- `ResourceLoader.Exists(path)` 检查存在
- `[Export] PackedScene` 注入场景 → `Instantiate<T>()` 实例化
- `GetNode<Type>(path)` / `GetNodeOrNull<Type>(path)` 获取节点

### 本地化系统
- **入口**：`GameManager.SetLanguage()`（Autoload 唯一入口），内部委托 `Localization.Localization.SetLanguage()`
- **事件**：`GameManager.LanguageChanged` — 所有场景订阅此事件刷新 UI 文本
- **数据**：YAML 翻译文件位于 `Resources/Localization/{lang}.yaml`，扁平化 key 以 `.` 分隔
- **查找**：`Localization.Localization.T("key", "默认值")` — 优先返回 YAML 翻译，缺失时返回默认值
- **占位符**：`T()` 支持 `{key}` 占位符替换，或手动 `.Replace("{key}", value)`
- **卡牌翻译**：`Card.GetLocalizedName()` / `Card.GetLocalizedDescription()` — 惰性求值，优先读 YAML `cards.{id}.name`，回退到 `CardData.CardName`
- **新增内容接线**：新增 UI 控件/场景时，**必须**：
  1. 所有用户可见文本使用 `Localization.T("key", "默认中文")` 包裹
  2. 在 `Resources/Localization/zh.yaml` 和 `en.yaml` 中添加对应 key
  3. 在场景根节点订阅 `GameManager.Instance.LanguageChanged += OnLanguageChanged`，触发时刷新所有文本
  4. `OnLanguageChanged` 回调中加 `IsInsideTree()` 守卫，防止场景切换时崩溃
- **新增语言**：需同步更新 `Localization.TryLoadTranslationsViaDirAccess()` 中的回退列表

### 注释
- XML doc 使用中文（`/// <summary>` 中文内容）
- 日志标签格式：`GD.Print("[ClassName] 消息")`

## EXPORT BUILD GOTCHAS（导出构建注意事项）

### DirAccess 枚举在 .pck 中不可靠

Godot 4 中 `DirAccess.Open("res://...")` 在导出的 .pck 文件中无法枚举目录。本项目已采用 **「DirAccess 优先 + 硬编码列表回退」** 模式：

| 文件 | 回退机制 |
|------|----------|
| `GameManager.cs` | `CardResourcePaths[32]` 硬编码卡牌路径数组 |
| `Localization.cs` | `TryLoadTranslationsViaDirAccess()` + 已知文件回退 |

**新增卡牌时必须同步更新** `GameManager.CardResourcePaths[]` 数组，否则导出版本中新卡牌无法加载。
**新增语言时必须同步更新** `Localization.TryLoadTranslationsViaDirAccess()` 中的回退列表。

### YAML 翻译文件导出

`export_presets.cfg` 中已添加 `include_filter="*.yaml,*.yml"` 强制跟踪 YAML 文件（原始文件无 `.import`，默认不导出）。

### 导出前清理

导出前需删除 `user://save.json`（路径：`%APPDATA%/OdysseyCards/save.json`）以确保干净初始化。旧存档中可能残留不兼容数据导致收藏界面无卡牌。

## ANTI-PATTERNS (THIS PROJECT)
- **禁止** 对 `Card`/`Minion`/`Hero`/`Board`/`GameState`/`EnemyUnit` 调用 Godot 方法（它们不继承 Node）
- **禁止** 直接操作 `Board.PlayerSlots[index]` — 使用 PlaceMinion/RemoveMinion
- **禁止** `async void` — CardAnimation.cs 当前全用 async void，勿模仿
- **禁止** 混淆 `Player._core` 和 `CombatManager._playerCore` — 两个不同的 CommanderCore 实例
- **禁止** 在未检查 null 的情况下向 DamageResolver 传 null source — `Minion.TakeDamage(value, null)` 会 NRE
- **禁止** 在 Hero 有护甲时假设 DamageResolver 生效 — 护甲吸收绕过 DamageResolver
- **禁止** 在新增 UI 内容时使用硬编码中文字符串 — 必须使用 `Localization.Localization.T()` 包裹，并在两版 YAML 中添加对应 key
- **禁止** `MouseFilter=Ignore` 的控件中依赖 `_Input()` — Ignore 完全阻断输入事件（拖拽追踪改用 `_Process` 轮询 `Input` 全局状态）
- **禁止** 在非 Container 父控件中使用 `Offset*` 系列属性 — 位置全由 `Position` 控制
- **禁止** DragLayer 同时持有超过 1 张卡 — 切换选中时必须先完整归还旧卡（数据+UI），不能只 QueueFree
- **禁止** 用 `GetGlobalMousePosition()` 替代 `InputEventMouseButton.GlobalPosition` 作为点击初始坐标 — 两者在 MCP 合成事件中不一致
- **禁止** 新增卡牌后不同步更新 `GameManager.CardResourcePaths[]` — 导出版本会无法加载
- **禁止** 新增语言后不同步更新 `Localization.TryLoadTranslationsViaDirAccess()` 回退列表
- 新文件统一使用 `file_scoped` (`namespace X;`)

## UNIQUE STYLES
- **程序化 UI**：CombatUI 和所有子组件纯代码创建，无 .tscn 依赖（Combat.tscn 仅提供布局容器）
- **双层 CommanderCore**：Player 有自己的 `_core`，CombatManager 创建独立的 `_playerCore`，通过 `internal Deck setter` 共享牌堆
- **Card 基类纯 C#**：与 Godot 场景树零耦合，CardUI 是独立的视觉包装器
- **手动法力同步**：GameState 和 CommanderCore 各维护法力值，CombatManager 手动 SetMana 同步
- **三阶段伤害**：DamageResolver 支持 ADDITIVE → MULTIPLICATIVE → CAPPING 管线，Clamp 最小 0
- **效果引擎复用**：`CombatManager.ExecuteEffect(CardEffectData, object)` 是法术/战吼/亡语的共享效果解析入口
- **STS2 风扇手牌**：HandUI 不使用 HBoxContainer，改为 `Control` + 手动 LayoutChildren 实现交叠悬停（OVERLAP_FACTOR=0.85, BASE_SCALE=0.85）
- **卡牌双交互模式**：点击选中（单击→移动→再击目标）和拖拽（按住→拖动→松手）两种路径，DragThreshold=10f 区分快速点击与拖拽
- **攻击双交互模式**：随从攻击同样支持点击模式（点击→箭头指示→再击目标）和拖拽模式（按住槽位→拖动→松手攻击/取消），AttackDragThreshold=10f
- **导出回退模式**：DirAccess 优先 + 硬编码列表回退，保障导出版本运行

## COMMANDS

```bash
# Build
dotnet build
dotnet build -c Release

# Format check (CI)
dotnet format OdysseyCards.sln --verify-no-changes

# Auto-format
dotnet format OdysseyCards.sln

# Test (xunit, 4 test files)
dotnet test
```

## Autoload 单例

| 名称 | 路径 | 用途 |
|------|------|------|
| `GameManager` | `Scripts/Core/GameManager.cs` | 全局状态，跨战斗持久化，语言切换，卡牌注册表 |
| `UIScaler` | `Scripts/UI/UIScaler.cs` | UI 缩放，基准 1152×648 |
| `DevConsole` | `Scripts/Infrastructure/DevConsole.cs` | 开发者控制台，`` ` `` 键呼出 |

## NOTES

- ✅ **本地化系统已全场景接线**：所有 UI 通过 `GameManager.LanguageChanged` 感知语言切换，Card.GetLocalizedName() 实现卡牌名称动态翻译。2026-06-03 审计：包装 10 处硬编码字符串，新增 12 YAML key
- ✅ **导出构建兼容**：DirAccess 回退机制保障卡牌加载和 YAML 加载在 .pck 导出中正常。export_presets.cfg 已添加 include_filter
- ✅ **初始化缺口已修复**：CombatManager._Ready → CallDeferred(BootstrapCombat) 自动启动战斗
- ✅ **敌人 AI 多类型**：IntentAI（Cultist/SlimeBoss/WolfRider）+ MechanicalRoachBrain + DefaultAttackMinionBrain + EnemyRegistry
- ✅ **游戏结束弹窗**：胜利/失败显示 AcceptDialog，"返回主菜单"→ChangeSceneToFile(Main.tscn)
- ✅ **战斗中暂停界面**：ESC 或右上角 ⏸ 按钮触发，全屏覆盖层。PauseMenu._Input 拦截 ESC，CombatUI._UnhandledInput 仅在非暂停状态下响应 ESC
- ✅ **攻击交互**：攻击选择支持点击/拖拽双模式 + 右键取消。HandleAttackDrop NRE 已修复（_enemyHeroPanel 是死代码，改用 _enemyCards[0]）
- ✅ **卡牌选中/拖拽**：多选归位、点击跟随、拖拽追踪已全部修复。DragLayer 单卡原则
- ⚠️ **Spell.cs 从未实例化** — CombatManager 对所有卡牌使用 Card 基类（死代码）
- ⚠️ **EventSelector 未接线** — 战后奖励逻辑完整但无调用入口
- ⚠️ **英雄技能未实现** — IHeroPower 接口为空
- ⚠️ **手牌无上限 + 无疲劳** — 抽牌堆耗尽未处理
- ⚠️ **CardEffectData.GetDescription() 26 个 debug 字符串仍未本地化**（仅 GD.Print 用，低优）
- ⚠️ **DevConsole 约 40 个帮助文本仍未本地化**（开发者工具，低优）
- 删除 `.godot/` 后编辑器会重新生成 UID 文件，若编辑器加载异常优先检查残留的 .tscn 引用
- 每个 .cs 文件对应一个 `.uid` 文件（Godot 自动生成），git 中已追踪，重命名/移动时需同步处理
- 端口冲突时旧 Godot 进程可能残留，需手动 Stop-Process
- godot-mcp 运行工具硬编码路径 `G:\dev\godot-mcp-fc-a\build\scripts\`，需确保该目录存在

## 杂项

- 在工作中的自然语言部分使用中文，包括任务交流和调度、代码注释等
- **当 agent 无法通过 TDD 或 godot-mcp 完成验证时，必须分析是否可以通过增强 MCP 功能（暴露方法、添加命令）来验证**