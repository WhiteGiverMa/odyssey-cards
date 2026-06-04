using System;

namespace OdysseyCards.AI.Intents;

/// <summary>
/// 攻击意图抽象基类。
/// 包含伤害计算委托和重复次数，是所有攻击类意图的公共父类。
/// </summary>
public abstract class AttackIntent : AbstractIntent
{
    /// <summary>
    /// 伤害计算委托——每次调用根据当前战场状态（力量、易伤等）重新计算单次伤害。
    /// 若为 null 则返回 0。
    /// </summary>
    public Func<Combat.CombatManager, int>? DamageCalc { get; init; }

    /// <summary>攻击重复次数，默认为 1。</summary>
    public virtual int Repeats => 1;

    /// <inheritdoc />
    public override string IntentPrefix => "ATTACK";

    /// <summary>
    /// 获取单次攻击的伤害值（经过所有修饰后的预览值）。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    /// <returns>单次伤害值</returns>
    public int GetSingleDamage(Combat.CombatManager combat)
    {
        return DamageCalc?.Invoke(combat) ?? 0;
    }

    /// <summary>
    /// 获取总伤害值 = 单次伤害 × 重复次数。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    /// <returns>总伤害值</returns>
    public int GetTotalDamage(Combat.CombatManager combat)
    {
        return GetSingleDamage(combat) * Repeats;
    }

    /// <inheritdoc />
    /// <summary>
    /// 返回意图标签：单次攻击显示伤害数值（如 "6"），多次攻击显示"伤害×次数"（如 "3x4"）。
    /// </summary>
    public override string GetIntentLabel(Combat.CombatManager combat)
    {
        int singleDmg = GetSingleDamage(combat);
        if (Repeats <= 1)
            return singleDmg.ToString();
        return $"{singleDmg}x{Repeats}";
    }

    /// <inheritdoc />
    /// <summary>
    /// 返回完整的伤害描述：单次攻击显示"造成 X 点伤害"，多次攻击显示"造成 X 点伤害 ×N 次"。
    /// </summary>
    public override string GetIntentDescription(Combat.CombatManager combat)
    {
        int singleDmg = GetSingleDamage(combat);
        if (Repeats <= 1)
        {
            string fmt = OdysseyCards.Localization.Localization.T("intents.ATTACK.description_single", "造成 {damage} 点伤害");
            return fmt.Replace("{damage}", singleDmg.ToString());
        }
        else
        {
            string fmt = OdysseyCards.Localization.Localization.T("intents.ATTACK.description_multi", "造成 {damage} 点伤害 ×{repeats} 次");
            return fmt
                .Replace("{damage}", singleDmg.ToString())
                .Replace("{repeats}", Repeats.ToString());
        }
    }
}
