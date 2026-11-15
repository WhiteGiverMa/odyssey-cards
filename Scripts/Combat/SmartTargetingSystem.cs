using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

internal static class SmartTargetingSystem
{
	private const int NearTieRange = 2;

	public static IDamageTarget? SelectEnemyAttackTarget(Board board, Hero playerHero, IDamageSource source, int baseDamage)
	{
		// ponytail: 烟幕——有烟幕的友方单位无法被敌方武器/随从攻击命中。
		// 嘲讽目标中有烟幕的也排除；嘲讽拦截也只使用未被烟幕保护的目标。
		// 全部候选都被烟幕保护 → 返回 null（攻击将被调用方跳过）。
		var candidates = new List<IDamageTarget>();
		if (!playerHero.IsDead)
			candidates.Add(playerHero);
		candidates.AddRange(board.GetPlayerMinions().Where(m => !m.IsDead));
		var legalTargets = AttackTargetRules.GetLegalAttackTargets(board, source, candidates);
		if (legalTargets.Count == 0)
			return null;

		return SelectBest(
			legalTargets,
			source,
			baseDamage,
			preferBoardControl: legalTargets.Any(t => t is Minion { HasTaunt: true }));
	}

	public static IDamageTarget SelectEnemySpellTarget(Board board, Hero playerHero, IDamageSource source, int baseDamage)
	{
		var candidates = new List<IDamageTarget> { playerHero };
		candidates.AddRange(board.GetPlayerMinions());
		return SelectBest(candidates, source, baseDamage, preferBoardControl: false, DamageKind.Effect);
	}

	public static bool TrySelectPlayerAttackTarget(Board board, IReadOnlyList<EnemyUnit> enemyUnits, Minion attacker,
		out IDamageTarget target, Func<IDamageTarget, bool>? canAttack = null)
	{
		var candidates = new List<IDamageTarget>();
		candidates.AddRange(board.GetEnemyMinions().Where(m => !m.IsDead));
		foreach (var unit in enemyUnits)
		{
			if (!unit.Body.IsDead)
				candidates.Add(unit.Body);
		}

		var legalTargets = canAttack == null ? AttackTargetRules.GetLegalAttackTargets(board, attacker, candidates)
			: candidates.Where(canAttack).ToList();
		if (legalTargets.Count == 0)
		{
			target = attacker;
			return false;
		}

		target = SelectBest(
			legalTargets,
			attacker,
			attacker.Attack,
			preferBoardControl: legalTargets.Any(t => t is Minion { HasTaunt: true }));
		return true;
	}

	private static IDamageTarget SelectBest(
		IReadOnlyList<IDamageTarget> candidates,
		IDamageSource source,
		int baseDamage,
		bool preferBoardControl,
		DamageKind kind = DamageKind.Attack)
	{
		var scored = candidates
			.Select(target => new ScoredTarget(target, ScoreTarget(target, source, baseDamage, preferBoardControl, kind)))
			.OrderByDescending(item => item.Score)
			.ToList();

		int bestScore = scored[0].Score;
		var nearBest = scored.Where(item => bestScore - item.Score <= NearTieRange).ToList();
		return nearBest[Random.Shared.Next(nearBest.Count)].Target;
	}

	private static int ScoreTarget(IDamageTarget target, IDamageSource source, int baseDamage, bool preferBoardControl, DamageKind kind)
	{
		int previewDamage = DamageResolver.ResolvePreviewDamage(baseDamage, source, target, kind);
		return target switch
		{
			Minion minion => ScoreMinion(minion, previewDamage, preferBoardControl),
			Hero hero => ScoreHero(hero, previewDamage),
			_ => previewDamage,
		};
	}

	private static int ScoreMinion(Minion minion, int previewDamage, bool preferBoardControl)
	{
		int effectiveHealth = minion.CurrentHealth + minion.CurrentArmor;
		int score = previewDamage;
		if (previewDamage >= effectiveHealth)
			score += 18;
		score += Math.Min(12, minion.Attack * 2);
		if (minion.HasTaunt)
			score += 8;
		if (preferBoardControl)
			score += 4;
		score -= Math.Max(0, effectiveHealth - previewDamage) / 2;
		return score;
	}

	private static int ScoreHero(Hero hero, int previewDamage)
	{
		int effectiveHealth = hero.CurrentHealth + hero.CurrentArmor;
		int score = previewDamage + 4;
		if (previewDamage >= effectiveHealth)
			score += 100;
		return score;
	}

	private readonly record struct ScoredTarget(IDamageTarget Target, int Score);
}
