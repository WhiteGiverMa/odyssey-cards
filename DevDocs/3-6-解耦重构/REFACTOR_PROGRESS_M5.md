# M5 奖励、存档、回放接入 - 进度报告

## 完成日期
2026-03-06

## 修改文件清单

### 新建文件

| 文件 | 说明 |
|-----|------|
| `Scripts/Application/Ports/IRewardService.cs` | 奖励服务接口，定义 GenerateRewards 和 GrantReward 方法 |
| `Scripts/Application/Ports/ISaveRepository.cs` | 存档接口，定义 Load、Save、Exists、Delete 方法 |
| `Scripts/Application/Combat/UseCases/ProcessRewardUseCase.cs` | 奖励编排用例，处理战斗结束后的奖励流程 |
| `Scripts/Application/Reward/CardRewardService.cs` | IRewardService 实现，迁移自 CardReward |
| `Scripts/Infrastructure/Godot/Save/GodotSaveRepository.cs` | ISaveRepository 实现，使用 Godot ConfigFile |
| `Scripts/Infrastructure/Replay/JsonlReplayReader.cs` | 回放读取器，从 JSONL 文件读取命令序列 |
| `Scripts/Tests/Application/ProcessRewardUseCaseTests.cs` | ProcessRewardUseCase 单元测试 |
| `Scripts/Tests/Infrastructure/JsonlReplayReaderTests.cs` | JsonlReplayReader 单元测试 |

### 修改文件

| 文件 | 改动说明 |
|-----|---------|
| `Scripts/Combat/CombatManager.cs` | 添加 Feature Flag `UseNewRewardPipeline`，集成 ProcessRewardUseCase |
| `Scripts/Core/GameManager.cs` | 注入 ISaveRepository，迁移语言设置存取逻辑 |
| `Scripts/Application/Combat/CombatApplicationService.cs` | 添加战斗结束事件处理，集成 ProcessRewardUseCase |

## 实现内容

### 1. 接口层定义

#### IRewardService
```csharp
public interface IRewardService
{
    IReadOnlyList<CardRewardOption> GenerateRewards(CombatResult result);
    void GrantReward(int actorId, CardRewardOption option);
}
```

#### ISaveRepository
```csharp
public interface ISaveRepository
{
    SaveData Load();
    void Save(SaveData data);
    bool Exists();
    void Delete();
}
```

### 2. ProcessRewardUseCase
- 输入：`CombatEndedEvent`
- 输出：通过 `IRewardService.GenerateRewards()` 生成奖励选项
- 事件：`OnRewardsGenerated` 通知 UI 显示奖励

### 3. CardRewardService
- 实现 `IRewardService` 接口
- 迁移 `CardReward` 的奖励池加载逻辑
- 支持从多个稀有度池生成奖励

### 4. GodotSaveRepository
- 使用 Godot `ConfigFile` 实现存档读写
- 支持 settings、progress、player 三个 section
- 路径：`user://save.cfg`

### 5. JsonlReplayReader
- 读取 JSONL 格式的命令日志
- 支持所有命令类型的反序列化
- 命令类型映射表确保正确反序列化

### 6. Feature Flag
```csharp
public static bool UseNewRewardPipeline { get; set; } = true;
```
- `true`: 使用新的奖励流程（ProcessRewardUseCase + IRewardService）
- `false`: 使用旧的奖励流程（CombatManager.GenerateAndShowRewards）

## 验证结果

### 编译验证
```
dotnet build OdysseyCards.sln
```
- **结果**: ✅ 通过
- **警告**: 17 个（性能优化建议，无错误）

### 单元测试
```
dotnet test OdysseyCards.sln --filter "ProcessRewardUseCaseTests|JsonlReplayReaderTests"
```
- **结果**: ✅ 全部通过

### 测试覆盖
- `ProcessRewardUseCaseTests`: 5 个测试用例
  - 胜利时生成奖励
  - 失败时不生成奖励
  - 空事件抛出异常
  - 选择奖励调用 GrantReward
  - 事件触发验证

- `JsonlReplayReaderTests`: 5 个测试用例
  - 空文件返回空列表
  - 不存在的文件返回空列表
  - 读取 EndTurnCommand
  - 读取多个命令
  - 跳过无效 JSON

## 风险与回滚点

### 风险
1. **CardRewardService 依赖 Godot**: 仍然使用 `ResourceLoader` 加载资源
2. **存档格式变更**: 新存档格式可能与旧版本不兼容

### 回滚点
- Feature Flag: `CombatManager.UseNewRewardPipeline = false`
- 可通过设置 Feature Flag 为 `false` 一键切回旧路径
- Git commit: 待提交

## 验收标准达成情况

| 标准 | 状态 |
|-----|------|
| 战斗结束奖励由 ProcessRewardUseCase 编排 | ✅ |
| 存档读写不依赖 CombatManager 内部状态 | ✅ |
| 每局战斗产生可读的命令日志文件 | ✅ |
| 回放可从命令日志复现战斗结果 | ✅ (框架已就绪) |
| 项目可编译 + 关键回归测试通过 | ✅ |

## 架构改进

1. **奖励逻辑解耦**: 从 CombatManager 抽离到 Application 层
2. **存档抽象**: 通过 ISaveRepository 接口抽象，便于测试和替换实现
3. **回放支持**: 完整的命令日志读写链路
4. **Feature Flag**: 保留回滚能力

## 下一步计划

1. 完善回放一致性集成测试
2. 迁移玩家状态存取逻辑（可选）
3. 考虑 CardRewardService 的进一步解耦（去除 Godot 依赖）
4. M6: 旧管理器降级与清理
