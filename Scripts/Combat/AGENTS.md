# Scripts/Combat — 战斗核心与拆分模块

## Scope

战斗规则中介层。这里允许同时看见纯 C# 模型、CombatManager、UI 回调，但新逻辑优先落到小系统，不继续膨胀 CombatManager。

## Map

| 文件 | 职责 | 注意 |
|------|------|------|
| `CombatManager.cs` | 战斗编排、玩家操作入口、场景树桥 | 仍是中介，不塞新规则 |
| `Board.cs` | 2×5 槽位、嘲讽、随从死亡事件 | 纯 C#；用方法改槽位 |
| `GameState.cs` | 回合、阶段、法力 | 纯 C#；法力与 CommanderCore 手动同步 |
| `EnemyUnit.cs` | Hero 身体 + EnemyEncounter 大脑 | 纯 C#；多敌人 actor |
| `CardEffectDispatcher.cs` | `EffectType` → handler | 新卡效果优先加这里 |
| `DomainTriggerManager.cs` | 领域触发点 | 不把领域逻辑塞 CombatManager |
| `AttackTracker.cs` | 随从攻击次数/风怒状态 | 小状态容器 |
| `DeathHandler.cs` | 死亡、亡语、牌堆回收 | 订阅 Board 事件 |
| `SelectionSystem.cs` | 发现、弃牌选择 | `TaskCompletionSource` + 回调 |
| `WeaponAttackSystem.cs` | 英雄/武器攻击 | 与 UI 攻击选择耦合 |
| `VictoryDefeatResolver.cs` | 胜负与奖励计算 | 不做 UI |
| `EmoteSystem.cs` | 表情定时 Node | 少数 Godot Node 子系统 |
| `CombatRuntimeQa.cs` | `/qa_*` 运行时验证 | 生产规则外置 |

## Boundaries

- `Board` / `GameState` / `EnemyUnit` 必须保持纯 C#，不调用 Godot API。
- `CombatManager` 可以接 UI/Node，但拆分出的规则模块默认纯 C#。
- 拆分模块通过构造注入 + `Action` 回调接 CombatManager；不要新增 Godot Signal。
- 新卡牌规则：数据在 `CardEffectData`，执行在 `CardEffectDispatcher`，长期触发在 `DomainTriggerManager`/藏品/状态。
- 新战斗事件先问：是卡牌效果、领域、藏品、死亡、选择、攻击、胜负，还是回合流？不要直接塞进主循环。

## Anti-Patterns

- 禁止直接写 `Board.PlayerSlots[index]` / `EnemySlots[index]`。
- 禁止在纯 C# 类里 `GD.Print`、`Node`、`ResourceLoader`。
- 禁止给 CombatManager 增加“临时兼容入口”。当前周期旧形状直接删除。
- 禁止在敌方回合动画期间刷新意图 UI；尊重 `IsEnemyTurnAnimating`。
- 禁止让 Encounter 指挥所有敌人；多敌人协同用事件/被动。

## QA

- 纯规则先补/跑 `dotnet test`。
- 运行时走 DevConsole：`/qa_tombstone`、`/qa_bait_tactics`、`/qa_new_cards`。
- MCP 可调 `/damage`、`/draw`、`/mana`、`/end`，拖拽/视觉仍需人工或编辑器预览。
