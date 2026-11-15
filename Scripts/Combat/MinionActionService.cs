using System;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>移动与一步突击的验证/扣费事务。失败不得扣油、改变站位或触发首攻被动。</summary>
internal sealed class MinionActionService(Board board, AttackTracker tracker, Hero owner)
{
	public event Action<Minion>? OnAssaultAdvance;
	public bool CanMove(Minion minion, int slot = -1) => minion.IsPlayerSide == owner.IsPlayerSide
		&& tracker.CanMove(minion) && board.CanMoveMinionToFrontline(minion, slot) && owner.CanSpendMana(minion.ActionCost);

	public bool Move(Minion minion, int slot = -1)
	{
		if (!CanMove(minion, slot))
			return false;
		owner.SpendMana(minion.ActionCost);
		board.MoveMinionToFrontline(minion, slot);
		tracker.RecordMove(minion);
		return true;
	}

	public bool CanAttack(Minion minion, IDamageTarget target, out bool advances)
	{
		advances = false;
		if (minion.IsPlayerSide != owner.IsPlayerSide || !board.Contains(minion) || !tracker.CanAttack(minion) || minion.Attack <= 0)
			return false;
		if (AttackTargetRules.CanAttackTarget(board, minion, target))
			return owner.CanSpendMana(minion.ActionCost);
		advances = minion.UnitClass == UnitClass.Assault && CanMove(minion)
			&& owner.CurrentMana >= 2L * minion.ActionCost
			&& AttackTargetRules.CanAttackTarget(board, minion, target, BoardRow.Frontline);
		return advances;
	}

	public bool Attack(Minion minion, IDamageTarget target, Action resolveCombat, Action? firstAttack = null)
	{
		if (!CanAttack(minion, target, out bool advances))
			return false;
		if (advances)
		{
			if (!Move(minion))
				return false;
			OnAssaultAdvance?.Invoke(minion);
		}
		owner.SpendMana(minion.ActionCost);
		firstAttack?.Invoke();
		resolveCombat();
		tracker.RecordAttack(minion);
		return true;
	}
}
