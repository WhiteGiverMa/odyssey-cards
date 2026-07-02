#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>
/// 选择系统——管理发现选牌、手牌选择等所有选择交互的状态和逻辑。
/// 从 CombatManager 中拆出，让 CombatManager 仅保留战斗流转与玩家操作入口。
/// </summary>
internal sealed class SelectionSystem
{
	// ===== 依赖 =====

	private readonly Hero _playerHero;
	private readonly CommanderCore _playerCore;
	private readonly Board _board;
	private readonly GameState _state;
	private readonly Action _notifyCombatStateChanged;
	private readonly Action _checkDeaths;
	private readonly Func<bool> _checkVictoryOrDefeat;

	// ===== 发现选牌状态 =====

	/// <summary>
	/// 发现选牌候选卡牌列表（null 表示不在发现阶段）。
	/// </summary>
	private List<CardData>? _pendingDiscoverOptions;

	/// <summary>
	/// 当前发现/选牌界面候选的运行时卡牌。用于从弃牌堆选择原卡牌实例。
	/// </summary>
	private List<Card.Card>? _pendingDiscoverRuntimeOptions;

	/// <summary>
	/// 触发发现效果的法术牌（选牌完成后从手牌移除）。
	/// </summary>
	private Card.Card? _pendingDiscoverSpellCard;

	/// <summary>
	/// 当前选牌模式，供 UI 读取以自定义标题/行为。
	/// </summary>
	private CombatManager.PendingSelectionMode _pendingSelectionMode = CombatManager.PendingSelectionMode.Discover;

	// ===== 手牌选择状态（STS2 风格） =====

	/// <summary>
	/// 当前待选的手牌列表。null 表示不在手牌选择模式。
	/// </summary>
	private List<Card.Card>? _pendingHandDiscardSelection;

	/// <summary>
	/// 手牌选择需选的最小张数。
	/// </summary>
	private int _pendingDiscardMin;

	/// <summary>
	/// 手牌选择可选的最大张数。
	/// </summary>
	private int _pendingDiscardMax;

	/// <summary>
	/// 是否为刀盾危机模式（完成后需要放置 Token + 抽牌）。
	/// </summary>
	private bool _pendingDiscardIsBladeCrisis;

	/// <summary>
	/// 手牌选择是否允许取消。强制弃牌效果支付后不能取消。
	/// </summary>
	private bool _pendingHandSelectCanCancel = true;

	/// <summary>
	/// 自定义手牌选择完成回调。为空时使用默认弃牌/刀盾危机结算。
	/// </summary>
	private Action<IReadOnlyList<Card.Card>>? _pendingHandSelectConfirmed;

	// ===== 公开属性 =====

	/// <summary>
	/// 当前正在等待玩家进行发现选牌或手牌选择。
	/// </summary>
	public bool IsDiscovering => _pendingDiscoverOptions != null || _pendingHandDiscardSelection != null;

	/// <summary>
	/// 当前发现选牌的 N 张候选卡牌（只读）。
	/// </summary>
	public IReadOnlyList<CardData>? DiscoverOptions => _pendingDiscoverOptions?.AsReadOnly();

	/// <summary>
	/// 当前选牌需要选择的张数。
	/// </summary>
	public int DiscoverPickCount { get; private set; } = 1;

	/// <summary>
	/// 当前选牌是否使用运行时卡牌实例。
	/// </summary>
	public IReadOnlyList<Card.Card>? DiscoverRuntimeOptions => _pendingDiscoverRuntimeOptions?.AsReadOnly();

	/// <summary>
	/// 当前选牌模式，供 UI 读取以自定义标题/行为。
	/// </summary>
	public CombatManager.PendingSelectionMode CurrentSelectionMode => _pendingSelectionMode;

	/// <summary>
	/// 是否处于手牌选择模式（STS2 风格）。
	/// </summary>
	public bool IsHandSelecting => _pendingHandDiscardSelection != null;

	/// <summary>
	/// 手牌选择模式的待选卡牌列表（只读）。
	/// </summary>
	public IReadOnlyList<Card.Card>? HandSelectOptions => _pendingHandDiscardSelection?.AsReadOnly();

	/// <summary>
	/// 手牌选择最少需选张数。
	/// </summary>
	public int HandSelectMin => _pendingDiscardMin;

	/// <summary>
	/// 手牌选择最多可选张数。
	/// </summary>
	public int HandSelectMax => _pendingDiscardMax;

	// ===== 构造函数 =====

