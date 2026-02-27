# Checklist

## 1.1 卡牌效果系统重构

- [x] CardEffect基类创建完成，包含抽象Execute方法和CardEffectType属性
- [x] CardEffectType枚举包含所有基础效果类型
- [x] 基础效果实现类（DamageEffect, GainBlockEffect, GainEnergyEffect, DrawCardsEffect, ApplyDebuffEffect）创建完成
- [x] CardEffectData配置类创建完成，支持效果参数序列化
- [x] CardData添加Effects数组字段，可以配置多个效果
- [x] Card.Create方法从CardData加载效果并执行
- [x] CardFactory迁移到新系统，使用CardData配置效果
- [x] 编译通过，无错误
- [x] 效果执行逻辑正确验证
