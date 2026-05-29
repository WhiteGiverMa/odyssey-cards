using System;

namespace OdysseyCards.Core;

/// <summary>
/// 防御力伤害修改器。
/// 在 ADDITIVE 阶段从受到的伤害中扣除防御力值（currentDamage - defense）。
/// 负防御会产生额外伤害（如防御 -3 会多受 3 点伤害）。
/// 参考 <see cref="DamageResolver"/> 的三阶段伤害管线。
/// </summary>
public class DefenseModifier : IDamageModifier
{
    private readonly Func<int> _getDefense;

    public DefenseModifier(Func<int> getDefense)
    {
        _getDefense = getDefense ?? throw new ArgumentNullException(nameof(getDefense));
    }

    public DamagePhase Phase => DamagePhase.ADDITIVE;

    /// <summary>
    /// 防御力不影响造成的伤害。
    /// </summary>
    public int ModifyDamageDealt(int currentDamage, DamageContext context) => currentDamage;

    public int ModifyDamageTaken(int currentDamage, DamageContext context)
    {
        int defense = _getDefense();
        return currentDamage - defense;
    }
}
