using System.Collections.Generic;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>四兵种行动配额。部署冷却、移动与攻击次数不依赖 UI 或法力镜像。</summary>
internal sealed class AttackTracker
{
	private readonly HashSet<Minion> _ready = new();
	private readonly HashSet<Minion> _moved = new();
	private readonly Dictionary<Minion, int> _attacks = new();

	public void Reset()
	{
		_ready.Clear();
		_moved.Clear();
		_attacks.Clear();
	}

	public void AddCharged(Minion minion) => _ready.Add(minion);
	public void RegisterDeployment(Minion minion)
	{
		Remove(minion);
		if (minion.HasCharge)
			AddCharged(minion);
	}

	private bool IsReady(Minion minion) => !minion.IsDead && !minion.IsIncapacitated && minion.BoardRow != null && _ready.Contains(minion);
	public bool CanMove(Minion minion) => IsReady(minion) && !_moved.Contains(minion)
		&& (minion.UnitClass == UnitClass.Assault || _attacks.GetValueOrDefault(minion) == 0);
	public bool CanAttack(Minion minion) => IsReady(minion) && !minion.HasStatusEffect("attack_ban")
		&& (minion.UnitClass == UnitClass.Assault || !_moved.Contains(minion))
		&& _attacks.GetValueOrDefault(minion) < (minion.UnitClass == UnitClass.Assault && minion.HasWindfury ? 2 : 1);
	public void RecordMove(Minion minion) => _moved.Add(minion);
	public void RecordAttack(Minion minion) => _attacks[minion] = _attacks.GetValueOrDefault(minion) + 1;
	public void Remove(Minion minion)
	{
		_ready.Remove(minion);
		_moved.Remove(minion);
		_attacks.Remove(minion);
	}
}
