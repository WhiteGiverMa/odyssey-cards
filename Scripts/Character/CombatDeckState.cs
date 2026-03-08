using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Map;

namespace OdysseyCards.Character;

public class CombatDeckState
{
    public List<Card.Card> Hand { get; } = new();
    public List<Card.Card> DrawPile { get; } = new();
    public List<Card.Card> DiscardPile { get; } = new();
    public int FatigueCount { get; private set; } = 0;
    public int MaxHandSize { get; set; } = 9;

    public event Action OnHandChanged;
    public event Action OnDrawPileChanged;
    public event Action OnDiscardPileChanged;

    private Headquarters _hq;

    public void SetHQ(Headquarters hq)
    {
        _hq = hq;
    }

    public void DrawCards(int count)
    {
        int cardsToDraw = Mathf.Min(count, MaxHandSize - Hand.Count);

        for (int i = 0; i < cardsToDraw; i++)
        {
            if (DrawPile.Count == 0)
            {
                FatigueCount++;
                _hq?.TakeDamage(FatigueCount);
                continue;
            }

            if (DrawPile.Count > 0)
            {
                Card.Card card = DrawPile[0];
                DrawPile.RemoveAt(0);
                Hand.Add(card);
            }
        }

        OnHandChanged?.Invoke();
        OnDrawPileChanged?.Invoke();
    }

    public void DiscardCard(Card.Card card)
    {
        if (!Hand.Contains(card))
        {
            return;
        }

        Hand.Remove(card);
        DiscardPile.Add(card);
        OnHandChanged?.Invoke();
        OnDiscardPileChanged?.Invoke();
    }

    public void RemoveFromHand(Card.Card card)
    {
        if (!Hand.Contains(card))
        {
            return;
        }

        Hand.Remove(card);
        OnHandChanged?.Invoke();
    }

    public void ReturnToDrawPile(Card.Card card)
    {
        if (!Hand.Contains(card))
        {
            return;
        }

        Hand.Remove(card);

        RandomNumberGenerator random = new();
        random.Randomize();
        int insertIndex = random.RandiRange(0, DrawPile.Count);
        DrawPile.Insert(insertIndex, card);

        OnHandChanged?.Invoke();
        OnDrawPileChanged?.Invoke();
    }

    public void ShuffleDrawPile()
    {
        RandomNumberGenerator random = new();
        random.Randomize();

        for (int i = DrawPile.Count - 1; i > 0; i--)
        {
            int j = random.RandiRange(0, i);
            (DrawPile[i], DrawPile[j]) = (DrawPile[j], DrawPile[i]);
        }

        OnDrawPileChanged?.Invoke();
    }

    public void DiscardHand()
    {
        while (Hand.Count > 0)
        {
            Card.Card card = Hand[0];
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

    public void SetupDrawPile(List<Card.Card> cards)
    {
        DrawPile.Clear();
        foreach (Card.Card card in cards)
        {
            DrawPile.Add(card);
        }
        ShuffleDrawPile();
        OnDrawPileChanged?.Invoke();
    }
}
