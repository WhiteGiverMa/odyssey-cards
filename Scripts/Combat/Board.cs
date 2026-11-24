using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Card;
using RuntimeCard = OdysseyCards.Card.Card;

namespace OdysseyCards.Combat;

/// <summary>4/5/4 三线棋盘。部署只能进入支援线，推进是独立事务。</summary>
public sealed class Board
{
	public const int SupportCapacity = 4;
	public const int FrontlineCapacity = 5;
	private readonly Minion?[] _playerSupport = new Minion?[SupportCapacity];
	private readonly Minion?[] _enemySupport = new Minion?[SupportCapacity];
	private readonly Minion?[] _frontline = new Minion?[FrontlineCapacity];

	public IReadOnlyList<Minion?> PlayerSlots { get; }
	public IReadOnlyList<Minion?> EnemySlots { get; }
	public IReadOnlyList<Minion?> FrontlineSlots { get; }
	public FrontlineOwner FrontlineOwner { get; private set; }

	public Board()
	{
		PlayerSlots = Array.AsReadOnly(_playerSupport);
		EnemySlots = Array.AsReadOnly(_enemySupport);
		FrontlineSlots = Array.AsReadOnly(_frontline);
	}

	/// <summary>槽位已写入、规则监听尚未执行。只登记表现；源牌仅在常规手牌部署时存在。</summary>
	internal event Action<Minion, BoardPosition, RuntimeCard?>? OnMinionEntering;
	public event Action<Minion, BoardPosition>? OnMinionPlaced;
	public event Action<Minion>? OnMinionRemoved;
	public event Action<Minion>? OnMinionDied;
	public event Action<Minion, BoardPosition>? OnMinionPreRemove;
	public event Action<Minion, BoardPosition, BoardPosition>? OnMinionMoved;
	public event Action<FrontlineOwner, FrontlineOwner>? OnFrontlineOwnerChanged;

	public static BoardRow SupportRow(bool isPlayerSide) => isPlayerSide ? BoardRow.PlayerSupport : BoardRow.EnemySupport;
	public static int GetRowCapacity(BoardRow row) => row == BoardRow.Frontline ? FrontlineCapacity : SupportCapacity;

	private Minion?[] Slots(BoardRow row) => row switch
	{
		BoardRow.PlayerSupport => _playerSupport,
		BoardRow.EnemySupport => _enemySupport,
		BoardRow.Frontline => _frontline,
		_ => throw new ArgumentOutOfRangeException(nameof(row)),
	};

	public IReadOnlyList<Minion?> GetRow(BoardRow row) => row switch
	{
		BoardRow.PlayerSupport => PlayerSlots,
		BoardRow.EnemySupport => EnemySlots,
		BoardRow.Frontline => FrontlineSlots,
		_ => throw new ArgumentOutOfRangeException(nameof(row)),
	};
	public Minion? GetMinionAt(BoardPosition position)
	{
		var slots = Slots(position.Row);
		return position.SlotIndex >= 0 && position.SlotIndex < slots.Length ? slots[position.SlotIndex] : null;
	}

	/// <summary>按阵营查询支援线，绝不回退到前线。</summary>
	public Minion? GetMinionAt(int index, bool isPlayerSide) => GetMinionAt(new BoardPosition(SupportRow(isPlayerSide), index));
	public int GetEmptySlotIndex(bool isPlayerSide) => Array.FindIndex(Slots(SupportRow(isPlayerSide)), m => m == null);
	public bool CanPlaceMinion(bool isPlayerSide) => GetEmptySlotIndex(isPlayerSide) >= 0;

	/// <summary>同支援线相邻复制：左、右、任意空槽；满格返回 -1。</summary>
	public int GetAdjacentSupportSlotIndex(Minion source)
	{
		if (!Contains(source) || source.BoardRow != SupportRow(source.IsPlayerSide))
			return -1;
		var row = Slots(source.BoardRow.Value);
		int index = source.BoardSlotIndex;
		if (index > 0 && row[index - 1] == null)
			return index - 1;
		if (index + 1 < row.Length && row[index + 1] == null)
			return index + 1;
		return GetEmptySlotIndex(source.IsPlayerSide);
	}
	public bool Contains(Minion minion) => minion.BoardRow is BoardRow row
		&& ReferenceEquals(GetMinionAt(new BoardPosition(row, minion.BoardSlotIndex)), minion);

	/// <summary>只部署到所属方支援线。满格或占用时安全失败，不替换、不溢入前线。</summary>
	public bool PlaceMinion(Minion minion, int slotIndex)
	{
		return PlaceMinion(minion, slotIndex, null);
	}

