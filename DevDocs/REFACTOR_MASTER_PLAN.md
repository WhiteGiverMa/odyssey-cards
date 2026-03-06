# OdysseyCards 可推倒重构总计划（REFACTOR MASTER PLAN）

> 目标：从架构根开始重建分层，彻底建立**表现层（Presentation）**与**逻辑层（Application/Domain）**分离。  
> 策略：采用 **Strangler Fig（绞杀者）渐进替换**，允许大改，但每一步可回滚。  
> 背景痛点：`CombatManager` 过载；UI 输入（`HandUI/CombatUI/CardUI`）直连战斗逻辑；逻辑与 Godot 节点生命周期耦合。

---

## A. 重构目标与非目标

### A.1 重构目标（Goals）

1. **分层硬隔离**：表现层仅负责输入/显示，不包含规则决策。
2. **新战斗内核可独立运行**：可在无 Godot 场景下执行回合、指令、结算（便于单元测试/回放/AI）。
3. **命令-事件驱动**：输入统一变为 `CombatCommand`，状态变化统一产出 `CombatEvent`。
4. **可替换前端**：Godot UI 只是一个 Adapter；未来可接 CLI/AI/自动化测试。
5. **可回滚迁移**：每个里程碑都有保底回退点，不“一刀切停工”。

### A.2 非目标（Non-Goals）

1. **不在本轮重写全部玩法数值**（卡牌平衡、数值微调不是核心目标）。
2. **不在本轮完成全新美术/UI 改版**（仅做适配层改造）。
3. **不在本轮扩展完整 meta 玩法**（地图房间、商店、遗物系统可后续接入）。
4. **不追求首版网络同步**（先把本地内核边界做正确）。

---

## B. 目标架构（分层图 + 模块职责 + 依赖方向）

### B.1 分层图（目标）

```text
┌─────────────────────────────────────────────────────────────┐
│ Presentation Layer (Godot UI / Scene / Animation / Input)  │
│ - CombatUI, HandUI, BattleMapUI, CardUI                    │
│ - InputAdapter, ViewModelMapper, EventPresenter            │
└───────────────▲─────────────────────────────────────────────┘
                │ 仅通过 Ports / DTO / Events 通信
┌───────────────┴─────────────────────────────────────────────┐
│ Application Layer                                           │
│ - CombatApplicationService                                  │
│ - CommandDispatcher                                         │
│ - UseCases: StartCombat / SubmitCommand / EndTurn          │
│ - Anti-Corruption for legacy bridge                         │
└───────────────▲─────────────────────────────────────────────┘
                │ 调用 Domain 接口，不依赖 Godot
┌───────────────┴─────────────────────────────────────────────┐
│ Domain Layer (核心规则)                                     │
│ - CombatEngine, TurnSystem, ActionQueue                     │
│ - Aggregate: CombatState                                    │
│ - ValueObj: CombatCommand / CombatEvent                     │
│ - Services: IEffectResolver / Targeting / DamagePipeline    │
└───────────────▲─────────────────────────────────────────────┘
                │ 通过接口访问外部资源
┌───────────────┴─────────────────────────────────────────────┐
│ Infrastructure Layer                                        │
│ - GodotResourceCardRepository                               │
│ - EnemyDeckProvider / RewardProvider / SaveRepository       │
│ - Logger / ReplayStore / RandomProvider                     │
└─────────────────────────────────────────────────────────────┘
```

### B.2 模块职责

- **Presentation**
  - 接受玩家输入（拖拽、点击、按钮）并转译成 `CombatCommand`。
  - 订阅 `CombatEvent`，驱动 UI 刷新、动画与提示。
  - 不持有战斗规则状态，不直接调用 `Unit/Enemy/Player` 规则方法。

- **Application**
  - 负责编排：命令接收、会话生命周期、事务边界、调用引擎。
  - 做输入校验（格式、来源权限）与错误映射（用户提示）。
  - 不做具体战斗规则判断。

- **Domain**
  - 纯规则：回合推进、能否部署/移动/攻击、伤害结算、胜负判定。
  - 命令入、事件出；内部可用 ActionQueue 处理连锁效果。
  - 不依赖 Godot Node/Scene/Resource。

- **Infrastructure**
  - 实现领域端口：资源加载、存档、随机数、回放持久化、日志。
  - 可替换实现（Godot 版 / 测试内存版）。

