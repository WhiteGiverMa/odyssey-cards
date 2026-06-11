# Scripts/Relic — 藏品系统

## Scope

跨战斗/战斗内被动系统。藏品通过生命周期钩子响应事件，不把逻辑塞进 CombatManager。

## Map

| 文件 | 职责 |
|------|------|
| `AbstractRelic.cs` | 基类、ID/显示、生命周期钩子 |
| `RelicManager.cs` | 拥有、触发、持久化入口 |
| `*Relic.cs` | 具体藏品规则 |
| `UI/RelicBar.cs` | 展示层，不写规则 |
| `Resources/Relics/` | 资源化结构桩，当前为空 |

## Hooks

- 战斗开始/结束、回合开始/结束、伤害/出牌等事件优先以钩子表达。
- 需要 Combat 数据时传最小上下文，不传整个上帝对象。
- 多藏品顺序要稳定；若顺序影响结果，写明并测试。

## Add Relic Checklist

1. 新建具体 `*Relic`，继承 `AbstractRelic`。
2. 只覆写需要的生命周期钩子。
3. 在 `RelicManager` 注册/创建。
4. 如要显示，同步 `RelicBar` 和本地化。
5. 若资源化，补 `Resources/Relics/` 并处理导出路径。

## Anti-Patterns

- 禁止在 CombatManager 中写某个具体藏品的 if 分支。
- 禁止藏品直接操作 UI。
- 禁止把临时战斗状态永久写进藏品对象，除非它是设计上的跨战斗状态。
