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
        Assert.Equal(0, state.PlayerMana);
        Assert.Equal(0, state.PlayerMaxMana);
    }

    [Fact]
    public void StartPlayerTurn_IncrementsTurnAndMana()
    {
        var state = new GameState();
        state.StartGame();
        state.StartPlayerTurn();

        Assert.Equal(1, state.TurnCount);
        Assert.Equal(1, state.PlayerMaxMana);
        Assert.Equal(1, state.PlayerMana);
    }

    [Fact]
    public void PlayerManaCapsAt10()
    {
        var state = new GameState();
        state.StartGame();

        for (int i = 0; i < 11; i++)
            state.StartPlayerTurn();

        Assert.Equal(GameState.MaxManaCrystals, state.PlayerMaxMana);
        Assert.Equal(GameState.MaxManaCrystals, state.PlayerMana);
    }

    [Fact]
    public void SpendPlayerMana_ReducesCurrentMana()
    {
        var state = new GameState();
        state.StartGame();
        state.StartPlayerTurn();
        state.StartPlayerTurn();

        bool spent = state.SpendPlayerMana(1);

        Assert.True(spent);
        Assert.Equal(1, state.PlayerMana);
    }

    [Fact]
    public void SpendPlayerMana_Insufficient_ReturnsFalse()
    {
        var state = new GameState();
        state.StartGame();
        state.StartPlayerTurn();

        bool spent = state.SpendPlayerMana(5);

        Assert.False(spent);
        Assert.Equal(1, state.PlayerMana);
    }

    [Fact]
    public void EndPlayerTurn_TransitionsToEnemyTurn()
    {
        var state = new GameState();
        state.StartGame();
        state.StartPlayerTurn();

        state.EndPlayerTurn();

        Assert.Equal(CombatPhase.EnemyTurn, state.Phase);
        Assert.True(state.IsEnemyTurn);
    }

    [Fact]
    public void EndEnemyTurn_TransitionsToPlayerTurn()
    {
        var state = new GameState();
        state.StartGame();
        state.StartPlayerTurn();
        state.EndPlayerTurn();

        state.EndEnemyTurn();

        Assert.Equal(CombatPhase.PlayerTurn, state.Phase);
        Assert.True(state.IsPlayerTurn);
    }
}
