using System.Collections.Generic;
using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.Character;

/// <summary>
/// 牌堆定义。存储玩家的卡牌数据资源列表。
/// 最大30张牌。
/// </summary>
public class Deck
{
    public const int MaxCards = 30;

    /// <summary>
    /// 卡牌数据列表。
    /// </summary>
    public List<CardData> Cards { get; private set; } = new();

    public int CardCount => Cards.Count;

    public bool CanAddCard()
    {
        return CardCount < MaxCards;
    }

    /// <summary>
    /// 添加卡牌（带上限检查）。
    /// </summary>
    public bool AddCardWithCheck(CardData card)
    {
        if (!CanAddCard())
            return false;

        Cards.Add(card);
        return true;
    }

    /// <summary>
    /// 添加卡牌（无检查）。
    /// </summary>
    public void AddCard(CardData card)
    {
        Cards.Add(card);
    }

    /// <summary>
    /// 移除卡牌。
    /// </summary>
    public void RemoveCard(CardData card)
    {
        Cards.Remove(card);
    }

    /// <summary>
    /// 是否超过上限。
    /// </summary>
    public bool IsOverLimit()
    {
        return CardCount > MaxCards;
    }

    /// <summary>
    /// 用初始卡牌列表初始化牌堆。
    /// </summary>
    public void Initialize(List<CardData> initialCards)
    {
        Cards = new List<CardData>(initialCards);
    }
}
