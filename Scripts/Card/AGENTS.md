# Scripts/Card — 运行时卡牌、英雄、随从与武器

## Scope

纯运行时模型层。这里描述卡牌实例、英雄/随从身体、状态、领域、武器和英雄技能；数据来自 `Scripts/Core/CardData.cs`，展示由 `Scripts/UI/` 负责，结算编排由 `Scripts/Combat/` 负责。

## Map

| 文件 | 职责 | 注意 |
|------|------|------|
| `Card.cs` | 运行时卡牌基类 | 包装 `CardData`；持有临时费用/领域触发等运行时修饰 |
| `Minion.cs` | 随从身体 | `IDamageSource` + `IDamageTarget`；贴膜式治疗可抬高 MaxHealth |
| `Hero.cs` | 英雄身体 | 包装 `CommanderCore`；护甲、武器、状态、领域入口 |
| `Weapon.cs` | 武器实例 | 攻击力、花费、被动/主动技能组合 |
| `WeaponSkill.cs` | 武器接口与具体实现集合 | 新武器默认加在这里；不要仿照孤立的 `RailPistolPassive.cs` |
| `StatusEffect.cs` | 临时状态 | `ITemporaryEffect`；`TickOn` 衰减；`Polarity` 决定净化筛选 |
| `ActiveDomain.cs` | 领域运行时数据 | `IPermanentEffect`；同 ID 叠层，Counter 消耗 |
| `IHeroPower.cs`, `HeroPowers/*` | 英雄技能 | `IChargeCooldownSkill` 表示可存储冷却层数 |
| `Spell.cs` | 旧法术类型 | 死代码；运行时统一走 `Card` |

## Runtime Rules

- `CardData` 不存战斗临时状态；费用修正、领域触发层数等运行时状态放在 `Card`。
- 复制/转移卡牌实例时，新增运行时字段必须接入 `CopyRuntimeModifiersFrom()`。
- `Hero` 和 `Minion` 的易伤/虚弱/脆弱等通过长期存在的 modifier 实例 + 状态层数条件生效；不要直接在伤害公式里写状态名。
- `StatusEffect` 是临时效果，`ActiveDomain` 是永久领域；不要用普通状态模拟领域。
- 英雄治疗受最大生命限制；随从治疗允许突破并抬高 MaxHealth。

## Weapon / Skill Rules

- 被动接口按触发点细分：普通武器攻击、友方随从替代攻击、命中后效果不要混在一个 if 链里。
- 主动技能需要目标时由 UI/Combat 选择目标，技能类只实现可用性与效果。
- 带冷却和可存储层数的技能实现 `IChargeCooldownSkill`；回合推进只调用统一的 tick 入口。
- 新英雄技能在 `HeroPowers/` 建类，再通过 `HeroProfile.CreateHeroPower` 接入。

## Anti-Patterns

- 禁止新增继承 `Spell` 的运行时路径。
- 禁止新建孤立武器文件来绕过 `WeaponSkill.cs` 既有接口体系。
- 禁止在 Card 层直接操作 UI、场景树或发起选择流程。
- 禁止混淆 `Hero` 包装的 `CommanderCore` 与 `Player` / `CombatManager` 各自持有的 core。
