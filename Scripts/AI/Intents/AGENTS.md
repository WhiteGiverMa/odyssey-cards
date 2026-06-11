# Scripts/AI/Intents — 新意图类型体系

## Scope

敌方意图的数据模型。这里描述“敌人准备做什么”，不直接操作 Godot UI，也不执行战斗结算。

## Map

| 文件 | 职责 |
|------|------|
| `AbstractIntent.cs` | 所有意图基类，显示/执行入口 |
| `IntentType.cs` | 新版意图枚举，UI 图标/tooltip 依赖它 |
| `MoveState.cs` | 敌人行动链，可组合多个 Intent |
| `AttackIntent.cs`, `SingleAttackIntent.cs`, `MultiAttackIntent.cs` | 攻击意图 |
| `DefendIntent.cs`, `BuffIntent.cs`, `DebuffIntent.cs`, `StatusIntent.cs` | 状态/防御 |
| `SummonIntent.cs`, `SpellCastIntent.cs`, `HealIntent.cs` | 非攻击行动 |
| `SleepIntent.cs`, `StunIntent.cs`, `EscapeIntent.cs`, `HiddenIntent.cs`, `UnknownIntent.cs` | 特殊显示/状态 |
| `IntentHoverTip.cs` | hover 文案模型 |

## Rules

- 意图不缓存静态伤害；使用 lambda / 运行时查询，战场变化时实时重算。
- MoveState 是“下一步行动”，不是敌人 AI 全状态机。
- 多意图敌人用 MoveState 组合，不回退到旧 `EnemyIntent` 枚举。
- UI 桥接仍在 `CombatUI`/`EnemyIntent`；新增类型要同步桥接、图标、tooltip。
- 动画期间由 CombatManager 冻结 UI，不在 Intent 内处理。

## Add Intent Checklist

1. 在 `IntentType.cs` 加枚举。
2. 新建具体 `*Intent.cs`，继承 `AbstractIntent`。
3. 在 `AbstractIntent`/图标映射里补显示元数据。
4. 在 `IntentIcon` / `IntentTooltip` 路径补图标和文案。
5. 在敌人 Brain/MoveState 中使用。
6. 不添加 legacy shim；旧路径只桥接已有系统。

## Anti-Patterns

- 禁止把目标 HP、护甲等快照写进 Intent 后长期持有。
- 禁止让 Intent 直接改 Board/Hero；执行仍走 Encounter/CombatManager。
- 禁止新增旧 `IntentAI` 风格枚举或 `legacy_pattern_*`。