	/// <summary>仅手牌部署传入原始卡牌实例；其战吼、亡语等衍生召唤不继承此来源。</summary>
	internal bool PlaceMinion(Minion minion, int slotIndex, RuntimeCard? fromHand)
	{
		ArgumentNullException.ThrowIfNull(minion);
		if (slotIndex < 0 || slotIndex >= SupportCapacity || minion.IsDead || minion.BoardRow != null)
			return false;
		var row = SupportRow(minion.IsPlayerSide);
		var slots = Slots(row);
		if (slots[slotIndex] != null)
			return false;
		slots[slotIndex] = minion;
		minion.BoardRow = row;
		minion.BoardSlotIndex = slotIndex;
		var position = new BoardPosition(row, slotIndex);
		OnMinionEntering?.Invoke(minion, position, fromHand);
		OnMinionPlaced?.Invoke(minion, position);
		return true;
	}

	public bool CanMoveMinionToFrontline(Minion minion, int preferredFrontlineSlot = -1)
	{
		if (minion.IsDead || minion.IsIncapacitated || !Contains(minion) || minion.BoardRow != SupportRow(minion.IsPlayerSide))
			return false;
		var owner = minion.IsPlayerSide ? FrontlineOwner.Player : FrontlineOwner.Enemy;
		if (FrontlineOwner != FrontlineOwner.None && FrontlineOwner != owner)
			return false;
		return preferredFrontlineSlot == -1
			? Array.Exists(_frontline, m => m == null)
			: preferredFrontlineSlot >= 0 && preferredFrontlineSlot < FrontlineCapacity && _frontline[preferredFrontlineSlot] == null;
	}

	/// <summary>原子更新两线及控制权，不触发部署、移除、死亡事件。</summary>
	public bool MoveMinionToFrontline(Minion minion, int preferredFrontlineSlot = -1)
	{
		if (!CanMoveMinionToFrontline(minion, preferredFrontlineSlot))
			return false;
		int slot = preferredFrontlineSlot == -1 ? Array.FindIndex(_frontline, m => m == null) : preferredFrontlineSlot;
		var from = new BoardPosition(minion.BoardRow!.Value, minion.BoardSlotIndex);
		var to = new BoardPosition(BoardRow.Frontline, slot);
		Slots(from.Row)[from.SlotIndex] = null;
		_frontline[slot] = minion;
		minion.BoardRow = to.Row;
		minion.BoardSlotIndex = slot;
		SetFrontlineOwner(minion.IsPlayerSide ? FrontlineOwner.Player : FrontlineOwner.Enemy);
		OnMinionMoved?.Invoke(minion, from, to);
		return true;
	}

	public void RemoveMinion(Minion minion) => Remove(minion, died: true);
	public bool RecallMinion(Minion minion) => Remove(minion, died: false);

	private bool Remove(Minion minion, bool died)
	{
		ArgumentNullException.ThrowIfNull(minion);
		if (!Contains(minion))
			return false;
		var position = new BoardPosition(minion.BoardRow!.Value, minion.BoardSlotIndex);
		OnMinionPreRemove?.Invoke(minion, position);
		Slots(position.Row)[position.SlotIndex] = null;
		minion.BoardRow = null;
		minion.BoardSlotIndex = -1;
		if (position.Row == BoardRow.Frontline && Array.TrueForAll(_frontline, m => m == null))
			SetFrontlineOwner(FrontlineOwner.None);
		OnMinionRemoved?.Invoke(minion);
		if (died)
			OnMinionDied?.Invoke(minion);
		return true;
	}

	private void SetFrontlineOwner(FrontlineOwner owner)
	{
		if (FrontlineOwner == owner)
			return;
		var previous = FrontlineOwner;
		FrontlineOwner = owner;
		OnFrontlineOwnerChanged?.Invoke(previous, owner);
	}

	/// <summary>包含待清理的死亡单位，供死亡扫描使用。</summary>
	public IEnumerable<Minion> GetAllMinions() => _enemySupport.Concat(_frontline).Concat(_playerSupport).OfType<Minion>();
	public List<Minion> GetPlayerMinions() => GetAllMinions().Where(m => m.IsPlayerSide && !m.IsDead).ToList();
	public List<Minion> GetEnemyMinions() => GetAllMinions().Where(m => !m.IsPlayerSide && !m.IsDead).ToList();
	public List<Minion> GetTaunts(bool ofEnemy) => GetAllMinions().Where(m => m.IsPlayerSide != ofEnemy && m.HasTaunt && !m.IsDead).ToList();
}