	public SelectionSystem(
		Hero playerHero,
		CommanderCore playerCore,
		Board board,
		GameState state,
		Action notifyCombatStateChanged,
		Action checkDeaths,
		Func<bool> checkVictoryOrDefeat)
	{
		_playerHero = playerHero ?? throw new ArgumentNullException(nameof(playerHero));
		_playerCore = playerCore ?? throw new ArgumentNullException(nameof(playerCore));
		_board = board ?? throw new ArgumentNullException(nameof(board));
		_state = state ?? throw new ArgumentNullException(nameof(state));
		_notifyCombatStateChanged = notifyCombatStateChanged ?? throw new ArgumentNullException(nameof(notifyCombatStateChanged));
		_checkDeaths = checkDeaths ?? throw new ArgumentNullException(nameof(checkDeaths));
		_checkVictoryOrDefeat = checkVictoryOrDefeat ?? throw new ArgumentNullException(nameof(checkVictoryOrDefeat));
	}

	// ===== 法术牌关联 =====

	/// <summary>
	/// 设置触发发现效果的法术牌引用。由 CombatManager.PlaySpell 在选牌触发时调用。
	/// </summary>
	public void SetPendingDiscoverSpellCard(Card.Card? card)
	{
		_pendingDiscoverSpellCard = card;
	}

	// ===== 发现选牌系统 =====

	/// <summary>
	/// 处理发现效果——从卡牌池中随机生成 N 张候选卡牌，进入发现选牌阶段。
	/// </summary>
	/// <param name="effect">Discover 效果数据。Value=选项数量</param>
	public void HandleDiscoverEffect(CardEffectData effect)
	{
		int count = effect.Value > 0 ? effect.Value : 3;
		var pool = GetRandomCardsFromPool(count);
		if (pool.Count == 0)
		{
			GD.PrintErr("[SelectionSystem] HandleDiscoverEffect 失败 — 卡牌池为空");
			return;
		}

		_pendingDiscoverOptions = pool;
		_pendingDiscoverRuntimeOptions = null;
		DiscoverPickCount = 1;
		_pendingSelectionMode = CombatManager.PendingSelectionMode.Discover;
		_state.SetDiscovering();
		GD.Print($"[SelectionSystem] ◆ 发现：展示 {pool.Count} 张候选卡牌");
		foreach (var c in pool)
			GD.Print($"[SelectionSystem]     {c.GetLocalizedName()} — {c.Description}");

		_notifyCombatStateChanged();
	}

	/// <summary>
	/// 确认发现选牌结果——由 DiscoverUI 在选择/跳过时调用。
	/// </summary>
	/// <param name="chosen">玩家选中的卡牌数据，null 表示跳过</param>
	public void ConfirmDiscoverChoice(CardData? chosen)
	{
		if (!IsDiscovering)
		{
			GD.PrintErr("[SelectionSystem] ConfirmDiscoverChoice 失败 — 不在发现阶段");
			return;
		}

		if (chosen != null)
		{
			GD.Print($"[SelectionSystem] ◆ 发现选牌：{chosen.GetLocalizedName()}");

			var card = new Card.Card(chosen);
			_playerCore.AddToHand(card);
			GD.Print($"[SelectionSystem]   已将 {chosen.GetLocalizedName()} 加入手牌（共 {_playerCore.Hand.Count} 张）");
		}
		else
		{
			GD.Print("[SelectionSystem] ◆ 发现选牌：跳过");
		}

		// 移除触发发现的法术牌
		if (_pendingDiscoverSpellCard != null)
		{
			_playerHero.RemoveFromHand(_pendingDiscoverSpellCard);
			_pendingDiscoverSpellCard = null;
		}

		// 清除发现状态
		_pendingDiscoverOptions = null;
		_pendingDiscoverRuntimeOptions = null;
		DiscoverPickCount = 1;
		_pendingSelectionMode = CombatManager.PendingSelectionMode.Discover;
		_state.ResumePlayerTurn();

		// 检查死亡
		_checkDeaths();

		_notifyCombatStateChanged();
		GD.Print("[SelectionSystem] 发现选牌完成，恢复玩家回合");
	}

	/// <summary>
	/// 取消发现选牌（等同跳过）。
	/// </summary>
	public void CancelDiscover()
	{
		ConfirmDiscoverChoice(null);
	}

