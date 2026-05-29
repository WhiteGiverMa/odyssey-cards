# OdysseyCards — Godot 4.6 C# · 类炉石传说Rougelite卡牌游戏

**Branch:** refactor/hearthstone-rework · **Commit:** 3314103
**Generated:** 2026-05-26 · **Updated:** 2026-05-27 (能玩MVP打通)

## OVERVIEW

类炉石传说的回合制卡牌对战，带 Roguelike 路线选择。2×5 随从棋盘，法力水晶系统，5 种关键词。Godot 4.6 + C# (.NET 8.0, Godot.NET.Sdk/4.6.2)。46 个 .cs 文件，约 6800 行。

**当前状态：能玩MVP** — 回合制战斗循环完整可运行：主菜单→起手6张→出牌/施法/随从攻击→结束回合→敌人AI行动→新回合→胜负弹窗→返回主菜单。

## STRUCTURE

```
Scripts/
├── Core/ (14)          # CardData, DamageResolver, GameManager(Autoload), Keyword
├── UI/ (11)            # CombatUI, BoardUI, HandUI, CardUI, CardAnimation, PauseMenu
├── Card/ (5)           # Card(纯C#), Minion, Spell, Hero(纯C#), IHeroPower
├── Character/ (5)      # Player, CommanderCore, Deck, CombatDeckState, ICommander
├── Combat/ (3)         # CombatManager, Board(纯C#), GameState(纯C#)
├── AI/ (1)             # IntentAI (Cultist/SlimeBoss/WolfRider) — 已接线
├── Roguelike/ (1)      # EventSelector (3选1战利品) — 未接线
├── Localization/ (5)   # YAML-based 多语言系统
└── Infrastructure/ (1) # DevConsole (Autoload) — 开发者控制台
Resources/Cards/        # 6 张卡牌 .tres (3法术 + 3随从, 各2张=12张起手)
Scenes/                 # Main.tscn, Combat.tscn (仅2个场景)
```

## WHERE TO LOOK

| Task | Location | Notes |
|------|----------|-------|
| 卡牌数据定义 | `Scripts/Core/CardData.cs` | Godot Resource, [Export] 字段 |
| 卡牌运行时 | `Scripts/Card/Card.cs` → Minion/Spell | **纯 C#，不继承 Node** |
| 战斗核心循环 | `Scripts/Combat/CombatManager.cs` | 895行，PlayMinion/PlaySpell/MinionAttack/EndPlayerTurn/ExecuteEnemyTurn |
| 棋盘管理 | `Scripts/Combat/Board.cs` | 2×5 槽位，嘲讽检测，C# event 而非 Godot signal |
| 回合/法力 | `Scripts/Combat/GameState.cs` | 纯 C#，法力上限10，CombatPhase枚举控制流转 |
| 英雄/护甲/技能 | `Scripts/Card/Hero.cs` | 纯 C#，包装 CommanderCore，Heal/GainArmor |
| 伤害计算 | `Scripts/Core/DamageResolver.cs` | 三阶段管线 ADDITIVE→MULTIPLICATIVE→CAPPING |
| 敌人 AI | `Scripts/AI/IntentAI.cs` | 尖塔式意图轮转，3种敌人已接线 |
| 战后奖励 | `Scripts/Roguelike/EventSelector.cs` | Fisher-Yates 洗牌，未接线 |
| UI 编排 | `Scripts/UI/CombatUI.cs` | 程序化布局，4种选择模式 + 游戏结束弹窗 |
| UI 缩放 | `Scripts/UI/UIScaler.cs` | Autoload 单例，基准 1152×648 |
| 暂停菜单 | `Scripts/UI/PauseMenu.cs` | ESC/按钮触发，全屏覆盖，内嵌设置（语言切换） |
| 全局状态 | `Scripts/Core/GameManager.cs` | Autoload，跨战斗持久化 |
| 开发者控制台 | `Scripts/Infrastructure/DevConsole.cs` | Autoload，`键呼出，AI可调用 |

## CODE MAP

| Symbol | Type | Location | Role |
|--------|------|----------|------|
| `CombatManager` | partial Node | `Combat/CombatManager.cs` | 战斗编排器，Instance 单例，含 ExecuteEnemyTurn/EnemyMinionsAttack |
| `Board` | 纯 C# class | `Combat/Board.cs` | 2×5 槽位，event OnMinionPlaced/Removed |
| `GameState` | 纯 C# class | `Combat/GameState.cs` | 回合流转，法力水晶，CombatPhase状态机 |
| `Card` | 纯 C# class | `Card/Card.cs` | 运行时基类，包装 CardData |
| `Minion : Card, IDamageSource, IDamageTarget` | 纯 C# class | `Card/Minion.cs` | 随从，关键词，BattlecryEffects/DeathrattleEffects |
| `Hero : IDamageTarget` | 纯 C# class | `Card/Hero.cs` | 英雄，护甲，包装 CommanderCore，Heal/GainArmor |
| `CardData : Resource` | Godot Resource | `Core/CardData.cs` | 卡牌数据，[Export] |
| `CardEffectData : Resource` | Godot Resource | `Core/CardEffectData.cs` | 效果数据，EffectType枚举+Value |
| `GameManager` | partial Node | `Core/GameManager.cs` | Autoload，全局单例 |
| `CommanderCore` | 纯 C# class | `Character/CommanderCore.cs` | HP/Mana/Deck 核心逻辑 |
| `Player : Node, ICommander` | partial Node | `Character/Player.cs` | 包装 CommanderCore |
| `CombatUI : Control` | partial Control | `UI/CombatUI.cs` | UI 编排器，Normal/PlacingMinion/TargetingSpell/SelectingAttackTarget 四种模式 |
| `BoardUI : Control` | partial Control | `UI/BoardUI.cs` | 2×5 棋盘渲染，内嵌 BoardSlot 类 |
| `PauseMenu : Control` | partial Control | `UI/PauseMenu.cs` | 全屏暂停覆盖层，ESC/按钮触发，内嵌语言切换设置 |
| `DamageResolver` | static class | `Core/DamageResolver.cs` | 三阶段伤害计算 |
| `EventSelector` | sealed class | `Roguelike/EventSelector.cs` | 战后奖励（未接线） |
| `DevConsole` | partial Node | `Infrastructure/DevConsole.cs` | Autoload 开发者控制台 |
| `EnemyEncounter` | abstract class | `AI/IntentAI.cs` | 敌人AI基类，ExecuteIntent(CombatManager) |
| `Cultist / SlimeBoss / WolfRider` | class | `AI/IntentAI.cs` | 3种具体敌人实现 |

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
DevCommand("/damage 19")  # 邪教徒 20→1 HP
DevCommand("/damage 1")   # 触发胜利弹窗

# 场景：验证亡语抽牌
DevCommand("/draw 10")  # 找联树侦察犬
DevCommand("/mana 5")   # 获取法力
# → 打出联树侦察犬 → 让它死亡 → 检查手牌数
```

