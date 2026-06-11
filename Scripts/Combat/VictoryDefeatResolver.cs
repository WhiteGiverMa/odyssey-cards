#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;
using OdysseyCards.Roguelike;

namespace OdysseyCards.Combat;

/// <summary>
/// 胜负判定器——检查战斗结束条件，触发胜负事件。
/// 从 CombatManager 拆出为纯 C# 类。
/// </summary>
internal sealed class VictoryDefeatResolver
{
	private readonly Board _board;
	private readonly GameState _state;
	private readonly IReadOnlyList<EnemyUnit> _enemyUnits;
	private readonly CommanderCore _playerCore;
	private bool _devSkipGoldReward;

	/// <summary>
	/// 游戏结束事件（true=胜利, false=失败）。
	/// </summary>
	public event Action<bool>? OnGameOver;

	public VictoryDefeatResolver(Board board, GameState state, IReadOnlyList<EnemyUnit> enemyUnits, CommanderCore playerCore)
	{
		_board = board;
		_state = state;
		_enemyUnits = enemyUnits;
		_playerCore = playerCore;
	}

	/// <summary>
	/// DevConsole 强制胜利时跳过金币奖励。
	/// </summary>
	public bool DevSkipGoldReward { set => _devSkipGoldReward = value; }

	/// <summary>
	/// 检查是否达成胜利或失败条件。
	/// 所有敌方英雄死亡 → 胜利；玩家英雄死亡 → 失败。
	/// 触发 OnGameOver 事件（含 AwardGold 奖励发放）。
	/// </summary>
	/// <returns>游戏结束返回 true</returns>
	public bool CheckVictoryOrDefeat()
	{
		if (_state.IsGameOver)
			return true;

		// 胜利 = 所有敌方英雄均已死亡
		if (_enemyUnits.All(u => u.Body.IsDead))
		{
			GD.Print("[CombatManager] ★★★ 敌方全部被击败 — 玩家胜利！★★★");

			AwardGold();

			_state.SetVictory();
			OnGameOver?.Invoke(true);
			return true;
		}

		if (_playerCore.IsDead)
		{
			GD.Print("[CombatManager] ☠☠☠ 玩家英雄被击败 — 玩家失败 ☠☠☠");

			_state.SetDefeat();
			OnGameOver?.Invoke(false);
			return true;
		}

		return false;
	}

	/// <summary>
	/// 根据房间类型发放金币奖励。
	/// 怪物 10-15，精英 25-35，Boss 50。
	/// </summary>
	private void AwardGold()
	{
		if (_devSkipGoldReward)
			return;

		var gm = GameManager.Instance;
		if (gm == null)
			return;

		int goldReward = gm.RunState?.SelectedRoom?.Type switch
		{
			RoomType.Monster => new System.Random().Next(10, 16),  // 10-15
			RoomType.Elite => new System.Random().Next(25, 36),     // 25-35
			RoomType.Boss => 50,                                     // 50
			_ => 0,
		};

		if (goldReward > 0)
		{
			gm.AddGold(goldReward);
			GD.Print($"[CombatManager] 战斗奖励 {goldReward} 金币（当前总金币 {gm.RunGold}）");
		}
	}

	/// <summary>
	/// 从卡牌列表中移除所有状态牌（Status 类型）。
	/// 战斗结束时清理手牌、抽牌堆、弃牌堆中的状态牌。
	/// </summary>
	public static void RemoveStatusCardsFromList(IList<Card.Card> cards)
	{
		for (int i = cards.Count - 1; i >= 0; i--)
		{
			if (cards[i].Type == CardType.Status)
				cards.RemoveAt(i);
		}
	}
}
