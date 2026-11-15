#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>玩家决策适配器。只管理 UI 暂停和选择，不隐式操作牌堆或清理法术。</summary>
internal sealed class SelectionSystem : IDecisionProvider
{
	private readonly Hero _owner;
	private readonly GameState _state;
	private readonly Action _notify;
	private readonly Action _checkDeaths;
	private readonly Func<bool> _checkVictoryOrDefeat;
	private CardDecisionRequest? _pending;
	private Action<IReadOnlyList<Card.Card>>? _onChosen;
	private CombatPhase _previousPhase;
	private bool _choosingOption;
	public Action<OptionDecisionRequest, Action<int>>? OptionRequested { get; set; }
	public bool IsDiscovering => _pending != null || _choosingOption;
	public IReadOnlyList<CardData>? DiscoverOptions => IsHandSelecting ? null : _pending?.Options.Select(card => card.Data).ToArray();
	public IReadOnlyList<Card.Card>? DiscoverRuntimeOptions => IsHandSelecting ? null : _pending?.Options;
	public int DiscoverPickCount => _pending?.Max ?? 1;
	public bool IsHandSelecting => _pending != null && _pending.Kind is not (CardDecisionKind.Discover or CardDecisionKind.RecoverDiscard);
	public IReadOnlyList<Card.Card>? HandSelectOptions => IsHandSelecting ? _pending?.Options : null;
	public int HandSelectMin => _pending?.Min ?? 0;
	public int HandSelectMax => _pending?.Max ?? 0;
	public CombatManager.PendingSelectionMode CurrentSelectionMode => _pending?.Kind switch
	{
		CardDecisionKind.RecoverDiscard => CombatManager.PendingSelectionMode.Discard,
		CardDecisionKind.Discard => CombatManager.PendingSelectionMode.ChooseDiscard,
		CardDecisionKind.BladeCrisis => CombatManager.PendingSelectionMode.BladeCrisis,
		CardDecisionKind.CopyHandFill => CombatManager.PendingSelectionMode.CopyHandFill,
		CardDecisionKind.RestructureDiscard => CombatManager.PendingSelectionMode.RestructureDiscard,
		_ => CombatManager.PendingSelectionMode.Discover,
	};

	public SelectionSystem(Hero playerHero, CommanderCore playerCore, Board board, GameState state,
		Action notifyCombatStateChanged, Action checkDeaths, Func<bool> checkVictoryOrDefeat)
	{
		_owner = playerHero;
		_state = state;
		_notify = notifyCombatStateChanged;
		_checkDeaths = checkDeaths;
		_checkVictoryOrDefeat = checkVictoryOrDefeat;
	}

	public void ChooseCards(CardDecisionRequest request, Action<IReadOnlyList<Card.Card>> onChosen)
	{
		if (IsDiscovering)
			throw new InvalidOperationException("不能同时发起两个玩家决策。");
		if (request.Options.Count == 0 || request.Max == 0)
		{
			onChosen(Array.Empty<Card.Card>());
			_notify();
			return;
		}
		if (!request.CanCancel && request.Min == request.Max && request.Min == request.Options.Count)
		{
			onChosen(request.Options.ToArray());
			_notify();
			return;
		}
		_pending = request;
		_onChosen = onChosen;
		_previousPhase = _state.Phase;
		_state.SetDiscovering();
		_notify();
	}

	public void ChooseOption(OptionDecisionRequest request, Action<int> onChosen)
	{
		if (IsDiscovering)
			throw new InvalidOperationException("不能同时发起两个玩家决策。");
		_previousPhase = _state.Phase;
		_choosingOption = true;
		_state.SetDiscovering();
		void Complete(int index)
		{
			if (!_choosingOption)
				return;
			_choosingOption = false;
			ResumePhase();
			onChosen(index);
			_notify();
		}
		if (OptionRequested == null)
			Complete(0);
		else
			OptionRequested(request, Complete);
	}

	public void ConfirmDiscoverChoice(CardData? chosen)
	{
		if (_pending == null)
			return;
		var card = _pending.Options.FirstOrDefault(option => ReferenceEquals(option.Data, chosen));
		Complete(card == null ? Array.Empty<Card.Card>() : new[] { card }, allowSkip: true);
	}

	public void ConfirmDiscoverCards(IReadOnlyList<Card.Card> cards) => Complete(cards);
	public void ConfirmHandDiscardSelection(IReadOnlyList<Card.Card> cards) => Complete(cards);
	public void CancelDiscover() => Complete(Array.Empty<Card.Card>(), allowSkip: true);
	public void CancelHandDiscardSelection()
	{
		if (_pending is { CanCancel: true })
			Complete(Array.Empty<Card.Card>(), allowSkip: true);
	}

	private void Complete(IReadOnlyList<Card.Card> cards, bool allowSkip = false)
	{
		if (_pending == null || _onChosen == null)
			return;
		var valid = cards.Distinct().Where(_pending.Options.Contains).ToArray();
		if (valid.Length > _pending.Max || (!allowSkip && valid.Length < _pending.Min))
			return;
		var callback = _onChosen;
		_pending = null;
		_onChosen = null;
		ResumePhase();
		callback(valid);
		_notify();
	}

	private void ResumePhase()
	{
		if (_state.IsGameOver)
			return;
		if (_previousPhase == CombatPhase.EnemyTurn)
			_state.StartEnemyTurn();
		else
			_state.ResumePlayerTurn();
	}

	public void BeginCustomHandDiscardSelection(List<Card.Card> options, int min, int max,
		CombatManager.PendingSelectionMode mode, bool canCancel, Action<IReadOnlyList<Card.Card>> onConfirmed)
	{
		var kind = mode == CombatManager.PendingSelectionMode.RestructureDiscard
			? CardDecisionKind.RestructureDiscard : CardDecisionKind.Discard;
		ChooseCards(new CardDecisionRequest(_owner, kind, options, min, max, canCancel), onConfirmed);
	}

	/// <summary>
	/// 旧版强制弃牌入口（由既有测试与旧调用方使用）：全选即弃时直接结算，不进入选择模式。
	/// 新规则路径统一走 <see cref="ChooseCards"/>，由施法者上下文的决策提供者返回结果。
	/// </summary>
	public void BeginHandDiscardSelection(List<Card.Card> handOptions, int min, int max, bool isBladeCrisis)
	{
		if (!isBladeCrisis && ShouldAutoConfirm(handOptions, min, max))
		{
			foreach (var card in handOptions)
				_owner.DiscardCard(card);
			_checkDeaths();
			_checkVictoryOrDefeat();
			_notify();
			return;
		}
		ChooseCards(new CardDecisionRequest(_owner,
			isBladeCrisis ? CardDecisionKind.BladeCrisis : CardDecisionKind.Discard,
			handOptions, min, max), cards =>
		{
			foreach (var card in cards)
				_owner.DiscardCard(card);
			_checkDeaths();
			_checkVictoryOrDefeat();
		});
	}

	private static bool ShouldAutoConfirm(List<Card.Card> options, int min, int max)
		=> options.Count > 0 && min == max && options.Count == min;
}
