using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.Roguelike;

/// <summary>
/// 战斗后事件类型枚举。
/// </summary>
public enum EventType
{
	/// <summary>
	/// 卡牌奖励：从多个卡牌选项中选择一张加入牌堆。
	/// </summary>
	CardReward,

	/// <summary>
	/// 治疗事件：恢复英雄生命值（原型阶段暂未实现）。
	/// </summary>
	Heal
}

/// <summary>
/// 战斗后事件选择器。
/// 负责生成战后事件类型和卡牌奖励选项。
/// 纯 C# 逻辑类，不继承 Node，可直接在任何上下文中使用。
/// 卡牌数据源从 <see cref="GameManager"/> 获取，不再硬编码。
/// </summary>
public sealed class EventSelector : IDisposable
{
	/// <summary>
	/// Godot 随机数生成器，用于事件选择和奖励抽选。
	/// </summary>
	private readonly RandomNumberGenerator _random = new();

	/// <summary>
	/// 创建事件选择器并随机化种子。
	/// </summary>
	public EventSelector()
	{
		_random.Randomize();
	}

	/// <summary>
	/// 获取一个随机事件类型。
	/// 原型阶段始终返回 CardReward；
	/// 后续将支持基于权重的随机选择。
	/// </summary>
	/// <returns>当前仅返回 <see cref="EventType.CardReward"/>。</returns>
	public EventType GetRandomEvent()
	{
		// 原型阶段：固定返回卡牌奖励
		return EventType.CardReward;

		// 未来扩展：基于权重的随机选择
		// var roll = _random.RandfRange(0f, 1f);
		// if (roll < 0.7f) return EventType.CardReward;
		// return EventType.Heal;
	}

	/// <summary>
	/// 从 <see cref="GameManager"/> 的奖励资格卡牌池中生成不重复的奖励捆绑包。
	/// 每个捆绑包含一张卡牌及其最大可发放张数（由稀有度决定）。
	/// </summary>
	/// <param name="count">需要生成的捆绑包数量，默认为 3。</param>
	/// <returns>不重复的 (卡牌数据, 最多可发放张数) 列表。</returns>
	/// <exception cref="ArgumentException">当 <paramref name="count"/> 超过可用奖励卡牌数时抛出。</exception>
	public List<(CardData Card, int CopyCount)> GenerateRewardBundles(int count = 3)
	{
		var eligibleCards = GameManager.Instance?.GetRewardEligibleCards();
		if (eligibleCards == null || eligibleCards.Count < count)
		{
			throw new ArgumentException(
				$"可用奖励卡牌不足 ({eligibleCards?.Count ?? 0} < {count})",
				nameof(count));
		}

		// Fisher-Yates 洗牌后取前 count 个，保证不重复
		var shuffled = eligibleCards
			.OrderBy(_ => _random.Randi())
			.Take(count)
			.ToList();

		return shuffled.Select(card => (card, card.Rarity.GetMaxRewardCopies())).ToList();
	}

	/// <summary>
	/// [已废弃] 将玩家选择的奖励卡牌加入牌堆。
	/// 请改用 <see cref="GameManager.AddCardToDeckInCombat"/>。
	/// </summary>
	/// <param name="chosen">玩家选择的卡牌数据。</param>
	[Obsolete("请改用 GameManager.Instance.AddCardToDeckInCombat(chosen)")]
	public void ApplyReward(CardData chosen)
	{
		ArgumentNullException.ThrowIfNull(chosen);
		GameManager.Instance?.AddCardToDeckInCombat(chosen);
	}

	/// <summary>
	/// 释放随机数生成器资源。
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (!disposing)
			return;
		_random.Dispose();
	}
}