### B.3 依赖方向（必须遵守）

1. `Presentation -> Application -> Domain`
2. `Infrastructure -> Domain Interface（实现）`
3. **禁止** `Domain -> Godot`、`Domain -> UI`、`UI -> Domain Entity` 直连。

---

## C. 新目录结构提案（具体到文件夹）

> 以 `Scripts/` 为根，按分层拆分（先并存，后迁移）。

```text
Scripts/
├── Presentation/
│   ├── Combat/
│   │   ├── CombatUI.cs
│   │   ├── HandUI.cs
│   │   ├── BattleMapUI.cs
│   │   ├── CardUI.cs
│   │   └── Presenters/
│   │       ├── CombatEventPresenter.cs
│   │       └── CombatViewModelMapper.cs
│   └── Input/
│       ├── CombatInputAdapter.cs
│       └── DragDropInputTranslator.cs
│
├── Application/
│   ├── Combat/
│   │   ├── CombatApplicationService.cs
│   │   ├── CombatCommandDispatcher.cs
│   │   ├── UseCases/
│   │   │   ├── StartCombatUseCase.cs
│   │   │   ├── SubmitCombatCommandUseCase.cs
│   │   │   └── EndTurnUseCase.cs
│   │   └── DTO/
│   │       ├── CombatSnapshotDto.cs
│   │       └── CommandResultDto.cs
│   └── Ports/
│       ├── ICombatSessionPort.cs
│       └── IReplayPort.cs
│
├── Domain/
│   ├── Combat/
│   │   ├── Engine/
│   │   │   ├── ICombatEngine.cs
│   │   │   ├── CombatEngine.cs
│   │   │   └── ActionQueue.cs
│   │   ├── Commands/
│   │   │   └── CombatCommand.cs
│   │   ├── Events/
│   │   │   └── CombatEvent.cs
│   │   ├── State/
│   │   │   ├── CombatState.cs
│   │   │   ├── TurnState.cs
│   │   │   └── BoardState.cs
│   │   ├── Services/
│   │   │   ├── IEffectResolver.cs
│   │   │   ├── EffectResolver.cs
│   │   │   ├── TargetingService.cs
│   │   │   └── DamagePipeline.cs
│   │   └── Model/
│   │       ├── Combatant.cs
│   │       ├── UnitModel.cs
│   │       └── CardRuntimeModel.cs
│   └── Shared/
│       ├── Result.cs
│       └── DomainError.cs
│
├── Infrastructure/
│   ├── Godot/
│   │   ├── Resources/
│   │   │   ├── GodotCardRepository.cs
│   │   │   └── GodotEnemyDeckProvider.cs
│   │   ├── Save/
│   │   │   └── GodotSaveRepository.cs
│   │   └── Logging/
│   │       └── GodotCombatLogger.cs
│   ├── Replay/
│   │   ├── JsonlReplayWriter.cs
│   │   └── JsonlReplayReader.cs
│   └── Random/
│       └── SeededRandomProvider.cs
│
├── Legacy/
│   ├── Combat/
│   │   └── CombatManager.LegacyBridge.cs
│   └── Adapters/
│       └── LegacyCombatAdapter.cs
│
└── Tests/
    ├── Domain/
    │   ├── CombatEngineTests.cs
    │   ├── EffectResolverTests.cs
    │   └── DamagePipelineTests.cs
    └── Integration/
        ├── CombatFlowIntegrationTests.cs
        └── ReplayConsistencyTests.cs
```

---

## D. 迁移策略（Strangler Fig + 回滚点）

### D.1 总体策略

采用 **Strangler Fig**：
- 新内核在 `Domain/Application` 并行建设。
- 旧 `CombatManager` 先保留，通过 `LegacyBridge` 转发部分流程。
- 按“输入口 → 规则口 → 事件口”逐段替换，不中断开发。

### D.2 关键开关（Feature Flags）

- `UseNewCombatInputPipeline`：UI 是否走命令通道。
- `UseNewCombatEngineForPlayerTurn`：玩家回合是否由新引擎驱动。
- `UseNewCombatEngineForEnemyTurn`：敌方回合是否由新引擎驱动。
- `UseNewRewardPipeline`：奖励是否由新应用层编排。

### D.3 回滚点（Rollback Points）

