using System.Collections.Generic;
using OdysseyCards.Combat;

namespace OdysseyCards.Relic;

/// <summary>
/// 藏品管理器。
/// 持有玩家所有藏品的列表，负责事件分发。
/// 挂在 GameManager 或 Player 上，跨战斗持久化。
/// </summary>
public class RelicManager
{
    private readonly List<AbstractRelic> _relics = new();

    /// <summary>当前持有的藏品列表（只读）。</summary>
    public IReadOnlyList<AbstractRelic> Relics => _relics;

    /// <summary>添加藏品。</summary>
    public void AddRelic(AbstractRelic relic)
    {
        if (relic == null) return;
        _relics.Add(relic);
    }

    /// <summary>移除藏品。</summary>
    public void RemoveRelic(AbstractRelic relic)
    {
        _relics.Remove(relic);
    }

    /// <summary>清空所有藏品。</summary>
    public void Clear()
    {
        _relics.Clear();
    }

    // ===== 事件分发 =====

    /// <summary>战斗开始时——通知所有藏品。</summary>
    public void TriggerBattleStart(CombatManager combat)
    {
        foreach (var relic in _relics)
            relic.OnBattleStart(combat);
    }

    /// <summary>玩家回合开始时——通知所有藏品。</summary>
    public void TriggerTurnStart(CombatManager combat)
    {
        foreach (var relic in _relics)
            relic.OnTurnStart(combat);
    }

    /// <summary>玩家回合结束时——通知所有藏品。</summary>
    public void TriggerTurnEnd(CombatManager combat)
    {
        foreach (var relic in _relics)
            relic.OnTurnEnd(combat);
    }

    /// <summary>敌方回合结束时——通知所有藏品。</summary>
    public void TriggerEnemyTurnEnd(CombatManager combat)
    {
        foreach (var relic in _relics)
            relic.OnEnemyTurnEnd(combat);
    }

    /// <summary>玩家打出一张卡牌——通知所有藏品。</summary>
    public void TriggerCardPlayed(CombatManager combat, OdysseyCards.Card.Card card, int actualCost)
    {
        foreach (var relic in _relics)
            relic.OnCardPlayed(combat, card, actualCost);
    }

    /// <summary>修改卡牌费用——收集所有藏品的修改。</summary>
    public int ApplyCostModifiers(OdysseyCards.Card.Card card, int originalCost)
    {
        int cost = originalCost;
        foreach (var relic in _relics)
            cost = relic.ModifyCost(card, cost);
        return cost;
    }

    /// <summary>玩家花费法力值——通知所有藏品。</summary>
    public void TriggerManaSpent(CombatManager combat, int amount)
    {
        foreach (var relic in _relics)
            relic.OnManaSpent(combat, amount);
    }

    /// <summary>修改热力值系统——通知所有藏品。</summary>
    public void ModifyHeatSystem(Heat.HeatSystem heat)
    {
        foreach (var relic in _relics)
            relic.ModifyHeatSystem(heat);
    }

    /// <summary>收集所有藏品的敌方伤害修改器。</summary>
    public List<Core.IDamageModifier> CollectEnemyDamageModifiers(CombatManager combat)
    {
        var mods = new List<Core.IDamageModifier>();
        foreach (var relic in _relics)
        {
            var mod = relic.GetEnemyDamageModifier(combat);
            if (mod != null) mods.Add(mod);
        }
        return mods;
    }
}
