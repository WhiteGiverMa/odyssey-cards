using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OdysseyCards.Character;

/// <summary>
/// 战斗中的牌堆状态管理。
/// 管理手牌、抽牌堆、弃牌堆和疲劳机制。
/// 已移除对 Map/Headquarters 的依赖，疲劳伤害通过回调委托。
/// </summary>
public class CombatDeckState
{
    public List<OdysseyCards.Card.Card> Hand { get; } = new();
    public List<OdysseyCards.Card.Card> DrawPile { get; } = new();
    public List<OdysseyCards.Card.Card> DiscardPile { get; } = new();
    public int FatigueCount { get; private set; } = 0;
    public int MaxHandSize { get; set; } = 9;

    public event Action OnHandChanged;
    public event Action OnDrawPileChanged;
    public event Action OnDiscardPileChanged;

    /// <summary>
    /// 疲劳伤害回调。当抽牌堆空时，调用此委托造成疲劳伤害。
    /// 参数为疲劳伤害值（FatigueCount）。
    /// </summary>
    private Action<int> _onFatigueDamage;

    public void SetFatigueCallback(Action<int> callback)
    {
        _onFatigueDamage = callback;
    }

    public void DrawCards(int count)
    {
        int cardsToDraw = Mathf.Min(count, MaxHandSize - Hand.Count);

        for (int i = 0; i < cardsToDraw; i++)
        {
            if (DrawPile.Count == 0)
            {
                FatigueCount++;
                _onFatigueDamage?.Invoke(FatigueCount);
                continue;
            }

            if (DrawPile.Count > 0)
            {
                var card = DrawPile[0];
                DrawPile.RemoveAt(0);
                Hand.Add(card);
            }
        }

        OnHandChanged?.Invoke();
        OnDrawPileChanged?.Invoke();
    }

    public void DiscardCard(OdysseyCards.Card.Card card)
    {
        if (!Hand.Contains(card))
            return;

        Hand.Remove(card);
        DiscardPile.Add(card);
        OnHandChanged?.Invoke();
        OnDiscardPileChanged?.Invoke();
    }

    public void RemoveFromHand(OdysseyCards.Card.Card card)
    {
        if (!Hand.Contains(card))
            return;

        Hand.Remove(card);
        OnHandChanged?.Invoke();
    }

    public void ReturnToDrawPile(OdysseyCards.Card.Card card)
    {
        if (!Hand.Contains(card))
            return;

        Hand.Remove(card);

        var random = new RandomNumberGenerator();
        random.Randomize();
        int insertIndex = random.RandiRange(0, DrawPile.Count);
        DrawPile.Insert(insertIndex, card);

        OnHandChanged?.Invoke();
        OnDrawPileChanged?.Invoke();
    }

    public void ShuffleDrawPile()
    {
        var random = new RandomNumberGenerator();
        random.Randomize();

        for (int i = DrawPile.Count - 1; i > 0; i--)
        {
            int j = random.RandiRange(0, i);
            (DrawPile[i], DrawPile[j]) = (DrawPile[j], DrawPile[i]);
        }

        OnDrawPileChanged?.Invoke();
    }

    /// <summary>
    /// 将一张卡牌插入抽牌堆的随机位置。
    /// 用于领域效果等外部来源向牌库添加卡牌。
    /// </summary>
    /// <param name="card">要插入的卡牌实例</param>
    public void InsertCardToDrawPile(OdysseyCards.Card.Card card)
    {
        var random = new RandomNumberGenerator();
        random.Randomize();
        int insertIndex = random.RandiRange(0, DrawPile.Count);
        DrawPile.Insert(insertIndex, card);

        GD.Print($"[CombatDeckState] 将「{card.CardName}」插入抽牌堆位置 {insertIndex}（共 {DrawPile.Count} 张）");
        OnDrawPileChanged?.Invoke();
    }

    public void DiscardHand()
    {
        while (Hand.Count > 0)
        {
            var card = Hand[0];
            Hand.RemoveAt(0);
            DiscardPile.Add(card);
        }

        OnHandChanged?.Invoke();
        OnDiscardPileChanged?.Invoke();
    }

    public void ResetFatigue()
    {
        FatigueCount = 0;
    }

    public void ClearPiles()
    {
        Hand.Clear();
        DrawPile.Clear();
        DiscardPile.Clear();
        OnHandChanged?.Invoke();
        OnDrawPileChanged?.Invoke();
        OnDiscardPileChanged?.Invoke();
    }

    public void SetupDrawPile(List<OdysseyCards.Card.Card> cards)
    {
        DrawPile.Clear();
        foreach (var card in cards)
            DrawPile.Add(card);
        ShuffleDrawPile();
        OnDrawPileChanged?.Invoke();
    }
}
