# Tasks

## 1.4 战斗流程测试

- [x] Task 1: 修改CombatManager - 添加OnReady自动开始战斗逻辑
  - [x] SubTask 1.1: 添加GetTree().CurrentSceneChanged监听，检测场景切换
  - [x] SubTask 1.2: 当切换到Combat场景时，自动初始化并开始战斗

- [x] Task 2: 修改Combat.tscn场景 - 添加敌人节点容器和初始化逻辑
  - [x] SubTask 2.1: 在场景中添加敌人节点（暂时硬编码Slime）
  - [x] SubTask 2.2: 确保敌人节点与EnemyContainer正确关联

- [x] Task 3: 修改MainMenu - 正确创建玩家并切换场景
  - [x] SubTask 3.1: OnStartPressed中创建新玩家
  - [x] SubTask 3.2: 切换到Combat场景前确保玩家已创建

- [x] Task 4: 修复敌人初始化 - 确保敌人正确从资源加载
  - [x] SubTask 4.1: 使用EnemyFactory.FromData加载敌人
  - [x] SubTask 4.2: 确保敌人行为正确（AI决定动作并执行）

- [x] Task 5: 添加战斗结果UI - 显示胜利/失败
  - [x] SubTask 5.1: 连接CombatManager.OnCombatEnd事件
  - [x] SubTask 5.2: 根据结果显示ResultLabel

- [x] Task 6: 验证完整战斗流程 - 运行测试
  - [x] SubTask 6.1: 验证玩家可以打牌
  - [x] SubTask 6.2: 验证敌人回合正确执行
  - [x] SubTask 6.3: 验证战斗可以正常结束

# Task Dependencies

- [Task 3] 必须在 [Task 1] 之前完成
- [Task 4] 依赖 [Task 1] 完成
- [Task 5] 依赖 [Task 1] 完成
- [Task 6] 依赖 [Task 2, Task 3, Task 4, Task 5] 完成
