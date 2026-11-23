using System.Linq;
using OdysseyCards.AI;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 0.2.2「断流」卡包纯规则测试：弃牌费用过滤、随从回卡组顶、指定卡入手、
/// 能量币 +n、反制激活/取消与敌方回合拦截。全部使用纯 C# 运行时实例，不依赖 Godot Resource。
/// </summary>
public class CutoffCurrentTests
{
	private static Hero CreateHero(bool isPlayerSide, int mana = 10)
	{
		var core = new CommanderCore();
		core.InitializeHealth(30, 30);
		core.SetMana(mana, mana);
		core.MaxHandSize = 10;
		return new Hero(core, isPlayerSide);
	}

	private static (Hero Player, Hero Enemy, EnemyUnit Unit, Board Board) CreateBattle(int enemyMana = 10)
	{
		var player = CreateHero(true);
		var enemy = CreateHero(false, enemyMana);
		var unit = new EnemyUnit(enemy, new StubEncounter());
		var board = new Board();
		return (player, enemy, unit, board);
	}

	private static CountermeasureService CreateService() => new(_ => null);

	// ===== 093B SSN =====

	[Fact]
	public void SSN093B_DiscardsOnlyCardsWithinCostLimit()
	{
		var (_, enemy, _, _) = CreateBattle();
		enemy.Core.AddToHand(new Card.Card("expensive_a", CardType.Spell, cost: 9));
		enemy.Core.AddToHand(new Card.Card("expensive_b", CardType.Spell, cost: 10));
		enemy.Core.AddToHand(new Card.Card("cheap", CardType.Spell, cost: 3));

		int discarded = CutoffCurrentEffects.DiscardRandomFromHand(enemy, 1, maxCost: 8);

		Assert.Equal(1, discarded);
		Assert.Equal(2, enemy.Hand.Count);
		Assert.DoesNotContain(enemy.Hand, card => card.Id == "cheap");
		Assert.Contains(enemy.DeckState.DiscardPile, card => card.Id == "cheap");
	}

	[Fact]
	public void SSN093B_WhenAllCardsTooExpensive_DiscardsNothing()
	{
		var (_, enemy, _, _) = CreateBattle();
		enemy.Core.AddToHand(new Card.Card("expensive", CardType.Spell, cost: 9));

		int discarded = CutoffCurrentEffects.DiscardRandomFromHand(enemy, 1, maxCost: 8);

		Assert.Equal(0, discarded);
		Assert.Single(enemy.Hand);
		Assert.Empty(enemy.DeckState.DiscardPile);
	}

	// ===== 印第安纳号 =====

	[Fact]
	public void Indiana_ReturnsMinionToOwnerDrawPileTop_WithoutDeathrattle()
	{
		var (player, enemy, _, board) = CreateBattle();
		var minion = new Minion("minion_test", isPlayerSide: false, attack: 2, health: 3)
		{
			OwnerHero = enemy,
		};
		Assert.True(board.PlaceMinion(minion, 0));

		int deathEvents = 0;
		board.OnMinionDied += _ => deathEvents++;

		bool returned = CutoffCurrentEffects.ReturnMinionToOwnerDeckTop(board, player, enemy, minion);

		Assert.True(returned);
		Assert.Equal(0, deathEvents);
		Assert.DoesNotContain(minion, board.GetAllMinions());
		Assert.Equal("minion_test", enemy.DeckState.DrawPile[0].Id);
		Assert.Equal(CardType.Minion, enemy.DeckState.DrawPile[0].Type);

		// 下次抽牌必抽到这张牌。
		enemy.DrawCards(1);
		Assert.Contains(enemy.Hand, card => card.Id == "minion_test");
	}

	// ===== 黑水之下 =====

