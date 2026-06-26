> **中文** | [English](README_EN.md) | [日本語](README_JA.md) | [한국어](README_KO.md)

# 少女星途卡牌（星途卡牌）<br><small>Shoujo Odyssey Cards</small>

类炉石传说的回合制卡牌对战 Roguelite 游戏 — Godot 4.7 + C#。

> **分支:** `dev` | **状态:** 能玩 MVP 继续扩展中<br>
> 175 个 `Scripts/*.cs` 文件，约 37,000 行游戏代码。完整回合制战斗循环可运行，含卡牌收藏、地图路线、存档、本地化、开发者控制台与运行时 QA。术语对照（领域 = STS2 Power、单位 = Hero+Minion 等）见根 `AGENTS.md` 的 Architecture Rules 段。

## 核心系统

### 卡牌对战

回合制战斗，2×5 随从棋盘（玩家/敌方各一排 5 槽位），法力水晶系统（自然上限/硬上限见测试）。

- **随从 (Minion)**：放置在棋盘上，具有攻击力/生命值，支持关键词
- **法术 (Spell)**：卡牌类型存在；运行时统一通过 `Card` 基类处理
- **领域 (Domain)**：永久 Power（类比 STS2 Power），在战斗事件点长期触发；限时挂载效果（如四夜雷电光、星途精神）走 StatusEffect 通道，不作为领域
- **武器 (Weapon)**：英雄装备，提供攻击与武器技能
- **英雄 (Hero)**：护甲机制已接入；4 个英雄技能实现已存在，战斗 UI 仍需按英雄逐项验证
- **藏品 (Relic)**：生命周期钩子系统已存在，资源化仍在进行
- **热力 (Heat)**：战斗全局节奏压力，接入伤害倍率阶段

> **术语对照**：项目术语（领域/限时挂载效果/目标单位/直伤卡等）的精确定义见根 `AGENTS.md` 的「Architecture Rules → 术语对照」段。

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

四阶段管线：`ADDITIVE → MULTIPLICATIVE → HEAT → CAPPING`（DamageResolver），护甲吸收在管线之后。

### AI / 意图

多敌人独立 actor 架构：每个敌人拥有自己的 HP、MoveState 链和意图。新版意图系统位于 `Scripts/AI/Intents/`，支持多意图组合、动态伤害预览、图标与 tooltip。

### Roguelike

地图路线选择（MapUI）、奖励/发现、事件/商店/休息处 UI 均已存在；后者仍在接入流程中。战后奖励逻辑与旧 `EventSelector` 仍未完全统一。

### 卡牌收藏

CollectionUI 提供卡牌浏览、卡组编辑、分页/过滤。当前战斗使用当前卡组快照，卡组修改在后续流程生效。

### 多语言

YAML-based 本地化系统（`Scripts/Localization/`），中文/英文双语支持。UI 文本通过 `Localization.T()` 与 `GameManager.LanguageChanged` 动态刷新。

### 开发者控制台

