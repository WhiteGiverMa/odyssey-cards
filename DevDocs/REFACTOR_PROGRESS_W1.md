# OdysseyCards 重构进度报告 W1 (M0+M1)

> 完成时间: 2026-03-06
> 里程碑: M0 架构基线 + M1 输入命令化

---

## 一、修改文件清单

### 新建文件 (10 个)

| 文件路径 | 说明 |
|---------|------|
| `Scripts/Domain/Combat/Commands/CombatCommand.cs` | 命令基类与 6 种子命令 |
| `Scripts/Domain/Combat/Events/CombatEvent.cs` | 事件基类与 8 种子事件 |
| `Scripts/Domain/Combat/Engine/ICombatEngine.cs` | 战斗引擎接口定义 |
| `Scripts/Application/Combat/CombatApplicationService.cs` | 应用服务入口 |
| `Scripts/Presentation/Input/CombatInputAdapter.cs` | UI 输入适配器 |
| `Scripts/Infrastructure/Replay/JsonlReplayWriter.cs` | JSONL 命令日志写入器 |
| `Scripts/Legacy/Adapters/LegacyCombatAdapter.cs` | 旧代码桥接适配器 |
| `Scripts/Legacy/Adapters/LegacyCombatEngine.cs` | Legacy 引擎实现 |

### 修改文件 (4 个)

| 文件路径 | 改动说明 |
|---------|---------|
| `Scripts/Combat/CombatManager.cs` | 新增命令系统初始化，添加 `UseCommandPipeline` 开关 |
| `Scripts/UI/CombatUI.cs` | `OnEndTurnPressed()` 改为提交 `EndTurnCommand` |
| `Scripts/UI/HandUI.cs` | `OnCardDroppedOnNodeHandler()` 改为提交 `DeployUnitCommand` |
| `Scripts/UI/BattleMapUI.cs` | 节点选择逻辑适配命令通道 |

### 新建目录结构

```
Scripts/
├── Domain/
│   └── Combat/
│       ├── Commands/
│       ├── Events/
│       ├── Engine/
│       └── State/
│   └── Shared/
├── Application/
│   ├── Combat/
│   │   └── UseCases/
│   └── Ports/
├── Presentation/
│   └── Input/
├── Infrastructure/
│   └── Replay/
└── Legacy/
    └── Adapters/
```

---

## 二、验证结果

### 编译验证
- ✅ `dotnet build OdysseyCards.sln` 成功
- ⚠️ 1 个已存在的警告（Unit.TakeDamage 过时方法，非本次改动引入）

### 功能验证

| 验证项 | 结果 | 说明 |
|-------|------|------|
| 项目启动 | ✅ 通过 | 可正常进入战斗场景 |
| EndTurn 命令通道 | ✅ 通过 | 点击结束回合按钮，日志显示 `[CombatUI] EndTurn submitted via command pipeline` |
| DeployUnit 命令通道 | ✅ 通过 | 拖拽单位到部署节点，日志显示 `[HandUI] DeployUnit submitted via command pipeline` |
| 命令日志生成 | ✅ 通过 | 每局战斗在 `user://replays/` 生成 `.jsonl` 文件 |
| Legacy 桥接 | ✅ 通过 | 命令正确转发到旧 `CombatManager`，功能行为一致 |

### 架构验证

| 验证项 | 结果 | 说明 |
|-------|------|------|
| Domain 层无 Godot 引用 | ✅ 通过 | `Domain/` 目录下无 `using Godot` |
| Domain 层无 UI 引用 | ✅ 通过 | `Domain/` 目录下无 UI 命名空间引用 |
| 命名空间规范 | ✅ 通过 | 如 `OdysseyCards.Domain.Combat.Commands` |

---

## 三、架构变更说明

### 命令系统

```
UI (Presentation)
    │
    ▼ Submit(CombatCommand)
CombatInputAdapter
    │
    ▼ Submit(command)
CombatApplicationService
    │
    ▼ Submit(command) + WriteCommand(command)
ICombatEngine (LegacyCombatEngine)
    │
    ▼ ExecuteLegacy(command)
LegacyCombatAdapter
    │
    ▼ 转换为旧方法调用
CombatManager (Legacy)
```

### Feature Flag

- `CombatManager.UseCommandPipeline = true` (默认开启)
- 设为 `false` 可回退到旧路径

### 命令类型

| 命令 | 用途 | 已实现 |
|-----|------|-------|
| `EndTurnCommand` | 结束回合 | ✅ |
| `DeployUnitCommand` | 部署单位 | ✅ |
| `MoveUnitCommand` | 移动单位 | ✅ |
| `AttackCommand` | 攻击目标 | ✅ |
| `CancelSelectionCommand` | 取消选择 | ✅ |
| `PlayCardCommand` | 打出卡牌 | ✅ |

### 事件类型

| 事件 | 用途 | 已实现 |
|-----|------|-------|
| `TurnStartedEvent` | 回合开始 | ✅ |
| `TurnEndedEvent` | 回合结束 | ✅ |
| `UnitDeployedEvent` | 单位部署 | ✅ |
| `UnitMovedEvent` | 单位移动 | ✅ |
| `DamageAppliedEvent` | 伤害应用 | ✅ |
| `UnitDestroyedEvent` | 单位销毁 | ✅ |
| `CombatEndedEvent` | 战斗结束 | ✅ |
| `SelectionCancelledEvent` | 选择取消 | ✅ |

---

## 四、下一步计划 (M2)

### 目标：新引擎骨架可跑

1. **实现纯 Domain 层 CombatEngine**
   - 不依赖 Legacy，直接处理命令
   - 状态管理：`CombatState`, `TurnState`, `BoardState`
   - 规则服务：`IEffectResolver`, `TargetingService`, `DamagePipeline`

2. **增加 Domain 单元测试**
   - 目标：至少 20 个测试用例
   - 覆盖：命令合法性、状态迁移、胜负判定

3. **实现事件驱动 UI 更新**
   - `CombatEventPresenter` 订阅事件
   - UI 仅消费事件刷新视图

4. **Feature Flag 切换**
   - `UseNewCombatEngineForPlayerTurn` 开关
   - 可在新旧引擎间切换

### 预计工作量

- 时间：1 周
- 新建文件：~15 个
- 修改文件：~5 个
- 测试用例：~20 个

---

## 五、风险与缓解

| 风险 | 状态 | 缓解措施 |
|-----|------|---------|
| 行为回归 | ⚠️ 监控中 | 命令日志 + 事件回放对比 |
| 双轨复杂度 | ✅ 可控 | M2 结束后删除旧路径 |
| UI 动画时序 | ⚠️ 待验证 | M2 引入事件分类 |

---

## 六、总结

M0+M1 里程碑已完成，建立了清晰的分层架构和命令-事件驱动模式。所有改动均可回滚，功能行为与旧版一致。下一步 M2 将实现纯 Domain 层引擎，为后续完全迁移奠定基础。
