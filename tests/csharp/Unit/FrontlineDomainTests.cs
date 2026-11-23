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
	private static (Hero Hero, Board Board, GameState State) CreatePlayerSide()
	{
		var core = new CommanderCore();
		core.InitializeHealth(30, 30);
		var hero = new Hero(core, true);
		var board = new Board();
		var state = new GameState(core);
		state.StartGame();
		state.StartPlayerTurn(GameState.MaxManaCrystals);
		return (hero, board, state);
	}

	[Fact]
	public void ExplosiveReactiveArmor_ConsumesOneStackPerLifeDamagingAttack()
	{
		var (hero, _, _) = CreatePlayerSide();
		var enemy = new Minion("enemy", false, attack: 3, health: 3);

		hero.AddDomain("explosive_reactive_armor", null);
		hero.AddDomain("explosive_reactive_armor", null);
		Assert.Equal(2, hero.ActiveDomains["explosive_reactive_armor"].StackCount);

		hero.TakeDamage(3, enemy);
		Assert.Equal(30, hero.CurrentHealth);
		Assert.Equal(1, hero.ActiveDomains["explosive_reactive_armor"].StackCount);

		hero.TakeDamage(3, enemy);
		Assert.Equal(30, hero.CurrentHealth);
		Assert.False(hero.HasDomain("explosive_reactive_armor"));

		// 层数耗尽后不再抵挡生命伤害。
		hero.TakeDamage(3, enemy);
		Assert.Equal(27, hero.CurrentHealth);
	}

	[Fact]
	public void ExplosiveReactiveArmor_NotConsumedWhenArmorAbsorbsEverything()
	{
		var (hero, _, _) = CreatePlayerSide();
		var enemy = new Minion("enemy", false, attack: 3, health: 3);
		hero.GainArmor(10);
		hero.AddDomain("explosive_reactive_armor", null);

		hero.TakeDamage(3, enemy);
		Assert.Equal(7, hero.CurrentArmor);
		Assert.True(hero.HasDomain("explosive_reactive_armor"));
		Assert.Equal(1, hero.ActiveDomains["explosive_reactive_armor"].StackCount);

		// 护甲被打穿后才会抵消剩余生命伤害并消耗层数。
		hero.TakeDamage(9, enemy);
		Assert.Equal(30, hero.CurrentHealth);
		Assert.Equal(0, hero.CurrentArmor);
		Assert.False(hero.HasDomain("explosive_reactive_armor"));
	}

	[Fact]
	public void ExplosiveReactiveArmor_IgnoresEffectDamage()
	{
		var (hero, _, _) = CreatePlayerSide();
		var enemy = new Minion("enemy", false, attack: 3, health: 3);
		hero.AddDomain("explosive_reactive_armor", null);

		hero.TakeDamage(3, enemy, DamageKind.Effect);

		Assert.Equal(27, hero.CurrentHealth);
		Assert.True(hero.HasDomain("explosive_reactive_armor"));
	}

	[Fact]
	public void FlyingAway_FirstEnemyAttackDrawsAndGivesUkemi_ThenExpires()
	{
		var (hero, board, state) = CreatePlayerSide();
		var domains = new DomainTriggerManager(hero.Core, hero, board, state, Array.Empty<EnemyUnit>(), () => { });
		hero.OnAttacked += domains.HandlePlayerHeroAttacked;
		for (int i = 0; i < 4; i++)
			hero.AddToDrawPileBottom(new OdysseyCards.Card.Card("alert", CardType.Spell));
		hero.AddDomain("flying_away", null);

		// 防御方回合 = 敌方回合。
		state.EndPlayerTurn();
		int handBefore = hero.Hand.Count;
		hero.TakeDamage(1, new Minion("enemy", false, attack: 1, health: 1), DamageKind.Attack);

		Assert.Equal(handBefore + 2, hero.Hand.Count);
		Assert.False(hero.HasDomain("flying_away"));
	}
}
