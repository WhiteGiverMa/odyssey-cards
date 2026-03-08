using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Map;

namespace OdysseyCards.Character;

public partial class Player : Node, ICommander
{
    public static Player Instance { get; private set; }

    private readonly CommanderCore _core = new();

    public int CommanderId => 0;
    public string CharacterName { get; set; } = "Player";
    public Headquarters HQ => _core.HQ;
    public bool IsDefeated => HQ?.IsDestroyed ?? true;

    public int CurrentEnergy => _core.CurrentEnergy;
    public int MaxEnergy => _core.MaxEnergy;
    public event Action<int, int> OnEnergyChanged
    {
        add => _core.OnEnergyChanged += value;
        remove => _core.OnEnergyChanged -= value;
    }

    public Deck Deck => _core.Deck;
    public IReadOnlyList<Card.Card> Hand => _core.Hand;
    public IReadOnlyList<Card.Card> DrawPile => _core.DrawPile;
    public IReadOnlyList<Card.Card> DiscardPile => _core.DiscardPile;
    public int MaxHandSize { get => _core.MaxHandSize; set => _core.MaxHandSize = value; }
    public int FatigueCount => _core.FatigueCount;

    public event Action OnHandChanged
    {
        add => _core.OnHandChanged += value;
        remove => _core.OnHandChanged -= value;
    }
    public event Action OnDrawPileChanged
    {
        add => _core.OnDrawPileChanged += value;
        remove => _core.OnDrawPileChanged -= value;
    }
    public event Action OnDiscardPileChanged
    {
        add => _core.OnDiscardPileChanged += value;
        remove => _core.OnDiscardPileChanged -= value;
    }

    public int Gold { get; private set; } = 0;
    public int CurrentFloor { get; set; } = 0;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }
        Instance = this;
        AddToGroup("Player");
    }

    public void Initialize(int maxHealth, int initialGold = 0)
    {
        InitializeHQ(maxHealth);
        Gold = initialGold;
    }

    public void Initialize(Deck deck)
    {
        _core.Deck = deck;
    }

    public void InitializeHQ(int maxHealth, int currentHealth = -1, int deploymentNodeId = -1)
    {
        _core.InitializeHQ(maxHealth, currentHealth, deploymentNodeId);
    }

    public void AddGold(int amount)
    {
        Gold += amount;
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount)
        {
            return false;
        }
        Gold -= amount;
        return true;
    }

    public void SetupDrawPile()
    {
        _core.SetupDrawPile();
    }

    public void ResetForCombat()
    {
        _core.ClearPiles();
        _core.ResetFatigue();
        SetupDrawPile();
    }

    public void AddCardToDeck(Resource cardData)
    {
        if (cardData is UnitData unitData)
        {
            Deck.AddUnit(unitData);
        }
        else if (cardData is OrderData orderData)
        {
            Deck.AddOrder(orderData);
        }
    }

    public void ExhaustCard(Card.Card card)
    {
        RemoveFromHand(card);
    }

    public void PurgeCard(Card.Card card)
    {
        RemoveFromHand(card);
    }

    public void RestoreHQHealth(int currentHealth, int maxHealth)
    {
        HQ?.SetHealth(currentHealth, maxHealth);
    }

    public void SetMaxEnergy(int max)
    {
        _core.SetMaxEnergy(max);
    }

    public void SetCurrentEnergy(int current)
    {
        _core.SetCurrentEnergy(current);
    }

    public void SpendEnergy(int amount) => _core.SpendEnergy(amount);
    public void GainEnergy(int amount) => _core.GainEnergy(amount);
    public void ResetEnergy() => _core.ResetEnergy();
    public void SetEnergy(int current, int max) => _core.SetEnergy(current, max);
    public void IncreaseMaxEnergy(int amount) => _core.IncreaseMaxEnergy(amount);
    public void DrawCards(int count) => _core.DrawCards(count);
    public void DiscardCard(Card.Card card) => _core.DiscardCard(card);
    public void RemoveFromHand(Card.Card card) => _core.RemoveFromHand(card);
    public void ReturnToDrawPile(Card.Card card) => _core.ReturnToDrawPile(card);
    public void ShuffleDrawPile() => _core.ShuffleDrawPile();
    public void DiscardHand() => _core.DiscardHand();
    public bool CanSpendEnergy(int amount) => _core.CanSpendEnergy(amount);
    public void StartTurn() => _core.StartTurn();
    public void EndTurn() => _core.EndTurn();
}
