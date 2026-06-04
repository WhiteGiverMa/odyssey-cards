using System;

namespace OdysseyCards.AI.Intents;

/// <summary>
/// 多重攻击意图。
/// 对目标造成多次伤害（如连击、扫射等）。意图标签显示为 "单次×次数" 格式。
/// </summary>
public sealed class MultiAttackIntent : AttackIntent
{
    private readonly int _repeats;

    /// <inheritdoc />
    public override IntentType Type => IntentType.MultiAttack;

    /// <inheritdoc />
    public override int Repeats => _repeats;

    /// <inheritdoc />
    public override string? SpritePath => "res://Assets/Intents/multi_attack.png";

    /// <summary>
    /// 创建固定伤害、固定次数的多重攻击意图。
    /// </summary>
    /// <param name="damage">单次伤害值</param>
    /// <param name="repeats">攻击次数</param>
    public MultiAttackIntent(int damage, int repeats)
    {
        DamageCalc = _ => damage;
        _repeats = repeats;
    }

    /// <summary>
    /// 创建固定伤害、动态次数的多重攻击意图。
    /// </summary>
    /// <param name="damage">单次伤害值</param>
    /// <param name="repeatCalc">攻击次数计算委托</param>
    public MultiAttackIntent(int damage, Func<int> repeatCalc)
    {
        DamageCalc = _ => damage;
        _repeats = repeatCalc();
    }

    /// <summary>
    /// 创建动态伤害、固定次数的多重攻击意图。
    /// </summary>
    /// <param name="damageCalc">伤害计算委托</param>
    /// <param name="repeats">攻击次数</param>
    public MultiAttackIntent(Func<Combat.CombatManager, int> damageCalc, int repeats)
    {
        DamageCalc = damageCalc;
        _repeats = repeats;
    }
}
