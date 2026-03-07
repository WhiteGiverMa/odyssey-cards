using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Map;

namespace OdysseyCards.Character;

public class Deck
{
    public const int MaxCards = 30;

    public List<Resource> Cards { get; private set; } = new();

    public int CardCount => Cards.Count;

    public bool CanAddCard()
    {
        return CardCount < MaxCards;
    }

    public bool AddCardWithCheck(Resource card)
    {
        if (!CanAddCard())
        {
            return false;
        }

        Cards.Add(card);
        return true;
    }

    public bool IsOverLimit()
    {
        return CardCount > MaxCards;
    }

    public void AddUnit(UnitData unit)
    {
        Cards.Add(unit);
    }

    public void AddOrder(OrderData order)
    {
        Cards.Add(order);
    }

    public void RemoveCard(Resource card)
    {
        Cards.Remove(card);
    }

    public void Initialize(List<Resource> initialCards)
    {
        Cards = initialCards;
    }

    public List<Card.Card> CreateDrawPile()
    {
        List<Card.Card> pile = new();
        foreach (Resource cardData in Cards)
        {
            if (cardData is UnitData unitData)
            {
                pile.Add(Unit.Create(unitData));
            }
            else if (cardData is OrderData orderData)
            {
                pile.Add(Order.Create(orderData));
            }
        }
        return pile;
    }
}

public class CommanderCore
{
    public const int NaturalMaxEnergyCap = 12;
    public const int HardMaxEnergyCap = 24;

    public Headquarters HQ { get; private set; }
    public Deck Deck { get; internal set; }
    public List<Card.Card> Hand { get; } = new();
    public List<Card.Card> DrawPile { get; } = new();
    public List<Card.Card> DiscardPile { get; } = new();

    public int CurrentEnergy { get; private set; }
    public int MaxEnergy { get; private set; } = 3;
    public int MaxHandSize { get; set; } = 9;
    public int FatigueCount { get; private set; } = 0;

    public event Action<int, int> OnEnergyChanged;
    public event Action OnHandChanged;
    public event Action OnDrawPileChanged;
    public event Action OnDiscardPileChanged;

    public CommanderCore()
    {
        Deck = new Deck();
        CurrentEnergy = MaxEnergy;
    }

    public void InitializeHQ(int maxHealth, int currentHealth = -1, int deploymentNodeId = -1)
    {
        HQ = new Headquarters(NodeOwner.Player, maxHealth, deploymentNodeId);
        if (currentHealth >= 0)
        {
            HQ.SetHealth(currentHealth, maxHealth);
        }
    }

    public void SetHQ(Headquarters hq)
    {
        HQ = hq;
    }

    public void SpendEnergy(int amount)
    {
        CurrentEnergy = Math.Max(0, CurrentEnergy - amount);
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
    }

    public void GainEnergy(int amount)
    {
        CurrentEnergy = Math.Min(MaxEnergy + amount, CurrentEnergy + amount);
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
    }

    public void ResetEnergy()
    {
        CurrentEnergy = MaxEnergy;
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
    }

    public void SetEnergy(int current, int max)
    {
        MaxEnergy = max;
        CurrentEnergy = current;
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
    }

    public void IncreaseMaxEnergy(int amount)
    {
        MaxEnergy = Mathf.Min(MaxEnergy + amount, HardMaxEnergyCap);
    }

    public void SetMaxEnergy(int max)
    {
        MaxEnergy = max;
    }

    public void SetCurrentEnergy(int current)
    {
        CurrentEnergy = current;
    }

    public void DrawCards(int count)
    {
        int cardsToDraw = Mathf.Min(count, MaxHandSize - Hand.Count);

        for (int i = 0; i < cardsToDraw; i++)
        {
            if (DrawPile.Count == 0)
            {
                FatigueCount++;
                HQ?.TakeDamage(FatigueCount);
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

    public bool CanSpendEnergy(int amount)
    {
        return CurrentEnergy >= amount;
    }

    public void StartTurn()
    {
        if (MaxEnergy < NaturalMaxEnergyCap)
        {
            MaxEnergy++;
        }
        CurrentEnergy = MaxEnergy;
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
    }

    public void EndTurn()
    {
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

    public void SetupDrawPile()
    {
        DrawPile.Clear();
        foreach (Card.Card card in Deck.CreateDrawPile())
        {
            DrawPile.Add(card);
        }
        ShuffleDrawPile();
        OnDrawPileChanged?.Invoke();
    }
}