	[Fact]
	public void BeneathBlackWater_DrawsOneAndMakesEnemyDiscardOne()
	{
		var (player, enemy, _, _) = CreateBattle();
		enemy.Core.AddToHand(new Card.Card("enemy_card", CardType.Spell, cost: 2));
		player.DeckState.AddToDrawPileTop(new Card.Card("drawn_card", CardType.Spell));
		int handBefore = player.Hand.Count;

		player.DrawCards(1);
		int discarded = CutoffCurrentEffects.DiscardRandomFromHand(enemy, 1, 0);

		Assert.Equal(handBefore + 1, player.Hand.Count);
		Assert.Contains(player.Hand, card => card.Id == "drawn_card");
		Assert.Equal(1, discarded);
		Assert.Empty(enemy.Hand);
		Assert.Single(enemy.DeckState.DiscardPile);
	}

	// ===== 最后通牒 =====

	[Fact]
	public void Ultimatum_AddsThreeSSNToHand()
	{
		var (player, _, _, _) = CreateBattle();
		int before = player.Hand.Count;

		int added = CutoffCurrentEffects.AddNamedCardsToHand(player.Core, "spell_093b_ssn", 3,
			id => new Card.Card(id, CardType.Spell));

		Assert.Equal(3, added);
		Assert.Equal(before + 3, player.Hand.Count);
		Assert.Equal(3, player.Hand.Count(card => card.Id == "spell_093b_ssn"));
	}

	// ===== 能量币 =====

	[Fact]
	public void EnergyCoin_GrantsOneMana_AndPlusOneGrantsTwo()
	{
		var plain = CreateHero(true, 0);
		Assert.Equal(1, CutoffCurrentEffects.GainEnergyCoinMana(plain, 0));
		Assert.Equal(1, plain.CurrentMana);

		var plusOne = CreateHero(true, 0);
		Assert.Equal(2, CutoffCurrentEffects.GainEnergyCoinMana(plusOne, 1));
		Assert.Equal(2, plusOne.CurrentMana);

		var original = new Card.Card("spell_energy_coin", CardType.Spell) { BonusEnergyValue = 2 };
		var copy = new Card.Card("spell_energy_coin", CardType.Spell);
		copy.CopyRuntimeModifiersFrom(original);
		Assert.Equal(2, copy.BonusEnergyValue);
	}

	// ===== 反制：激活 / 取消激活 =====

	[Fact]
	public void Countermeasure_ArmAndDisarm_RefundsManaAndKeepsCardInHand()
	{
		var (player, _, _, _) = CreateBattle();
		var counter = new Card.Card("counter_borei", CardType.Countermeasure, cost: 2);
		player.Core.AddToHand(counter);
		var service = CreateService();

		Assert.True(service.Arm(player, counter));
		Assert.True(counter.IsCountermeasureArmed);
		Assert.Equal(2, counter.ArmedManaCost);
		Assert.Equal(8, player.CurrentMana);
		Assert.Single(player.Hand);
		Assert.Contains(counter, player.Hand);

		Assert.True(service.Disarm(player, counter));
		Assert.False(counter.IsCountermeasureArmed);
		Assert.Equal(0, counter.ArmedManaCost);
		Assert.Equal(10, player.CurrentMana);
		Assert.Single(player.Hand);
		Assert.Contains(counter, player.Hand);
	}

	[Fact]
	public void Countermeasure_Toggle_RoundTrips()
	{
		var (player, _, _, _) = CreateBattle();
		var counter = new Card.Card("counter_ohio_ssbn", CardType.Countermeasure, cost: 4);
		player.Core.AddToHand(counter);
		var service = CreateService();

		Assert.True(service.Toggle(player, counter));
		Assert.True(counter.IsCountermeasureArmed);
		Assert.Equal(6, player.CurrentMana);

		Assert.True(service.Toggle(player, counter));
		Assert.False(counter.IsCountermeasureArmed);
		Assert.Equal(10, player.CurrentMana);
	}

