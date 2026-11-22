#nullable enable
using System;
using System.Collections.Generic;
using OdysseyCards.Card;
using OdysseyCards.Core;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.Combat;

/// <summary>战斗日志与亮牌表现接线；所有规则仍由原结算方法执行。</summary>
public partial class CombatManager
{
	private readonly Stack<IDamageSource?> _logPrimarySources = new();
	/// <summary>正在静默进弃牌堆的卡牌；用栈支持嵌套自动出牌，避免外层状态被内层覆盖。</summary>
	private readonly Stack<Card.Card> _logSilentDiscardCards = new();

	public CombatLogService CombatLog { get; } = new();
	public event Action<CardPlayPresentationEvent>? OnCardPlayPresented;

	private void InitializeCombatLogging()
	{
		SubscribeLogHero(PlayerHero);
		foreach (var unit in EnemyUnits)
			SubscribeLogHero(unit.Body);
		Intel.OnIntelRevealed += CombatLog.RecordIntel;
		Board.OnMinionPlaced += (minion, _) => SubscribeLogMinion(minion);
		Board.OnMinionPlaced += OnBoardMinionPlacedForLog;
		Board.OnMinionDied += OnBoardMinionDiedForLog;
		Board.OnMinionMoved += OnBoardMinionMovedForLog;
	}

	private void SubscribeLogHero(Hero hero)
	{
		hero.OnDamageTaken += OnLogDamageTaken;
		hero.OnHealed += CombatLog.RecordHeal;
		hero.OnArmorGained += CombatLog.RecordArmor;
		hero.OnDeath += OnLogHeroDeath;
		hero.DeckState.OnCardsDrawn += OnLogCardsDrawn;
		hero.DeckState.OnCardDiscarded += OnLogCardDiscarded;
	}

	private void SubscribeLogMinion(Minion minion)
	{
		minion.OnDamageTaken += OnLogDamageTaken;
		minion.OnHealed += CombatLog.RecordHeal;
		minion.OnArmorGained += CombatLog.RecordArmor;
	}

	private void OnLogDamageTaken(DamageEventInfo info, IDamageSource? source)
	{
		int total = info.HpLost + info.ArmorAbsorbed;
		bool counter = source != null && _logPrimarySources.Count > 0
			&& !ReferenceEquals(source, _logPrimarySources.Peek());
		CombatLog.RecordDamage(total, counter);
	}

	private void OnLogCardsDrawn(IReadOnlyList<Card.Card> cards) => CombatLog.RecordDraw(cards.Count);

	private void OnLogCardDiscarded(Card.Card card)
	{
		if (card.Type == CardType.Countermeasure || IsLogSilentDiscard(card))
			return;
		CombatLog.RecordDiscard(1);
	}

	/// <summary>递归检查整条静默弃牌栈（引用相等），嵌套自动出牌不会互相覆盖。</summary>
	private bool IsLogSilentDiscard(Card.Card card)
	{
		foreach (Card.Card silent in _logSilentDiscardCards)
		{
			if (ReferenceEquals(silent, card))
				return true;
		}
		return false;
	}

	/// <summary>按引用移出静默弃牌栈；嵌套改变栈序时重建，保证外层标记继续生效。</summary>
	private void RemoveLogSilentDiscard(Card.Card card)
	{
		if (_logSilentDiscardCards.Count == 0)
			return;
		if (ReferenceEquals(_logSilentDiscardCards.Peek(), card))
		{
			_logSilentDiscardCards.Pop();
			return;
		}
		var kept = new List<Card.Card>(_logSilentDiscardCards.Count);
		foreach (Card.Card silent in _logSilentDiscardCards)
		{
			if (!ReferenceEquals(silent, card))
				kept.Add(silent);
		}
		_logSilentDiscardCards.Clear();
		for (int i = kept.Count - 1; i >= 0; i--)
			_logSilentDiscardCards.Push(kept[i]);
	}

	private void OnLogHeroDeath(Hero hero)
	{
		string name = GetLogHeroName(hero);
		if (CombatLog.HasPendingAction)
		{
			CombatLog.RecordUnitKilled(name);
			return;
		}
		CombatLog.Record(new CombatLogEntry(State.TurnCount, hero.IsPlayerSide,
			CombatLogEntryKind.UnitDestroyed, name));
	}

	private void OnBoardMinionDiedForLog(Minion minion)
	{
		if (CombatLog.HasPendingAction)
		{
			CombatLog.RecordUnitKilled(minion.GetLocalizedName());
			return;
		}
		CombatLog.Record(new CombatLogEntry(State.TurnCount, minion.IsPlayerSide,
			CombatLogEntryKind.UnitDestroyed, minion.GetLocalizedName(), minion.ToRuntimeCard()));
	}

