# 初始卡组初始化 Spec

## Why
GameManager.CreateStartingDeck()方法当前返回空Deck，导致玩家开始游戏时没有卡牌可用。需要实现该方法，使用CardFactory.GetStarterDeck()填充初始卡组。

## What Changes
- 修改 GameManager.CreateStartingDeck() 使用CardFactory.GetStarterDeck()
- 确保Deck类正确处理CardData列表
- 验证卡组初始化流程

## Impact
- Affected specs: 1.2 初始卡组初始化
- Affected code: GameManager.cs, Player.cs, CardFactory.cs

---

## ADDED Requirements

### Requirement: 初始卡组生成
系统应在玩家创建时生成初始卡组。

#### Scenario: 新游戏开始
- **WHEN** CreateNewPlayer被调用时
- **THEN** CreateStartingDeck返回包含10张卡牌的Deck

#### Scenario: 卡组包含正确卡牌
- **WHEN** 初始卡组生成时
- **THEN** 包含5张Strike、4张Defend、1张Bash

---

## MODIFIED Requirements

### Requirement: CreateStartingDeck实现
GameManager应正确实现CreateStartingDeck方法。

#### Scenario: 创建卡组
- **WHEN** CreateStartingDeck被调用时
- **THEN** 返回包含CardFactory.GetStarterDeck()中所有卡牌的Deck对象

---

## REMOVED Requirements

### Requirement: 空Deck返回
**Reason**: 现在需要返回有内容的Deck
**Migration**: N/A
