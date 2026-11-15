using System.Linq;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using Xunit;

namespace OdysseyCards.Tests.Unit;

public class BoardTests
{
	[Fact]
	public void Constructor_ThreeRows_AreFourFiveFourAndNeutral()
	{
		var board = new Board();
		Assert.Equal(4, board.PlayerSlots.Count);
		Assert.Equal(5, board.FrontlineSlots.Count);
		Assert.Equal(4, board.EnemySlots.Count);
		Assert.Equal(FrontlineOwner.None, board.FrontlineOwner);
		Assert.Empty(board.GetAllMinions());
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void PlaceMinion_FullSupport_NeverOverflowsIntoFrontline(bool player)
	{
		var board = new Board();
		for (int i = 0; i < 4; i++)
			Assert.True(board.PlaceMinion(new Minion($"unit{i}", player), i));
		var extra = new Minion("overflow", player);
		Assert.False(board.CanPlaceMinion(player));
		Assert.Equal(-1, board.GetEmptySlotIndex(player));
		Assert.False(board.PlaceMinion(extra, -1));
		Assert.False(board.PlaceMinion(extra, 4));
		Assert.False(board.PlaceMinion(extra, 0));
		Assert.Null(extra.BoardRow);
		Assert.Equal(-1, extra.BoardSlotIndex);
		Assert.All(board.FrontlineSlots, Assert.Null);
	}

	[Fact]
	public void MoveMinion_ChangesLocationAndOwner_OnlyBroadcastsMoveEvents()
	{
		var board = new Board();
		var minion = new Minion("unit", true);
		board.PlaceMinion(minion, 2);
		int placed = 0, removed = 0, died = 0, moved = 0, ownerChanged = 0;
		board.OnMinionPlaced += (_, _) => placed++;
		board.OnMinionRemoved += _ => removed++;
		board.OnMinionDied += _ => died++;
		board.OnMinionMoved += (unit, from, to) =>
		{
			Assert.Same(minion, unit);
			Assert.Equal(new BoardPosition(BoardRow.PlayerSupport, 2), from);
			Assert.Equal(new BoardPosition(BoardRow.Frontline, 4), to);
			moved++;
		};
		board.OnFrontlineOwnerChanged += (old, current) =>
		{
			Assert.Equal(FrontlineOwner.None, old);
			Assert.Equal(FrontlineOwner.Player, current);
			ownerChanged++;
		};
		Assert.True(board.MoveMinionToFrontline(minion, 4));
		Assert.Null(board.GetMinionAt(2, true));
		Assert.Same(minion, board.GetMinionAt(new BoardPosition(BoardRow.Frontline, 4)));
		Assert.Equal(1, moved);
		Assert.Equal(1, ownerChanged);
		Assert.Equal(0, placed + removed + died);
	}

	[Fact]
	public void MoveMinion_EnemyOccupiedFrontline_RejectsUntilFinalUnitLeaves()
	{
		var board = new Board();
		var player = new Minion("player", true);
		var enemy = new Minion("enemy", false);
		board.PlaceMinion(player, 0);
		board.PlaceMinion(enemy, 0);
		Assert.True(board.MoveMinionToFrontline(player));
		Assert.False(board.MoveMinionToFrontline(enemy));
		Assert.True(board.RecallMinion(player));
		Assert.Equal(FrontlineOwner.None, board.FrontlineOwner);
		Assert.True(board.MoveMinionToFrontline(enemy));
		Assert.Equal(FrontlineOwner.Enemy, board.FrontlineOwner);
	}

	[Fact]
	public void Frontline_FiveUnitsFull_AggregatesAllNineAndDeathResetIsImmediate()
	{
		var board = new Board();
		for (int i = 0; i < 5; i++)
		{
			var unit = new Minion($"front{i}", true);
			board.PlaceMinion(unit, 0);
			Assert.True(board.MoveMinionToFrontline(unit));
		}
		for (int i = 0; i < 4; i++)
			board.PlaceMinion(new Minion($"support{i}", true), i);
		Assert.Equal(9, board.GetPlayerMinions().Count);
		Assert.False(board.CanMoveMinionToFrontline(board.PlayerSlots[0]!));
		foreach (var unit in board.FrontlineSlots.OfType<Minion>().ToList())
			board.RemoveMinion(unit);
		Assert.Equal(FrontlineOwner.None, board.FrontlineOwner);
		Assert.Equal(4, board.GetPlayerMinions().Count);
	}

	[Fact]
	public void RecallMinion_DoesNotFireDeath_AndDeadScanIncludesEveryRow()
	{
		var board = new Board();
		var front = new Minion("front", true);
		var support = new Minion("support", true);
		var enemy = new Minion("enemy", false);
		board.PlaceMinion(front, 0);
		board.MoveMinionToFrontline(front);
		board.PlaceMinion(support, 0);
		board.PlaceMinion(enemy, 0);
		int deaths = 0;
		board.OnMinionDied += _ => deaths++;
		Assert.True(board.RecallMinion(enemy));
		Assert.False(board.RecallMinion(enemy));
		Assert.Equal(0, deaths);
		front.ApplyDevDamage(1);
		support.ApplyDevDamage(1);
		Assert.Empty(board.GetPlayerMinions());
		Assert.Equal(2, board.GetAllMinions().Count(m => m.IsDead));
	}

	[Fact]
	public void AdjacentCopy_LeftThenRightThenAny_AndFullSupportReturnsMinusOne()
	{
		var board = new Board();
		var source = new Minion("source", true);
		board.PlaceMinion(source, 1);
		Assert.Equal(0, board.GetAdjacentSupportSlotIndex(source));
		board.PlaceMinion(new Minion("left", true), 0);
		Assert.Equal(2, board.GetAdjacentSupportSlotIndex(source));
		board.PlaceMinion(new Minion("right", true), 2);
		Assert.Equal(3, board.GetAdjacentSupportSlotIndex(source));
		board.PlaceMinion(new Minion("last", true), 3);
		Assert.Equal(-1, board.GetAdjacentSupportSlotIndex(source));
		Assert.False(board.PlaceMinion(new Minion("overflow", true), board.GetAdjacentSupportSlotIndex(source)));
	}
}