	private void OnBoardMinionPlacedForLog(Minion minion, BoardPosition position)
	{
		if (CombatLog.HasPendingAction)
		{
			CombatLog.RecordSummon();
			return;
		}
		BeginLogAction(CombatLogEntryKind.DeployUnit,
			minion.OwnerHero ?? GetOwnerHero(minion.IsPlayerSide), minion.ToRuntimeCard());
		CombatLog.RecordSummon();
		CommitLogAction();
	}

	private void OnBoardMinionMovedForLog(Minion minion, BoardPosition from, BoardPosition to)
	{
		BeginLogAction(CombatLogEntryKind.AdvanceFrontline,
			minion.OwnerHero ?? GetOwnerHero(minion.IsPlayerSide), minion.ToRuntimeCard(),
			Loc.T("ui.combat.log_to_frontline", "前线"));
		CombatLog.RecordMove();
		CommitLogAction();
	}

	internal CombatLogEntry BeginLogAction(
		CombatLogEntryKind kind,
		Hero actor,
		Card.Card? sourceCard = null,
		object? target = null,
		IDamageSource? primarySource = null,
		string? actorName = null)
	{
		Card.Card? targetCard = null;
		string? targetName = null;
		switch (target)
		{
			case null:
				break;
			case Minion minion:
				targetCard = minion.ToRuntimeCard();
				targetName = minion.GetLocalizedName();
				break;
			case Card.Card card:
				targetCard = card;
				targetName = card.GetLocalizedName();
				break;
			case IDamageTarget damageTarget:
				targetName = GetLogTargetName(damageTarget);
				break;
			default:
				targetName = target.ToString();
				break;
		}
		var entry = CombatLog.BeginAction(State.TurnCount, actor.IsPlayerSide, kind,
			actorName ?? sourceCard?.GetLocalizedName() ?? GetLogHeroName(actor),
			sourceCard, targetName, targetCard);
		_logPrimarySources.Push(primarySource ?? actor);
		return entry;
	}

	internal void CommitLogAction(CombatLogEntry? scope = null)
	{
		if (CombatLog.CommitAction(scope) == null)
			return;
		if (_logPrimarySources.Count > 0)
			_logPrimarySources.Pop();
	}

	internal void PresentCardPlay(Hero caster, Card.Card card, object? target,
		bool isCountermeasureTrigger = false, bool isCanceled = false, Card.Card? targetCard = null)
	{
		Card.Card? presentedTargetCard = targetCard ?? target switch
		{
			Minion minion => minion.ToRuntimeCard(),
			Card.Card targetCardValue => targetCardValue,
			_ => null,
		};
		OnCardPlayPresented?.Invoke(new CardPlayPresentationEvent(
			caster, card, target as IDamageTarget, isCountermeasureTrigger, isCanceled, presentedTargetCard));
	}

	private void RecordCountermeasureTriggered(Hero defender, Card.Card counter, Card.Card spell,
		bool canceled, bool discards)
	{
		BeginLogAction(CombatLogEntryKind.CountermeasureTriggered, defender, counter, spell, primarySource: defender);
		CombatLog.RecordCountermeasure();
		if (discards)
			CombatLog.RecordDiscard(1);
		if (canceled)
			CombatLog.RecordCanceled();
		CommitLogAction();
		// 主体是被触发的反制牌，被拦截/被削弱的那张法术作为目标卡快照一并展示。
		PresentCardPlay(defender, counter, spell, isCountermeasureTrigger: true, isCanceled: canceled, targetCard: spell);
	}

	private void ExecuteLoggedDeathrattle(Minion minion, Action execute)
	{
		BeginLogAction(CombatLogEntryKind.EffectTriggered,
			minion.OwnerHero ?? GetOwnerHero(minion.IsPlayerSide), minion.ToRuntimeCard(), primarySource: minion);
		try
		{
			CombatLog.RecordStatus(Loc.T("keyword.deathrattle", "亡语"));
			execute();
		}
		finally
		{
			CommitLogAction();
		}
	}

	private string GetLogTargetName(IDamageTarget target) => target switch
	{
		Minion minion => minion.GetLocalizedName(),
		Hero hero => GetLogHeroName(hero),
		_ => Loc.T("ui.combat.select_target", "选择目标"),
	};

	private string GetLogHeroName(Hero hero)
	{
		if (hero.IsPlayerSide)
			return Loc.T("ui.combat.player_hero", "我方英雄");
		foreach (var unit in EnemyUnits)
		{
			if (ReferenceEquals(unit.Body, hero))
				return unit.Brain.DisplayName;
		}
		return Loc.T("ui.combat.enemy_hero", "敌方英雄");
	}
}
