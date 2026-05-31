using System;

namespace OdysseyCards.Core;

/// <summary>
/// 单次伤害上限修改器——在 CAPPING 阶段将最终伤害压到上限值以内。
/// 用于固璋（3）等"单次受到的伤害最高为 N"的被动技能。
/// </summary>
public class DamageCapModifier : IDamageModifier
{
    private readonly int _cap;

    /// <summary>
    /// 创建伤害上限修改器。
    /// </summary>
    /// <param name="cap">单次伤害上限（例如 3）</param>
    public DamageCapModifier(int cap)
    {
        _cap = cap;
    }

    public DamagePhase Phase => DamagePhase.CAPPING;

    public int ModifyDamageDealt(int currentDamage, DamageContext context)
        => currentDamage; // 只影响目标侧

    public int ModifyDamageTaken(int currentDamage, DamageContext context)
        => Math.Min(currentDamage, _cap);
}
