using System;
using Godot;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

#pragma warning disable CS0618

namespace OdysseyCards.AI;

/// <summary>
/// 张郎 — 第2层精英敌人。
/// 被动：固璋（3）— 单次受到的生命伤害最高 3（由 CombatManager 初始化时注入 DamageCapModifier）。
/// 意图：A(3~4伤害) / B(1伤害×3) 随机交换 → C(武器攻击+1) → 循环。
/// D 每 2~4 回合替代主意图：召唤机械小蠊。
/// </summary>
public class ZhangLang : EnemyEncounter
{
	private int _dTurnsRemaining;
	private bool _dJustExecuted;
	private bool _abAFirst;
	private int _cycleStep; // 0=first-AB, 1=second-AB, 2=C
	private int _currentSingleAttackDamage;

	public ZhangLang()
		: base("张郎", 20, new EnemyIntent[]
		{
			new(IntentType.Attack, 3, "造成 3~4 点伤害"),
			new(IntentType.Attack, 3, "造成 1 点伤害 ×3"),
			new(IntentType.Buff, 1, "武器攻击力 +1"),
		})
	{
		MoveStates = new[]
		{
			new MoveState("zhanglang_a", null, new UnknownIntent()),
			new MoveState("zhanglang_b", null, new UnknownIntent()),
			new MoveState("zhanglang_c", null, new UnknownIntent()),
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
				"zhanglang_summon",
				(cm, _) => ExecuteSummon(cm),
				new SummonIntent());
		}

		if (_cycleStep == 2)
		{
			return new MoveState(
				"zhanglang_buff",
				(_, hero) => ApplyWeaponBuff(hero, 1),
				new BuffIntent());
		}

		bool isA = IsCurrentAttackAMove();
		if (isA)
		{
			int damage = _currentSingleAttackDamage;
			return new MoveState(
				"zhanglang_single_attack",
				(cm, hero) => ExecuteAttackIntent(cm, hero),
				new SingleAttackIntent(c => DamageResolver.ResolvePreviewDamage(damage + Attack, self, ResolveAttackTarget(c, self))));
		}

		return new MoveState(
			"zhanglang_multi_attack",
			(cm, hero) => ExecuteMultiHit(cm, hero, 1, 3),
			new MultiAttackIntent(c => DamageResolver.ResolvePreviewDamage(1 + Attack, self, ResolveAttackTarget(c, self)), 3));
	}

	public override void ExecuteIntent(CombatManager combat, Hero self)
	{
		_cachedAttackTarget = null;

		if (_dTurnsRemaining <= 0)
		{
			ExecuteSummon(combat);
			_dTurnsRemaining = Random.Shared.Next(2, 5);
			_dJustExecuted = true;
			GD.Print($"[张郎] 召唤机械小蠊！下个D={_dTurnsRemaining}");
			return;
		}

		_dJustExecuted = false;
		var move = GetCurrentMove(combat, self);
		GD.Print($"[张郎] 执行 MoveState：{move.Id}");
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
		GD.Print($"[张郎] 武器攻击力 +{value} → {self.Weapon.Attack}");
	}

	private void ExecuteMultiHit(CombatManager combat, Hero self, int perHit, int hits)
	{
		for (int i = 0; i < hits; i++)
		{
			if (self.IsDead)
				break;

			GD.Print($"[张郎] 多段攻击 {i + 1}/{hits}");
			combat.ExecuteEnemyHeroSmartAttack(self, perHit + Attack);
		}
	}

	private void ExecuteSummon(CombatManager combat)
	{
		if (!combat.Board.CanPlaceMinion(isPlayerSide: false))
			return;

		const string path = "res://Resources/Cards/Minion_Roach.tres";
		if (!ResourceLoader.Exists(path))
		{
			GD.PrintErr($"[张郎] 未找到机械小蠊卡牌资源：{path}");
			return;
		}

		var data = GD.Load<CardData>(path);
		if (data == null)
			return;

		var roach = new Minion(data, isPlayerSide: false);
		roach.IntentBrain = new MechanicalRoachBrain(roach);
		int slot = combat.Board.GetEmptySlotIndex(isPlayerSide: false);
		combat.Board.PlaceMinion(roach, slot);
		GD.Print($"[张郎] 槽位{slot} 召唤机械小蠊 ({roach.Attack}/{roach.CurrentHealth})，已挂载意图大脑");
	}
}

#pragma warning restore CS0618
