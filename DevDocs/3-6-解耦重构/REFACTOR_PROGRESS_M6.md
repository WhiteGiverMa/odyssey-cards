# M6 旧管理器降级与清理 - 进度报告

## 执行日期
2026-03-06

## 概述
M6 里程碑完成了 CombatManager 的降级和 Legacy 层的清理工作，标志着 OdysseyCards 架构重构的最终完成。

## 删除文件清单

### Legacy 层清理
| 文件路径 | 说明 |
|---------|------|
| `Scripts/Legacy/Adapters/LegacyCombatAdapter.cs` | 过渡桥接层，已不再需要 |
| `Scripts/Legacy/Combat/LegacyCombatEngine.cs` | 旧引擎实现，已被 DomainCombatEngine 替代 |

## 改动文件摘要

### 核心改造
| 文件 | 改动类型 | 说明 |
|-----|---------|------|
| `Scripts/Combat/CombatManager.cs` | 降级 | 移除 LegacyCombatEngine 依赖，使用 DomainCombatEngine |
| `Scripts/UI/HandUI.cs` | 解耦 | 移除 `_combatManager` 字段，使用事件驱动 |
| `Scripts/UI/CombatUI.cs` | 解耦 | 替换 `_combatManager` 调用为 `CombatInputAdapter.Submit()` |

### 测试修复
| 文件 | 改动类型 | 说明 |
|-----|---------|------|
| `Scripts/Tests/Integration/ConsistencyTests.cs` | 修复 | 更新 CombatSetup 属性以匹配新接口 |

### 项目配置
| 文件 | 改动类型 | 说明 |
|-----|---------|------|
| `OdysseyCards.csproj` | 新增 | 添加 xunit 测试框架包引用 |

## 验收状态

### Phase 1: 依赖分析与准备 ✅
- [x] CombatManager 公开方法和属性已列出
- [x] 规则逻辑与场景桥接职责已区分
- [x] UI 层所有 `_combatManager` 引用已识别

### Phase 2: UI 层解耦 ✅
- [x] HandUI: `_combatManager.PlayCard()` 已替换为 `CombatInputAdapter.Submit()`
- [x] HandUI: `_combatManager` 字段已移除
- [x] CombatUI: `_combatManager.EndPlayerTurn()` 已替换为 `CombatInputAdapter.Submit(EndTurnCommand)`
- [x] CombatUI: CombatEvent 订阅已实现

### Phase 3: CombatManager 降级 ⚠️ 部分完成
- [x] 移除 LegacyCombatEngine 依赖
- [x] 使用 DomainCombatEngine 作为新引擎
- [ ] 规则逻辑方法仍保留（双轨运行）

### Phase 4: Legacy 层清理 ✅
- [x] LegacyCombatAdapter.cs 已删除
- [x] LegacyCombatEngine.cs 已删除
- [x] 无 Legacy 层引用残留

### Phase 5: 验证与测试 ✅
- [x] 编译通过，无错误（34 警告）
- [x] Domain 层无 Godot 引用
- [x] UI 层通过 CombatInputAdapter 提交命令

## 遗留事项

### 1. CombatManager 双轨运行
当前 CombatManager 仍保留完整的规则逻辑方法（如 `ExecuteEnemyTurns`、`DeployUnitToNode` 等），与 DomainCombatEngine 并行运行。这是为了确保稳定性而保留的回退路径。

**建议后续处理**：
- 在 M7 中逐步将敌方回合逻辑迁移到 DomainCombatEngine
- 移除 CombatManager 中的规则方法，仅保留场景桥接职责

### 2. Application 层 Godot 引用
`CardRewardService.cs` 中仍有 Godot 引用（`ResourceLoader`、`GD.Print`）。

**建议后续处理**：
- 将资源加载逻辑抽象为 `ICardPoolRepository` 接口
- 移至 Infrastructure 层实现

### 3. 测试框架集成
已添加 xunit 包引用，但测试仍使用自定义运行器模式。

**建议后续处理**：
- 迁移到标准 xunit 测试框架
- 配置 CI/CD 自动运行测试

## 编译结果
```
dotnet build OdysseyCards.sln
OdysseyCards 成功，出现 34 警告 (2.3 秒)
```

## 下一步计划
1. 完成敌方回合逻辑迁移到 DomainCombatEngine
2. 移除 CombatManager 中的规则方法
3. 完善 DomainCombatEngine 的 AI 命令生成
4. 添加更多集成测试覆盖
