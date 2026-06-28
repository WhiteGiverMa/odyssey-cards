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

	public static IDamageTarget SelectEnemyAttackTarget(Board board, Hero playerHero, IDamageSource source, int baseDamage)
	{
		var taunts = board.GetTaunts(ofEnemy: false).Cast<IDamageTarget>().ToList();
		if (taunts.Count > 0)
			return SelectBest(taunts, source, baseDamage, preferBoardControl: true);

		var candidates = new List<IDamageTarget> { playerHero };
		candidates.AddRange(board.GetPlayerMinions());
		return SelectBest(candidates, source, baseDamage, preferBoardControl: false);
	}

	public static IDamageTarget SelectEnemySpellTarget(Board board, Hero playerHero, IDamageSource source, int baseDamage)
	{
		var candidates = new List<IDamageTarget> { playerHero };
		candidates.AddRange(board.GetPlayerMinions());
		return SelectBest(candidates, source, baseDamage, preferBoardControl: false, DamageKind.Effect);
	}

	public static bool TrySelectPlayerAttackTarget(Board board, IReadOnlyList<EnemyUnit> enemyUnits, Minion attacker, out IDamageTarget target)
	{
		var taunts = board.GetTaunts(ofEnemy: true).Cast<IDamageTarget>().ToList();
		if (taunts.Count > 0)
		{
			target = SelectBest(taunts, attacker, attacker.Attack, preferBoardControl: true);
			return true;
		}

		var candidates = new List<IDamageTarget>();
		candidates.AddRange(board.GetEnemyMinions());
		foreach (var unit in enemyUnits)
		{
			if (!unit.Body.IsDead)
				candidates.Add(unit.Body);
		}

		if (candidates.Count == 0)
		{
			target = attacker;
			return false;
		}

		target = SelectBest(candidates, attacker, attacker.Attack, preferBoardControl: false);
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
