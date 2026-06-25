# tests — xUnit 单元测试与集成测试

## Scope

一个 csproj：`tests/csharp/OdysseyCards.Tests.csproj`（xUnit + coverlet + Roslynator），13 个测试文件（12 Unit + 1 Integration），~101 `[Fact]`。**无 CI 自动运行**，手动 `dotnet test`。

## 命名约定

- 项目：`OdysseyCards.Tests`，命名空间 `OdysseyCards.Tests.Unit` / `.Integration`
- 文件/类名：`<Module>Tests.cs` / `<Module>Tests`
- 方法名：`MethodName_Scenario_ExpectedResult`（如 `PlaceMinion_ValidSlot_SetsSlot`）
- 内联 stub：`private sealed class StubName`，helper：`private static`

## 测试模式

### RED phase（预期失败）
`InteractionFSMTests` 全部标记为 **预期失败**——FSM 是桩实现，尚未完成。注释写明 `RED phase: FSM not yet implemented`。**Agent 不要自行"修复"这些测试**，直到 FSM 正式完成。

### Skip 约定
需要 Godot 运行时的测试用统一文案：
```
[Fact(Skip = "需要 Godot 运行时 — 原因")]
```
当前 9 个 skip：`EvalGatewayTests`（7 个，反射/Variant 需要 Godot API）+ `CombatIntegrationTests`（2 个，CardData 继承 Resource）。

### 无 mock 框架
纯手动 stub + private sealed class。无 Moq/NSubstitute。不引入 mock 框架是主动设计选择——纯 C# 核心层足够简单，手写 stub 比 learn mock API 更快。

## 覆盖分布

| 模块 | 测试文件 | 覆盖范围 |
|------|----------|---------|
| Core/ | `DamageResolverTests`, `KeywordTests` | 伤害管线、关键字枚举 |
| Combat/ | `GameStateTests`, `BoardTests`, `VictoryDefeatResolverTests` | 回合状态机、棋盘、多敌判定 |
| UI/（部分） | `InteractionFSMTests`（RED phase）, `CardFlyVfxTests`, `MinionDeployVfxTests` | 交互状态机、贝塞尔数学 |
| Card/ | `AyameHeroTests`, `SokouHeroTests` | HeroProfile 配置、武器 |
| Infrastructure/ | `EvalGatewayTests` | C# 反射求值网关 |
| Localization/ | `YamlParserTests` | YAML 解析 + tab 缩进回归 |

**0 覆盖的模块**：AI/（29 文件）、Heat/（2）、Relic/（7）、Roguelike/（5）、Character/（5）、Card/HeroPowers/（4）、Infrastructure 剩余（~16）、UI 剩余（~35）。

## Anti-Patterns

- 不要删除 RED phase 测试——它们标记了未完成的 FSM。
- 不要把 `[Fact(Skip = "...")]` 改成 `[Fact]` 然后在 Godot 外跑——会 crash。
- 不要引入 Godot API 依赖到纯 C# 测试中——保持纯框架可运行。
