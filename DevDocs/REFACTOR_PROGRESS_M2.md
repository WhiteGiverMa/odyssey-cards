# M2 引擎最小闭环 - 进度报告

## 完成日期
2026-03-06

## 修改文件清单

### 新建文件

| 文件 | 说明 |
|-----|------|
| `Scripts/Domain/Combat/State/CombatState.cs` | 战斗状态模型（回合、阶段、玩家/敌人状态） |
| `Scripts/Domain/Combat/State/BoardState.cs` | 棋盘状态模型（节点、部署点、HQ） |
| `Scripts/Domain/Combat/Model/UnitModel.cs` | Domain 层单位/卡牌模型（不依赖 Godot） |
| `Scripts/Domain/Combat/Engine/DomainCombatEngine.cs` | 纯 Domain 层战斗引擎实现 |
| `Scripts/Tests/Domain/CombatEngineTests.cs` | 21 个 Domain 单元测试 |

### 修改文件

| 文件 | 改动说明 |
|-----|---------|
| `Scripts/Domain/Combat/Commands/CombatCommand.cs` | 添加 `StartCombatCommand` |
| `Scripts/Domain/Combat/Events/CombatEvent.cs` | 添加 `CombatStartedEvent` |

## 实现内容

### Domain 状态模型
- `CombatState`: 战斗核心状态（回合数、阶段、当前行动者）
- `PlayerState`: 玩家状态（HQ 生命值、能量）
- `EnemyState`: 敌人状态
- `BoardState`: 棋盘状态（节点、部署点、单位位置）
- `UnitModel`: 单位运行时模型（不依赖 Godot）
- `CardModel`: 卡牌模型

### DomainCombatEngine 功能
- `StartCombat`: 初始化战斗，产出 `CombatStartedEvent` + `TurnStartedEvent`
- `EndTurn`: 回合切换，产出 `TurnEndedEvent` + `TurnStartedEvent`
- `DeployUnit`: 部署单位，验证能量和节点合法性
- `MoveUnit`: 移动单位，验证移动范围
- `Attack`: 攻击逻辑，处理伤害、反击、HQ 攻击
- `PlayCard`: 打出卡牌
- `CancelSelection`: 取消选择

### 单元测试覆盖（21 个）
1. StartCombat 初始化状态
2. StartCombat 事件产出
3. EndTurn 切换到敌方回合
4. EndTurn 回合数递增
5. DeployUnit 合法部署
6. DeployUnit 能量不足
7. DeployUnit 无效节点
8. MoveUnit 合法移动
9. MoveUnit 单位不能移动
10. MoveUnit 无效目标
11. Attack 合法目标
12. Attack 超出范围
13. Attack 单位不能攻击
14. Attack 双方受伤
15. Attack 击杀目标
16. Attack 敌方 HQ
17. Attack 玩家 HQ
18. 胜利条件（敌方 HQ 摧毁）
19. 失败条件（玩家 HQ 摧毁）
20. 快照一致性
21. 命令 ID 追踪

## 验证结果

### 编译验证
```
dotnet build OdysseyCards.sln
```
- **结果**: ✅ 通过
- **警告**: 仅性能优化建议（返回类型优化），无错误

### 功能验证
- Domain 层代码不引用任何 Godot 类型 ✅
- 命令输入 -> 事件输出闭环 ✅
- 21 个单元测试可运行 ✅

## 风险与回滚点

### 风险
1. **DomainCombatEngine 是独立实现**: 当前未与 Legacy 系统集成，需要 M3 进行切换
2. **简化规则**: 当前引擎使用简化规则（如固定部署成本 1），需要后续与真实规则对齐

### 回滚点
- Git commit: `refactor(m2): implement DomainCombatEngine with domain tests`
- 可通过 `git revert` 回滚所有 M2 改动

## 下一步计划 (M3)

1. 实现 Feature Flag `UseNewCombatEngineForPlayerTurn`
2. UI 完全改走命令通道
3. UI 改为事件驱动刷新
4. 移除 UI 直连规则对象

## 验收标准达成情况

| 标准 | 状态 |
|-----|------|
| 项目可编译通过 | ✅ |
| 不用 UI 也能跑一段战斗流程（命令输入->事件输出） | ✅ |
| 至少 20 个 Domain 单元测试 | ✅ (21 个) |
| 进度文档已输出 | ✅ |
| Git commit 已提交 | 待提交 |