	[Fact]
	public void Countermeasure_UntriggeredArmedCards_ExpireAtNewTurn()
	{
		var (player, _, _, _) = CreateBattle();
		var counter = new Card.Card("counter_borei", CardType.Countermeasure, cost: 2);
		player.Core.AddToHand(counter);
		var service = CreateService();
		Assert.True(service.Arm(player, counter));

		Assert.Equal(1, service.DisarmAll(player));
		Assert.False(counter.IsCountermeasureArmed);
		Assert.Equal(8, player.CurrentMana); // 失效不退费
		Assert.Contains(counter, player.Hand);
	}

	// ===== 反制：敌方回合拦截 =====

	[Fact]
	public void Borei_InterceptsEnemySpell_MakesEnemyDiscardOne()
	{
		var (player, enemy, _, _) = CreateBattle();
		var borei = new Card.Card("counter_borei", CardType.Countermeasure, cost: 2);
		player.Core.AddToHand(borei);
		enemy.Core.AddToHand(new Card.Card("enemy_card_a", CardType.Spell, cost: 1));
		enemy.Core.AddToHand(new Card.Card("enemy_card_b", CardType.Spell, cost: 3));
		var service = CreateService();
		Assert.True(service.Arm(player, borei));

		var spell = new Card.Card("enemy_spell", CardType.Spell, cost: 3);
		var result = service.Intercept(enemy, player, spell);

		Assert.False(result.Canceled);
		Assert.Equal(1, result.BoreiTriggers);
		Assert.Single(enemy.Hand);
		Assert.Single(enemy.DeckState.DiscardPile);
		Assert.Empty(player.Hand);
		Assert.Single(player.DeckState.DiscardPile);
		Assert.Contains(borei, player.DeckState.DiscardPile);
	}

	[Fact]
	public void Ohio_InterceptsSpellCostingFourOrMore_CancelsAndPutsCoinOnTop()
	{
		var (player, enemy, _, _) = CreateBattle();
		var ohio = new Card.Card("counter_ohio_ssbn", CardType.Countermeasure, cost: 4);
		player.Core.AddToHand(ohio);
		enemy.DeckState.AddToDrawPileTop(new Card.Card("filler", CardType.Spell));
		var service = CreateService();
		Assert.True(service.Arm(player, ohio));

		var spell = new Card.Card("enemy_big_spell", CardType.Spell, cost: 5);
		var result = service.Intercept(enemy, player, spell);

		Assert.True(result.Canceled);
		Assert.Equal(1, result.OhioTriggers);
		Assert.Empty(player.Hand);
		Assert.Contains(ohio, player.DeckState.DiscardPile);
		Assert.Equal(2, enemy.DeckState.DrawPile.Count);
		Assert.Equal("spell_energy_coin", enemy.DeckState.DrawPile[0].Id);
		Assert.Equal(1, ((Card.Card)enemy.DeckState.DrawPile[0]).BonusEnergyValue);

		var drawn = enemy.DrawCards(1).Single();
		Assert.Equal("spell_energy_coin", drawn.Id);
	}

	[Fact]
	public void Ohio_DoesNotInterceptSpellCheaperThanFour()
	{
		var (player, enemy, _, _) = CreateBattle();
		var ohio = new Card.Card("counter_ohio_ssbn", CardType.Countermeasure, cost: 4);
		player.Core.AddToHand(ohio);
		enemy.DeckState.AddToDrawPileTop(new Card.Card("filler", CardType.Spell));
		var service = CreateService();
		Assert.True(service.Arm(player, ohio));

		var spell = new Card.Card("enemy_small_spell", CardType.Spell, cost: 3);
		var result = service.Intercept(enemy, player, spell);

		Assert.False(result.Canceled);
		Assert.Equal(0, result.OhioTriggers);
		Assert.True(ohio.IsCountermeasureArmed);
		Assert.Contains(ohio, player.Hand);
		Assert.Equal("filler", enemy.DeckState.DrawPile[0].Id);
	}

	// ===== 弃牌溢出：不产生任何额外收益 =====

