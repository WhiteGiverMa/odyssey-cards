# Scripts/Heat — 热力系统

## Scope

每场战斗的全局节奏压力。Heat 影响伤害倍率，但不是卡牌状态、不是藏品、不是 UI 效果。

## Files

| 文件 | 职责 |
|------|------|
| `HeatSystem.cs` | 热力值、倍率、回合推进 |
| `HeatDamageModifier.cs` | 作为 DamageResolver 的 HEAT 阶段 modifier 接入 |

## Rules

- Heat 阶段位于 `MULTIPLICATIVE` 之后、`CAPPING` 之前。
- Heat 是战斗级状态；不要挂到 Card/Minion/Hero。
- 意图预览也必须经过 Heat 阶段，保证显示与实际结算一致。
- UI 展示走 `HeatBar`，规则变化不写在 UI。

## Anti-Patterns

- 禁止在 `DamageResolver` 里硬编码热力公式；用 `HeatDamageModifier`。
- 禁止把 Heat 作为普通 StatusEffect 实现。
- 禁止让藏品直接改 Heat 内部字段；通过清晰接口或战斗事件。