	/// <summary>
	/// 确认运行时卡牌选择结果。当前用于「捞月」从弃牌堆移牌回手牌。
	/// </summary>
	public void ConfirmDiscoverCards(IReadOnlyList<Card.Card> chosenCards)
	{
		if (!IsDiscovering)
		{
			GD.PrintErr("[SelectionSystem] ConfirmDiscoverCards 失败 — 不在选牌阶段");
			return;
		}

		if (_pendingDiscoverSpellCard != null)
		{
			_playerHero.DiscardCard(_pendingDiscoverSpellCard);
			_pendingDiscoverSpellCard = null;
		}

		if (_pendingSelectionMode == CombatManager.PendingSelectionMode.Discard)
		{
			int moved = 0;
			foreach (var card in chosenCards)
			{
				if (moved >= DiscoverPickCount)
					break;
				if (_playerCore.Hand.Count >= _playerCore.MaxHandSize)
				{
					GD.Print($"[SelectionSystem]   手牌已满（{_playerCore.MaxHandSize}张），停止加入弃牌堆卡牌");
					break;
				}

				if (_playerHero.DeckState.MoveFromDiscardToHand(card))
					moved++;
			}
			GD.Print($"[SelectionSystem] ◆ 捞月完成：加入 {moved} 张牌");
		}
		else if (_pendingSelectionMode == CombatManager.PendingSelectionMode.ChooseDiscard)
		{
			int discarded = 0;
			foreach (var card in chosenCards)
			{
				if (discarded >= DiscoverPickCount)
					break;
				_playerHero.DiscardCard(card);
				discarded++;
			}
			GD.Print($"[SelectionSystem] ◆ 弃牌完成：弃掉 {discarded} 张牌");
		}
		else if (_pendingSelectionMode == CombatManager.PendingSelectionMode.BladeCrisis)
		{
			int discarded = 0;
			foreach (var card in chosenCards)
			{
				_playerHero.DiscardCard(card);
				discarded++;
			}
			GD.Print($"[SelectionSystem]   刀盾危机弃牌：弃掉{discarded}张");

			var tokenData = GD.Load<CardData>("res://Resources/Cards/Minion_WhatTheDogDoing.tres");
			if (tokenData != null)
			{
				for (int i = 0; i < discarded; i++)
				{
					int emptySlot = _board.GetEmptySlotIndex(isPlayerSide: true);
					if (emptySlot >= 0)
					{
						var tokenMinion = new Minion(tokenData, isPlayerSide: true);
						_board.PlaceMinion(tokenMinion, emptySlot);
						GD.Print($"[SelectionSystem]   刀盾危机：在槽位{emptySlot}放置我的刀盾");
					}
					else
					{
						GD.Print($"[SelectionSystem]   刀盾危机：棋盘已满，停止放置（已放{i}个）");
						break;
					}
				}
			}
			else
				GD.PrintErr("[SelectionSystem] 刀盾危机：无法加载我的刀盾Token卡牌");

			_playerHero.DrawCards(discarded);
			GD.Print($"[SelectionSystem] ◆ 刀盾危机完成：弃{discarded}张，抽{discarded}张");
		}

		_pendingDiscoverOptions = null;
		_pendingDiscoverRuntimeOptions = null;
		DiscoverPickCount = 1;
		_pendingSelectionMode = CombatManager.PendingSelectionMode.Discover;
		_state.ResumePlayerTurn();

		_checkDeaths();
		_checkVictoryOrDefeat();
		_notifyCombatStateChanged();
		GD.Print("[SelectionSystem] 选牌完成，恢复玩家回合");
	}

	// ===== 弃牌堆选牌启动 =====

	/// <summary>
	/// 开始从弃牌堆选牌——设置运行时卡牌选项，进入选牌阶段。
	/// </summary>
	public void BeginDiscardDiscoverSelection(List<Card.Card> options, int pickCount)
	{
		_pendingDiscoverRuntimeOptions = options;
		_pendingDiscoverOptions = options.Select(c => c.Data).ToList();
		DiscoverPickCount = Math.Min(pickCount, options.Count);
		_pendingSelectionMode = CombatManager.PendingSelectionMode.Discard;
		_state.SetDiscovering();
		_notifyCombatStateChanged();
	}

	/// <summary>
	/// 开始手牌选择——设置手牌选项和选择范围。
	/// </summary>
	public void BeginHandDiscardSelection(List<Card.Card> handOptions, int min, int max, bool isBladeCrisis)
	{
		_pendingHandDiscardSelection = handOptions;
		_pendingDiscardMin = min;
		_pendingDiscardMax = max;
		_pendingDiscardIsBladeCrisis = isBladeCrisis;
		_pendingHandSelectCanCancel = true;
		_pendingHandSelectConfirmed = null;
		if (!isBladeCrisis && ShouldAutoConfirmForcedAll(handOptions, min, max))
		{
			AutoConfirmForcedAllHandSelection(handOptions);
			return;
		}

		SetHandSelectingState();
	}

