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
			? "res://Resources/Cards/Spell_Shoushen.tres"
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
}