`ChatScreen`（Autoload）按 `` ` `` 呼出。架构：`ChatScreen` → `ChatScreenEngine` → `Commands/*`。支持资源、伤害、召唤、战斗跳转、藏品、标签、运行时 QA 等命令；AI 可通过 godot-mcp 远程调用。

### 卡牌标签编辑工具

独立于游戏的 .NET 8 控制台工具（`Tools/CardTagEditor/`，零 Godot 依赖），用于可视化编辑卡牌的机制标签/关键词和角色主题画像，直接回写 `.tres` 资源。

**启动 Web UI**（推荐，从项目根目录运行）：

```bash
cd G:\dev\odyssey-cards
dotnet run --project Tools/CardTagEditor -- serve
# 浏览器打开 http://localhost:8765
```

`serve` 不带 `--path` 时自动从当前目录向上找 `project.godot` 检测仓库根。在其他目录运行需显式指定：

```bash
dotnet run --project G:\dev\odyssey-cards\Tools\CardTagEditor -- serve --path G:\dev\odyssey-cards
```

**CLI 子命令**：

```bash
dotnet run --project Tools/CardTagEditor -- list                    # 列出所有卡牌/主题摘要
dotnet run --project Tools/CardTagEditor -- dump minion_Mech_Lancer # JSON 转储单卡
dotnet run --project Tools/CardTagEditor -- dump theme:ayame        # JSON 转储单主题
dotnet run --project Tools/CardTagEditor -- validate                # 校验未知位/越界关键词/悬空引用
dotnet run --project Tools/CardTagEditor -- migrate --dry-run       # 预览 Tags→MechanicTags 迁移
dotnet run --project Tools/CardTagEditor -- serve --port 9000       # 指定端口启动 Web UI
```

**Web UI 功能**：
- 卡牌编辑器：41 张卡列表 + 17 机制标签 checkbox + 11 关键词 checkbox + 保存回写
- 主题编辑器：3 个 ThemeProfile + 标签/关键词权重表格 + 核心卡 ID 编辑 + 保存回写
- 保存时只修改目标字段，其他 `.tres` 行保持原样（round-trip 保真）

**技术栈**：纯 .NET 8 BCL（`HttpListener` + `System.Text.Json`）+ Alpine.js CDN（零 node/npm 构建步骤）。

## 技术栈

- **引擎**: Godot 4.7
- **语言**: C# (.NET 8.0, Godot.NET.Sdk/4.7.0)
- **测试**: xUnit（10 个 Unit + 1 个 Integration；Integration 因 Godot Resource 依赖跳过）
- **平台**: Windows；Android 导出脚本存在

## 项目结构

```
Scripts/
├── Core/ (35)           # CardData, DamageResolver, GameManager, HeroProfile, RarityColorScheme…
├── UI/ (39)             # CombatUI partials, CardUI, BoardUI, CollectionUI, MapUI, Shop/Rest/Event UI…
├── Card/ (15)           # Card, Minion, Hero, Weapon, StatusEffect, ActiveDomain, HeroPowers/*
├── Character/ (5)       # Player, CommanderCore, Deck, CombatDeckState
├── Combat/ (14)         # CombatManager + AttackTracker/SelectionSystem/DeathHandler/WeaponAttackSystem/DomainTriggerManager…
├── AI/ (27)             # EnemyEncounter, EnemyRegistry, Brains, Intents/(19)
├── Heat/ (2)            # HeatSystem + HeatDamageModifier
├── Relic/ (7)           # AbstractRelic, RelicManager, concrete relics
├── Roguelike/ (5)       # EventSelector, RoomData, GameRunState, EventData, BlessingData
├── Localization/ (5)    # YAML 多语言系统
└── Infrastructure/ (20) # ChatScreen, InputManager, HotkeyManager, MobileInputRouter, Commands/8
Resources/Cards/         # 37 .tres（领域6 + 随从11 + 法术19 + 状态1）
Resources/Localization/  # zh.yaml / en.yaml
Scenes/                  # Main, Combat, Collection, Map + Card/Board/CombatPreview
```

### 架构特点

- **纯 C# 核心**：Card/Minion/Hero/Board/GameState/EnemyUnit 不继承 Node
- **程序化 UI**：Combat.tscn 仅容器，CombatUI 子组件纯代码；预览靠 `[Tool]` 场景
- **CombatUI partial**：核心 / Layout / Refresh / Selection 四分
- **CombatManager 拆分**：纯 C# 小系统 + 构造注入 + Action 回调
- **C# event**：不使用 Godot `[Signal]`
- **三层输入**：InputManager → HotkeyManager → 场景 UI
- **导出回退**：DirAccess 失败时依赖硬编码资源路径

## 克隆后首次运行

> **前提**：安装 [Godot 4.7 Mono](https://godotengine.org/download/) 和 [.NET 8.0 SDK](https://dotnet.microsoft.com/download)。

```bash
git clone <repo-url>
cd OdysseyCards
dotnet build                          # 编译 C# 程序集（必须）
# 用 Godot_v4.7-stable_mono_win64.exe 打开项目一次 → 编辑器会自动重建 .godot 缓存
# 然后按 F5 运行
```

> [!IMPORTANT]
> **先 `dotnet build`，再用 Mono 版 Godot 打开编辑器**。跳过任何一步都会导致「缺少 MainMenu.cs」或「No loader found for resource」错误——因为 `.cs` 脚本需要已编译的程序集才能加载。

## 构建 / 测试 / 导出

```bash
dotnet build
dotnet build -c Release
dotnet test
dotnet format OdysseyCards.sln --verify-no-changes

./build_export.ps1 [-Debug] [-SkipBuild]
./build_android.ps1 [-SkipBuild] [-ExportOnly]
./package_release.ps1 [version] [-OpenFolder]
```

当前没有 GitHub Actions / Dockerfile / Makefile。GUT 插件已安装但暂无 GDScript 测试。

## 场景

| 场景 | 路径 | 说明 |
|------|------|------|
| 主菜单 | `Scenes/Main.tscn` | 入口场景 |
| 战斗 | `Scenes/Combat.tscn` | 战斗场景，程序化 UI 布局 |
| 收藏 | `Scenes/Collection.tscn` | 卡牌收藏与卡组编辑 |
| 地图 | `Scenes/Map.tscn` | Roguelike 路线选择 |
| 卡牌预览 | `Scenes/CardPreview.tscn` | 编辑器预览 |
| 棋盘预览 | `Scenes/BoardPreview.tscn` | 编辑器预览 |
| 战斗预览 | `Scenes/CombatPreview.tscn` | 编辑器预览 |

## Autoload 单例

- **GameManager** (`Scripts/Core/GameManager.cs`) — 全局状态、卡牌注册表、跨战斗持久化、语言切换
- **UIScaler** (`Scripts/UI/UIScaler.cs`) — UI 缩放，当前基准 1152×648
- **ChatScreen** (`Scripts/Infrastructure/ChatScreen.cs`) — 开发者控制台
- **MobileInputHelper** (`Scripts/Infrastructure/MobileInputHelper.cs`) — 旧触控辅助，非战斗 UI 仍有使用
- **MobileInputRouter** (`Scripts/Infrastructure/MobileInputRouter.cs`) — 移动端触控路由与模态栈
- **InputManager** (`Scripts/Infrastructure/InputManager.cs`) — 物理键到逻辑动作
- **HotkeyManager** (`Scripts/Infrastructure/HotkeyManager.cs`) — 动作到回调栈

## 已知限制

- `Spell.cs` 从未实例化；运行时统一用 `Card` 基类
- `RailPistolPassive.cs`、`SafeAreaContainer.cs` 当前孤立
- `ShopUI` / `RestSiteUI` / `EventUI` 存在但未完整接入 MapUI 流程
- 英雄技能已有 IronWill / 星光补给 / 火力筛选 / 重整；战斗 UI 仍需按英雄逐项验证
- 手牌无上限 + 疲劳系统未完整化（已有 `Status_Fatigue.tres`）
- `InfoScreen.cs` 使用 Godot 过时 API `SplitOffset`

## 许可

本项目采用混合许可证：

- **代码**（`Scripts/` 下的 `.cs` 源文件及项目配置文件）：[MIT](LICENSE_CODE)
- **美术/音频资源**（`Assets/` 目录下的图片、音频等媒体文件）：[CC BY 4.0](LICENSE_ASSETS)

## 游戏剧情摘要

2048年，前AGI时代。大国之间开启超限战与太空博弈，中国启动「星途计划」，在民间推出「星途卡牌」这一娱乐活动，各行各业民众踊跃参与。据知，「794局」正在收集全国人民的对战数据，来训练在宇宙空间中参与生产生活、甚至应用到军事中的AGI agent「领航者」。毕业季的盛夏，少女绮梦安装了《星途卡牌》桌面版，结识了许多同好。她潜心切磋牌技、打磨策略……在一次比赛中层层胜出后，等待她的决赛对手竟是……

## 致谢

本项目架构设计参考了 [slay-the-model](https://github.com/wkzMagician/slay-the-model)，一个结构清晰的《杀戮尖塔》核心框架，为卡牌游戏架构设计提供了宝贵的学习资源。
