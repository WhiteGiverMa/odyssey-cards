using System;
using System.Collections.Generic;

namespace OdysseyCards.Combat;

/// <summary>战斗日志条目类型。</summary>
public enum CombatLogEntryKind
{
	TurnStart,
	DeployUnit,
	PlayOrder,
	PlayDomain,
	CountermeasureTriggered,
	AdvanceFrontline,
	Attack,
	WeaponAction,
	HeroPower,
	EffectTriggered,
	UnitDestroyed,
}

/// <summary>聚合在一次行动后的实际结果徽章。</summary>
public enum CombatLogTagKind
{
	Damage,
	CounterDamage,
	Heal,
	Armor,
	Kill,
	Canceled,
	Intel,
	Draw,
	Discard,
	Summon,
	Status,
	Move,
	Countermeasure,
}

/// <summary>
/// 战斗日志结果标签；纯数据，可由 UI 映射为纯色徽章或可悬停的卡面芯片。
/// 带 <see cref="Card"/> 的标签用于弃牌等需要展示具体卡牌的行动；
/// <see cref="IsPlayerCard"/> 记录该卡牌的归属阵营，供 UI 着色。
/// </summary>
public readonly record struct CombatLogTag(
	CombatLogTagKind Kind,
	int Value = 0,
	string? Text = null,
	Card.Card? Card = null,
	bool? IsPlayerCard = null);

/// <summary>一条带主体/目标历史卡面快照的战术行动。</summary>
public sealed class CombatLogEntry
{
	private readonly List<CombatLogTag> _tags = new();
	private readonly IReadOnlyList<CombatLogTag> _readOnlyTags;

	public CombatLogEntry(
		int turnNumber,
		bool isPlayerSide,
		CombatLogEntryKind kind,
		string actorName,
		Card.Card? sourceCard = null,
		string? targetName = null,
		Card.Card? targetCard = null)
	{
		TurnNumber = turnNumber;
		IsPlayerSide = isPlayerSide;
		Kind = kind;
		ActorName = actorName ?? string.Empty;
		SourceCard = sourceCard?.CreateSnapshot();
		TargetName = targetName;
		TargetCard = targetCard?.CreateSnapshot();
		_readOnlyTags = _tags.AsReadOnly();
	}

	public int TurnNumber { get; }
	public bool IsPlayerSide { get; }
	public CombatLogEntryKind Kind { get; }
	public string ActorName { get; }
	public Card.Card? SourceCard { get; }
	public string? TargetName { get; }
	public Card.Card? TargetCard { get; }
	public IReadOnlyList<CombatLogTag> Tags => _readOnlyTags;

	/// <summary>添加实际结果；数值型同类结果会聚合，击杀等离散事件保持独立。</summary>
	public void AddTag(CombatLogTag tag)
	{
		// 带真实卡面的标签必须逐张保留，绝不能被纯数字聚合吞掉卡面。
		if (tag.Card == null && CanMerge(tag.Kind))
		{
			int index = _tags.FindIndex(existing => existing.Card == null
				&& existing.Kind == tag.Kind && existing.Text == tag.Text);
			if (index >= 0)
			{
				_tags[index] = _tags[index] with { Value = _tags[index].Value + tag.Value };
				return;
			}
		}
		_tags.Add(tag);
	}

	private static bool CanMerge(CombatLogTagKind kind) => kind is CombatLogTagKind.Damage
		or CombatLogTagKind.CounterDamage or CombatLogTagKind.Heal or CombatLogTagKind.Armor
		or CombatLogTagKind.Intel or CombatLogTagKind.Draw or CombatLogTagKind.Discard
		or CombatLogTagKind.Summon;
}

/// <summary>
/// 纯 C# 战斗日志服务。支持行动作用域聚合、200 条环形上限与 UI 添加/清空事件。
/// 反制待命状态不属于本服务输入；只有真实结算点应写入条目。
/// </summary>
public sealed class CombatLogService
{
	public const int DefaultCapacity = 200;
	private readonly List<CombatLogEntry> _entries = new();
	private readonly IReadOnlyList<CombatLogEntry> _readOnlyEntries;
	private readonly Stack<CombatLogEntry> _pendingActions = new();

