# 代码审查报告 - 2025-03-05

## 🐛 Bug 级别问题

### 1. Fallback Enemy Deck 没有卡牌
**文件**: `CombatManager.cs:CreateFallbackEnemyDeck()`

```csharp
private EnemyDeckData CreateFallbackEnemyDeck()
{
    var deckData = new EnemyDeckData
    {
        EnemyName = "Enemy Commander",
        StartingHealth = 8,
        StartingEnergy = 3,
        MaxEnergy = 3
    };
    return deckData;  // 没有任何卡牌！
}
```

**问题**: 如果 `EnemyDeckData.tres` 加载失败，敌人会没有牌可抽，每回合都会疲劳掉血。

**建议**: 添加至少几张基础卡牌作为 fallback。

---

### 2. Enemy AI 潜在死循环风险
**文件**: `CombatManager.cs:ExecuteEnemyTurns()`

```csharp
while (true)
{
    AIAction action = enemy.AI.DecideAction(enemy, this);
    
    if (action.Type == AIActionType.EndTurn)
        break;
    
    if (!action.IsValid())
        break;
    // ...
}
```

**问题**: 如果 `DecideAction()` 返回的 action 既不是 `EndTurn` 也不是 `Valid`，循环会继续。

**建议**: 添加回合最大操作次数限制作为安全阀。

---

### 3. HQ 血量可以为负数
**文件**: `Player.cs` 和 `Enemy.cs` 中的 `TakeHQDamage()`

```csharp
public void TakeHQDamage(int damage)
{
    HQCurrentHealth -= damage;  // 没有下界检查！
}
```

**问题**: UI 可能显示负血量。

**建议**: 
```csharp
HQCurrentHealth = Math.Max(0, HQCurrentHealth - damage);
```

---

### 4. 过时的 TakeDamage 方法
**文件**: `Unit.cs`

```csharp
[Obsolete("Use TakeDamage(int baseDamage, IDamageSource source) instead.")]
public void TakeDamage(int amount)
```

**问题**: 旧方法可能被某些代码路径调用。

**建议**: 全局搜索并移除所有对旧方法的调用。

---

## ⚠️ 设计问题

### 5. GameManager.ResetRun() 缺乏完整状态重置

```csharp
public void ResetRun()
{
    CurrentFloor = 1;
    CurrentAct = 1;
    CreateNewPlayer();
    // 没有调用 ResetPlayerHQHealth()!
}
```

**问题**: 玩家 HQ 血量没有被重置，新游戏会继承上一局的低血量。

**建议**: 添加 `ResetPlayerHQHealth()` 调用。

---

### 6. DamageModifier 可能产生负伤害

**文件**: `Unit.cs:DefenseModifier`

```csharp
public int ModifyDamageTaken(int currentDamage, DamageContext context)
{
    return currentDamage - _unit.Defense;
}
```

**问题**: 如果 `Defense > currentDamage`，返回负数。虽然 `DamageResolver` 会 clamp 到 0，但中间过程可能有逻辑问题。

**建议**: 在 modifier 内部 clamp 或添加注释说明依赖外部 clamp。

---

### 7. CombatUI 事件订阅未完全清理

**文件**: `CombatUI.cs`

```csharp
// 订阅了但没取消
_player.OnEnergyChanged += UpdateEnergy;
_player.OnDrawPileChanged += UpdateDrawPile;
_player.OnDiscardPileChanged += UpdateDiscardPile;
```

**问题**: `_ExitTree()` 中只取消了 `_combatManager` 的事件，`_player` 的事件泄漏了。

**建议**: 在 `_ExitTree()` 中取消所有订阅。

---

## 📝 代码质量建议

### 8. 重复的 DrawPile 检查

**文件**: `Player.DrawCards()`

```csharp
for (int i = 0; i < cardsToDraw; i++)
{
    if (DrawPile.Count == 0)
    {
        FatigueCount++;
        TakeHQDamage(FatigueCount);
        continue;
    }

    if (DrawPile.Count > 0)  // ← 这个检查是多余的
    {
        // ...
    }
}
```

**建议**: 移除冗余检查。

---

### 9. 缺少设计文档中的系统

根据 `dev_designdoc.md`，以下系统尚未实现：
- ❌ 遗物系统
- ❌ 地图/关卡选择系统  
- ❌ 商店系统
- ❌ 卡牌升级/移除系统

---

### 10. CardReward 奖励池加载不健壮

**文件**: `CombatManager.cs:LoadRewardPools()`

```csharp
if (ResourceLoader.Exists(poolPaths[i]))
{
    // 静默跳过，无日志
}
```

**建议**: 添加 debug 日志或创建默认奖励池。

---

## 🧪 建议测试场景

1. 敌人牌堆抽空后的疲劳机制
2. Player HQ 血量持久化跨战斗场景
3. 新游戏开始时状态是否完全重置
4. Unit 防御值大于攻击伤害时的伤害计算
5. 战斗胜利后奖励选择 UI

---

## 总结

代码整体质量较高：
- ✅ 架构清晰（单例模式、事件驱动、工厂模式）
- ✅ 命名规范
- ✅ 注释完善

问题主要集中在边界情况处理和资源清理。建议按优先级修复 Bug 级别问题后再继续开发新功能。