1. **R0（M1后）**：仅接入命令入口，规则仍旧。失败可一键切回旧 UI 调用路径。
2. **R1（M3后）**：玩家回合迁移完成。可回滚到 `CombatManager` 玩家逻辑。
3. **R2（M4后）**：敌方回合迁移完成。可仅回滚 AI 执行模块。
4. **R3（M5后）**：奖励与持久化迁移。可保留新战斗、回退奖励流程。
5. **R4（M6后）**：旧桥接删除前保留一个 tag（`pre-engine-cutover`）用于紧急恢复。

---

## E. 里程碑分解（M0~M6，含验收标准）

### M0 — 架构基线冻结（1周）
- 产出：边界文档、依赖规则、命名规范、事件字典。
- 验收标准：
  - 有可评审架构文档；
  - 新增代码不得出现 `Domain` 直接引用 `Godot`；
  - CI 增加层级依赖检查（可脚本化 grep 先行）。

### M1 — 输入命令化（1周）
- 产出：`CombatCommand` + `CombatInputAdapter` + UI 转译。
- 验收标准：
  - `HandUI/CombatUI/BattleMapUI` 不再直接调用核心规则方法；
  - 同一操作（拖拽部署）可记录为命令日志；
  - 功能行为与旧版一致（冒烟测试通过）。

### M2 — 新引擎骨架可跑（1周）
- 产出：`ICombatEngine`/`CombatEngine`/`CombatEvent` 最小闭环。
- 验收标准：
  - 能处理 StartCombat、PlayOrder、EndTurn 基础命令；
  - 事件序列稳定可断言；
  - 至少 20 个 Domain 单元测试。

### M3 — 玩家回合切换到新内核（1~2周）
- 产出：玩家出牌、部署、移动、攻击迁移。
- 验收标准：
  - 玩家回合全部由新引擎驱动；
  - UI 仅消费 `CombatEvent` 更新；
  - 与旧逻辑对比回放一致率 ≥ 95%。

### M4 — 敌方回合/AI 执行迁移（1~2周）
- 产出：AI 通过命令注入引擎，敌方动作事件化。
- 验收标准：
  - `EnemyAI` 不直接操作 `CombatManager`；
  - 敌方回合可单测；
  - 性能不低于旧版（同局帧耗时不回退超过 10%）。

### M5 — 奖励、存档、回放接入（1周）
- 产出：应用层编排奖励与持久化，回放可重演。
- 验收标准：
  - 战斗结束奖励由 Application 调度；
  - 回放可从命令日志复现关键事件；
  - 存档读取不依赖 `CombatManager` 内部状态。

### M6 — 旧管理器降级与清理（1周）
- 产出：`CombatManager` 变薄（壳层/适配器），移除关键旧路径。
- 验收标准：
  - 旧 `CombatManager` 不再承载规则；
  - 关键回归测试全绿；
  - 代码审查确认分层依赖无反向引用。

---

## F. 任务清单（按周）

### Week 1（对应 M0 + M1 启动）
- 建立 `Domain/Application/Presentation/Infrastructure` 目录骨架。
- 定义 `CombatCommand`/`CombatEvent` 基础模型。
- 建立 `CombatInputAdapter`，让 UI 输入转为命令。
- 增加命令日志（JSONL）用于对比回放。

### Week 2（M1 收尾 + M2）
- 落地 `ICombatEngine` 最小实现（Start/Submit/Events）。
- 接通玩家基础操作命令（部署、攻击、结束回合）。
- 增加 Domain 单测和第一版回放校验测试。

### Week 3（M3）
- 迁移玩家回合逻辑到新引擎。
- `CombatUI` 改为基于 `CombatEventPresenter` 刷新视图。
- 保留旧逻辑 feature flag 作为 fallback。

### Week 4（M4）
- 迁移敌方回合与 AI 交互。
- `EnemyAI` 输出命令而非直接执行。
- 做敌方回合一致性回放测试。

### Week 5（M5）
- 奖励发放、存档读写从 `CombatManager` 抽离到 Application/Infrastructure。
- 接入 `IRewardService` 与 `ISaveRepository`。

### Week 6（M6）
- 削减 `CombatManager` 到桥接层。
- 清理废弃事件与旧直连调用。
- 完成迁移总结与开发规范更新。

---

## G. 风险清单与缓解手段

