using System;

namespace OdysseyCards.AI.Intents;

/// <summary>
/// 单次攻击意图。
/// 对单个目标造成一次伤害。
/// </summary>
public sealed class SingleAttackIntent : AttackIntent
{
	/// <inheritdoc />
	public override IntentType Type => IntentType.Attack;

	/// <inheritdoc />
	public override int Repeats => 1;

	/// <inheritdoc />
	public override string? SpritePath => "res://Assets/Intents/attack.png";

	/// <summary>
	/// 创建固定伤害的单次攻击意图。
	/// </summary>
	/// <param name="damage">固定伤害值</param>
	public SingleAttackIntent(int damage)
	{
		DamageCalc = _ => damage;
	}

	/// <summary>
	/// 创建动态伤害的单次攻击意图。
	/// </summary>
	/// <param name="damageCalc">伤害计算委托（每次调用重算）</param>
	public SingleAttackIntent(Func<Combat.CombatManager, int> damageCalc)
	{
		DamageCalc = damageCalc;
	}
}
