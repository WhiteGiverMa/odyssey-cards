> **中文** | [English](README_EN.md) | [日本語](README_JA.md)

# 少女星途卡牌（星途卡牌）<br><small>Shoujo Odyssey Cards</small>

类炉石传说的回合制卡牌对战 Roguelite 游戏 — Godot 4.6 + C#

> **分支:** `main` | **状态:** 能玩MVP
> 78 个 .cs 文件，约 20,100 行代码。完整回合制战斗循环可运行，含卡牌收藏、地图路线、存档系统。

## 核心系统

### 卡牌对战

回合制战斗，2×5 随从棋盘（玩家/敌方各一排 5 槽位），法力水晶系统（初始 1 费，每回合 +1，上限 10）。

- **随从 (Minion)**：可放置在棋盘上，具有攻击力/生命值，支持 7 种关键词
- **法术 (Spell)**：从手牌打出立即生效
- **领域 (Domain)**：持续性场地效果，影响全局规则
- **武器 (Weapon)**：英雄装备，提供攻击力和武器技能
- **英雄 (Hero)**：各有英雄技能（待实现）和护甲机制

### 关键词

| 关键词 | 英文 | 效果 |
|--------|------|------|
| 闪击 | Charge | 召唤的回合即可攻击 |
| 嘲讽 | Taunt | 敌方随从必须优先攻击此随从 |
| 战吼 | Battlecry | 从手牌打出时触发效果 |
| 亡语 | Deathrattle | 随从死亡时触发效果 |
| 风怒 | Windfury | 每回合可以攻击两次 |
| 伏击 | Ambush | 每回合首次被攻击时，先于攻击者造成反击伤害 |
| 冲击 | Impact | 攻击时抵消所有反击伤害（一次性消耗） |

### 伤害计算

三阶段管线：`ADDITIVE → MULTIPLICATIVE → CAPPING`（DamageResolver），Clamp 最小伤害为 1。

### AI 系统

尖塔式意图轮转，3 种敌人类型：

- **邪教徒 (Cultist)**：HP 20，模式 Attack(6)→Attack(6)→Defend(5)
- **史莱姆首领 (SlimeBoss)**：HP 40，模式 Attack(8)→Summon(1)→Defend(4)，会召唤 1/1 软泥怪
- **狼骑兵 (WolfRider)**：HP 12，模式 Attack(5)，每回合稳定输出

### Roguelike

战后 3 选 1 战利品（EventSelector + RewardUI），Fisher-Yates 洗牌。地图路线选择（MapUI）。

> ⚠️ EventSelector 战后奖励逻辑完整但尚未接入战斗循环。

### 卡牌收藏

CollectionUI 提供卡牌浏览、卡组编辑功能。支持按稀有度颜色区分、描述自适应显示、删除确认。卡组有软上限。

### 多语言

YAML-based 本地化系统（`Scripts/Localization/`），中文/英文双语支持，所有 UI 文本通过 `GameManager.LanguageChanged` 事件动态刷新。

### 开发者控制台

`DevConsole`（Autoload 单例）— 按 `` ` `` 键呼出，支持 11+ 命令：`/damage`、`/draw`、`/mana`、`/heal`、`/armor`、`/end` 等，用于快速测试和调试。

### 暂停菜单

ESC 或按钮触发全屏覆盖层。包含继续游戏、设置（语言切换）、存档、快速 SL 功能。

### 存档系统

SaveDataManager + GameSaveData 提供游戏进度持久化。

## 技术栈

- **引擎**: Godot 4.6
- **语言**: C# (.NET 8.0, Godot.NET.Sdk/4.6.2)
- **测试**: xunit（4 个测试文件，303 行）
- **平台**: Windows

## 项目结构

```
Scripts/
├── Core/ (25)           # CardData, DamageResolver, GameManager(Autoload), Keyword, CardType, SaveDataManager…
├── UI/ (18)             # CombatUI, BoardUI, HandUI, CardUI, CollectionUI, MapUI, PauseMenu, DiscoverUI, RewardUI…
├── Card/ (10)           # Card, Minion, Spell, Hero, Weapon, WeaponSkill, ActiveDomain, StatusEffect (纯 C#)
├── Character/ (5)       # Player, CommanderCore, Deck, CombatDeckState, ICommander
├── Combat/ (4)          # CombatManager(1740行), Board, EnemyUnit, GameState (纯 C#)
├── AI/ (7)              # IntentAI, EnemyRegistry, MechanicalRoachBrain, ZhangLang, ShanHu, DefaultAttackMinionBrain
├── Roguelike/ (3)       # EventSelector, RoomData, GameRunState
├── Localization/ (5)    # YAML-based 多语言系统（LocalStr/ConcatLocalStr/ILocalizable/YamlParser）
└── Infrastructure/ (1)  # DevConsole (Autoload) — 开发者控制台
Resources/Cards/         # 32 张卡牌数据 .tres（法术15 + 随从11 + 领域6）
Resources/Localization/  # zh.yaml / en.yaml 翻译文件
Scenes/                  # Main.tscn, Combat.tscn, Collection.tscn, Map.tscn（4 个场景）
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

# 运行测试
dotnet test
```

## 场景

| 场景 | 路径 | 说明 |
|------|------|------|
| 主菜单 | `Scenes/Main.tscn` | 入口场景 |
| 战斗 | `Scenes/Combat.tscn` | 战斗场景，程序化 UI 布局 |
| 收藏 | `Scenes/Collection.tscn` | 卡牌收藏与卡组编辑 |
| 地图 | `Scenes/Map.tscn` | Roguelike 路线选择 |

## Autoload 单例

- **GameManager** (`Scripts/Core/GameManager.cs`) — 全局状态，跨战斗持久化，语言切换
- **UIScaler** (`Scripts/UI/UIScaler.cs`) — UI 缩放，基准分辨率 1152×648
- **DevConsole** (`Scripts/Infrastructure/DevConsole.cs`) — 开发者控制台，`` ` `` 键呼出

## 已知限制

- ⚠️ **Spell.cs 从未实例化** — CombatManager 对所有卡牌使用 Card 基类（死代码）
- ⚠️ **EventSelector 未接线** — 战后奖励逻辑完整但无调用入口
- ⚠️ **英雄技能未实现** — IHeroPower 接口为空
- ⚠️ **手牌无上限 / 无疲劳** — 抽牌堆耗尽未处理

## 许可

本项目采用混合许可证：

- **代码**（`Scripts/` 下的 `.cs` 源文件及项目配置文件）：[MIT](LICENSE_CODE)
- **美术/音频资源**（`Assets/` 目录下的图片、音频等媒体文件）：[CC BY 4.0](LICENSE_ASSETS)

## 致谢

本项目架构设计参考了 [slay-the-model](https://github.com/wkzMagician/slay-the-model)，一个结构清晰的《杀戮尖塔》核心框架，为卡牌游戏架构设计提供了宝贵的学习资源。
