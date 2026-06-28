using System;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>
/// 默认敌方随从意图大脑——使用 MoveState 自循环，每回合攻击随机合法目标。
/// 参考 STS2 SneakyGremlin 的 TACKLE_MOVE 自循环模式。
/// </summary>
public class DefaultAttackMinionBrain : IIntentActor
{
	private readonly Minion _body;
	private readonly MoveState _attackMove;
	private MoveState _currentMove;

	public Hero? OwnerHero => null;
	public bool HasMoveStates => true;

	public DefaultAttackMinionBrain(Minion body)
	{
		_body = body ?? throw new ArgumentNullException(nameof(body));

		// 单个 MoveState：攻击 → 自循环
		_attackMove = new MoveState("ATTACK", OnPerformAttack,
			new SingleAttackIntent(_ => _body.Attack));
		_attackMove.FollowUpState = _attackMove;
		_currentMove = _attackMove;
	}

	private void OnPerformAttack(CombatManager combat, Hero? _)
	{
		combat.ExecuteEnemyMinionSmartAttack(_body);
	}

	// ── MoveState 入口 ──

	public MoveState? GetCurrentMove(CombatManager combat) => _currentMove;

	public void AdvanceMove()
	{
		_currentMove = _currentMove.FollowUpState ?? _currentMove;
	}

	// ── 旧系统兼容（箭头绘制 / 日志）──

	public EnemyIntent GetCurrentIntent(CombatManager combat)
	{
		IDamageTarget target = combat.SelectSmartEnemyAttackTarget(_body, _body.Attack);

		int dmg = DamageResolver.ResolvePreviewDamage(_body.Attack, _body, target);
		string targetName = target switch
		{
			Hero => Localization.Localization.T("intent.target_hero", "英雄"),
			Minion m => m.GetLocalizedName(),
			_ => "目标"
		};

		var intent = new EnemyIntent(IntentType.Attack, dmg,
			Localization.Localization.T("intent.attack_format", "对{target}造成 {damage} 点伤害")
				.Replace("{target}", targetName)
				.Replace("{damage}", dmg.ToString()));
		intent.TargetSelector = _ => target;
		return intent;
	}

	public void ExecuteIntent(CombatManager combat)
	{
		OnPerformAttack(combat, null);
	}

	public void AdvanceIntent()
	{
		// 自循环，无需变更
	}
}