	/// <summary>
	/// 开始自定义手牌弃牌选择。规则由调用方在确认回调中结算。
	/// </summary>
	public void BeginCustomHandDiscardSelection(
		List<Card.Card> handOptions,
		int min,
		int max,
		CombatManager.PendingSelectionMode mode,
		bool canCancel,
		Action<IReadOnlyList<Card.Card>> onConfirmed)
	{
		_pendingHandDiscardSelection = handOptions;
		_pendingDiscardMin = min;
		_pendingDiscardMax = max;
		_pendingDiscardIsBladeCrisis = false;
		_pendingSelectionMode = mode;
		_pendingHandSelectCanCancel = canCancel;
		_pendingHandSelectConfirmed = onConfirmed;
		if (ShouldAutoConfirmForcedAll(handOptions, min, max))
		{
			AutoConfirmForcedAllHandSelection(handOptions);
			return;
		}

		SetHandSelectingState();
	}

	private static bool ShouldAutoConfirmForcedAll(List<Card.Card> handOptions, int min, int max)
	{
		return handOptions.Count > 0 && min == max && handOptions.Count == min;
	}

	private void AutoConfirmForcedAllHandSelection(IReadOnlyList<Card.Card> selectedCards)
	{
		if (_pendingHandSelectConfirmed != null)
		{
			_pendingHandSelectConfirmed(selectedCards);
		}
		else
		{
			foreach (var card in selectedCards)
				_playerHero.DiscardCard(card);
		}

		if (_pendingDiscoverSpellCard != null)
		{
			_playerHero.DiscardCard(_pendingDiscoverSpellCard);
			_pendingDiscoverSpellCard = null;
		}

		_pendingHandDiscardSelection = null;
		_pendingDiscardMin = 0;
		_pendingDiscardMax = 0;
		_pendingDiscardIsBladeCrisis = false;
		_pendingSelectionMode = CombatManager.PendingSelectionMode.Discover;
		_pendingHandSelectCanCancel = true;
		_pendingHandSelectConfirmed = null;
		_state.ResumePlayerTurn();

		_checkDeaths();
		_checkVictoryOrDefeat();
		_notifyCombatStateChanged();
	}

	// ===== 手牌选择系统（STS2 风格） =====

	/// <summary>
	/// 进入手牌选择状态（调用 State.SetDiscovering 暂停回合流转）。
	/// </summary>
	private void SetHandSelectingState()
	{
		_state.SetDiscovering();
		_notifyCombatStateChanged();
	}

	/// <summary>
	/// 确认手牌选择——由 CombatUI 的确认按钮调用。
	/// 根据是否为刀盾危机执行不同的结算逻辑。
	/// </summary>
	/// <param name="selectedCards">玩家选中的卡牌列表</param>
	public void ConfirmHandDiscardSelection(IReadOnlyList<Card.Card> selectedCards)
	{
		if (_pendingHandDiscardSelection == null)
		{
			GD.PrintErr("[SelectionSystem] ConfirmHandDiscardSelection 失败 — 不在手牌选择模式");
			return;
		}

		int count = selectedCards.Count;
		if (count < _pendingDiscardMin || count > _pendingDiscardMax)
		{
			GD.PrintErr($"[SelectionSystem] ConfirmHandDiscardSelection 失败 — 选择了{count}张，需要{_pendingDiscardMin}-{_pendingDiscardMax}张");
			return;
		}

		if (_pendingHandSelectConfirmed != null)
		{
			_pendingHandSelectConfirmed(selectedCards);
			GD.Print($"[SelectionSystem] ◆ 自定义手牌选择完成：选择 {count} 张");
		}
		else if (_pendingDiscardIsBladeCrisis)
		{
			// 刀盾危机：弃牌 + 放置Token + 抽牌
			int discarded = 0;
			foreach (var card in selectedCards)
			{
				_playerHero.DiscardCard(card);
				discarded++;
			}
			GD.Print($"[SelectionSystem]   刀盾危机弃牌：弃掉{discarded}张");

			var tokenData = GD.Load<CardData>("res://Resources/Cards/Minion_WhatTheDogDoing.tres");
			if (tokenData != null)
			{
				for (int i = 0; i < discarded; i++)
				{
					int emptySlot = _board.GetEmptySlotIndex(isPlayerSide: true);
					if (emptySlot >= 0)
					{
						var tokenMinion = new Minion(tokenData, isPlayerSide: true);
						_board.PlaceMinion(tokenMinion, emptySlot);
						GD.Print($"[SelectionSystem]   刀盾危机：在槽位{emptySlot}放置我的刀盾");
					}
					else
					{
						GD.Print($"[SelectionSystem]   刀盾危机：棋盘已满，停止放置（已放{i}个）");
						break;
					}
				}
			}
			else
				GD.PrintErr("[SelectionSystem] 刀盾危机：无法加载我的刀盾Token卡牌");

			_playerHero.DrawCards(discarded);
			GD.Print($"[SelectionSystem] ◆ 刀盾危机完成：弃{discarded}张，抽{discarded}张");
		}
		else
		{
			// 普通主动弃牌（白色军团等）
			int discarded = 0;
			foreach (var card in selectedCards)
			{
				if (discarded >= _pendingDiscardMax)
					break;
				_playerHero.DiscardCard(card);
				discarded++;
			}
			GD.Print($"[SelectionSystem] ◆ 弃牌完成：弃掉 {discarded} 张牌");
		}

		// 清理触发法术牌
		if (_pendingDiscoverSpellCard != null)
		{
			_playerHero.DiscardCard(_pendingDiscoverSpellCard);
			_pendingDiscoverSpellCard = null;
		}

		_pendingHandDiscardSelection = null;
		_pendingDiscardMin = 0;
		_pendingDiscardMax = 0;
		_pendingDiscardIsBladeCrisis = false;
		_pendingSelectionMode = CombatManager.PendingSelectionMode.Discover;
		_pendingHandSelectCanCancel = true;
		_pendingHandSelectConfirmed = null;
		_state.ResumePlayerTurn();

		_checkDeaths();
		_checkVictoryOrDefeat();
		_notifyCombatStateChanged();
		GD.Print("[SelectionSystem] 手牌选择完成，恢复玩家回合");
	}