## MCP TESTING GUIDANCE

### 可验证 vs 不可验证的判断流程

当 agent 发现一个测试场景无法通过 TDD (xunit) 或 godot-mcp 完成验证时，**必须**分析：

1. **godot-mcp 现有能力是否足够？**
   - `game_call_method` — 调用公共方法（DevConsole, CombatManager 等）
   - `game_eval` — 执行 GDScript 表达式（注意：可能因语法问题卡住 debugger）
   - `game_get_logs` / `game_get_ui` — 读取日志和 UI 状态
   - `game_click` — 模拟点击交互

2. **是否可以通过暴露新的 public/internal 方法增强 MCP 测试能力？**
   - 在 CombatManager 或 GameState 添加 `internal` 查询方法（如 `GetHandCount()`, `GetEnemyHP()`）
   - 在 DevConsole 添加新的 `/` 命令封装复杂操作
   - 将关键状态检查封装为可通过 `game_call_method` 调用的一行方法

3. **是否可以通过 DevConsole 命令组合模拟测试场景？**
   - `/damage` + `/heal` + `/draw` + `/mana` 可模拟大多数战斗状态
   - `/end` 可跳回合
   - 组合使用可在秒级完成原本需要多回合的测试

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
| UI 控件位置/大小 | 无视觉模型，需人类肉眼确认 |
| 卡牌拖拽交互 | 鼠标事件链复杂，建议人工验证 |
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
- 无 `_Process` 轮询
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

### 注释
- XML doc 使用中文（`/// <summary>` 中文内容）
- 日志标签格式：`GD.Print("[ClassName] 消息")`

