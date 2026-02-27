# 敌人数据资源 Spec

## Why
当前敌人配置通过代码硬编码（EnemyType属性和Initialize方法），无法通过资源文件配置，限制了敌人数据的可扩展性。需要建立敌人数据资源系统，使敌人类型可以通过EnemyData资源定义，实现数据驱动。

## What Changes
- 创建 `EnemyData` 资源类，定义敌人基础属性
- 创建 `EnemyActionData` 资源类，定义敌人行为配置
- 创建基础敌人资源文件（Slime, Goblin）
- 创建 `EnemyFactory` 敌人生成工厂
- 修改 `Enemy` 从 `EnemyData` 初始化

## Impact
- Affected specs: 1.3 敌人数据资源
- Affected code: Enemy.cs, CombatManager.cs, 新增 EnemyData.cs, EnemyActionData.cs, EnemyFactory.cs

---

## ADDED Requirements

### Requirement: EnemyData资源类
系统应提供敌人数据资源类，用于配置敌人的基础属性。

#### Scenario: EnemyData定义
- **WHEN** 创建EnemyData资源时
- **THEN** 可以配置敌人名称、最大生命值、最大能量、行动列表

#### Scenario: EnemyData字段
- **THEN** 包含CharacterName、MaxHealth、MaxEnergy、Actions、IntentDisplayName属性

### Requirement: EnemyActionData资源类
系统应提供敌人行为数据资源类，用于配置敌人的具体行动。

#### Scenario: EnemyActionData定义
- **WHEN** 创建EnemyActionData资源时
- **THEN** 可以配置行动类型、数值、描述、次数

#### Scenario: EnemyActionData字段
- **THEN** 包含Type、Value、Description、Hits属性，与现有EnemyAction对应

### Requirement: EnemyFactory工厂类
系统应提供敌人工厂，用于从资源创建敌人实例。

#### Scenario: 工厂创建敌人
- **WHEN** EnemyFactory根据EnemyData创建敌人时
- **THEN** 自动设置敌人的属性并初始化行动池

---

## MODIFIED Requirements

### Requirement: Enemy从EnemyData初始化
Enemy类应支持从EnemyData资源加载配置。

#### Scenario: 敌人初始化
- **WHEN** Enemy.Initialize(enemyData)被调用时
- **THEN** 从enemyData加载名称、生命值、能量、行动列表

---

## REMOVED Requirements

### Requirement: Enemy.EnemyType属性
**Reason**: 敌人类型应通过EnemyData配置，而非硬编码属性
**Migration**: 使用EnemyData.CharacterName替代
