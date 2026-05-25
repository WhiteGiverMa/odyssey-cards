# OdysseyCards — Godot 4.6 C# · Hearthstone-Style Roguelike Card Game

**Branch:** refactor/hearthstone-rework · **Commit:** 3c81f45
**Generated:** 2026-05-26

## OVERVIEW

类炉石传说的回合制卡牌对战，带 Roguelike 路线选择。2×5 随从棋盘，法力水晶系统，5 种关键词。Godot 4.6 + C# (.NET 8.0, Godot.NET.Sdk/4.6.2)。44 个 .cs 文件，约 6200 行。

## STRUCTURE

```
Scripts/
├── Core/ (14)       # CardData, DamageResolver, GameManager(Autoload), Keyword
├── UI/ (10)         # CombatUI(927行), BoardUI, HandUI, CardUI(642行), CardAnimation
├── Card/ (5)        # Card(纯C#), Minion, Spell, Hero(纯C#), IHeroPower
├── Character/ (5)   # Player, CommanderCore, Deck, CombatDeckState, ICommander
├── Combat/ (3)      # CombatManager(754行), Board(纯C#), GameState(纯C#)
├── Localization/ (5)# YAML-based 多语言系统
├── AI/ (1)          # IntentAI (Cultist/SlimeBoss/WolfRider)
└── Roguelike/ (1)   # EventSelector (3选1战利品)
Resources/Cards/     # 6 张迁移卡牌 .tres (Spell_*/Minion_*)
Scenes/              # Main.tscn, Combat.tscn (仅2个场景)
```

## WHERE TO LOOK

| Task | Location | Notes |
|------|----------|-------|
| 卡牌数据定义 | `Scripts/Core/CardData.cs` | Godot Resource, [Export] 字段 |
| 卡牌运行时 | `Scripts/Card/Card.cs` → Minion/Spell | **纯 C#，不继承 Node** |
| 战斗核心循环 | `Scripts/Combat/CombatManager.cs` | 754行，PlayMinion/PlaySpell/MinionAttack/EndPlayerTurn |
| 棋盘管理 | `Scripts/Combat/Board.cs` | 2×5 槽位，嘲讽检测，C# event 而非 Godot signal |
| 回合/法力 | `Scripts/Combat/GameState.cs` | 纯 C#，法力上限10 |
| 英雄/护甲/技能 | `Scripts/Card/Hero.cs` | 纯 C#，包装 CommanderCore |
| 伤害计算 | `Scripts/Core/DamageResolver.cs` | 三阶段管线 ADDITIVE→MULTIPLICATIVE→CAPPING |
| 敌人 AI | `Scripts/AI/IntentAI.cs` | 尖塔式意图轮转 |
| 战后奖励 | `Scripts/Roguelike/EventSelector.cs` | Fisher-Yates 洗牌 |
| UI 编排 | `Scripts/UI/CombatUI.cs` | 程序化布局，选择模式状态机 |
| UI 缩放 | `Scripts/UI/UIScaler.cs` | Autoload 单例，基准 1152×648 |
| 全局状态 | `Scripts/Core/GameManager.cs` | Autoload，跨战斗持久化 |

## CODE MAP

| Symbol | Type | Location | Role |
|--------|------|----------|------|
| `CombatManager` | partial Node | `Combat/CombatManager.cs` | 战斗编排器，Instance 单例 |
| `Board` | 纯 C# class | `Combat/Board.cs` | 2×5 槽位，event OnMinionPlaced/Removed |
| `GameState` | 纯 C# class | `Combat/GameState.cs` | 回合流转，法力水晶 |
| `Card` | 纯 C# class | `Card/Card.cs` | 运行时基类，包装 CardData |
| `Minion : Card, IDamageSource, IDamageTarget` | 纯 C# class | `Card/Minion.cs` | 随从，关键词 |
| `Hero : IDamageTarget` | 纯 C# class | `Card/Hero.cs` | 英雄，护甲，包装 CommanderCore |
| `CardData : Resource` | Godot Resource | `Core/CardData.cs` | 卡牌数据，[Export] |
| `GameManager` | partial Node | `Core/GameManager.cs` | Autoload，全局单例 |
| `CommanderCore` | 纯 C# class | `Character/CommanderCore.cs` | HP/Mana/Deck 核心逻辑 |
| `Player : Node, ICommander` | partial Node | `Character/Player.cs` | 包装 CommanderCore |
| `CombatUI : Control` | partial Control | `UI/CombatUI.cs` | UI 编排器，正常/放置/目标/攻击 四种模式 |
| `BoardUI : Control` | partial Control | `UI/BoardUI.cs` | 2×5 棋盘渲染，内嵌 BoardSlot 类 |
| `DamageResolver` | static class | `Core/DamageResolver.cs` | 三阶段伤害计算 |
| `EventSelector` | sealed class | `Roguelike/EventSelector.cs` | 战后奖励 |

## CONVENTIONS

### 命名空间与 using
- 命名空间：**混用** file-scoped (`;`) 和 block-scoped (`{ }`)，正在迁移中
- `.editorconfig` 要求 `block_scoped`，实际 80% 用 `file_scoped`
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
- 命名空间混用已知，新文件统一使用 `file_scoped` (`namespace X;`)

## UNIQUE STYLES
- **程序化 UI**：CombatUI 和所有子组件纯代码创建，无 .tscn 依赖（Combat.tscn 仅提供布局容器）
- **双层 CommanderCore**：Player 有自己的 `_core`，CombatManager 创建独立的 `_playerCore`，通过 `internal Deck setter` 共享牌堆
- **Card 基类纯 C#**：与 Godot 场景树零耦合，CardUI 是独立的视觉包装器
- **手动法力同步**：GameState 和 CommanderCore 各维护法力值，CombatManager 手动 SetMana 同步
- **三阶段伤害**：DamageResolver 支持 ADDITIVE → MULTIPLICATIVE → CAPPING 管线，Clamp 最小 1

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

## NOTES

- ⚠️ **初始化缺口**：`CombatManager.Initialize()` / `CombatUI.Initialize()` / `StartCombat()` 已定义但从未被调用，场景切换后战斗不会自动启动
- ⚠️ **起始牌堆为空**：`GameManager.CreateStartingDeck()` 返回空列表，需加载 .tres 或硬编码 CardData
- ⚠️ **Spell.cs 从未实例化**：CombatManager 对所有卡牌使用 Card 基类，Spell 类目前是死代码
- ⚠️ **空目录存在**：`Scripts/Editor/` 和 `Scripts/Infrastructure/` 为空，`Autoload/` 为空（注册在 project.godot）
- 删除 `.godot/` 后编辑器会重新生成 UID 文件，若编辑器加载异常优先检查残留的 .tscn 引用
- 每个 .cs 文件对应一个 `.uid` 文件（Godot 自动生成），git 中已追踪，重命名/移动时需同步处理
