using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Core;
using OdysseyCards.Map;

namespace OdysseyCards.Character;

public class CommanderCore
{
    public const int NaturalMaxEnergyCap = 12;
    public const int HardMaxEnergyCap = 24;

    public Headquarters HQ { get; private set; }
    public Deck Deck { get; internal set; }
    public CombatDeckState CombatDeckState { get; } = new();

    public List<Card.Card> Hand => CombatDeckState.Hand;
    public List<Card.Card> DrawPile => CombatDeckState.DrawPile;
    public List<Card.Card> DiscardPile => CombatDeckState.DiscardPile;

    public int CurrentEnergy { get; private set; }
    public int MaxEnergy { get; private set; } = 3;
    public int MaxHandSize { get => CombatDeckState.MaxHandSize; set => CombatDeckState.MaxHandSize = value; }
    public int FatigueCount => CombatDeckState.FatigueCount;

    public event Action<int, int> OnEnergyChanged;
    public event Action OnHandChanged
    {
        add => CombatDeckState.OnHandChanged += value;
        remove => CombatDeckState.OnHandChanged -= value;
    }
    public event Action OnDrawPileChanged
    {
        add => CombatDeckState.OnDrawPileChanged += value;
        remove => CombatDeckState.OnDrawPileChanged -= value;
    }
    public event Action OnDiscardPileChanged
    {
        add => CombatDeckState.OnDiscardPileChanged += value;
        remove => CombatDeckState.OnDiscardPileChanged -= value;
    }

    public CommanderCore()
    {
        Deck = new Deck();
        CurrentEnergy = MaxEnergy;
    }

    public void InitializeHQ(int maxHealth, int currentHealth = -1, int deploymentNodeId = -1)
    {
        HQ = new Headquarters(NodeOwner.Player, maxHealth, deploymentNodeId);
        CombatDeckState.SetHQ(HQ);
        if (currentHealth >= 0)
        {
            HQ.SetHealth(currentHealth, maxHealth);
        }
    }

    public void SetHQ(Headquarters hq)
    {
        HQ = hq;
        CombatDeckState.SetHQ(hq);
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
        CombatDeckState.DrawCards(count);
    }

    public void DiscardCard(Card.Card card)
    {
        CombatDeckState.DiscardCard(card);
    }

    public void RemoveFromHand(Card.Card card)
    {
        CombatDeckState.RemoveFromHand(card);
    }

    public void ReturnToDrawPile(Card.Card card)
    {
        CombatDeckState.ReturnToDrawPile(card);
    }

    public void ShuffleDrawPile()
    {
        CombatDeckState.ShuffleDrawPile();
    }

    public void DiscardHand()
    {
        CombatDeckState.DiscardHand();
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
        CombatDeckState.ResetFatigue();
    }

    public void ClearPiles()
    {
        CombatDeckState.ClearPiles();
    }

    public void SetupDrawPile()
    {
        List<Card.Card> cards = Card.CardRuntimeFactory.CreateDrawPile(Deck);
        CombatDeckState.SetupDrawPile(cards);
    }
}
