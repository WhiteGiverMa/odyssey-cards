using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using Xunit;
using RuntimeCard = OdysseyCards.Card.Card;

namespace OdysseyCards.Tests.Unit;

public class IntelServiceTests
{
	[Fact]
	public void Resolve_RevealsHiddenInstancesFromLeftToRight_SkipsAlreadyPublic()
	{
		var owner = new Hero(new CommanderCore(), true);
		var hand = new[] { new RuntimeCard("same"), new RuntimeCard("same") { IsRevealedByIntel = true }, new RuntimeCard("same"), new RuntimeCard("last") };
		var result = new IntelService().Resolve(owner, 2, hand);
		Assert.Equal(new IntelResult(2, 0), result);
		Assert.True(hand[0].IsRevealedByIntel);
		Assert.True(hand[2].IsRevealedByIntel);
		Assert.False(hand[3].IsRevealedByIntel);
		Assert.Equal(0, owner.CurrentArmor);
	}

	[Fact]
	public void Resolve_IntentEnemy_OverflowsWithoutHealingOrArmor()
	{
		var core = new CommanderCore();
		core.InitializeHealth(30, 28);
		var owner = new Hero(core, true);
		var intel = new IntelService();
		int events = 0;
		intel.OnIntelOverflow += (hero, amount, reason) =>
		{
			Assert.Same(owner, hero);
			Assert.Equal(5, amount);
			Assert.Equal(IntelOverflowReason.NoEnemyHand, reason);
			events++;
		};
		Assert.Equal(new IntelResult(0, 5), intel.Resolve(owner, 5));
		Assert.Equal(1, events);
		// 溢出只发通知：既不能回血（28/30）也不能满血转格挡。
		Assert.Equal(28, owner.CurrentHealth);
		Assert.Equal(0, owner.CurrentArmor);
	}

	[Fact]
	public void Resolve_NotEnoughHiddenCards_OverflowsOnlyUnusedPoints()
	{
		var core = new CommanderCore();
		core.InitializeHealth(30, 30);
		var owner = new Hero(core, false);
		var hand = new[] { new RuntimeCard("old") { IsRevealedByIntel = true }, new RuntimeCard("new") };
		var intel = new IntelService();
		IntelOverflowReason? observed = null;
		intel.OnIntelOverflow += (_, _, reason) => observed = reason;
		Assert.Equal(new IntelResult(1, 2), intel.Resolve(owner, 3, hand));
		Assert.Equal(IntelOverflowReason.NotEnoughHiddenCards, observed);
		Assert.Equal(0, owner.CurrentArmor);
		Assert.Equal(30, owner.CurrentHealth);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-3)]
	public void Resolve_NonPositivePoints_NoRevealHealArmorOrEvent(int points)
	{
		var owner = new Hero(new CommanderCore(), true);
		var card = new RuntimeCard("hidden");
		var intel = new IntelService();
		int events = 0;
		intel.OnIntelOverflow += (_, _, _) => events++;
		Assert.Equal(default, intel.Resolve(owner, points, new[] { card }));
		Assert.False(card.IsRevealedByIntel);
		Assert.Equal(0, events);
	}

	[Fact]
	public void CopyRuntimeModifiers_PreservesIntel_ButFreshCopyStartsHidden()
	{
		var original = new RuntimeCard("card") { IsRevealedByIntel = true };
		var copied = new RuntimeCard("card");
		copied.CopyRuntimeModifiersFrom(original);
		Assert.True(copied.IsRevealedByIntel);
		Assert.False(new RuntimeCard("card").IsRevealedByIntel);
	}

	[Fact]
	public void MinionToRuntimeCard_KeepsIntelWithoutEngineResource()
	{
		var minion = new Minion("runtime", true) { IsRevealedByIntel = true };
		var card = minion.ToRuntimeCard();
		Assert.Equal(minion.Id, card.Id);
		Assert.True(card.IsRevealedByIntel);
	}
}
