using OdysseyCards.Combat;

namespace OdysseyCards.Relic;

/// <summary>
/// 藏品（遗物）抽象基类。
/// 每个具体藏品是独立的 sealed class，override 需要的钩子方法。
/// 参考 STS2 AbstractModel 的虚方法策略模式。
/// </summary>
public abstract class AbstractRelic
{
    /// <summary>藏品唯一标识。</summary>
    public abstract string Id { get; }

    /// <summary>藏品名称。</summary>
    public abstract string Name { get; }

    /// <summary>藏品描述。</summary>
    public abstract string Description { get; }

    /// <summary>正面藏品：纯增益效果。</summary>
    public virtual bool IsBeneficial => true;

    /// <summary>微妙藏品：正面+负面混合。</summary>
    public virtual bool IsSubtle => false;

    /// <summary>负面藏品：纯负面效果。</summary>
    public bool IsNegative => !IsBeneficial && !IsSubtle;

    // ===== 战斗生命周期钩子 =====

    /// <summary>战斗开始时触发。</summary>
    public virtual void OnBattleStart(CombatManager combat) { }

    /// <summary>玩家回合开始时触发。</summary>
    public virtual void OnTurnStart(CombatManager combat) { }

    /// <summary>玩家回合结束时触发。</summary>
    public virtual void OnTurnEnd(CombatManager combat) { }

    /// <summary>敌方回合结束时触发。</summary>
    public virtual void OnEnemyTurnEnd(CombatManager combat) { }

    // ===== 卡牌相关钩子 =====

    /// <summary>玩家打出一张卡牌后触发。</summary>
    /// <param name="combat">战斗管理器</param>
    /// <param name="card">打出的卡牌</param>
    /// <param name="actualCost">实际消耗的法力值（经减费后）</param>
    public virtual void OnCardPlayed(CombatManager combat, OdysseyCards.Card.Card card, int actualCost) { }

    /// <summary>修改卡牌费用（在打出前计算）。返回修改后的费用。</summary>
    public virtual int ModifyCost(OdysseyCards.Card.Card card, int originalCost) => originalCost;

    // ===== 费用相关钩子 =====

    /// <summary>玩家花费法力值时触发。</summary>
    public virtual void OnManaSpent(CombatManager combat, int amount) { }

    // ===== 热力值相关钩子 =====

    /// <summary>修改热力值系统（如冰袋降低初始值和上限）。</summary>
    public virtual void ModifyHeatSystem(Heat.HeatSystem heat) { }

    // ===== 伤害相关钩子 =====

    /// <summary>战斗开始时提供的伤害修改器（返回追加到敌方的 modifier）。</summary>
    public virtual Core.IDamageModifier? GetEnemyDamageModifier(CombatManager combat) => null;
}
