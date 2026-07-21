using System.Collections.Generic;
using Godot;
using OdysseyCards.Card;

namespace OdysseyCards.Combat;

/// <summary>
/// 死亡处理器——管理随从死亡检测、亡语触发、牌堆回收。
/// 从 CombatManager 拆出为独立类，解除死亡处理与回合流转之间的耦合。
/// </summary>
internal sealed class DeathHandler
{
	private readonly Board _board;
	private readonly Hero _playerHero;
	private readonly CardEffectDispatcher _effectDispatcher;
	private readonly AttackTracker _attackTracker;

	public DeathHandler(Board board, Hero playerHero, CardEffectDispatcher effectDispatcher, AttackTracker attackTracker)
	{
		_board = board;
		_playerHero = playerHero;
		_effectDispatcher = effectDispatcher;
		_attackTracker = attackTracker;

		// 亡语驱动：随从死亡时自动触发亡语（替换不触发），无需在各处手动调用
		_board.OnMinionDied += TriggerDeathrattle;

		// 牌堆回收驱动：随从死亡时自动进入弃牌堆或返回抽牌堆（轮战），无需在各处手动调用
		_board.OnMinionDied += HandleMinionDeathPile;
	}

	/// <summary>
	/// 遍历战场双方所有随从，移除已死亡随从并触发亡语效果。
	/// 先收集再处理以避免迭代中修改集合。
	/// 死亡随从从槽位直接收集（GetPlayerMinions 会过滤死亡随从，不能用）。
	/// </summary>
	internal void CheckDeaths()
	{
		var deadMinions = new List<Minion>();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			if (_board.PlayerSlots[i] is Minion m && m.IsDead)
				deadMinions.Add(m);
			if (_board.EnemySlots[i] is Minion em && em.IsDead)
				deadMinions.Add(em);
		}

		foreach (var minion in deadMinions)
		{
			GD.Print($"[CombatManager] ☠ {minion.CardName}（{minion.IsPlayerSide switch { true => "玩家方", false => "敌方" }}）死亡");

			// Board.RemoveMinion 自动触发：
			//   - TriggerDeathrattle（亡语）
			//   - HandleMinionDeathPile（轮战回收 / 进入弃牌堆）
			//   - NotifyCombatStateChanged（UI 刷新）
			_board.RemoveMinion(minion);

			// 清理攻击追踪
			_attackTracker.Remove(minion);
		}
	}

	/// <summary>
	/// 触发随从的亡语效果。
	/// 原型阶段仅输出日志；后续可扩展为完整效果解析。
	/// </summary>
	/// <param name="minion">已死亡的随从</param>
	private void TriggerDeathrattle(Minion minion)
	{
		if (!minion.HasDeathrattle)
			return;

		GD.Print($"[CombatManager]   ◆ 触发亡语：{minion.CardName}");
		foreach (var effect in minion.DeathrattleEffects)
		{
			GD.Print($"[CombatManager]     亡语效果：{effect.GetDescription()}");
			_effectDispatcher.ExecuteEffect(effect, minion, minion);
		}
	}

	/// <summary>
	/// 处理随从死亡后的牌堆流转（订阅 <see cref="Board.OnMinionRemoved"/> 事件自动触发）。
	/// 玩家方随从：轮战→返回抽牌堆底部，否则→进入弃牌堆。
	/// 敌方随从不参与牌堆流转。
	/// </summary>
	/// <param name="minion">已从棋盘移除的随从</param>
	private void HandleMinionDeathPile(Minion minion)
	{
		if (!minion.IsPlayerSide)
			return;

		var cardFromMinion = minion.ToRuntimeCard();
		if (minion.HasRecycle)
		{
			_playerHero.AddToDrawPileBottom(cardFromMinion);
			GD.Print($"[CombatManager]   ♻ {minion.CardName}（轮战）返回抽牌堆底部");
		}
		else
		{
			_playerHero.AddToDiscardPile(cardFromMinion);
			GD.Print($"[CombatManager]   🗑 {minion.CardName} 进入弃牌堆");
		}
	}
}
