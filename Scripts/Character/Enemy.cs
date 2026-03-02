using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.AI;
using OdysseyCards.Core;

namespace OdysseyCards.Character;

/// <summary>
/// Represents an enemy character in combat.
/// Manages enemy-specific card drawing, AI, and headquarters health.
/// </summary>
public partial class Enemy : Character
{
    /// <summary>
    /// The enemy's deck configuration.
    /// </summary>
    public Deck Deck { get; private set; }

    /// <summary>
    /// Cards currently in the enemy's hand.
    /// </summary>
    public List<Card.Card> Hand { get; private set; } = new();

    /// <summary>
    /// Cards in the enemy's draw pile.
    /// </summary>
    public List<Card.Card> DrawPile { get; private set; } = new();

    /// <summary>
    /// Cards in the enemy's discard pile.
    /// </summary>
    public List<Card.Card> DiscardPile { get; private set; } = new();

    /// <summary>
    /// Maximum number of cards in the enemy's hand.
    /// </summary>
    public int MaxHandSize { get; set; } = 9;

    /// <summary>
    /// Number of times the enemy has drawn from an empty deck.
    /// </summary>
    public int FatigueCount { get; private set; } = 0;

    /// <summary>
    /// Current health of the enemy's headquarters.
    /// </summary>
    public int HQCurrentHealth { get; set; } = 8;

    /// <summary>
    /// Maximum health of the enemy's headquarters.
    /// </summary>
    public int HQMaxHealth { get; set; } = 8;

    private EnemyAI _ai;

    /// <summary>
    /// The AI controller for this enemy.
    /// </summary>
    public EnemyAI AI => _ai;

    /// <summary>
    /// Fired when the hand contents change.
    /// </summary>
    public event Action OnHandChanged;

    /// <summary>
    /// Fired when the draw pile contents change.
    /// </summary>
    public event Action OnDrawPileChanged;

    /// <summary>
    /// Fired when the discard pile contents change.
    /// </summary>
    public event Action OnDiscardPileChanged;

    /// <summary>
    /// Fired when HQ health changes. Parameters: currentHealth, maxHealth.
    /// </summary>
    public event Action<int, int> OnHQHealthChanged;

    /// <summary>
    /// Initializes the enemy with deck data.
    /// </summary>
    /// <param name="deckData">The enemy deck configuration.</param>
    public void Initialize(EnemyDeckData deckData)
    {
        CharacterName = deckData.EnemyName;
        MaxHealth = deckData.StartingHealth;
        MaxEnergy = deckData.StartingEnergy;
        CurrentHealth = MaxHealth;
        CurrentEnergy = MaxEnergy;
        Block = 0;

        HQMaxHealth = deckData.StartingHealth;
        HQCurrentHealth = HQMaxHealth;

        _ai = new EnemyAI();

        Deck = new Deck();
        List<Resource> cards = deckData.GetAllCards();
        Deck.Initialize(cards);
        DrawPile = Deck.CreateDrawPile();
        ShuffleDrawPile();

        GD.Print($"[Enemy] Initialized: {CharacterName}, HQ Health: {HQCurrentHealth}/{HQMaxHealth}");
    }

    /// <summary>
    /// Draws cards from the draw pile into hand.
    /// Triggers fatigue damage if draw pile is empty.
    /// </summary>
    /// <param name="count">Number of cards to draw.</param>
    public void DrawCards(int count)
    {
        int cardsToDraw = Mathf.Min(count, MaxHandSize - Hand.Count);

        for (int i = 0; i < cardsToDraw; i++)
        {
            if (DrawPile.Count == 0)
            {
                FatigueCount++;
                TakeHQDamage(FatigueCount);
                GD.Print($"[Enemy] Fatigue damage: {FatigueCount}");
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

    /// <summary>
    /// Applies damage to the enemy's headquarters.
    /// </summary>
    /// <param name="damage">The amount of damage to apply.</param>
    public void TakeHQDamage(int damage)
    {
        HQCurrentHealth -= damage;
        GD.Print($"[Enemy] HQ took {damage} damage. HQ Health: {HQCurrentHealth}/{HQMaxHealth}");
        OnHQHealthChanged?.Invoke(HQCurrentHealth, HQMaxHealth);
    }

    /// <summary>
    /// Moves a card from hand to the discard pile.
    /// </summary>
    /// <param name="card">The card to discard.</param>
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

    /// <summary>
    /// Removes a card from hand without adding it to any pile.
    /// </summary>
    /// <param name="card">The card to remove.</param>
    public void RemoveFromHand(Card.Card card)
    {
        if (!Hand.Contains(card))
        {
            return;
        }

        Hand.Remove(card);
        OnHandChanged?.Invoke();
    }

    /// <summary>
    /// Shuffles the draw pile randomly.
    /// </summary>
    public void ShuffleDrawPile()
    {
        RandomNumberGenerator random = new();
        random.Randomize();

        for (int i = DrawPile.Count - 1; i > 0; i--)
        {
            int j = random.RandiRange(0, i);
            (DrawPile[i], DrawPile[j]) = (DrawPile[j], DrawPile[i]);
        }
    }

    /// <summary>
    /// Returns a card from hand to a random position in the draw pile.
    /// </summary>
    /// <param name="card">The card to return.</param>
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
}
