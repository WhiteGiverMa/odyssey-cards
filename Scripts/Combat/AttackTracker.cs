using System.Collections.Generic;
using OdysseyCards.Card;

namespace OdysseyCards.Combat;

/// <summary>
/// 攻击追踪器——管理本回合内随从的"可否攻击"和"已攻击次数"状态。
/// 从 CombatManager 拆出，解除回合流转/攻击系统/死亡处理之间的数据耦合。
/// </summary>
internal sealed class AttackTracker
{
	private readonly HashSet<Minion> _canAttackThisTurn = new();
	private readonly Dictionary<Minion, int> _attackCountThisTurn = new();

	public void Reset()
	{
		_canAttackThisTurn.Clear();
		_attackCountThisTurn.Clear();
	}

	public void AddCharged(Minion minion) => _canAttackThisTurn.Add(minion);

	public bool CanAttack(Minion attacker)
	{
		if (!_canAttackThisTurn.Contains(attacker))
			return false;
		int attacks = _attackCountThisTurn.GetValueOrDefault(attacker, 0);
		int maxAttacks = attacker.HasWindfury ? 2 : 1;
		return attacks < maxAttacks;
	}

	public void RecordAttack(Minion attacker)
	{
		int newCount = _attackCountThisTurn.GetValueOrDefault(attacker, 0) + 1;
		_attackCountThisTurn[attacker] = newCount;
		int maxAttacks = attacker.HasWindfury ? 2 : 1;
		if (newCount >= maxAttacks)
			_canAttackThisTurn.Remove(attacker);
	}

	public void Remove(Minion minion)
	{
		_canAttackThisTurn.Remove(minion);
		_attackCountThisTurn.Remove(minion);
	}
}
