using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>
/// 智能臭鸡蛋——敌方衍生随从大脑。
/// 下个敌方回合对玩家方全体造成 4 点法术伤害，然后自毁。
/// </summary>
public sealed class SmartStinkyEggBrain : IIntentActor
{
	private readonly Minion _body;
	private readonly MoveState _burstMove;

	public Hero? OwnerHero => null;
	public bool HasMoveStates => true;

	public SmartStinkyEggBrain(Minion body)
	{
		_body = body ?? throw new ArgumentNullException(nameof(body));
		_burstMove = new MoveState(
			"smart_stinky_egg_burst",
			OnPerformBurst,
			new SpellDamageIntent(
				spellName: "臭蛋爆裂",
				spellDescription: "对玩家方全体造成 4 点法术伤害，然后死亡",
				damageCalc: combat => DamageResolver.ResolvePreviewDamage(4, _body, combat.PlayerHero, DamageKind.Effect)));
		_burstMove.FollowUpState = _burstMove;
	}

	public MoveState? GetCurrentMove(CombatManager combat) => _burstMove;

	public void AdvanceMove()
	{
	}

	public EnemyIntent GetCurrentIntent(CombatManager combat)
	{
		int damage = DamageResolver.ResolvePreviewDamage(4, _body, combat.PlayerHero, DamageKind.Effect);
		return new EnemyIntent(IntentType.SpellCast, damage, $"对玩家方全体造成 {damage} 点法术伤害，然后死亡");
	}

	public void ExecuteIntent(CombatManager combat)
	{
		OnPerformBurst(combat, null);
	}

	public void AdvanceIntent()
	{
	}

	private void OnPerformBurst(CombatManager combat, Hero? _)
	{
		var targets = new List<IDamageTarget> { combat.PlayerHero };
		foreach (var minion in combat.Board.GetPlayerMinions())
		{
			if (!minion.IsDead)
				targets.Add(minion);
		}

		foreach (var target in targets)
		{
			if (target is Minion minion && minion.IsDead)
				continue;

			combat.RequestDamageVfx(_body, target, DamageKind.Effect, CombatDamageVfxKind.Spell);
			target.TakeDamage(4, _body, DamageKind.Effect);
		}

		_body.TakeDamage(_body.CurrentHealth + _body.CurrentArmor + Math.Max(0, _body.Defense) + 1, _body, DamageKind.Effect);
		if (_body.IsDead)
			combat.Board.RemoveMinion(_body);

		GD.Print("[智能臭鸡蛋] 臭蛋爆裂：对玩家方全体造成法术伤害后自毁");
	}
}