| 风险 | 表现 | 缓解手段 |
|---|---|---|
| 行为回归 | 新旧逻辑细节不一致 | 命令日志 + 事件回放对比；关键战斗脚本金标测试 |
| 迁移期间双轨复杂度高 | 新旧代码并存难维护 | 里程碑结束必须删一批旧路径，禁止无限并存 |
| UI 动画时序错乱 | 事件驱动后动画触发节奏变化 | 事件分类：StateEvent vs VisualEvent，动画由 Presenter 做节流 |
| AI 行为异常 | 命令化后目标选择差异 | 保留 AI 快照测试（同局同 seed 输出一致） |
| 性能抖动 | 事件/对象分配增多 | 使用 struct/池化；基准测试跟踪每回合耗时 |
| 团队习惯反弹 | 新代码偷走捷径直连 UI | Code review 门禁：禁止 Domain 引用 Godot，禁止 UI 调规则实体 |

---

## H. 测试策略（单元/集成/回放测试）

### H.1 单元测试（Domain 为主）

覆盖目标：
- `CombatEngine`：命令合法性、状态迁移、胜负判定。
- `IEffectResolver`：效果结算顺序与叠加规则。
- `DamagePipeline`：加算/乘算/封顶阶段正确性。

最低标准：
- 核心规则分支覆盖率 > 80%。

### H.2 集成测试（Application + Infra）

覆盖目标：
- 从 `SubmitCombatCommandUseCase` 到 `CombatEvent` 输出链路。
- 资源加载、奖励发放、存档落盘。

最低标准：
- 典型 5 条战斗路径（先手/后手、部署、攻击、疲劳、结算）全绿。

### H.3 回放测试（Replay）

做法：
- 记录命令流（含 seed）。
- 重放后断言关键事件序列一致（伤害值、单位死亡、胜负结果）。

最低标准：
- 每次重构跑固定 20 份战斗回放，结果一致率 100%。

---

## I. “第一周立即执行”详细 TODO（文件级）

> 目标：1 周内把“UI 直连规则”切成“UI -> Command”。不改玩法，只改入口形态。

### I.1 新建文件

1. `Scripts/Domain/Combat/Commands/CombatCommand.cs`
   - 定义命令基类与子命令：`PlayCard`, `DeployUnit`, `MoveUnit`, `Attack`, `EndTurn`, `CancelSelection`。
2. `Scripts/Domain/Combat/Events/CombatEvent.cs`
   - 定义事件基类与子事件：`TurnStarted`, `CardPlayed`, `UnitMoved`, `DamageApplied`, `CombatEnded`。
3. `Scripts/Application/Combat/CombatApplicationService.cs`
   - 提供 `Submit(CombatCommand command)` 与 `GetSnapshot()`。
4. `Scripts/Presentation/Input/CombatInputAdapter.cs`
   - 将 UI 输入映射为命令对象。
5. `Scripts/Legacy/Adapters/LegacyCombatAdapter.cs`
   - 首周用旧 `CombatManager` 执行命令（过渡桥）。
6. `Scripts/Infrastructure/Replay/JsonlReplayWriter.cs`
   - 记录命令流（时间戳/seed/command payload）。

### I.2 修改文件

1. `Scripts/UI/HandUI.cs`
   - 删除直接 `_combatManager?.PlayCard(...)` 路径；
   - 改为调用 `CombatInputAdapter.Submit(...)`。
2. `Scripts/UI/CombatUI.cs`
   - EndTurn 按钮改为提交 `EndTurnCommand`；
   - 仅订阅事件更新 UI，不直接触发规则。
3. `Scripts/UI/BattleMapUI.cs`
   - 节点点击/拖放改为 `SelectNode` 或 `DeployUnit` 命令。
4. `Scripts/Combat/CombatManager.cs`
   - 增加过渡入口：`ExecuteLegacy(CombatCommand command)`；
   - 暂不删旧方法，仅作为兼容实现。

### I.3 本周验收清单

- [ ] 玩家部署单位可完成全流程（拖拽 -> 命令 -> 旧桥执行 -> UI 更新）。
- [ ] 玩家结束回合通过命令执行成功。
- [ ] 每局产生可读取的命令日志文件。
- [ ] 核心流程无新增 NullRef / 卡死。

---

## 附录 1：新战斗内核接口草案（C# 伪代码）

