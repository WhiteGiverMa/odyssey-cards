using System;
using Godot;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>
/// 智能臭鸡蛋——敌方衍生随从大脑。
/// 下个敌方回合自爆（自杀触发亡语），亡语对敌方全体造成法术伤害。
/// 伤害逻辑由亡语机制（DeathHandler + CardEffectDispatcher）统一处理，Brain 只驱动自杀意图。
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
				spellDescription: "自爆：亡语对玩家方（你）全体造成 4 点法术伤害",
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
		return new EnemyIntent(IntentType.SpellCast, damage, $"自爆：亡语对玩家方（你）全体造成 {damage} 点法术伤害");
	}

	public void ExecuteIntent(CombatManager combat)
	{
		OnPerformBurst(combat, null);
	}

	public void AdvanceIntent()
	{
	}

	/// <summary>
	/// 自爆：自杀触发亡语。亡语机制自动对敌方全体造成法术伤害。
	/// </summary>
	private void OnPerformBurst(CombatManager combat, Hero? _)
	{
		// 自杀：伤害值足够击杀自身（含护甲与格挡）
		_body.TakeDamage(_body.CurrentHealth + _body.CurrentArmor + Math.Max(0, _body.Defense) + 1, _body, DamageKind.Effect);
		if (_body.IsDead)
			combat.Board.RemoveMinion(_body);

		GD.Print("[智能臭鸡蛋] 自爆：自杀触发亡语");
	}
}
