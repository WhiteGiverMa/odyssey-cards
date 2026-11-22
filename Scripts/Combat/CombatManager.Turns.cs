#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Godot;
using OdysseyCards.AI;

namespace OdysseyCards.Combat;

/// <summary>有 UI 时的敌方回合异步步进；规则阶段与 <see cref="EndPlayerTurn"/> 共用。</summary>
public partial class CombatManager
{
	private int _combatSessionVersion;
	private bool _enemyTurnAsyncRunning;

	public bool IsEnemyTurnStepping => _enemyTurnAsyncRunning;

	/// <summary>使等待中的 UI 回合安静失效；快速 SL、保存退出与场景清理可调用。</summary>
	public void CancelEnemyTurnStepping()
	{
		_combatSessionVersion++;
		_enemyTurnAsyncRunning = false;
		_isEnemyTurnAnimating = false;
	}

	/// <summary>
	/// UI 专用异步回合。stepDelay 为 null 时不引入任何定时等待；
	/// 单测与 CommanderRuntimeQa 应继续使用完全同步的 <see cref="EndPlayerTurn"/>。
	/// </summary>
	[SuppressMessage("Reliability", "CA2007:DoNotConfigureAwait", Justification = "Godot 回调与节点访问必须回到主线程。")]
	public async Task EndPlayerTurnAsync(Func<EnemyStepKind, Task>? stepDelay = null)
	{
		if (_enemyTurnAsyncRunning || !CanEndPlayerTurn())
			return;
		_enemyTurnAsyncRunning = true;
		int session = _combatSessionVersion;
		try
		{
			if (!RunPlayerTurnEndPhase())
				return;
			State.EndPlayerTurn();
			if (!BeginEnemyTurnPhase())
				return;

			foreach (var unit in EnemyUnits)
			{
				if (!CanContinueAsync(session))
					return;
				if (unit.Body.IsDead)
					continue;

				if (unit.Controller is IDeckDrivenCommander commander)
				{
					PrepareCommanderTurn(unit.Body);
					commander.BeginTurn(unit.Body);
					await DelayStep(stepDelay, EnemyStepKind.TurnPrepare);
					int guard = 0;
					while (guard++ < CommanderTurnLoop.MaxActionsPerTurn && CanContinueAsync(session))
					{
						if (!commander.TryExecuteSingleBestAction(this, unit))
							break;
						await DelayStep(stepDelay, commander.LastActionKind);
						if (!CanContinueAsync(session) || _victoryResolver.CheckVictoryOrDefeat())
							return;
					}
					if (!CanContinueAsync(session))
						return;
					commander.ArmRemainingCountermeasures(this, unit.Body);
					CheckDeaths();
					if (_victoryResolver.CheckVictoryOrDefeat())
						return;
					GD.Print($"[CombatManager] {unit.Brain.Name} 指挥官回合结束（法力 {unit.Body.CurrentMana}/{unit.Body.MaxMana}，手牌 {unit.Body.Hand.Count}）");
				}
				else
				{
					if (!ExecuteEnemyActorTurn(unit))
						return;
					await DelayStep(stepDelay, EnemyStepKind.MinionAction);
				}
			}

			foreach (var attacker in GetLegacyEnemyMinionsForTurn())
			{
				if (!CanContinueAsync(session))
					return;
				if (ExecuteLegacyEnemyMinionAction(attacker))
					await DelayStep(stepDelay, EnemyStepKind.MinionAction);
				CheckDeaths();
				if (_victoryResolver.CheckVictoryOrDefeat())
					return;
			}

			if (!CanContinueAsync(session) || !RunEnemyTurnEndPhase())
				return;
			await DelayStep(stepDelay, EnemyStepKind.TurnEnd);
			if (CanContinueAsync(session))
				CompletePlayerRoundAfterEnemyTurn();
		}
		finally
		{
			_enemyTurnAsyncRunning = false;
			_isEnemyTurnAnimating = false;
		}
	}

	[SuppressMessage("Reliability", "CA2007:DoNotConfigureAwait", Justification = "Godot UI 步进必须回到主线程。")]
	private static async Task DelayStep(Func<EnemyStepKind, Task>? stepDelay, EnemyStepKind kind)
	{
		if (stepDelay != null)
			await stepDelay(kind);
	}

	private bool CanContinueAsync(int session)
	{
		return GodotObject.IsInstanceValid(this)
			&& IsInsideTree()
			&& !IsQueuedForDeletion()
			&& session == _combatSessionVersion
			&& !State.IsGameOver;
	}

	public override void _ExitTree()
	{
		CancelEnemyTurnStepping();
	}
}