	[Fact]
	public void DiscardRandomFromHand_EmptyHand_ReturnsZeroWithoutTouchingHero()
	{
		var (_, enemy, _, _) = CreateBattle();
		int health = enemy.CurrentHealth;
		int armor = enemy.CurrentArmor;

		int discarded = CutoffCurrentEffects.DiscardRandomFromHand(enemy, 3, 0);

		Assert.Equal(0, discarded);
		Assert.Equal(health, enemy.CurrentHealth);
		Assert.Equal(armor, enemy.CurrentArmor);
		Assert.Empty(enemy.Hand);
		Assert.Empty(enemy.DeckState.DiscardPile);
	}

	[Fact]
	public void Borei_WithNothingToDiscard_LeavesCasterHealthAndArmorUntouched()
	{
		var (player, enemy, _, _) = CreateBattle();
		var borei = new Card.Card("counter_borei", CardType.Countermeasure, cost: 2);
		player.Core.AddToHand(borei);
		var service = CreateService();
		Assert.True(service.Arm(player, borei));
		int health = enemy.CurrentHealth;
		int armor = enemy.CurrentArmor;

		var spell = new Card.Card("enemy_spell", CardType.Spell, cost: 3);
		var result = service.Intercept(enemy, player, spell);

		Assert.False(result.Canceled);
		Assert.Equal(1, result.BoreiTriggers);
		Assert.Empty(enemy.Hand);
		Assert.Empty(enemy.DeckState.DiscardPile);
		Assert.Equal(health, enemy.CurrentHealth);
		Assert.Equal(armor, enemy.CurrentArmor);
		Assert.Single(player.DeckState.DiscardPile);
	}

	[Fact]
	public void Borei_ReportsExactlyWhichEnemyCardsWereDiscarded()
	{
		var (player, enemy, _, _) = CreateBattle();
		var borei = new Card.Card("counter_borei", CardType.Countermeasure, cost: 2);
		player.Core.AddToHand(borei);
		enemy.Core.AddToHand(new Card.Card("enemy_card_a", CardType.Spell, cost: 1));
		enemy.Core.AddToHand(new Card.Card("enemy_card_b", CardType.Spell, cost: 3));
		var service = CreateService();
		Assert.True(service.Arm(player, borei));

		var result = service.Intercept(enemy, player, new Card.Card("enemy_spell", CardType.Spell, cost: 3));

		Assert.False(result.Canceled);
		Assert.Equal(1, result.BoreiTriggers);
		TriggeredCountermeasure trigger = Assert.Single(result.Triggers!);
		Assert.Equal("counter_borei", trigger.Counter.Id);
		Assert.False(trigger.Canceled);
		// 反制结果必须带回实际被弃掉的那张牌，而不是只给一个数字。
		Card.Card discarded = Assert.Single(trigger.DiscardedCards!);
		Assert.Same(discarded, Assert.Single(enemy.DeckState.DiscardPile));
		Assert.True(discarded.Id is "enemy_card_a" or "enemy_card_b");
	}

	[Fact]
	public void Borei_WithNothingToDiscard_ReportsEmptyDiscardDetails()
	{
		var (player, enemy, _, _) = CreateBattle();
		var borei = new Card.Card("counter_borei", CardType.Countermeasure, cost: 2);
		player.Core.AddToHand(borei);
		var service = CreateService();
		Assert.True(service.Arm(player, borei));

		var result = service.Intercept(enemy, player, new Card.Card("enemy_spell", CardType.Spell, cost: 3));

		// 真实触发但敌方没有其他手牌：触发计数为 1，实际弃牌明细为空。
		Assert.Equal(1, result.BoreiTriggers);
		TriggeredCountermeasure trigger = Assert.Single(result.Triggers!);
		Assert.Empty(trigger.DiscardedCards!);
		Assert.Empty(enemy.DeckState.DiscardPile);
	}

	private sealed class StubEncounter : EnemyEncounter
	{
		public StubEncounter()
			: base("测试敌人", 30, new[] { new EnemyIntent(IntentType.Buff, 0, "") })
		{
		}
	}
}
