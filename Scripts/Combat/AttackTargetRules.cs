using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>普通攻击唯一目标规则：基础射程 → 嘲讽 → 邻格守护 → 游击护航。</summary>
public static class AttackTargetRules
{
	public static bool BaseReachable(Board board, IDamageSource attacker, IDamageTarget target, BoardRow? attackerRow = null)
	{
		ArgumentNullException.ThrowIfNull(board);
		ArgumentNullException.ThrowIfNull(attacker);
		ArgumentNullException.ThrowIfNull(target);
		if (attacker is not IDamageTarget sourceTarget || IsDead(sourceTarget) || IsDead(target))
			return false;
		bool targetSide = target switch { Minion m => m.IsPlayerSide, Hero h => h.IsPlayerSide, _ => attacker.IsPlayerSide };
		if (attacker.IsPlayerSide == targetSide || target is Minion tm && (!board.Contains(tm) || tm.HasSmokeScreen)
			|| target is Hero { HasSmokeScreen: true })
			return false;
		if (attacker is Minion am && !board.Contains(am))
			return false;
		if (attacker is Hero { Weapon: null })
			return false;

		bool ranged = attacker is Minion { UnitClass: UnitClass.Support or UnitClass.Skirmisher }
			|| attacker is Hero { Weapon.Range: WeaponRange.Ranged };
		if (attacker is Hero { Weapon.Range: WeaponRange.FriendlySupport })
			return false;
		if (ranged)
			return true;
		if (target is Minion { UnitClass: UnitClass.Skirmisher, BoardRow: not BoardRow.Frontline })
			return false;

		bool targetFrontline = GetTargetRow(target) == BoardRow.Frontline;
		if (attacker is Minion minion)
			return (attackerRow ?? minion.BoardRow) == BoardRow.Frontline ? !targetFrontline : targetFrontline;
		var opposingOwner = attacker.IsPlayerSide ? FrontlineOwner.Enemy : FrontlineOwner.Player;
		return board.FrontlineOwner == opposingOwner ? targetFrontline : !targetFrontline;
	}

	public static bool CanAttackTarget(Board board, IDamageSource attacker, IDamageTarget target, BoardRow? attackerRow = null)
	{
		if (!BaseReachable(board, attacker, target, attackerRow))
			return false;
		BoardRow row = target is Minion minion ? minion.BoardRow!.Value : Board.SupportRow(!attacker.IsPlayerSide);
		var reachable = board.GetAllMinions()
			.Where(m => BaseReachable(board, attacker, m, attackerRow)).ToList();
		bool tauntIntercepts = reachable.Any(m => m.HasTaunt && (target is Hero || m.BoardRow == row));
		if (tauntIntercepts)
			return target is Minion { HasTaunt: true };
		if (target is Minion { HasTaunt: true })
			return true;

		// 暂时调整：支援阵线的存活守护单位同样保护所属方英雄（失能守护不提供保护）。
		// 该守护本身仍在 reachable 中，可被优先拆除，因此不会形成无解防线。
		if (target is Hero heroTarget)
		{
			var heroRow = Board.SupportRow(heroTarget.IsPlayerSide);
			if (reachable.Any(g => g.HasGuard && !g.IsIncapacitated && g.BoardRow == heroRow))
				return false;
		}

		if (target is Minion { HasGuard: false } guarded && reachable.Any(g => Protects(g, guarded)))
			return false;
		if (attacker is not Minion { UnitClass: UnitClass.Skirmisher })
			return true;
		var escorts = reachable.Where(m => m.BoardRow == row && m.UnitClass == UnitClass.Skirmisher && !m.IsIncapacitated).ToList();
		if (escorts.Count == 0 || target is Minion { UnitClass: UnitClass.Skirmisher })
			return true;
		// 挡刀守护主动暴露：允许先拆守护，再拆游击，绝不形成护航/守护循环锁。
		return target is Minion { HasGuard: true } guard && guard.UnitClass != UnitClass.Skirmisher
			&& escorts.Any(escort => Protects(guard, escort));
	}

	private static bool Protects(Minion guard, Minion target) => guard.HasGuard && !target.HasGuard && !target.HasTaunt
		&& guard.BoardRow == target.BoardRow && Math.Abs(guard.BoardSlotIndex - target.BoardSlotIndex) == 1;

	public static List<IDamageTarget> GetLegalAttackTargets(Board board, IDamageSource attacker, IEnumerable<IDamageTarget> candidates)
		=> candidates.Where(target => CanAttackTarget(board, attacker, target)).ToList();

	/// <summary>限前线法术只应用前线阻挡，不应用烟幕、嘲讽或游击免摸。</summary>
	public static bool CanTargetFrontlineSpell(Board board, bool casterIsPlayerSide, IDamageTarget target)
	{
		bool opposingFrontline = board.FrontlineOwner == (casterIsPlayerSide ? FrontlineOwner.Enemy : FrontlineOwner.Player);
		return !IsDead(target) && (target is not Minion minion || board.Contains(minion))
			&& (!opposingFrontline || target is Minion { BoardRow: BoardRow.Frontline });
	}

	private static bool IsDead(IDamageTarget target) => target switch { Minion m => m.IsDead, Hero h => h.IsDead, _ => true };
	private static BoardRow? GetTargetRow(IDamageTarget target) => target is Minion minion ? minion.BoardRow : null;
}