```csharp
namespace OdysseyCards.Domain.Combat;

public interface ICombatEngine
{
    CombatSnapshot StartCombat(CombatSetup setup, int seed);

    // 输入命令 -> 输出事件（可能多个）
    IReadOnlyList<CombatEvent> Submit(CombatCommand command);

    CombatSnapshot GetSnapshot();

    bool IsFinished { get; }
}

public abstract record CombatCommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public int Turn { get; init; }
    public int ActorId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record PlayCardCommand(int CardInstanceId, int? TargetNodeId, int? TargetUnitId) : CombatCommand;
public sealed record MoveUnitCommand(int UnitId, int ToNodeId) : CombatCommand;
public sealed record AttackCommand(int AttackerUnitId, int TargetNodeId, int? TargetUnitId) : CombatCommand;
public sealed record EndTurnCommand() : CombatCommand;
public sealed record CancelSelectionCommand() : CombatCommand;

public abstract record CombatEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid CausedByCommandId { get; init; }
    public int Turn { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record TurnStartedEvent(int ActiveActorId) : CombatEvent;
public sealed record CardPlayedEvent(int ActorId, int CardInstanceId) : CombatEvent;
public sealed record UnitDeployedEvent(int UnitId, int NodeId) : CombatEvent;
public sealed record UnitMovedEvent(int UnitId, int FromNodeId, int ToNodeId) : CombatEvent;
public sealed record DamageAppliedEvent(int? SourceUnitId, int? TargetUnitId, int TargetHQOwnerId, int Amount) : CombatEvent;
public sealed record CombatEndedEvent(int WinnerActorId, string Reason) : CombatEvent;

public interface IEffectResolver
{
    // 解析并产出后续事件（可内含连锁）
    IReadOnlyList<CombatEvent> Resolve(
        EffectContext context,
        CombatState state,
        CombatCommand command);
}
```

---

## 附录 2：老代码映射表（旧类 -> 新层归属 -> 迁移顺序）

| 旧类/文件 | 新层归属 | 迁移顺序 |
|---|---|---|
| `Scripts/Combat/CombatManager.cs` | Application（壳） + Domain（规则拆分） | 1（优先拆） |
| `Scripts/UI/CombatUI.cs` | Presentation | 2 |
| `Scripts/UI/HandUI.cs` | Presentation | 2 |
| `Scripts/UI/BattleMapUI.cs` | Presentation | 2 |
| `Scripts/UI/CardUI.cs` | Presentation | 2 |
| `Scripts/AI/EnemyAI.cs` | Domain Service（决策）+ Application（调度） | 4 |
| `Scripts/Card/Order.cs` | Domain Model + IEffectResolver（效果下沉） | 3 |
| `Scripts/Card/Unit.cs` | Domain Model（运行时实体） | 3 |
| `Scripts/Core/DamageResolver.cs` | Domain Service（保留并增强） | 3 |
| `Scripts/Map/BattleMap.cs` | Domain Model（BoardState/Pathing） | 3 |
| `Scripts/Map/MapNode.cs` | Domain Model | 3 |
| `Scripts/Map/MapEdge.cs` | Domain Model | 3 |
| `Scripts/Map/Headquarters.cs` | Domain Model | 3 |
| `Scripts/Character/Player.cs` | Domain Model（状态）+ Infra（持久化边界） | 4 |
| `Scripts/Character/Enemy.cs` | Domain Model | 4 |
| `Scripts/Core/GameManager.cs` | Application（会话/流程）+ Infra（存档） | 5 |
| `Scripts/Core/CardReward.cs` | Application Service（Reward） | 5 |
| `Scripts/Core/CardRewardData.cs` | Infrastructure（资源） | 5 |
| `Scripts/Card/CardFactory.cs` | Infrastructure Adapter（Resource -> Runtime） | 5 |
| `Scripts/Core/EnemyDeckData.cs` | Infrastructure（资源数据） | 5 |
| `Scripts/Core/UnitData.cs` / `OrderData.cs` | Infrastructure（数据定义） | 5 |
| `Scripts/Localization/*` | Presentation/Infrastructure（与规则解耦） | 6 |

> 建议执行原则：**先切输入口，再切引擎，再切资源与存档**。否则会陷入“新旧状态双向写入”的不可控复杂度。
