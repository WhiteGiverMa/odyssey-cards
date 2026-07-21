using System.Collections.Generic;
using OdysseyCards.Core;

namespace OdysseyCards.Character;

/// <summary>
/// 牌堆定义。存储玩家的卡牌数据资源列表。
/// 构筑时上限 20 张，战斗中通过奖励可突破上限。
/// </summary>
public class Deck
{
	/// <summary>
	/// 构筑时最小卡牌数。
	/// </summary>
	public const int MinCards = 10;

	/// <summary>
	/// 构筑时最大卡牌数。
	/// </summary>
	public const int MaxDeckSize = 20;

	/// <summary>
	/// 战斗中加入卡牌的硬上限（足够大，基本无限制）。
	/// </summary>
	public const int CombatMaxCards = 999;

	/// <summary>
	/// 牌组名称。
	/// </summary>
	public string Name { get; set; } = "";

	/// <summary>
	/// 卡牌数据列表。
	/// </summary>
	public List<CardData> Cards { get; private set; } = new();

	public int CardCount => Cards.Count;

	/// <summary>
	/// 创建牌堆的深拷贝（共享 CardData 引用）。
	/// </summary>
	public Deck Clone()
	{
		var clone = new Deck
		{
			Name = this.Name,
			Cards = new List<CardData>(this.Cards),
		};
		return clone;
	}

	/// <summary>
	/// 构筑时能否添加卡牌（上限 20）。
	/// </summary>
	public bool CanAddCard()
	{
		return CardCount < MaxDeckSize;
	}

	/// <summary>
	/// 战斗中能否添加卡牌（上限 999）。
	/// </summary>
	public bool CanAddCardInCombat()
	{
		return CardCount < CombatMaxCards;
	}

	/// <summary>
	/// 构筑时添加卡牌（带上限 20 检查）。
	/// </summary>
	public bool AddCardWithCheck(CardData card)
	{
		if (!CanAddCard())
			return false;

		Cards.Add(card);
		return true;
	}

	/// <summary>
	/// 战斗中通过奖励添加卡牌（带上限 999 检查）。
	/// </summary>
	public bool AddCardInCombat(CardData card)
	{
		if (!CanAddCardInCombat())
			return false;

		Cards.Add(card);
		return true;
	}

	/// <summary>
	/// 添加卡牌（无检查，直接追加）。
	/// </summary>
	public void AddCard(CardData card)
	{
		Cards.Add(card);
	}

	/// <summary>
	/// 移除卡牌（移除首个匹配引用）。
	/// </summary>
	public void RemoveCard(CardData card)
	{
		Cards.Remove(card);
	}

	/// <summary>
	/// 是否超过构筑上限。
	/// </summary>
	public bool IsOverLimit()
	{
		return CardCount > MaxDeckSize;
	}

	/// <summary>
	/// 是否达到构筑最小卡牌数。
	/// </summary>
	public bool MeetsMinimum()
	{
		return CardCount >= MinCards;
	}

	/// <summary>
	/// 用初始卡牌列表初始化牌堆。
	/// </summary>
	public void Initialize(List<CardData> initialCards)
	{
		Cards = new List<CardData>(initialCards);
	}
}
