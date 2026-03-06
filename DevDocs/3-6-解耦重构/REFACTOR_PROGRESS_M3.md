# M3 玩家回合全迁移 - 进度报告

## 完成日期
2026-03-06

## 修改文件清单

### 新建文件

| 文件 | 说明 |
|-----|------|
| `Scripts/Presentation/Presenters/CombatEventPresenter.cs` | 事件分发器，将 CombatEvent 分发到 UI 组件 |

### 修改文件

| 文件 | 改动说明 |
|-----|---------|
| `Scripts/Combat/CombatManager.cs` | 添加 `UseNewCombatEngineForPlayerTurn` Feature Flag，初始化 DomainCombatEngine |

## 实现内容

### Feature Flag 机制
- `UseCommandPipeline`: 控制 UI 是否走命令通道（M1 已实现）
- `UseNewCombatEngineForPlayerTurn`: 控制玩家回合是否使用新 Domain 引擎

### CombatEventPresenter
- 订阅 `CombatApplicationService.OnEvent`
- 将事件分发到对应 UI 组件
- 支持的事件类型：
  - `UnitDeployedEvent` -> `OnUnitDeployed`
  - `UnitMovedEvent` -> `OnUnitMoved`
  - `DamageAppliedEvent` -> `OnDamageApplied`
  - `UnitDestroyedEvent` -> `OnUnitDestroyed`
  - `TurnStartedEvent` -> `OnTurnStarted`
  - `TurnEndedEvent` -> `OnTurnEnded`
  - `CombatEndedEvent` -> `OnCombatEnded`

## 验证结果

### 编译验证
```
dotnet build OdysseyCards.sln
```
- **结果**: ✅ 通过
- **警告**: 仅性能优化建议，无错误

### 功能验证
- Feature Flag 机制已实现 ✅
- DomainCombatEngine 已初始化 ✅
- CombatEventPresenter 已创建 ✅

## 风险与回滚点

### 风险
1. **Domain 引擎与 Legacy 引擎并存**: 当前两套引擎同时存在，需要后续完全切换到 Domain 引擎
2. **UI 事件订阅未完成**: UI 组件尚未订阅 CombatEventPresenter 的事件

### 回滚点
- Git commit: `refactor(m3): add Feature Flag and CombatEventPresenter`
- 可通过 `git revert HEAD` 回滚所有 M3 改动

## 下一步计划 (M4)

1. 重构 EnemyAI 接口产出命令
2. 修改 CombatManager 敌方回合执行
3. 创建一致性测试框架
4. 验证新旧引擎行为一致

## 验收标准达成情况

| 标准 | 状态 |
|-----|------|
| Feature Flag 已实现 | ✅ |
| UI 命令通道已迁移 | 部分完成（M1 已完成 EndTurn 和 DeployUnit） |
| UI 事件驱动刷新 | 框架已创建，UI 订阅待完成 |
| 进度文档已输出 | ✅ |
| Git commit 已提交 | 待提交 |
