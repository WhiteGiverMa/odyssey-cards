using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Character;

/// <summary>
/// 玩家角色（Godot Node）。
/// 包装 CommanderCore，提供运行时角色管理。
/// </summary>
public partial class Player : Node, ICommander
{
    public static Player Instance { get; private set; }

    private readonly CommanderCore _core = new();

    /// <summary>
    /// 英雄技能。由 GameManager 在创建玩家时设置，
    /// CombatManager 在初始化时复制到 PlayerHero。
    /// </summary>
    public IHeroPower? HeroPower { get; set; }

    public int CommanderId => 0;
    public string CharacterName { get; set; } = "Player";

    public bool IsDefeated => _core.IsDead;

    public int CurrentHealth => _core.CurrentHealth;
    public int MaxHealth => _core.MaxHealth;

    public int CurrentMana => _core.CurrentMana;
    public int MaxMana => _core.MaxMana;

    public event Action<int, int> OnManaChanged
    {
        add => _core.OnManaChanged += value;
        remove => _core.OnManaChanged -= value;
    }

    public Deck Deck => _core.Deck;
    public IReadOnlyList<OdysseyCards.Card.Card> Hand => _core.Hand;
    public IReadOnlyList<OdysseyCards.Card.Card> DrawPile => _core.DrawPile;
    public IReadOnlyList<OdysseyCards.Card.Card> DiscardPile => _core.DiscardPile;
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

    public void InitializeHealth(int maxHealth, int currentHealth = -1)
    {
        _core.InitializeHealth(maxHealth, currentHealth);
    }

    public void Initialize(Deck deck)
    {
        _core.Deck = deck;
    }

    public void ResetForCombat()
    {
        _core.ClearPiles();
        _core.ResetFatigue();
        _core.SetupDrawPile();
    }

    public void AddCardToDeck(CardData cardData)
    {
        _core.Deck.AddCard(cardData);
    }

    public void ExhaustCard(OdysseyCards.Card.Card card)
    {
        RemoveFromHand(card);
    }

    public void SpendMana(int amount) => _core.SpendMana(amount);
    public void GainMana(int amount) => _core.GainMana(amount);
    public void ResetMana() => _core.ResetMana();
    public void SetMana(int current, int max) => _core.SetMana(current, max);
    public bool CanSpendMana(int amount) => _core.CanSpendMana(amount);
    public IReadOnlyList<OdysseyCards.Card.Card> DrawCards(int count) => _core.DrawCards(count);
    public void DiscardCard(OdysseyCards.Card.Card card) => _core.DiscardCard(card);
    public void RemoveFromHand(OdysseyCards.Card.Card card) => _core.RemoveFromHand(card);
    public void ReturnToDrawPile(OdysseyCards.Card.Card card) => _core.ReturnToDrawPile(card);
    public void AddToDrawPileBottom(OdysseyCards.Card.Card card) => _core.AddToDrawPileBottom(card);
    public void AddToDiscardPile(OdysseyCards.Card.Card card) => _core.AddToDiscardPile(card);
    public void ShuffleDrawPile() => _core.ShuffleDrawPile();
    public void DiscardHand() => _core.DiscardHand();
    public void StartTurn() => _core.StartTurn();
    public void EndTurn() => _core.EndTurn();
    public void SetupDrawPile() => _core.SetupDrawPile();
}
