# Rapid Expansion — 2026-06-08

> **分支**: `feature/rapid-expansion`
> **基线**: `main` (`394871f`)
> **构建**: ✅ 0 错误 0 警告 | 新增 ~1800 行

## 概述

一次性补齐了 4 个关键子系统 + 金币经济系统，将地图上全部 3 个占位符房间（Shop/RestSite/Event）从"尚未实现"替换为完整功能，同时实现了英雄技能系统。

## 改动清单

### 新建文件（6 个）

| 文件 | 说明 |
|------|------|
| `Scripts/Card/HeroPowers/IronWillHeroPower.cs` | 英雄技能：铁腕（2费+4护甲），每回合一次 |
| `Scripts/Roguelike/BlessingData.cs` | 金血祝颂数据模型 + 3 个占位祝福 |
| `Scripts/Roguelike/EventData.cs` | 事件数据模型 + 5 个叙事事件定义 |
| `Scripts/UI/ShopUI.cs` | 商店界面——卡牌列表 + 金币购买 |
| `Scripts/UI/RestSiteUI.cs` | 休息站界面——治疗 + 3 选 1 祝福 |
| `Scripts/UI/EventUI.cs` | 事件界面——叙事 + 选择 + 结果 |

### 修改文件（10 个）

| 文件 | 改动 |
|------|------|
| `Scripts/Core/GameManager.cs` | RunGold 属性 + AddGold/SpendGold 方法 + 新 run 重置 + 保存/加载 |
| `Scripts/Core/GameSaveData.cs` | RunGold 序列化字段 |
| `Scripts/Character/Player.cs` | HeroPower 属性 |
| `Scripts/Combat/CombatManager.cs` | 战后金币奖励 + TryUseHeroPower + StartPlayerTurn 重置 |
| `Scripts/UI/CombatUI.cs` | 英雄技能按钮（热键 H）+ UpdateHeroPowerButton 刷新 |
| `Scripts/UI/MapUI.cs` | Shop/RestSite/Event 三路房间路由 + ShowShopRoom/ShowRestSiteRoom/ShowEventRoom |
| `Scripts/Infrastructure/OdysseyInput.cs` | HeroPower 动作常量 |
| `Scripts/Infrastructure/InputManager.cs` | HeroPower 默认键位 H |
| `Resources/Localization/zh.yaml` | 英雄技能 + 商店 + 休息站 + 祝福 + 事件 本地化键 |
| `Resources/Localization/en.yaml` | 同上（英文） |

---

## 各系统详细

### 1. 金币经济系统

- `GameManager.RunGold`（int）——每局重置为 0，可在存档中持久化
- `GameManager.AddGold(int)`——添加金币
- `GameManager.SpendGold(int) → bool`——消费金币，余额不足返回 false
- **战后奖励**：Monster 10-15G / Elite 25-35G / Boss 50G（随机）
- 存档持久化：`GameSaveData.RunGold`

### 2. 英雄技能——铁腕

- **费用**：2 法力
- **效果**：获得 4 点护甲
- **限制**：每回合一次（`_heroPowerUsedThisTurn` 在 StartPlayerTurn 重置）
- **UI**：CombatUI 底部 PlayerArea 新增按钮，显示名称+费用/冷却状态
- **热键**：H 键（`OdysseyInput.HeroPower` → `InputManager` 默认绑定）
- **刷新**：按钮 disabled 状态实时反映（已用/法力不足/非玩家回合）

数据流：
```
GameManager.CreateNewPlayer() → Player.HeroPower = IronWillHeroPower
    ↓
CombatManager.Initialize() → PlayerHero.HeroPower = Player.HeroPower
    ↓
CombatUI 按钮/H 键 → CombatManager.TryUseHeroPower()
    → IronWillHeroPower.Execute(hero, this) → GainArmor(4) + SpendMana(2)
```

### 3. 休息站 + 祝福系统

- **治疗**：点击"回复 30% 生命值"按钮，治疗 `MaxHealth × 0.3`（一次性）
- **祝福选择**：3 选 1 金血祝颂占位符
  - 活力祝颂：战斗开始时获得 2 点法力
  - 坚韧祝颂：获得 3 点最大生命值
  - 狂怒祝颂：每回合首次攻击 +1 伤害
- **当前状态**：祝福选择仅记录日志（`GD.Print`），效果为占位符，后续实现具体逻辑
- **UI 模式**：MobileDialogHost 弹窗（与 Treasure 房间一致）

### 4. 商店

- **卡牌池**：从 `GetRewardEligibleCards()` Fisher-Yates 洗牌取 5 张
- **价格**（基于稀有度）：Common=50G / Good=75G / Excellent=100G / Master=150G
- **购买**：扣除金币 → 卡牌加入牌堆（`AddCardToDeckInCombat`）
- **UI 行**：卡名（稀有度色）+ 法力费用 + 类型标签 + 随从属性 + 描述 + 购买按钮
- **保护**：金币不足时按钮禁用，已购买显示"已购买"

### 5. 叙事事件

5 个事件，每个 2-3 个选择：

| 事件 | 选择 | 效果 |
|------|------|------|
| 神秘商人 | 买卡(50G)/卖卡(+40G)/离开 | 金币 ↔ 卡牌 |
| 古老神龛 | 祈祷(+5HP)/献祭(-3HP+15G)/无视 | 生命值 ↔ 金币 |
| 流浪铁匠 | 强化(+5G)/锻造 | 随机藏品（小风扇/好梦抱枕） |
| 命运之轮 | 转盘/绕行 | 30%+20G / 30%-10G / 20%+5HP / 20%无事 |
| 篝火旅人 | 分享(-15G换卡)/听故事/抢劫(+25G) | 多种交互 |

事件系统支持条件结果——Execute 可动态修改 ResultText（如"金币不足"）。

---

## 未完成 / 后续工作

- ⚠️ 金血祝颂祝福效果为占位符（仅日志），需后续实现具体 buff 逻辑
- ⚠️ 英雄技能仅"铁腕"一个——后续角色/职业扩展需更多实现
- ⚠️ Spell.cs 仍是死代码
- ⚠️ EventSelector.cs 仍是死代码（被新 EventData 系统取代）
- ⚠️ 没有卡牌升级系统——用户计划后续做崩铁差分宇宙式金血祝颂系统

## 验证

- ✅ `dotnet build` — 0 errors, 0 warnings
- ✅ LSP 诊断 — 仅项目既有 warning（CS8632 nullable / CS0618 deprecated）
- ⚠️ godot-mcp 表面 QA 待用户手工验证
