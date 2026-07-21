using OdysseyCards.Combat;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — Board 槽位结构
/// 测试 Board 的槽位属性和基本操作。
/// </summary>
public class BoardTests
{
	[Fact]
	public void Constructor_CreatesCorrectSlotCount()
	{
		var board = new Board();

		Assert.Equal(Board.MaxSlotsPerSide, board.PlayerSlots.Length);
		Assert.Equal(Board.MaxSlotsPerSide, board.EnemySlots.Length);
	}

	[Fact]
	public void PlayerSlots_InitiallyAllNull()
	{
		var board = new Board();

		foreach (var slot in board.PlayerSlots)
			Assert.Null(slot);

		foreach (var slot in board.EnemySlots)
			Assert.Null(slot);
	}

	[Fact]
	public void CanPlaceMinion_EmptyBoard_ReturnsTrue()
	{
		var board = new Board();

		Assert.True(board.CanPlaceMinion(isPlayerSide: true));
		Assert.True(board.CanPlaceMinion(isPlayerSide: false));
	}

	[Fact]
	public void GetEmptySlotIndex_EmptyBoard_ReturnsZero()
	{
		var board = new Board();

		int index = board.GetEmptySlotIndex(isPlayerSide: true);

		Assert.Equal(0, index);
	}

	[Fact]
	public void MaxSlotsPerSide_IsFive()
	{
		Assert.Equal(5, Board.MaxSlotsPerSide);
	}

	[Fact]
	public void GetMinionAt_EmptySlot_ReturnsNull()
	{
		var board = new Board();

		Assert.Null(board.GetMinionAt(0, isPlayerSide: true));
		Assert.Null(board.GetMinionAt(0, isPlayerSide: false));
	}
}
