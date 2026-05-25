# M8 CombatManager 一次性切换进度报告

## 完成时间

2026-03-06

## 概述

M8 完成了 CombatManager 从双轨运行到完全使用 Domain 层引擎的一次性切换。

## 主要变更

### 1. 新增文件

| 文件 | 说明 |
|-----|------|
| `Presentation/Events/CombatEventProcessor.cs` | 事件处理器，将 Domain 事件映射到 UI 更新 |
| `Presentation/Events/CombatEventUIBridge.cs` | UI 桥接接口 |
| `Presentation/Events/DefaultCombatEventUIBridge.cs` | 默认 UI 桥接实现 |

### 2. 修改文件

| 文件 | 变更 |
|-----|------|
| `Combat/CombatManager.cs` | 完全重构，移除所有旧规则方法，使用 Command-Event 模式 |
| `UI/CombatUI.cs` | 更新事件处理方法签名 |
| `Map/BattleMap.cs` | 添加 PlayerHQNodeId/EnemyHQNodeId 属性 |

### 3. 移除的旧代码

- `ExecuteEnemyTurns()` 旧版（直接操作 Enemy/Unit）
- `DeployEnemyUnit()` - 敌方部署
- `ExecuteEnemyAttack()` - 敌方攻击
- `ExecuteEnemyOrder()` - 敌方指令卡
- `ExecuteAttack()` - 攻击结算
- `AttackEnemyHQ()` - 攻击敌方 HQ
- `AttackPlayerHQ()` - 攻击玩家 HQ

### 4. 新架构

```
CombatManager (Presentation Layer)
├── 接收用户输入
├── 生成 CombatCommand
├── 订阅 CombatEvent
└── 更新 UI

CombatApplicationService (Application Layer)
├── 协调战斗流程
├── 调用 DomainCombatEngine
└── 执行敌方 AI

DomainCombatEngine (Domain Layer)
├── 处理 Command
├── 更新 CombatState
└── 生成 Event
```

## 架构改进

### Before (双轨运行)

```
CombatManager
├── 旧轨道: 直接操作 Unit/Enemy/Player
└── 新轨道: DomainCombatEngine (未使用)
```

### After (单一轨道)

```
CombatManager (Presentation)
    ↓ 委托
CombatApplicationService (Application)
    ↓ 调用
DomainCombatEngine (Domain)
```

## 验收标准

| 标准 | 状态 |
|-----|------|
| CombatManager 无直接规则逻辑 | ✅ |
| 所有操作通过 Command | ✅ |
| 所有 UI 更新通过 Event | ✅ |
| dotnet build 无错误 | ✅ |
| 战斗流程完整可用 | ⏳ 需运行时验证 |

## 后续工作

1. **运行时验证** - 在 Godot 编辑器中测试完整战斗流程
2. **警告清理** - 当前 47 个警告，主要是性能优化建议
3. **移除旧 EnemyAI** - `Scripts/AI/EnemyAI.cs` 已不再被新代码使用

## Git 信息

- Commit: `feat(refactor): M8 CombatManager 一次性切换到 Domain 层引擎`
- Tag: `post-combatmanager-cutover`