## ANTI-PATTERNS (THIS PROJECT)
- **禁止** 对 `Card`/`Minion`/`Hero`/`Board`/`GameState` 调用 Godot 方法（它们不继承 Node）
- **禁止** 直接操作 `Board.PlayerSlots[index]` — 使用 PlaceMinion/RemoveMinion
- **禁止** `async void` — CardAnimation.cs 当前全用 async void，勿模仿
- **禁止** 混淆 `Player._core` 和 `CombatManager._playerCore` — 两个不同的 CommanderCore 实例
- **禁止** 在未检查 null 的情况下向 DamageResolver 传 null source — `Minion.TakeDamage(value, null)` 会 NRE
- **禁止** 在 Hero 有护甲时假设 DamageResolver 生效 — 护甲吸收绕过 DamageResolver
- **禁止** 在新增 UI 内容时使用硬编码中文字符串 — 必须使用 `Localization.Localization.T()` 包裹，并在两版 YAML 中添加对应 key
- 新文件统一使用 `file_scoped` (`namespace X;`)

## UNIQUE STYLES
- **程序化 UI**：CombatUI 和所有子组件纯代码创建，无 .tscn 依赖（Combat.tscn 仅提供布局容器）
- **双层 CommanderCore**：Player 有自己的 `_core`，CombatManager 创建独立的 `_playerCore`，通过 `internal Deck setter` 共享牌堆
- **Card 基类纯 C#**：与 Godot 场景树零耦合，CardUI 是独立的视觉包装器
- **手动法力同步**：GameState 和 CommanderCore 各维护法力值，CombatManager 手动 SetMana 同步
- **三阶段伤害**：DamageResolver 支持 ADDITIVE → MULTIPLICATIVE → CAPPING 管线，Clamp 最小 0
- **效果引擎复用**：`CombatManager.ExecuteEffect(CardEffectData, object)` 是法术/战吼/亡语的共享效果解析入口

## COMMANDS

```bash
# Build
dotnet build
dotnet build -c Release

# Format check (CI)
dotnet format OdysseyCards.sln --verify-no-changes

# Auto-format
dotnet format OdysseyCards.sln

# Test (xunit declared, 0 tests written)
dotnet test
```

## Autoload 单例

| 名称 | 路径 | 用途 |
|------|------|------|
| `GameManager` | `Scripts/Core/GameManager.cs` | 全局状态，跨战斗持久化 |
| `UIScaler` | `Scripts/UI/UIScaler.cs` | UI 缩放，基准 1152×648 |
| `DevConsole` | `Scripts/Infrastructure/DevConsole.cs` | 开发者控制台，`键呼出 |

## NOTES

- ✅ **本地化系统已全场景接线**：MainMenu → MapUI → CombatUI → BoardUI 全部通过 `GameManager.LanguageChanged` 感知语言切换，`Card.GetLocalizedName()` 实现卡牌名称动态翻译
- ✅ **初始化缺口已修复**：`CombatManager._Ready` → `CallDeferred(BootstrapCombat)` 自动启动战斗
- ✅ **起始牌堆已修复**：`CreateStartingDeck()` 加载 6 个 .tres (各2张，共12张)
- ✅ **敌人 AI 已接线**：`BootstrapCombat` 创建 Cultist(20HP)，`EndPlayerTurn` 调用 `ExecuteEnemyTurn`
- ✅ **游戏结束弹窗**：胜利/失败显示 AcceptDialog，"返回主菜单"→`ChangeSceneToFile(Main.tscn)`
- ✅ **战斗中暂停界面**：ESC 或右上角 ⏸ 按钮触发，全屏覆盖层。包含「继续」「设置（语言切换）」「保存并退出」「快速SL」四个选项。内嵌设置页面复用 SettingsPage 模式（OptionButton + GameManager.SetLanguage()）。PauseMenu._Input 拦截 ESC（设置页→返回主菜单→关闭），CombatUI._UnhandledInput 仅在非暂停状态下响应 ESC。
- ⚠️ **Spell.cs 从未实例化**：CombatManager 对所有卡牌使用 Card 基类，Spell 类是死代码
- ⚠️ **EventSelector 未接线**：战后奖励逻辑完整但无调用入口
- ⚠️ **英雄技能未实现**：IHeroPower 接口为空
- ⚠️ **手牌无上限 + 无疲劳**：抽牌堆空了未处理
- 删除 `.godot/` 后编辑器会重新生成 UID 文件，若编辑器加载异常优先检查残留的 .tscn 引用
- 每个 .cs 文件对应一个 `.uid` 文件（Godot 自动生成），git 中已追踪，重命名/移动时需同步处理

## 杂项

- 在工作中的自然语言部分使用中文，包括任务交流和调度、代码注释等
- 参考 [杀戮尖塔2反编译源码](../slay-the-spire-2/) 获取架构参考
- **当 agent 无法通过 TDD 或 godot-mcp 完成验证时，必须分析是否可以通过增强 MCP 功能（暴露方法、添加命令）来验证**
