# Scripts/AI — 敌方大脑、敌人注册与旧意图桥接

## Scope

AI 父目录管理 EnemyEncounter、Brain（具体敌人 AI）、EnemyRegistry、旧意图桥接 `IntentAI`。新意图数据模型在 `AI/Intents/`（已有 AGENTS.md），这里只覆盖父级。

## Map

| 文件                          | 职责                                                                                  | 注意                                               |
| ----------------------------- | ------------------------------------------------------------------------------------- | -------------------------------------------------- |
| `EnemyRegistry.cs`            | 敌人数据库——ID → 牌手实例                                                             | 所有遭遇入口只返回 `DeckDrivenCommander`           |
| `DeckDrivenCommander.cs`      | 通用同构牌手控制器 + `DeckDrivenCommanderSpec`                                        | 真实卡组/手牌/法力 + 实时评分贪心 AI；纯 C# 可构造 |
| `EnemyDeckProfiles.cs`        | 全部敌人预设工厂与厚度常量                                                            | 新增敌人只改这里，不要再写意图 Brain               |
| `EnemyEncounter.cs`           | 敌人总控——持有 Brain + Hero(身体)                                                     | 每个敌人一组                                       |
| `IntentAI.cs`                 | 旧意图 → 新 `EnemyIntent` 桥接                                                        | **禁止新增兼容层**；新意图用 Intents/ 体系         |
| `IIntentActor.cs`             | 意图执行者接口                                                                        | Actor = 有意图的敌方单位                           |
| `DefaultAttackMinionBrain.cs` | 敌方随从默认脑——无意图自动攻击                                                        | 敌方随从意图不降级为此                             |
| `*Brain.cs`                   | 具体敌人 AI：Cosmonaut, Goutansha, ShanHu, ZhangLang, SmartStinkyEgg, MechanicalRoach | 每个敌人写一个 Brain 类；Brain 负责 MoveState 链   |

## 敌人架构

```
EnemyUnit = Hero（身体）+ EnemyEncounter（大脑）
                ↓
           Brain（MoveState 链决策）+ Intents/ 的数据模型
```

- 当前全员同构：普通 / 精英 / Boss 都是 `DeckDrivenCommander`，差异只在卡组厚度（10~20 / 21~39 / 40）与 HP。`Cultist` / `SlimeBoss` / `WolfRider` / `ApprenticeMechanic` / `GuardianBoss` / `Goutansha` / `ZhangLang` / `ShanHu` / `Cosmonaut` 是旧意图死代码，退出主流程遭遇池，仅为兼容旧单测保留。
- 每个敌人是独立 actor：自己的 HP、MoveState 链、Intent。
- 多敌人协同通过事件/被动监听，不通过上帝对象直接指挥。
- 敌方随从意图 ≥ `DefaultAttackMinionBrain`，不降级为"无意图自动攻击"。

## 新增敌人 Checklist

1. 在 `EnemyRegistry.cs` 注册（JSON 配置：HP、Brain 类型等）
2. 写 Brain 类继承 `IBrain`（或现有基类），在 `Scripts/AI/` 建独立文件
3. 定义 MoveState 链（用 `AI/Intents/MoveState.cs` 组合多个 Intent）
4. 如需要新 Intent 类型：按 `AI/Intents/AGENTS.md` 的 Checklist
5. 资源化：`Resources/Enemies/` 建对应 .tres（结构桩当前为空）
6. ChatScreen QA：`/fight <enemyId>` 测试

## Anti-Patterns

- 禁止敌人 Brain 直接改 Board/Hero——走 Encounter 或被动事件。
- 禁止给 `IntentAI.cs` 加新 shim——新增 Intent 直接走 `Intents/` 体系。
- 禁止 Encounter 指挥所有敌人（上帝对象模式）——多敌人用事件/被动。
- 禁止把 Brain 逻辑塞进 `EnemyUnit.Body`（Hero 身体）——纯战斗数据，不含 AI。
