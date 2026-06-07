using System;
using System.Collections.Generic;
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

    public IReadOnlyList<OdysseyCards.Card.Card> DrawCards(int count)
    {
        int cardsToDraw = Mathf.Min(count, MaxHandSize - Hand.Count);
        var drawn = new List<OdysseyCards.Card.Card>();

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
                drawn.Add(card);
            }
        }

        if (drawn.Count > 0)
        {
            OnHandChanged?.Invoke();
            OnDrawPileChanged?.Invoke();
        }

        return drawn;
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

    /// <summary>
    /// 直接将一张卡牌加入手牌（外部来源：发现、/token 命令等）。
    /// 与 DrawCards 不同，不经过抽牌堆，直接加入并触发 UI 刷新。
    /// </summary>
    /// <param name="card">要加入的卡牌实例</param>
    public void AddToHand(OdysseyCards.Card.Card card)
    {
        if (card == null) return;

        if (Hand.Count >= MaxHandSize)
        {
            GD.Print($"[CombatDeckState] 手牌已满（{MaxHandSize}张），「{card.CardName}」被烧毁！");
            return;
        }

        Hand.Add(card);
        OnHandChanged?.Invoke();
        GD.Print($"[CombatDeckState] 将「{card.CardName}」加入手牌（共 {Hand.Count} 张）");
    }

    /// <summary>
    /// 将手牌中的卡牌放回抽牌堆底部。用于轮战等机制。
    /// </summary>
    /// <param name="card">要放回的卡牌实例</param>
    public void ReturnToDrawPile(OdysseyCards.Card.Card card)
    {
        if (!Hand.Contains(card))
            return;

        Hand.Remove(card);

        DrawPile.Add(card); // 底部插入（末尾）

        GD.Print($"[CombatDeckState] 将「{card.CardName}」放回抽牌堆底部（共 {DrawPile.Count} 张）");
        OnHandChanged?.Invoke();
        OnDrawPileChanged?.Invoke();
    }

    /// <summary>
    /// 直接将一张卡牌加入抽牌堆底部（不从手牌移除，用于随从死亡等外部来源）。
    /// </summary>
    /// <param name="card">要加入的卡牌实例</param>
    public void AddToDrawPileBottom(OdysseyCards.Card.Card card)
    {
        DrawPile.Add(card);

        GD.Print($"[CombatDeckState] 将「{card.CardName}」加入抽牌堆底部（共 {DrawPile.Count} 张）");
        OnDrawPileChanged?.Invoke();
    }

    /// <summary>
    /// 直接将一张卡牌加入弃牌堆（不从手牌移除，用于随从死亡等外部来源）。
    /// </summary>
    /// <param name="card">要加入的卡牌实例</param>
    public void AddToDiscardPile(OdysseyCards.Card.Card card)
    {
        DiscardPile.Add(card);

        GD.Print($"[CombatDeckState] 将「{card.CardName}」加入弃牌堆（共 {DiscardPile.Count} 张）");
        OnDiscardPileChanged?.Invoke();
    }

    /// <summary>
    /// 将弃牌堆中的一张牌移回手牌。
    /// </summary>
    /// <returns>移动成功返回 true。</returns>
    public bool MoveFromDiscardToHand(OdysseyCards.Card.Card card)
    {
        if (!DiscardPile.Remove(card))
            return false;

        if (Hand.Count >= MaxHandSize)
        {
            // 手牌已满时回到弃牌堆
            DiscardPile.Add(card);
            GD.Print($"[CombatDeckState] 手牌已满（{MaxHandSize}张），「{card.CardName}」回到弃牌堆");
            return false;
        }

        Hand.Add(card);
        OnDiscardPileChanged?.Invoke();
        OnHandChanged?.Invoke();
        GD.Print($"[CombatDeckState] 将「{card.CardName}」从弃牌堆加入手牌（手牌 {Hand.Count} 张，弃牌堆 {DiscardPile.Count} 张）");
        return true;
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
    /// 将一张卡牌插入抽牌堆底部。
    /// 用于领域效果等外部来源向牌库添加卡牌。
    /// </summary>
    /// <param name="card">要插入的卡牌实例</param>
    public void InsertCardToDrawPile(OdysseyCards.Card.Card card)
    {
        DrawPile.Add(card); // 底部插入（末尾）

        GD.Print($"[CombatDeckState] 将「{card.CardName}」插入抽牌堆底部（共 {DrawPile.Count} 张）");
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
