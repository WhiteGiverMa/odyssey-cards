using System;
using System.Linq;
using Godot;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

#pragma warning disable CS0618

namespace OdysseyCards.AI;

/// <summary>
/// 珊胡 — 第2层精英敌人。
/// 被动：不破（1）— 每回合只能受到 1 次生命伤害（0 点不计）。
/// 意图：A(3~4伤害) / B(2伤害×2) 随机交换 → C(武器攻击+2) → 循环。
/// D 每 2~4 回合替代主意图：给随机友方目标 5 点护甲。
/// </summary>
public class ShanHu : EnemyEncounter
{
	private int _dTurnsRemaining;
	private bool _dJustExecuted;
	private bool _abAFirst;
	private int _cycleStep;
	private int _currentSingleAttackDamage;

	public ShanHu()
		: base("珊胡", 20, new EnemyIntent[]
		{
			new(IntentType.Attack, 3, "造成 3~4 点伤害"),
			new(IntentType.Attack, 4, "造成 2 点伤害 ×2"),
			new(IntentType.Buff, 2, "武器攻击力 +2"),
		})
	{
		MoveStates = new[]
		{
			new MoveState("shanhu_a", null, new UnknownIntent()),
			new MoveState("shanhu_b", null, new UnknownIntent()),
			new MoveState("shanhu_c", null, new UnknownIntent()),
		};
		_dTurnsRemaining = Random.Shared.Next(2, 5);
		_abAFirst = Random.Shared.Next(2) == 0;
		RollCurrentSingleAttackDamage();
	}

	public override MoveState GetCurrentMove(CombatManager combat, Hero self)
	{
		if (_dTurnsRemaining <= 0)
		{
			return new MoveState(
				"shanhu_defend",
				(cm, _) => ExecuteArmorGrant(cm),
				new DefendIntent());
		}

		if (_cycleStep == 2)
		{
			return new MoveState(
				"shanhu_buff",
				(_, hero) => ApplyWeaponBuff(hero, 2),
				new BuffIntent());
		}

		bool isA = IsCurrentAttackAMove();
		if (isA)
		{
			int damage = _currentSingleAttackDamage;
			return new MoveState(
				"shanhu_single_attack",
				(cm, hero) => ExecuteAttackIntent(cm, hero),
				new SingleAttackIntent(c => DamageResolver.ResolvePreviewDamage(damage + Attack, self, ResolveAttackTarget(c, self))));
		}

		return new MoveState(
			"shanhu_multi_attack",
			(cm, hero) => ExecuteMultiHit(cm, hero, 2, 2),
			new MultiAttackIntent(c => DamageResolver.ResolvePreviewDamage(2 + Attack, self, ResolveAttackTarget(c, self)), 2));
	}

	public override void ExecuteIntent(CombatManager combat, Hero self)
	{
		_cachedAttackTarget = null;

		if (_dTurnsRemaining <= 0)
		{
			ExecuteArmorGrant(combat);
			_dTurnsRemaining = Random.Shared.Next(2, 5);
			_dJustExecuted = true;
			GD.Print($"[珊胡] 给友方护甲！下个D={_dTurnsRemaining}");
			return;
		}

		_dJustExecuted = false;
		var move = GetCurrentMove(combat, self);
		GD.Print($"[珊胡] 执行 MoveState：{move.Id}");
		move.OnPerform?.Invoke(combat, self);
	}

	public override void AdvanceMove()
	{
		if (_dJustExecuted)
		{
			_dJustExecuted = false;
		}
		else
		{
			_cycleStep = (_cycleStep + 1) % 3;
			if (_cycleStep == 0)
			{
				_abAFirst = Random.Shared.Next(2) == 0;
			}
		}

		if (_dTurnsRemaining > 0)
			_dTurnsRemaining--;

		if (_cycleStep != 2 && IsCurrentAttackAMove())
			RollCurrentSingleAttackDamage();

		_cachedAttackTarget = null;
	}

	public override void AdvanceIntent()
	{
		AdvanceMove();
	}

	private bool IsCurrentAttackAMove()
	{
		return _cycleStep == 0 ? _abAFirst : !_abAFirst;
	}

	private void RollCurrentSingleAttackDamage()
	{
		_currentSingleAttackDamage = Random.Shared.Next(3, 5);
	}

	private static void ApplyWeaponBuff(Hero self, int value)
	{
		if (self.Weapon == null)
			return;
		self.Weapon.Attack += value;
		GD.Print($"[珊胡] 武器攻击力 +{value} → {self.Weapon.Attack}");
	}

	private void ExecuteMultiHit(CombatManager combat, Hero self, int perHit, int hits)
	{
		for (int i = 0; i < hits; i++)
		{
			if (self.IsDead)
				break;

			GD.Print($"[珊胡] 多段攻击 {i + 1}/{hits}");
			combat.ExecuteEnemyHeroSmartAttack(self, perHit + Attack);
		}
	}

	private void ExecuteArmorGrant(CombatManager combat)
	{
		var candidates = combat.EnemyUnits
			.Select(u => u.Body)
			.Where(b => !b.IsDead)
			.ToList();

		if (candidates.Count == 0)
			return;
		var target = candidates[Random.Shared.Next(candidates.Count)];
		target.GainArmor(5);
		GD.Print($"[珊胡] 给 {target} 5 点护甲（当前 {target.CurrentArmor}）");
	}
}

#pragma warning restore CS0618
