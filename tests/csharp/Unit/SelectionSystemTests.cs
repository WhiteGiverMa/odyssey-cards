using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — 手牌选择系统。
/// </summary>
public class SelectionSystemTests
{
	[Fact]
	public void BeginHandDiscardSelection_AllCardsForced_DiscardsWithoutEnteringSelection()
	{
		var context = CreateContext();
		var cards = new List<Card.Card> { CreateRuntimeCard(), CreateRuntimeCard() };
		context.Core.Hand.AddRange(cards);

		context.System.BeginHandDiscardSelection(cards, min: 2, max: 2, isBladeCrisis: false);

		Assert.False(context.System.IsHandSelecting);
		Assert.Empty(context.Core.Hand);
		Assert.Equal(cards, context.Core.DiscardPile);
		Assert.Equal(1, context.Notifications);
		Assert.Equal(1, context.DeathChecks);
		Assert.Equal(1, context.VictoryChecks);
	}

	[Fact]
	public void BeginCustomHandDiscardSelection_OnlyForcedCard_ConfirmsWithoutEnteringSelection()
	{
		var context = CreateContext();
		var card = CreateRuntimeCard();
		context.Core.Hand.Add(card);
		IReadOnlyList<Card.Card>? confirmed = null;

		context.System.BeginCustomHandDiscardSelection(
			new List<Card.Card> { card },
			min: 1,
			max: 1,
			CombatManager.PendingSelectionMode.CopyHandFill,
			canCancel: false,
			selectedCards => confirmed = selectedCards);

		Assert.False(context.System.IsHandSelecting);
		Assert.NotNull(confirmed);
		Assert.Same(card, Assert.Single(confirmed));
		Assert.Single(context.Core.Hand);
		Assert.Empty(context.Core.DiscardPile);
		Assert.Equal(1, context.Notifications);
	}

	[Fact]
	public void BeginHandDiscardSelection_HasMeaningfulChoice_EntersSelectionMode()
	{
		var context = CreateContext();
		var cards = new List<Card.Card> { CreateRuntimeCard(), CreateRuntimeCard() };
		context.Core.Hand.AddRange(cards);

		context.System.BeginHandDiscardSelection(cards, min: 1, max: 1, isBladeCrisis: false);

		Assert.True(context.System.IsHandSelecting);
		Assert.Equal(cards, context.System.HandSelectOptions);
		Assert.Equal(1, context.System.HandSelectMin);
		Assert.Equal(1, context.System.HandSelectMax);
		Assert.Equal(1, context.Notifications);
		Assert.Empty(context.Core.DiscardPile);
	}

	private static TestContext CreateContext()
	{
		var core = new CommanderCore();
		core.InitializeHealth(30, 30);
		var hero = new Hero(core, isPlayerSide: true);
		var board = new Board();
		var state = new GameState();

		var context = new TestContext(core);
		context.System = new SelectionSystem(
			hero,
			core,
			board,
			state,
			() => context.Notifications++,
			() => context.DeathChecks++,
			() =>
			{
				context.VictoryChecks++;
				return false;
			});

		return context;
	}

	private static Card.Card CreateRuntimeCard()
	{
		return (Card.Card)RuntimeHelpers.GetUninitializedObject(typeof(Card.Card));
	}

	private sealed class TestContext
	{
		public TestContext(CommanderCore core)
		{
			Core = core;
		}

		public CommanderCore Core { get; }

		public SelectionSystem System { get; set; } = null!;

		public int Notifications { get; set; }

		public int DeathChecks { get; set; }

		public int VictoryChecks { get; set; }
	}
}
