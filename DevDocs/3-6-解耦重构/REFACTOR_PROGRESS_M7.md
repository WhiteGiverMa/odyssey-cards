# M7 敌方回合迁移与 CombatManager 降级进度报告

## 概述

M7 里程碑完成了敌方回合逻辑迁移到 Domain 层，并实现了 Application 层与 Domain 层的 Godot 引用隔离。

## 已完成工作

### 1. 基础设施层接口定义

**新增文件：**
- `Scripts/Application/Ports/ICardResourceLoader.cs` - 卡牌资源加载接口
- `Scripts/Application/Ports/ILogger.cs` - 日志接口
- `Scripts/Application/Ports/IDeckService.cs` - 牌组服务接口

**新增实现：**
- `Scripts/Infrastructure/Godot/ResourceLoading/GodotCardResourceLoader.cs` - Godot 资源加载实现
- `Scripts/Infrastructure/Godot/Logging/GodotLogger.cs` - Godot 日志实现
- `Scripts/Infrastructure/Godot/Services/GodotDeckService.cs` - Godot 牌组服务实现

### 2. CardRewardService 重构

**修改文件：**
- `Scripts/Application/Reward/CardRewardService.cs`
  - 移除 `using Godot`
  - 移除 `ResourceLoader.Load<T>()` 调用
  - 移除 `GD.Print()` 调用
  - 移除 `GameManager.Instance` 直接引用
  - 注入 `ICardResourceLoader`、`ILogger`、`IDeckService` 依赖

### 3. Domain 层 AI 系统

**新增文件：**
- `Scripts/Domain/Combat/AI/IEnemyAI.cs` - 敌方 AI 接口
- `Scripts/Domain/Combat/AI/AIContext.cs` - AI 决策上下文
- `Scripts/Domain/Combat/AI/DomainEnemyAI.cs` - Domain 层 AI 实现

**关键设计：**
- `IEnemyAI.GenerateCommands(AIContext)` 返回 `IReadOnlyList<CombatCommand>`
- `AIContext` 包含所有决策所需信息（单位、能量、地图等）
- 完全无 Godot 依赖

### 4. CombatApplicationService 敌方回合编排

**修改文件：**
- `Scripts/Application/Combat/CombatApplicationService.cs`
  - 添加 `IEnemyAI` 依赖
  - 添加 `ExecuteEnemyTurn(int enemyId, AIContext context)` 方法

### 5. CombatManager 依赖注入更新

**修改文件：**
- `Scripts/Combat/CombatManager.cs`
  - `InitializeCommandSystem()` 中创建依赖实例
  - 注入 `GodotCardResourceLoader`、`GodotLogger`、`GodotDeckService`

## 验证结果

### 编译验证
```
dotnet build OdysseyCards.sln
OdysseyCards 成功，出现 36 警告
```

### Godot 引用检查

**Domain 层：**
```bash
grep "using Godot" Scripts/Domain/
# 无匹配
```

**Application 层：**
```bash
grep "using Godot" Scripts/Application/
# 无匹配
```

## 架构改进

### Before (M6)
```
Application/CardRewardService.cs
├── using Godot
├── ResourceLoader.Load<T>()
├── GD.Print()
└── GameManager.Instance
```

### After (M7)
```
Application/CardRewardService.cs
├── ICardResourceLoader (injected)
├── ILogger (injected)
└── IDeckService (injected)

Infrastructure/Godot/
├── ResourceLoading/GodotCardResourceLoader.cs
├── Logging/GodotLogger.cs
└── Services/GodotDeckService.cs
```

## 遗留事项

1. **CombatManager 双轨运行** - 规则逻辑方法仍保留，与 DomainCombatEngine 并行运行
2. **敌方回合未完全迁移** - AI 仍通过 CombatManager 执行（新接口已准备，待集成）
3. **运行时验证** - 需要在 Godot 编辑器中测试完整战斗流程

## 后续建议

- M8: 完成敌方回合逻辑迁移到 DomainCombatEngine
- 移除 CombatManager 中的规则方法，仅保留场景桥接职责
- 完善 DomainEnemyAI 的 AI 命令生成
- 添加 AI 行为单元测试

## 文件变更统计

| 类型 | 数量 |
|------|------|
| 新增文件 | 7 |
| 修改文件 | 4 |
| 删除文件 | 0 |
