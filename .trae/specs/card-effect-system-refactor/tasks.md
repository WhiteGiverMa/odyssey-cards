# Tasks

## 1.1 卡牌效果系统重构

- [ ] Task 1: 创建CardEffect基类 - 在Scripts/Card/Effects/下创建CardEffect.cs，包含抽象基类和IExecutor接口
- [ ] Task 2: 创建效果类型枚举 - 创建CardEffectType枚举，包含Damage/GainBlock/GainEnergy/DrawCards/ApplyDebuff等
- [ ] Task 3: 创建基础效果实现类 - 创建DamageEffect、GainBlockEffect、GainEnergyEffect、DrawCardsEffect、ApplyDebuffEffect
- [ ] Task 4: 创建效果数据配置类 - 创建CardEffectData类，用于在CardData中配置效果参数
- [ ] Task 5: 修改CardData支持效果配置 - 添加Effects数组字段和相关属性
- [ ] Task 6: 修改Card从CardData加载效果 - 修改Card.Create方法，从data.Effects加载并实例化效果
- [ ] Task 7: 迁移CardFactory到新系统 - 修改CardFactory.CreateStrike/Defend等方法，使用CardData配置效果
- [ ] Task 8: 测试效果执行 - 验证卡牌效果可以正确执行
