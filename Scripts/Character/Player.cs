using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.Character;

public partial class Player : Character
{
    public Deck Deck { get; private set; }
    public List<Card.Card> Hand { get; private set; } = new();
    public List<Card.Card> DrawPile { get; private set; } = new();
    public List<Card.Card> DiscardPile { get; private set; } = new();
    public List<Card.Card> ExhaustPile { get; private set; } = new();
    
    public int Gold { get; private set; } = 99;
    public int MaxHandSize { get; set; } = 5;

    public event Action OnHandChanged;
    public event Action OnDrawPileChanged;
    public event Action OnDiscardPileChanged;

    public void Initialize(Deck startingDeck)
    {
        Deck = startingDeck;
        DrawPile = Deck.CreateDrawPile();
        ShuffleDrawPile();
    }

    public void DrawCards(int count)
    {
        int cardsToDraw = Mathf.Min(count, MaxHandSize - Hand.Count);

        for (int i = 0; i < cardsToDraw; i++)
        {
            if (DrawPile.Count == 0)
            {
                if (DiscardPile.Count == 0)
                    break;
                
                ReshuffleDiscardPile();
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

    public void DiscardCard(Card.Card card)
    {
        if (!Hand.Contains(card))
            return;

        Hand.Remove(card);
        DiscardPile.Add(card);
        
        OnHandChanged?.Invoke();
        OnDiscardPileChanged?.Invoke();
    }

    public void ExhaustCard(Card.Card card)
    {
        if (!Hand.Contains(card))
            return;

        Hand.Remove(card);
        ExhaustPile.Add(card);
        
        OnHandChanged?.Invoke();
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

    public void ShuffleDrawPile()
    {
        var random = new RandomNumberGenerator();
        random.Randomize();

        for (int i = DrawPile.Count - 1; i > 0; i--)
        {
            int j = random.RandiRange(0, i);
            (DrawPile[i], DrawPile[j]) = (DrawPile[j], DrawPile[i]);
        }
    }

    private void ReshuffleDiscardPile()
    {
        DrawPile.AddRange(DiscardPile);
        DiscardPile.Clear();
        ShuffleDrawPile();
        
        OnDrawPileChanged?.Invoke();
        OnDiscardPileChanged?.Invoke();
    }

    public void AddGold(int amount)
    {
        Gold += amount;
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount)
            return false;

        Gold -= amount;
        return true;
    }
}

public class Deck
{
    public List<CardData> Cards { get; private set; } = new();

    public void AddCard(CardData card)
    {
        Cards.Add(card);
    }

    public void RemoveCard(CardData card)
    {
        Cards.Remove(card);
    }

    public List<Card.Card> CreateDrawPile()
    {
        var pile = new List<Card.Card>();
        foreach (var cardData in Cards)
        {
            pile.Add(Card.Card.Create(cardData));
        }
        return pile;
    }
}