	public CombatLogService(int capacity = DefaultCapacity)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
		Capacity = capacity;
		_readOnlyEntries = _entries.AsReadOnly();
	}

	public int Capacity { get; }
	public IReadOnlyList<CombatLogEntry> Entries => _readOnlyEntries;
	public bool HasPendingAction => _pendingActions.Count > 0;
	public event Action<CombatLogEntry>? OnEntryAdded;
	public event Action? OnCleared;

	private CombatLogEntry? PendingAction => _pendingActions.Count > 0 ? _pendingActions.Peek() : null;

	/// <summary>开始一次行动作用域；被动嵌套触发时内层先提交，外层继续聚合。</summary>
	public CombatLogEntry BeginAction(
		int turnNumber,
		bool isPlayerSide,
		CombatLogEntryKind kind,
		string actorName,
		Card.Card? sourceCard = null,
		string? targetName = null,
		Card.Card? targetCard = null)
	{
		var entry = new CombatLogEntry(turnNumber, isPlayerSide, kind, actorName, sourceCard, targetName, targetCard);
		_pendingActions.Push(entry);
		return entry;
	}

	public void RecordDamage(int amount, bool isCounterDamage = false)
	{
		if (amount > 0)
			PendingAction?.AddTag(new CombatLogTag(isCounterDamage ? CombatLogTagKind.CounterDamage : CombatLogTagKind.Damage, amount));
	}

	public void RecordHeal(int amount)
	{
		if (amount > 0)
			PendingAction?.AddTag(new CombatLogTag(CombatLogTagKind.Heal, amount));
	}

	public void RecordArmor(int amount)
	{
		if (amount > 0)
			PendingAction?.AddTag(new CombatLogTag(CombatLogTagKind.Armor, amount));
	}

	public void RecordIntel(int amount)
	{
		if (amount > 0)
			PendingAction?.AddTag(new CombatLogTag(CombatLogTagKind.Intel, amount));
	}

	public void RecordDraw(int amount)
	{
		if (amount > 0)
			PendingAction?.AddTag(new CombatLogTag(CombatLogTagKind.Draw, amount));
	}

	public void RecordDiscard(int amount)
	{
		if (amount > 0)
			PendingAction?.AddTag(new CombatLogTag(CombatLogTagKind.Discard, amount));
	}

	/// <summary>
	/// 记录一张真实被弃掉的牌：保存历史卡面快照与归属阵营，
	/// 供战报抽屉渲染可悬停/点按的卡面芯片。每张牌独立成标签，绝不合并。
	/// </summary>
	public void RecordDiscard(Card.Card card, bool? isPlayerCard = null)
	{
		if (card == null)
			return;
		PendingAction?.AddTag(new CombatLogTag(
			CombatLogTagKind.Discard, 1, null, card.CreateSnapshot(), isPlayerCard));
	}

	public void RecordSummon(int amount = 1)
	{
		if (amount > 0)
			PendingAction?.AddTag(new CombatLogTag(CombatLogTagKind.Summon, amount));
	}

	public void RecordUnitKilled(string? targetName = null)
	{
		PendingAction?.AddTag(new CombatLogTag(CombatLogTagKind.Kill, 0, targetName));
	}

	public void RecordCanceled()
	{
		PendingAction?.AddTag(new CombatLogTag(CombatLogTagKind.Canceled));
	}

	public void RecordStatus(string? text = null)
	{
		PendingAction?.AddTag(new CombatLogTag(CombatLogTagKind.Status, 0, text));
	}

	public void RecordMove()
	{
		PendingAction?.AddTag(new CombatLogTag(CombatLogTagKind.Move));
	}

	public void RecordCountermeasure(int amount = 1)
	{
		if (amount > 0)
			PendingAction?.AddTag(new CombatLogTag(CombatLogTagKind.Countermeasure, amount));
	}

	/// <summary>
	/// 记录一次反制的真实结算：反制徽章、逐张实际被弃掉的牌（含卡面快照）与取消徽章。
	/// 真实触发但对方无牌可弃时，<paramref name="discardedCards"/> 为空——绝不补记数字弃牌。
	/// </summary>
	public void RecordCountermeasureResult(
		bool canceled, IReadOnlyList<Card.Card>? discardedCards, bool discardedCardsArePlayerSide)
	{
		RecordCountermeasure();
		if (discardedCards != null)
		{
			foreach (Card.Card card in discardedCards)
				RecordDiscard(card, discardedCardsArePlayerSide);
		}
		if (canceled)
			RecordCanceled();
	}

	/// <summary>
	/// 提交当前行动并通知抽屉；重复提交为空操作。
	/// 传入 <paramref name="scope"/> 时会按引用校验栈序：作用域已被提交则无操作，
	/// 位于栈中间（异常/嵌套打乱顺序）时按引用移除后提交，绝不误提交外层作用域。
	/// </summary>
	public CombatLogEntry? CommitAction(CombatLogEntry? scope = null)
	{
		if (_pendingActions.Count == 0)
			return null;
		if (scope == null || ReferenceEquals(_pendingActions.Peek(), scope))
		{
			var committed = _pendingActions.Pop();
			Record(committed);
			return committed;
		}
		if (!RemovePendingAction(scope))
			return null;
		Record(scope);
		return scope;
	}

	/// <summary>按引用从待提交栈中移除；栈序被嵌套打乱时重建，返回是否曾存在。</summary>
	private bool RemovePendingAction(CombatLogEntry scope)
	{
		if (!_pendingActions.Contains(scope))
			return false;
		var kept = new List<CombatLogEntry>(_pendingActions.Count);
		foreach (CombatLogEntry pending in _pendingActions)
		{
			if (!ReferenceEquals(pending, scope))
				kept.Add(pending);
		}
		_pendingActions.Clear();
		for (int i = kept.Count - 1; i >= 0; i--)
			_pendingActions.Push(kept[i]);
		return true;
	}

	/// <summary>直接记录一个无需作用域的完整条目（回合头、被动触发等）。</summary>
	public void Record(CombatLogEntry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);
		_entries.Add(entry);
		if (_entries.Count > Capacity)
			_entries.RemoveRange(0, _entries.Count - Capacity);
		OnEntryAdded?.Invoke(entry);
	}

	public void RecordTurnStart(int turnNumber, bool isPlayerSide, string actorName)
	{
		Record(new CombatLogEntry(turnNumber, isPlayerSide, CombatLogEntryKind.TurnStart, actorName));
	}

	public void Clear()
	{
		_pendingActions.Clear();
		_entries.Clear();
		OnCleared?.Invoke();
	}
}