	/// <summary>
	/// 取消手牌选择——ESC/右键时由 CombatUI 调用。
	/// 清除待选状态并恢复玩家回合，不弃任何牌。
	/// </summary>
	public void CancelHandDiscardSelection()
	{
		if (_pendingHandDiscardSelection == null)
		{
			GD.Print("[SelectionSystem] CancelHandDiscardSelection 跳过 — 不在手牌选择模式");
			return;
		}

		if (!_pendingHandSelectCanCancel)
		{
			GD.Print("[SelectionSystem] CancelHandDiscardSelection 跳过 — 当前手牌选择必须完成");
			return;
		}

		// 清理触发法术牌（取消时法术仍进入弃牌堆）
		if (_pendingDiscoverSpellCard != null)
		{
			_playerHero.DiscardCard(_pendingDiscoverSpellCard);
			_pendingDiscoverSpellCard = null;
		}

		_pendingHandDiscardSelection = null;
		_pendingDiscardMin = 0;
		_pendingDiscardMax = 0;
		_pendingDiscardIsBladeCrisis = false;
		_pendingSelectionMode = CombatManager.PendingSelectionMode.Discover;
		_pendingHandSelectCanCancel = true;
		_pendingHandSelectConfirmed = null;
		_state.ResumePlayerTurn();
		_notifyCombatStateChanged();
		GD.Print("[SelectionSystem] 手牌选择已取消");
	}

	// ===== 卡牌池 =====

	/// <summary>
	/// 从全卡牌池中随机抽取不重复的 N 张卡牌。
	/// 加载 Resources/Cards/ 下所有 .tres 文件，Fisher-Yates 洗牌后取前 N 张。
	/// </summary>
	/// <param name="count">需要的卡牌数量</param>
	/// <returns>随机卡牌列表</returns>
	private static List<CardData> GetRandomCardsFromPool(int count)
	{
		var pool = new List<CardData>();

		// 从 GameManager 注册表加载全卡牌池（编辑器和导出版本均可用）
		var allCards = GameManager.Instance.GetAllCards();
		foreach (var cardData in allCards)
		{
			if (cardData != null && !string.IsNullOrEmpty(cardData.Id))
			{
				pool.Add(cardData);
			}
		}

		GD.Print($"[SelectionSystem] GetRandomCardsFromPool: 卡牌池共 {pool.Count} 张，请求 {count} 张");

		// 排除不可发现的卡牌（如「发现」自身不能发现「发现」）
		var nonDiscoverableIds = new HashSet<string>
		{
			"spell_discover",
		};
		pool.RemoveAll(c => nonDiscoverableIds.Contains(c.Id));
		GD.Print($"[SelectionSystem]   过滤后池共 {pool.Count} 张");

		if (pool.Count <= count)
			return pool;

		// Fisher-Yates 洗牌后取前 count 张
		using var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int i = pool.Count - 1; i > 0; i--)
		{
			int j = rng.RandiRange(0, i);
			(pool[i], pool[j]) = (pool[j], pool[i]);
		}
		return pool.Take(count).ToList();
	}
}
