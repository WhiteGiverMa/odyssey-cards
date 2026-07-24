using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>
/// 普通攻击目标合法性规则的唯一实现。
/// 反击、伏击和法术不经过此模块。
/// </summary>
internal static class AttackTargetRules
{
	/// <summary>
	/// 速度相等或攻击者更快时，攻击者可以追上目标。
	/// </summary>
	public static bool CanReach(int attackerSpeed, int targetSpeed)
	{
		return attackerSpeed >= targetSpeed;
	}

	/// <summary>
	/// 判断普通攻击目标是否合法。
	/// 速度检查先于嘲讽拦截，嘲讽不能让攻击者攻击原本追不上的非嘲讽目标。
	/// </summary>
	public static bool CanAttackTarget(
		int attackerSpeed,
		int targetSpeed,
		bool targetIsTaunt,
		IReadOnlyList<int> tauntSpeeds)
	{
		// 直接攻击存活嘲讽时，嘲讽目标本身豁免速度检查。
		if (targetIsTaunt)
			return true;

		if (!CanReach(attackerSpeed, targetSpeed))
			return false;

		return !tauntSpeeds.Any(tauntSpeed =>
			TauntIntercepts(attackerSpeed, targetSpeed, tauntSpeed));
	}

	/// <summary>
	/// 判断一只嘲讽是否能拦截当前攻击。
	/// 嘲讽必须不慢于攻击者，也必须追得上其正在保护的目标。
	/// </summary>
	public static bool TauntIntercepts(int attackerSpeed, int targetSpeed, int tauntSpeed)
	{
		return tauntSpeed >= attackerSpeed && tauntSpeed >= targetSpeed;
	}

	/// <summary>
	/// 使用运行时单位计算攻击目标合法性。
	/// </summary>
	public static bool CanAttackTarget(
		IDamageSource attacker,
		IDamageTarget target,
		IReadOnlyList<Minion> defenderTaunts)
	{
		ArgumentNullException.ThrowIfNull(attacker);
		ArgumentNullException.ThrowIfNull(target);
		ArgumentNullException.ThrowIfNull(defenderTaunts);

		bool targetIsTaunt = target is Minion { HasTaunt: true, IsDead: false };
		return CanAttackTarget(
			GetSpeed(attacker),
			GetSpeed(target),
			targetIsTaunt,
			defenderTaunts.Select(taunt => taunt.Speed).ToArray());
	}

	/// <summary>
	/// 从候选目标中筛出普通攻击可达的目标。
	/// </summary>
	public static List<IDamageTarget> GetLegalAttackTargets(
		IDamageSource attacker,
		IEnumerable<IDamageTarget> candidates,
		IReadOnlyList<Minion> defenderTaunts)
	{
		ArgumentNullException.ThrowIfNull(candidates);
		return candidates
			.Where(target => CanAttackTarget(attacker, target, defenderTaunts))
			.ToList();
	}

	/// <summary>
	/// 获取能拦截指定非嘲讽目标的存活嘲讽列表。
	/// </summary>
	public static List<Minion> GetInterceptingTaunts(
		IDamageSource attacker,
		IDamageTarget target,
		IEnumerable<Minion> defenderTaunts)
	{
		ArgumentNullException.ThrowIfNull(defenderTaunts);
		if (target is Minion { HasTaunt: true, IsDead: false })
			return new List<Minion>();

		int attackerSpeed = GetSpeed(attacker);
		int targetSpeed = GetSpeed(target);
		if (!CanReach(attackerSpeed, targetSpeed))
			return new List<Minion>();

		return defenderTaunts
			.Where(taunt => !taunt.IsDead
				&& taunt.HasTaunt
				&& TauntIntercepts(attackerSpeed, targetSpeed, taunt.Speed))
			.ToList();
	}

	public static int GetSpeed(IDamageSource source)
	{
		return source switch
		{
			Hero hero => hero.Speed,
			Minion minion => minion.Speed,
			_ => throw new ArgumentException("普通攻击来源必须是英雄或随从。", nameof(source)),
		};
	}

	public static int GetSpeed(IDamageTarget target)
	{
		return target switch
		{
			Hero hero => hero.Speed,
			Minion minion => minion.Speed,
			_ => throw new ArgumentException("普通攻击目标必须是英雄或随从。", nameof(target)),
		};
	}
}
