# 1.4 战斗流程测试 Spec

## Why
目前游戏从主菜单进入战斗场景后，战斗流程没有正确初始化。需要创建测试入口并验证完整战斗流程能正常运行。

## What Changes
- 修改Combat.tscn场景，正确初始化战斗
- 创建战斗入口代码，连接GameManager和CombatManager
- 验证完整战斗流程（玩家回合→敌人回合→循环）
- 添加战斗结果处理

## Impact
- Affected specs: 依赖1.1卡牌效果系统、1.2初始卡组初始化、1.3敌人数据资源
- Affected code: 
  - Scenes/Combat.tscn
  - Scripts/Combat/CombatManager.cs
  - Scripts/Core/GameManager.cs

## ADDED Requirements

### Requirement: 战斗场景初始化
战斗场景加载时，SHOULD自动初始化玩家和敌人，并开始战斗。

#### Scenario: 玩家进入战斗
- **WHEN** 玩家从主菜单点击开始游戏
- **THEN** 战斗场景加载 → 初始化玩家（从GameManager获取）→ 初始化敌人（从资源加载）→ 开始战斗

### Requirement: 回合流程
玩家和敌人按照回合制进行战斗。

#### Scenario: 玩家回合
- **WHEN** 玩家回合开始
- **THEN** 玩家抽5张牌 → 恢复能量 → 可以打牌或结束回合

#### Scenario: 敌人回合
- **WHEN** 玩家点击结束回合
- **THEN** 玩家手牌进入弃牌堆 → 敌人执行动作 → 玩家检查死亡 → 新回合开始

### Requirement: 战斗结束检测
战斗结束时显示结果。

#### Scenario: 战斗胜利
- **WHEN** 所有敌人血量≤0
- **THEN** 显示"VICTORY!"，战斗结束

#### Scenario: 战斗失败
- **WHEN** 玩家血量≤0
- **THEN** 显示"DEFEAT!"，战斗结束

## MODIFIED Requirements

### Requirement: CombatManager初始化
**现有**：CombatManager.Initialize()需要手动调用
**修改为**：Combat场景_Ready时自动初始化并开始战斗
