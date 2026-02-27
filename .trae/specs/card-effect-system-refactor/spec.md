# 卡牌效果系统重构 Spec

## Why
当前卡牌效果通过CardFactory中硬编码的Action委托添加，无法通过CardData资源文件配置，限制了卡牌数据驱动的能力。需要建立效果系统，使卡牌效果可以通过CardData资源定义。

## What Changes
- 创建 `CardEffect` 基类和效果类体系
- 创建效果类型枚举 `CardEffectType`
- 修改 `CardData` 添加效果配置字段
- 修改 `Card` 从 `CardData` 加载效果并执行
- 迁移现有CardFactory中的效果到新的效果类

## Impact
- Affected specs: 1.1 卡牌效果系统重构
- Affected code: Card.cs, CardData.cs, CardFactory.cs

---

## ADDED Requirements

### Requirement: CardEffect基类系统
系统应提供抽象的效果基类，支持不同类型效果的创建和执行。

#### Scenario: 效果基类定义
- **WHEN** 定义新的卡牌效果类时
- **THEN** 应继承CardEffect基类并实现Execute方法

#### Scenario: 效果执行
- **WHEN** 卡牌被打出时
- **THEN** 从CardData加载所有效果并按顺序执行

### Requirement: 基础效果实现
系统应提供常用的基础效果：造成伤害、获得护甲、获得能量、抽牌、施加Debuff。

#### Scenario: 造成伤害效果
- **WHEN** 执行DamageEffect时
- **THEN** 根据参数对目标造成指定数值的伤害

#### Scenario: 获得护甲效果
- **WHEN** 执行GainBlockEffect时
- **THEN** 给施法者增加指定数值的护甲

#### Scenario: 施加Debuff效果
- **WHEN** 执行ApplyDebuffEffect时
- **THEN** 给目标施加指定类型和层数的Debuff

### Requirement: CardData效果配置
CardData应支持在资源文件中配置效果。

#### Scenario: CardData包含效果配置
- **WHEN** 创建CardData资源时
- **THEN** 可以添加一个或多个效果定义

---

## MODIFIED Requirements

### Requirement: Card从CardData加载效果
Card类应从CardData中加载效果配置，而非手动AddEffect。

#### Scenario: 卡牌创建时加载效果
- **WHEN** 通过Card.Create(data)创建卡牌时
- **THEN** 自动从data.Effects中加载并初始化效果列表

### Requirement: CardFactory使用新系统
CardFactory应使用新的效果系统创建卡牌。

#### Scenario: 工厂创建带效果的卡牌
- **WHEN** CardFactory创建卡牌时
- **THEN** 通过CardData配置效果，而非手动AddEffect

---

## REMOVED Requirements

### Requirement: Card.AddEffect方法
**Reason**: 效果应从CardData加载，不应在运行时手动添加
**Migration**: 现有调用AddEffect的代码需要迁移到CardData配置
