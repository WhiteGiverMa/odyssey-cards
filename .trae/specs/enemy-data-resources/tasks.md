# Tasks

## 1.3 敌人数据资源

- [x] Task 1: 创建EnemyActionData资源类 - 在Scripts/Character/下创建EnemyActionData.cs，继承Resource，包含Type/Value/Description/Hits属性（已存在EnemyAction，复用）
- [x] Task 2: 创建EnemyData资源类 - 在Scripts/Character/下创建EnemyData.cs，继承Resource，包含CharacterName/MaxHealth/MaxEnergy/Actions属性
- [x] Task 3: 创建EnemyFactory工厂类 - 在Scripts/Character/下创建EnemyFactory.cs，提供FromData方法从EnemyData创建Enemy实例
- [x] Task 4: 修改Enemy支持从EnemyData初始化 - 修改Enemy.Initialize方法，支持传入EnemyData参数
- [x] Task 5: 创建基础敌人资源文件 - 在Resources/Enemies/下创建Slime.tres和Goblin.tres
- [x] Task 6: 测试敌人初始化 - 验证敌人可以从EnemyData正确加载属性和行动
- [x] Task 7: 编译测试 - 确保编译通过，无错误

# Task Dependencies

- Task 2 依赖 Task 1（EnemyActionData是EnemyData的组成部分）
- Task 3 依赖 Task 1 和 Task 2（工厂需要使用这两个资源类）
- Task 4 依赖 Task 2（Enemy需要从EnemyData加载）
- Task 5 依赖 Task 2（资源文件基于EnemyData创建）
- Task 6 依赖 Task 3、Task 4、Task 5（测试需要完整的实现）
- Task 7 依赖所有任务
