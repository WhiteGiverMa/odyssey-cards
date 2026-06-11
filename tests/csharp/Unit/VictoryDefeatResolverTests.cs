using System.Collections.Generic;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — 多敌人胜负判定。
/// </summary>
public class VictoryDefeatResolverTests
{
	[Fact]
	public void CheckVictoryOrDefeat_TwoEnemiesOneDead_DoesNotEndGame()
	{
		var state = new GameState();
		var firstEnemy = CreateEnemyUnit(new ZhangLang());
		var secondEnemy = CreateEnemyUnit(new ShanHu());
		var enemies = new List<EnemyUnit>
		{
			firstEnemy.Unit,
			secondEnemy.Unit,
		};
		var resolver = new VictoryDefeatResolver(new Board(), state, enemies, CreatePlayerCore());
		bool gameOverEventRaised = false;
		resolver.OnGameOver += _ => gameOverEventRaised = true;

		firstEnemy.Core.ApplyDamage(999);
		bool isGameOver = resolver.CheckVictoryOrDefeat();

		Assert.False(isGameOver);
		Assert.False(state.IsGameOver);
		Assert.False(gameOverEventRaised);
		Assert.True(enemies[0].Body.IsDead);
		Assert.False(enemies[1].Body.IsDead);
	}

	private static (EnemyUnit Unit, CommanderCore Core) CreateEnemyUnit(EnemyEncounter encounter)
	{
		var core = new CommanderCore();
		core.InitializeHealth(encounter.MaxHealth, encounter.MaxHealth);
		return (new EnemyUnit(new Hero(core, isPlayerSide: false), encounter), core);
	}

	private static CommanderCore CreatePlayerCore()
	{
		var core = new CommanderCore();
		core.InitializeHealth(30, 30);
		return core;
	}
}
