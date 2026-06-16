# Scripts/Core — 数据、伤害、存档与全局状态

## Scope

核心服务与契约层。这里定义 Godot Resource 数据、伤害管线接口、存档结构、英雄配置与全局 GameManager；战斗规则执行仍优先落到 `Scripts/Combat/`。

## Map

| 区域 | 文件 | 注意 |
|------|------|------|
| 全局入口 | `GameManager.cs` | Autoload；卡牌注册表、语言、跨战斗状态；新增卡牌同步 `CardResourcePaths[]` |
| 主菜单 | `MainMenu.cs`, `HeroProfile.cs` | 英雄选择、旧英雄 ID 映射、开始冒险入口 |
| 卡牌静态数据 | `CardData.cs`, `ICardData.cs`, `CardEffectData.cs` | Godot Resource + `[Export]`；运行时由 `Card/Card.cs` 包装 |
| 伤害管线 | `DamageResolver.cs`, `DamageContext.cs`, `IDamage*.cs` | `ADDITIVE → MULTIPLICATIVE → HEAT → CAPPING`；`DamageKind.Effect` 忽略防御 |
| 伤害修改器 | `*DamageModifier.cs`, `FragileArmorModifier.cs` | source/target 两侧都可提供 modifier；护甲吸收在管线后 |
| 牌组合法性 | `DeckValidityService.cs` | 构筑 10..20；战斗牌堆可突破构筑上限；不静默截断旧档 |
| 存档 | `SaveDataManager.cs`, `GameSaveData.cs`, `RunSaveData.cs` | `user://save.json`；selected hero / active run / deck snapshot |
| 显示元数据 | `EffectIconTable.cs`, `RarityColorScheme.cs`, `TargetTags.cs` | UI 只读映射；规则不写在显示表里 |

## Contracts

- `CardData` 是编辑器/资源层；`Card.Card` 是纯运行时层。新增卡牌：`.tres` → `GameManager.CardResourcePaths[]` → 本地化 key → QA。
- `IDamageSource` / `IDamageTarget` 是 DamageResolver 边界；不要绕过它直接扣血。
- `IDamageModifier` 同时有 outgoing 与 incoming 两个入口；新增 modifier 必须明确阶段和作用侧。
- `DamageContext.IsPreview` 路径不得产生副作用、日志 spam 或状态变更。
- `IPermanentEffect` / `ITemporaryEffect` 只是生命周期标记；具体行为由 `ActiveDomain` / `StatusEffect` 等类型实现。

## Anti-Patterns

- 禁止在 `DamageResolver` 内硬编码某张卡、某个藏品或某个敌人的特例。
- 禁止用 wiki 数值直接改 `CardData` / `CardEffectData`；先查资源和运行时 UX。
- 禁止新增卡牌后只依赖 `DirAccess` 扫描；导出包必须能走硬编码回退。
- 禁止对旧档超限牌组静默截断；用 `DeckValidityService` 返回 invalid。
