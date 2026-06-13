#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>
/// 领域触发管理器。
/// 负责部署、回合结束、受击等时机的领域行为，避免 CombatManager 持有零散的 TriggerDomains* 方法族。
/// </summary>
internal sealed class DomainTriggerManager
{
	private readonly CommanderCore _playerCore;
	private readonly Hero _playerHero;
	private readonly Board _board;
	private readonly GameState _state;
	private readonly IReadOnlyList<EnemyUnit> _enemyUnits;
	private readonly Action _notifyCombatStateChanged;

	// 接化发领域追踪：敌方回合中英雄是否攻击过 / 是否有穿透HP的攻击
	private bool _enemyHeroAttackedThisTurn;
	private bool _enemyHeroAttackPenetratedThisTurn;

	public DomainTriggerManager(
		CommanderCore playerCore,
		Hero playerHero,
		Board board,
		GameState state,
		IReadOnlyList<EnemyUnit> enemyUnits,
		Action notifyCombatStateChanged)
	{
		_playerCore = playerCore;
		_playerHero = playerHero;
		_board = board;
		_state = state;
		_enemyUnits = enemyUnits;
		_notifyCombatStateChanged = notifyCombatStateChanged;
	}

	public void OnMinionPlaced(Minion minion)
	{
		foreach (var domain in _playerHero.ActiveDomains.Values)
		{
			switch (domain.DomainId)
			{
				case "zhijian":
					int bonusAtk = domain.EffectData.Value * domain.StackCount;
					minion.ModifyAttack(bonusAtk);
					GD.Print($"[DomainTriggerManager] 「执锐」触发：{minion.CardName} 攻击力 +{bonusAtk}（{domain.StackCount}层）");
					break;
			}
		}
	}

	public void OnPlayerTurnEnd()
	{
		foreach (var domain in _playerHero.ActiveDomains.Values)
		{
			switch (domain.DomainId)
			{
				case "infinite_fire":
					int shuffleCount = domain.EffectData.Value * domain.StackCount;
					var strikeData = GD.Load<CardData>("res://Resources/Cards/Spell_Strike.tres");
					if (strikeData == null)
					{
						GD.PrintErr("[DomainTriggerManager] 无限火力触发失败：无法加载打击卡牌");
						break;
					}

					for (int i = 0; i < shuffleCount; i++)
						_playerHero.InsertCardToDrawPile(new Card.Card(strikeData));

					GD.Print($"[DomainTriggerManager] 「无限火力」触发：洗入 {shuffleCount} 张打击（{domain.StackCount}层）");
					break;
			}
		}
	}

	public void HandlePlayerHeroAttacked(Hero target, IDamageSource source, int finalDamage)
	{
		if (!ReferenceEquals(target, _playerHero))
			return;
		if (_state.IsPlayerTurn)
			return;

		bool isEnemyAttackSource = source is Minion { IsPlayerSide: false }
			|| (source is Hero h && _enemyUnits.Any(eu => ReferenceEquals(eu.Body, h)));
		if (!isEnemyAttackSource)
			return;

		if (!_playerHero.ActiveDomains.TryGetValue("flying_away", out var domain))
			return;
		if (domain.LastTriggeredTurn == _state.TurnCount)
			return;

		domain.LastTriggeredTurn = _state.TurnCount;

		int drawCount = domain.EffectData.SecondaryValue > 0 ? domain.EffectData.SecondaryValue : 2;
		_playerHero.DrawCards(drawCount);

		string tokenPath = string.IsNullOrWhiteSpace(domain.EffectData.TargetType)
			? "res://Resources/Cards/Spell_Ukemi.tres"
			: domain.EffectData.TargetType;
		var tokenData = GD.Load<CardData>(tokenPath);
		if (tokenData != null)
		{
			_playerCore.AddToHand(new Card.Card(tokenData));
			GD.Print($"[DomainTriggerManager] 「飞远」触发：抽 {drawCount} 张牌，将「{tokenData.GetLocalizedName()}」加入手牌");
		}
		else
		{
			GD.PrintErr($"[DomainTriggerManager] 「飞远」触发失败：无法加载受身卡牌 {tokenPath}");
		}

		if (domain.StackCount <= 1)
		{
			_playerHero.RemoveDomain("flying_away");
		}
		else
		{
			domain.StackCount--;
			GD.Print($"[DomainTriggerManager] 「飞远」剩余 {domain.StackCount} 层");
		}

		_notifyCombatStateChanged();
	}

	/// <summary>
	/// 敌方回合开始时重置接化发追踪状态。
	/// </summary>
	public void OnEnemyTurnStart()
	{
		_enemyHeroAttackedThisTurn = false;
		_enemyHeroAttackPenetratedThisTurn = false;
	}

	/// <summary>
	/// 玩家英雄受到伤害时，追踪敌方英雄攻击是否穿透HP（用于接化发领域）。
	/// 应订阅 PlayerHero.OnDamageTaken 事件。
	/// </summary>
	public void HandlePlayerHeroDamageTaken(DamageEventInfo info, IDamageSource? source)
	{
		// 仅追踪敌方回合
		if (_state.IsPlayerTurn)
			return;

		// 仅追踪敌方英雄来源的攻击
		bool isEnemyHeroSource = source is Hero h && _enemyUnits.Any(eu => ReferenceEquals(eu.Body, h));
		if (!isEnemyHeroSource)
			return;

		_enemyHeroAttackedThisTurn = true;
		if (!info.WasFullyBlocked)
			_enemyHeroAttackPenetratedThisTurn = true;
	}

	/// <summary>
	/// 敌方回合结束时检查「接化发」领域触发条件。
	/// </summary>
	public void OnEnemyTurnEnd()
	{
		if (!_playerHero.ActiveDomains.TryGetValue("jiehuafa", out var domain))
			return;

		// 条件：敌方英雄本回合攻击过，且所有攻击均未穿透HP
		if (!_enemyHeroAttackedThisTurn || _enemyHeroAttackPenetratedThisTurn)
			return;

		if (domain.LastTriggeredTurn == _state.TurnCount)
			return;

		domain.LastTriggeredTurn = _state.TurnCount;

		string tokenPath = string.IsNullOrWhiteSpace(domain.EffectData.TargetType)
			? "res://Resources/Cards/Spell_Ukemi.tres"
			: domain.EffectData.TargetType;
		var tokenData = GD.Load<CardData>(tokenPath);
		if (tokenData != null)
		{
			_playerCore.AddToHand(new Card.Card(tokenData));
			GD.Print($"[DomainTriggerManager] 「接化发」触发：敌方英雄攻击全被格挡，将「{tokenData.GetLocalizedName()}」加入手牌");
		}
		else
		{
			GD.PrintErr($"[DomainTriggerManager] 「接化发」触发失败：无法加载 {tokenPath}");
		}

		_notifyCombatStateChanged();
	}
}
