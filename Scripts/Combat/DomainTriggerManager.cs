using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>永久领域以持有者为中心订阅双侧事件，不以玩家作为隐含施法者。</summary>
internal sealed class DomainTriggerManager
{
	private readonly Hero _playerHero;
	private readonly Board _board;
	private readonly GameState _state;
	private readonly IReadOnlyList<EnemyUnit> _enemies;
	private readonly Action _notify;
	private readonly Func<string, CardData?> _load;
	private readonly HashSet<Hero> _preemptiveUsed = new();

	public DomainTriggerManager(CommanderCore playerCore, Hero playerHero, Board board, GameState state,
		IReadOnlyList<EnemyUnit> enemyUnits, Action notifyCombatStateChanged, Func<string, CardData?>? loadCard = null)
	{
		_playerHero = playerHero;
		_board = board;
		_state = state;
		_enemies = enemyUnits;
		_notify = notifyCombatStateChanged;
		_load = loadCard ?? (_ => null);
	}

	private IEnumerable<Hero> Heroes => new[] { _playerHero }.Concat(_enemies.Select(unit => unit.Body));
	private Hero? Owner(Minion minion) => minion.OwnerHero ?? (minion.IsPlayerSide ? _playerHero : _enemies.FirstOrDefault()?.Body);
	private IEnumerable<Minion> Minions(Hero owner) => _board.GetAllMinions().Where(minion => !minion.IsDead && minion.IsPlayerSide == owner.IsPlayerSide);

	public void OnMinionPlaced(Minion minion)
	{
		var owner = Owner(minion);
		if (owner == null)
			return;
		if (owner.ActiveDomains.TryGetValue("zhijian", out var sharp))
			minion.ModifyAttack((sharp.EffectData?.Value ?? 1) * sharp.StackCount);
		minion.HasIdolTwilightBuff = owner.HasDomain("idol_twilight");
	}

	public void OnDomainDeployed(string id) => OnDomainDeployed(_playerHero, id);
	public void OnDomainDeployed(Hero owner, string id)
	{
		if (id == "idol_twilight")
			foreach (var minion in Minions(owner))
				minion.HasIdolTwilightBuff = true;
	}
	public void OnDomainRemoved(string id) => OnDomainRemoved(_playerHero, id);
	public void OnDomainRemoved(Hero owner, string id)
	{
		if (id == "idol_twilight")
			foreach (var minion in Minions(owner))
				minion.HasIdolTwilightBuff = false;
	}

	public float ConsumePreemptiveStrikeMultiplier() => ConsumePreemptiveStrikeMultiplier(_playerHero);
	public float ConsumePreemptiveStrikeMultiplier(Hero owner)
	{
		if (!owner.HasDomain("preemptive_strike") || !_preemptiveUsed.Add(owner))
			return 1;
		return 1 + owner.Hand.Count * 0.1f;
	}
	public void OnPlayerTurnStart() => StartSide(true);
	public void OnEnemyTurnStart() => StartSide(false);
	private void StartSide(bool player)
	{
		foreach (var owner in Heroes)
		{
			if (owner.IsPlayerSide == player)
				_preemptiveUsed.Remove(owner);
		}
	}

	public void OnPlayerTurnEnd() => EndSide(true);
	public void OnEnemyTurnEnd() => EndSide(false);
	private void EndSide(bool player)
	{
		foreach (var owner in Heroes.Where(hero => !hero.IsDead))
		{
			if (owner.IsPlayerSide == player && owner.ActiveDomains.TryGetValue("infinite_fire", out var fire))
			{
				var strike = _load("res://Resources/Cards/Spell_Strike.tres");
				if (strike != null)
					for (int i = 0; i < (fire.EffectData?.Value ?? 1) * fire.StackCount; i++)
						owner.InsertCardToDrawPile(new Card.Card(strike));
			}
		}
	}

	public void HandlePlayerHeroAttacked(Hero target, IDamageSource source, int finalDamage)
	{
		if (!IsOpposingTurn(target) || source.IsPlayerSide == target.IsPlayerSide)
			return;
		if (!target.ActiveDomains.TryGetValue("flying_away", out var domain) || domain.LastTriggeredTurn == _state.TurnCount)
			return;
		domain.LastTriggeredTurn = _state.TurnCount;
		target.DrawCards(domain.EffectData?.SecondaryValue > 0 ? domain.EffectData.SecondaryValue : 2);
		GiveUkemi(target, domain);
		if (--domain.StackCount <= 0)
			target.RemoveDomain("flying_away");
		_notify();
	}

	private bool IsOpposingTurn(Hero owner) => owner.IsPlayerSide ? _state.IsEnemyTurn : _state.Phase == CombatPhase.PlayerTurn;
	private void GiveUkemi(Hero owner, ActiveDomain domain)
	{
		string path = domain.EffectData?.TargetType;
		if (string.IsNullOrEmpty(path) || !path.StartsWith("res://", StringComparison.Ordinal))
			path = "res://Resources/Cards/Spell_Ukemi.tres";
		var token = _load(path);
		if (token != null)
			owner.Core.AddToHand(new Card.Card(token));
	}
}
