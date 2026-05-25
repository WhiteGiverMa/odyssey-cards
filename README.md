# OdysseyCards

类炉石传说的回合制卡牌对战 Roguelite 游戏 — Godot 4.6 + C#

> **分支:** `refactor/hearthstone-rework` | **状态:** 可交互原型
> 44 个 .cs 文件，约 6200 行代码。场景加载后自动初始化并进入可游玩状态。

## 核心系统

### 卡牌对战

回合制战斗，2×5 随从棋盘（玩家/敌方各一排 5 槽位），法力水晶系统（初始 1 费，每回合 +1，上限 10）。

- **随从 (Minion)**：可放置在棋盘上，具有攻击力/生命值，支持 5 种关键词
- **法术 (Spell)**：从手牌打出立即生效
- **英雄 (Hero)**：各有英雄技能和护甲机制

### 关键词

| 关键词 | 英文 | 效果 |
|--------|------|------|
| 冲锋 | Charge | 召唤的回合即可攻击 |
| 嘲讽 | Taunt | 敌方随从必须优先攻击此随从 |
| 战吼 | Battlecry | 从手牌打出时触发效果 |
| 亡语 | Deathrattle | 随从死亡时触发效果 |
| 风怒 | Windfury | 每回合可以攻击两次 |

### 伤害计算

三阶段管线：`ADDITIVE → MULTIPLICATIVE → CAPPING`（DamageResolver），Clamp 最小伤害为 1。

### AI 系统

尖塔式意图轮转，3 种敌人类型：
- **邪教徒 (Cultist)**：HP 20，模式 Attack(6)→Attack(6)→Defend(5)
- **史莱姆首领 (SlimeBoss)**：HP 40，模式 Attack(8)→Summon(1)→Defend(4)，会召唤 1/1 软泥怪
- **狼骑兵 (WolfRider)**：HP 12，模式 Attack(5)，每回合稳定输出

### Roguelike

战后 3 选 1 战利品（EventSelector），Fisher-Yates 洗牌。

### 多语言

YAML-based 本地化系统（`Scripts/Localization/`）。

## 技术栈

- **引擎**: Godot 4.6
- **语言**: C# (.NET 8.0, Godot.NET.Sdk/4.6.2)
- **平台**: Windows

## 项目结构

```
Scripts/
├── Core/ (14)       # CardData, DamageResolver, GameManager(Autoload), Keyword
├── UI/ (10)         # CombatUI(927行), BoardUI, HandUI, CardUI(642行), CardAnimation
├── Card/ (5)        # Card, Minion, Spell, Hero (纯 C#，不继承 Node)
├── Character/ (5)   # Player, CommanderCore, Deck, CombatDeckState
├── Combat/ (3)      # CombatManager(754行), Board, GameState (纯 C#)
├── AI/ (1)          # IntentAI (Cultist/SlimeBoss/WolfRider)
├── Roguelike/ (1)   # EventSelector (3选1战利品)
└── Localization/ (5)# YAML-based 多语言系统
Resources/Cards/     # 6 张卡牌数据 .tres
Assets/              # 美术/音频资源（占位符）
Scenes/              # Main.tscn, Combat.tscn（仅 2 个场景）
```

### 架构特点

- **程序化 UI**：CombatUI 及子组件纯代码创建，不依赖 .tscn（Combat.tscn 仅提供布局容器）
- **纯 C# 核心**：Card/Minion/Hero/Board/GameState 均不继承 Godot Node，与场景树零耦合
- **双层 CommanderCore**：Player 和 CombatManager 各自维护 CommanderCore，通过 `internal Deck setter` 共享牌堆
- **C# event**：不使用 Godot `[Signal]`，全部使用 `event Action<...>`
- **Pull 模式 UI 刷新**：`CombatUI.RefreshAll()` 驱动，无 `_Process` 轮询
- **自动初始化**：场景加载后 `CallDeferred` 自动启动战斗，12 张起始牌堆

## 构建

```bash
# 调试构建
dotnet build

# 发布构建
dotnet build -c Release

# 代码风格检查
dotnet format OdysseyCards.sln --verify-no-changes

# 自动格式化
dotnet format OdysseyCards.sln
```

## 场景

| 场景 | 路径 | 说明 |
|------|------|------|
| 主菜单 | `Scenes/Main.tscn` | 入口场景 |
| 战斗 | `Scenes/Combat.tscn` | 战斗场景，程序化 UI 布局 |

## Autoload 单例

- **GameManager** (`Scripts/Core/GameManager.cs`) — 全局状态，跨战斗持久化
- **UIScaler** (`Scripts/UI/UIScaler.cs`) — UI 缩放，基准分辨率 1152×648

## 致谢

## 许可

本项目采用混合许可证：

- **代码**（`Scripts/` 下的 `.cs` 源文件及项目配置文件）：[MIT](LICENSE-CODE)
- **美术/音频资源**（`Assets/` 目录下的图片、音频等媒体文件）：[CC BY 4.0](LICENSE-ASSETS)

## 致谢

本项目架构设计参考了 [slay-the-model](https://github.com/wkzMagician/slay-the-model)，一个结构清晰的《杀戮尖塔》核心框架，为卡牌游戏架构设计提供了宝贵的学习资源。
