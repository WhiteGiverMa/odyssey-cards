using System;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using Xunit;

namespace OdysseyCards.Tests.Unit;

public class FrontlineDomainTests
{
	[Fact]
	public void Jiehuafa_OnlyEnemyAttacksAgainstHqOrFrontlineCount_AndNoAttackIsNotSuccess()
	{
		var board = new Board();
		var state = new GameState();
		state.StartGame();
		state.StartPlayerTurn(GameState.MaxManaCrystals);
		state.EndPlayerTurn();
		var core = new CommanderCore();
		var hero = new Hero(core, true);
		var domains = new DomainTriggerManager(core, hero, board, state, Array.Empty<EnemyUnit>(), () => { });
		hero.OnAttacked += domains.HandlePlayerHeroAttacked;
		var front = new Minion("front", true, health: 20);
		board.PlaceMinion(front, 0);
		domains.OnMinionPlaced(front);
		board.MoveMinionToFrontline(front);
		var rear = new Minion("rear", true, health: 20);
		board.PlaceMinion(rear, 0);
		domains.OnMinionPlaced(rear);
		var enemy = new Minion("enemy", false);
		domains.OnEnemyTurnStart();
		Assert.False(domains.AllDefendedAttacksBlocked);
		rear.TakeDamage(2, enemy, DamageKind.Attack);
		Assert.False(domains.AllDefendedAttacksBlocked);
		front.TakeDamage(2, enemy, DamageKind.Effect);
		Assert.False(domains.AllDefendedAttacksBlocked);
		front.GainArmor(3);
		front.TakeDamage(2, enemy, DamageKind.Attack);
		Assert.True(domains.AllDefendedAttacksBlocked);
		hero.GainArmor(3);
		hero.TakeDamage(2, enemy, DamageKind.Attack);
		Assert.True(domains.AllDefendedAttacksBlocked);
		front.TakeDamage(3, enemy, DamageKind.Attack);
		Assert.False(domains.AllDefendedAttacksBlocked);
		domains.OnEnemyTurnStart();
		Assert.False(domains.AllDefendedAttacksBlocked);
	}

	[Fact]
	public void Jiehuafa_DefenseReducesAttackToZero_CountsAsBlockedEvenWithoutArmor()
	{
		var board = new Board();
		var state = new GameState();
		state.StartGame();
		state.StartPlayerTurn(GameState.MaxManaCrystals);
		state.EndPlayerTurn();
		var core = new CommanderCore();
		var hero = new Hero(core, true);
		var domains = new DomainTriggerManager(core, hero, board, state, Array.Empty<EnemyUnit>(), () => { });
		hero.OnAttacked += domains.HandlePlayerHeroAttacked;
		hero.ModifyDefense(5);
		hero.TakeDamage(2, new Minion("enemy", false), DamageKind.Attack);
		Assert.True(domains.AllDefendedAttacksBlocked);
		state.EndEnemyTurn();
		domains.OnPlayerTurnStart();
		hero.TakeDamage(2, new Minion("enemy", false), DamageKind.Attack);
		Assert.False(domains.AllDefendedAttacksBlocked);
	}
}
