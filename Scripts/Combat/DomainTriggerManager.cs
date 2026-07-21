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

	// 先发制人领域：每回合首张直伤牌按手牌数 +10%/张
	private bool _preemptiveStrikeUsedThisTurn;

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

				case "idol_twilight":
					// 设置显示标记；触发逻辑（被攻击时+1/+1）在 CombatManager.ResolveMinionCombat 中查询领域
					minion.HasIdolTwilightBuff = true;
					GD.Print($"[DomainTriggerManager] 「偶像的黄昏」标记：{minion.CardName} 进场时领域已激活");
					break;
			}
		}
	}

	/// <summary>
	/// 领域展开后调用——对当前棋盘上已有的玩家方随从施加领域效果。
	/// 由 CombatManager.PlayDomain 在 AddDomain 之后调用。
	/// </summary>
	public void OnDomainDeployed(string domainId)
	{
		switch (domainId)
		{
			case "idol_twilight":
				var existing = _board.GetPlayerMinions().ToList();
				foreach (var minion in existing)
				{
					minion.HasIdolTwilightBuff = true;
				}
				GD.Print($"[DomainTriggerManager] 「偶像的黄昏」展开：标记 {existing.Count} 个现有玩家方随从");
				break;
		}
	}

	/// <summary>
	/// 领域被移除时调用——清除随从身上的显示标记。
	/// 由 CombatManager 在 RemoveDomain 之后调用。
	/// </summary>
	public void OnDomainRemoved(string domainId)
	{
		switch (domainId)
		{
			case "idol_twilight":
				foreach (var minion in _board.GetPlayerMinions())
				{
					minion.HasIdolTwilightBuff = false;
				}
				GD.Print("[DomainTriggerManager] 「偶像的黄昏」移除：清除所有随从的显示标记");
				break;
		}
	}

	public void OnPlayerTurnStart()
	{
		_preemptiveStrikeUsedThisTurn = false;
		_enemyHeroAttackedThisTurn = false;
		_enemyHeroAttackPenetratedThisTurn = false;
	}

	/// <summary>
	/// 先发制人伤害倍率查询——每回合首张直伤牌时返回（1 + 手牌数 × 10%），
	/// 之后同一回合内返回 1.0（不叠加）。
	/// </summary>
	public float ConsumePreemptiveStrikeMultiplier()
	{
		if (_preemptiveStrikeUsedThisTurn)
			return 1.0f;
		if (!_playerHero.HasDomain("preemptive_strike"))
			return 1.0f;
		_preemptiveStrikeUsedThisTurn = true;
		int handCount = _playerHero.DeckState.Hand.Count;
		return 1.0f + handCount * 0.1f;
	}

	public void OnPlayerTurnEnd()
	{
		foreach (var domain in _playerHero.ActiveDomains.Values.ToList())
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

					// 「shiyoru_raidenkou」「sutaraito_spirit」已迁移到限时 Power（StatusEffect）通道，
					// 触发逻辑在 CardEffectDispatcher.MountShiyoruRaidenkou/MountSutaraitoSpirit 的 OnTick 中。
					// 见 AGENTS.md「语义边界」节。
			}
		}
	}

	// OnPlayerTurnStart 已删除：原本只服务 sutaraito_spirit，现已改走 StatusEffect(PlayerTurnStart) 通道。

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
