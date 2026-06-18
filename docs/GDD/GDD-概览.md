# GDD — 游戏概览与架构

> 最后更新：2026-05-29
> 状态标注：✅ 已实现 / 🚧 部分实现 / ⬜ 待实现 / ⏸️ 搁置

## 游戏定位

类《炉石传说》的回合制卡牌对战 + Roguelike 路线选择。2×5 随从棋盘（玩家/敌方各 5 槽位），法力水晶系统，5 种关键词。玩家扮演英雄，携带武器，通过卡牌（随从/法术/领域）与敌人对战，战后选择路线进入下一个房间。

## 技术栈

- **引擎**: Godot 4.6
- **语言**: C# (.NET 8.0, Godot.NET.Sdk/4.6.2)
- **平台**: Windows

## 架构决策（每次会话都需要遵守）

### ✅ 纯 C# 核心
`Card`/`Minion`/`Hero`/`Board`/`GameState` 均**不继承 Godot Node**，与场景树零耦合。CardUI 是独立的视觉包装器。

### ✅ C# event 代替 Godot [Signal]
全部使用 `event Action<...>`。触发 `?.Invoke()`，订阅 `+=`，取消 `-=`。

### ✅ 程序化 UI
CombatUI 及子组件纯代码创建，不依赖 .tscn（Combat.tscn 仅提供布局容器）。

### ✅ Pull 模式 UI 刷新
`CombatUI.RefreshAll()` 驱动，无 `_Process` 轮询。

### ✅ 双层 CommanderCore
Player 和 CombatManager 各自维护 CommanderCore 实例，通过 `internal Deck setter` 共享牌堆。

### ✅ 文件级命名空间
新文件统一使用 `file_scoped` (`namespace X;`)，`using` 放在 namespace 外部。

### ✅ 本地化所有用户可见文本
所有 UI 文本必须使用 `Localization.T("key", "默认中文")`，同时更新 `zh.yaml` 和 `en.yaml`。订阅 `GameManager.LanguageChanged` 动态刷新。

### ✅ 英雄技能预留
IHeroPower 接口已定义但为空，为未来英雄技能留设计位置。

### ⬜ Spell.cs 死代码
CombatManager 对所有卡牌使用 Card 基类，Spell 类从未实例化，待清理。

## 游戏循环

```
主菜单 → 地图（选择房间）→ 战斗 → 胜负判定 → 奖励 → 地图 → ... → Boss → 通关/失败
```

## 场景

| 场景 | 说明 | 状态 |
|------|------|------|
| 主菜单 | 入口场景 | ✅ |
| 战斗 | 战斗场景（程序化 UI） | ✅ |
| 地图 | 路线选择 | 🚧 基础实现 |

## Autoload 单例

| 名称 | 用途 |
|------|------|
| `GameManager` | 全局状态，跨战斗持久化，语言切换 |
| `UIScaler` | UI 缩放，基准 1152×648 |
| `ChatScreen` | 开发者控制台，`` ` `` 键呼出 |

## ChatScreen 命令

| 命令 | 功能 |
|------|------|
| `/damage N` | 对敌方英雄造成 N 点伤害。加 `-c` 进入点击模式。 |
| `/damage_enemy N` | 同上（显式） |
| `/damage_self N` | 对己方英雄造成 N 点伤害 |
| `/damage_eslot X N` | 对敌方槽位 X(0-4) 随从造成 N 点伤害 |
| `/damage_pslot X N` | 对己方槽位 X(0-4) 随从造成 N 点伤害 |
| `/damage_all N` | 对所有敌方随从造成 N 点伤害 |
| `/draw N` | 抽 N 张牌 |
| `/mana N` | 获得 N 点法力 |
| `/heal N` | 恢复 N 点生命值 |
| `/armor N` | 获得 N 点护甲 |
| `/end` | 强制结束回合 |
| `/refresh` | 刷新战斗 UI |
| `/clear` | 清空控制台输出 |
| `/token <card_id>` | 将指定 ID 的卡牌加入手牌 |
| `/play <card_id>` | 从手牌打出领域/无目标法术 |
| `/summon_player <card_id> <slot>` | 在己方槽位直接召唤随从（QA） |
| `/unlock_all` | 解锁全部卡牌加入收藏 |
| `/intent_debug` | 显示当前敌方意图目标（QA） |
| `/qa_tombstone` | 验证墓碑伤害结算（QA） |
| `/qa_bait_tactics` | 验证诱饵战术双阵营触发（QA） |
| `/fight <enemy>` | 直接与指定敌人战斗（跳过地图） |
| `/help` | 显示帮助 |
