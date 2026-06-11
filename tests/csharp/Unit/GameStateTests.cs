using Xunit;
using OdysseyCards.Combat;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — GameState 回合/法力逻辑
/// GameState 是纯 C# 类，不依赖 Godot API，可直接测试
/// </summary>
public class GameStateTests
{
	[Fact]
	public void StartGame_InitializesToMulliganPhase()
	{
		var state = new GameState();
		state.StartGame();

		Assert.Equal(CombatPhase.Mulligan, state.Phase);
		Assert.Equal(GameState.StartingMaxMana, state.PlayerMana);
		Assert.Equal(GameState.StartingMaxMana, state.PlayerMaxMana);
	}

	[Fact]
	public void StartPlayerTurn_IncrementsTurnAndMana()
	{
		var state = new GameState();
		state.StartGame();
		state.StartPlayerTurn(GameState.MaxManaCrystals);

		// StartGame 设置 PMM=3, StartPlayerTurn +1 → 4
		Assert.Equal(1, state.TurnCount);
		Assert.Equal(GameState.StartingMaxMana + 1, state.PlayerMaxMana);
		Assert.Equal(GameState.StartingMaxMana + 1, state.PlayerMana);
	}

	[Fact]
	public void PlayerManaCapsAtNaturalGrowthCap()
	{
		var state = new GameState();
		state.StartGame();

		// 增长到自然上限为止（默认 12）
		int expectedCap = GameState.MaxManaCrystals;
		for (int i = 0; i < expectedCap; i++)
			state.StartPlayerTurn(expectedCap);

		Assert.Equal(expectedCap, state.PlayerMaxMana);
		Assert.Equal(expectedCap, state.PlayerMana);
	}

	[Fact]
	public void PlayerManaWithUnlimitedPotential_CanGrowToHardCap()
	{
		var state = new GameState();
		state.StartGame();

		// 有了无限潜能，上限提升到 30
		for (int i = 0; i < 28; i++)
			state.StartPlayerTurn(GameState.HardMaxManaCap);

		Assert.Equal(GameState.HardMaxManaCap, state.PlayerMaxMana);
		Assert.Equal(GameState.HardMaxManaCap, state.PlayerMana);
	}

	[Fact]
	public void SpendPlayerMana_ReducesCurrentMana()
	{
		var state = new GameState();
		state.StartGame();
		state.StartPlayerTurn(GameState.MaxManaCrystals);
		// StartGame=3, 第1回合+1=4
		state.StartPlayerTurn(GameState.MaxManaCrystals);
		// 第2回合+1=5

		bool spent = state.SpendPlayerMana(1);

		Assert.True(spent);
		Assert.Equal(4, state.PlayerMana);
	}

	[Fact]
	public void SpendPlayerMana_Insufficient_ReturnsFalse()
	{
		var state = new GameState();
		state.StartGame();
		state.StartPlayerTurn(GameState.MaxManaCrystals);
		// StartGame=3, 第1回合+1=4

		bool spent = state.SpendPlayerMana(5);

		Assert.False(spent);
		Assert.Equal(4, state.PlayerMana);
	}

	[Fact]
	public void EndPlayerTurn_TransitionsToEnemyTurn()
	{
		var state = new GameState();
		state.StartGame();
		state.StartPlayerTurn(GameState.MaxManaCrystals);

		state.EndPlayerTurn();

		Assert.Equal(CombatPhase.EnemyTurn, state.Phase);
		Assert.True(state.IsEnemyTurn);
	}

	[Fact]
	public void EndEnemyTurn_TransitionsToPlayerTurn()
	{
		var state = new GameState();
		state.StartGame();
		state.StartPlayerTurn(GameState.MaxManaCrystals);
		state.EndPlayerTurn();

		state.EndEnemyTurn();

		Assert.Equal(CombatPhase.PlayerTurn, state.Phase);
		Assert.True(state.IsPlayerTurn);
	}

	[Fact]
	public void GainManaSlot_IncreasesMaxManaOnly()
	{
		var state = new GameState();
		state.StartGame();
		// 初始 PMM=3, PM=3

		state.GainManaSlot(2);
		// PMM 应=5, PM 保持=3

		Assert.Equal(5, state.PlayerMaxMana);
		Assert.Equal(GameState.StartingMaxMana, state.PlayerMana);
	}

	[Fact]
	public void GainManaSlot_RespectsHardCap()
	{
		var state = new GameState();
		state.StartGame();

		state.GainManaSlot(100);

		Assert.Equal(GameState.HardMaxManaCap, state.PlayerMaxMana);
	}
}
