# tests — xUnit 单元测试与集成测试

## Scope

一个 csproj：`tests/csharp/OdysseyCards.Tests.csproj`（xUnit + coverlet + Roslynator），34 个测试文件（33 Unit + 1 Integration）。**无 CI 自动运行**，手动 `dotnet test`。

## 命名约定

- 项目：`OdysseyCards.Tests`，命名空间 `OdysseyCards.Tests.Unit` / `.Integration`
- 文件/类名：`<Module>Tests.cs` / `<Module>Tests`
- 方法名：`MethodName_Scenario_ExpectedResult`（如 `PlaceMinion_ValidSlot_SetsSlot`）
- 内联 stub：`private sealed class StubName`，helper：`private static`

## 测试模式

### RED phase（预期失败）

历史 RED phase 的 `InteractionFSMTests` 对应 FSM 现已实现并通过。保留状态转换、并发取消与区域边界测试；三线交互把桌面/移动端拖拽阈值统一为 10px，测试同步锁定此契约。

### Skip 约定

需要 Godot 运行时的测试用统一文案：

```
[Fact(Skip = "需要 Godot 运行时 — 原因")]
```

当前 10 个 skip：`EvalGatewayTests`（8 个，反射/Variant 需要 Godot API）+ `CombatIntegrationTests`（2 个，CardData 继承 Resource）。

### 无 mock 框架

纯手动 stub + private sealed class。无 Moq/NSubstitute。不引入 mock 框架是主动设计选择——纯 C# 核心层足够简单，手写 stub 比 learn mock API 更快。

## 覆盖分布

| 模块            | 测试文件                                                                                                                                     | 覆盖范围                                                                                    |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Core/           | `DamageResolverTests`, `KeywordTests`                                                                                                        | 伤害管线、关键字枚举                                                                        |
| Combat/         | `GameStateTests`, `BoardTests`, `VictoryDefeatResolverTests`                                                                                 | 回合状态机、棋盘、多敌判定                                                                  |
| Combat/ 三线    | `AttackTargetRulesTests`, `ActionTrackerTests`, `AttackCombatResolverTests`, `IntelServiceTests`, `FrontlineAiTests`, `FrontlineDomainTests` | 射程/防线、整备行动双油费、反击、情报溢出、AI 前线阻挡、接化发；防线穷举 1,296 种组合       |
| 资源            | `FrontlineResourceTests`                                                                                                                     | 全 61 张资源显式迁移/效果引用、19 随从兵种/油费、5 侦察牌 Intel、双语卡牌 key 与无 Tab 门禁 |
| UI/（部分）     | `InteractionFSMTests`, `HandMotionMathTests`, `CardFlyVfxTests`, `MinionDeployVfxTests`                                                      | 交互状态机、静止命中及运动数学、贝塞尔曲线                                                  |
| Card/           | `AyameHeroTests`, `SokouHeroTests`                                                                                                           | HeroProfile 配置、武器                                                                      |
| Infrastructure/ | `EvalGatewayTests`                                                                                                                           | C# 反射求值网关                                                                             |
| Localization/   | `YamlParserTests`                                                                                                                            | YAML 解析 + tab 缩进回归                                                                    |

| AI/ / Roguelike/ | `CommanderAiTests`, `DeckDrivenEnemyTests` | 同构牌手：10~20 / 21~39 / 40 张三档卡组生成、重复上限自动放宽、核心池与随机池均严禁衍生牌（真实 81 张资源门禁）、遭遇池全员 `DeckDrivenCommander`、被动标记 |

**0 覆盖的模块**：Heat/（2）、Relic/（7）、Character/（5）、Card/HeroPowers/（4）、Infrastructure 剩余（~16）、UI 剩余（~35）。

## Anti-Patterns

- 不要删除 RED phase 测试——它们标记了未完成的 FSM。
- 不要把 `[Fact(Skip = "...")]` 改成 `[Fact]` 然后在 Godot 外跑——会 crash。
- 不要引入 Godot API 依赖到纯 C# 测试中——保持纯框架可运行。
