using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.Character;

/// <summary>
/// Represents the player character in the game.
/// Manages hand, draw pile, discard pile, and headquarters health.
/// </summary>
public partial class Player : Character
{
    /// <summary>
    /// The player's deck configuration.
    /// </summary>
    public Deck Deck { get; private set; }

    /// <summary>
    /// Cards currently in hand.
    /// </summary>
    public List<Card.Card> Hand { get; private set; } = new();

    /// <summary>
    /// Cards in the draw pile.
    /// </summary>
    public List<Card.Card> DrawPile { get; private set; } = new();

    /// <summary>
    /// Cards in the discard pile.
    /// </summary>
    public List<Card.Card> DiscardPile { get; private set; } = new();

    /// <summary>
    /// Cards that have been exhausted (removed from play).
    /// </summary>
    public List<Card.Card> ExhaustPile { get; private set; } = new();

    /// <summary>
    /// Current gold amount.
    /// </summary>
    public int Gold { get; private set; } = 99;

    /// <summary>
    /// Maximum number of cards in hand.
    /// </summary>
    public int MaxHandSize { get; set; } = 9;

    /// <summary>
    /// Number of times the player has drawn from an empty deck.
    /// </summary>
    public int FatigueCount { get; private set; } = 0;

    /// <summary>
    /// Current health of the player's headquarters.
    /// </summary>
    public int HQCurrentHealth { get; set; } = 8;

    /// <summary>
    /// Maximum health of the player's headquarters.
    /// </summary>
    public int HQMaxHealth { get; set; } = 8;

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
    /// Fired when the exhaust pile contents change.
    /// </summary>
    public event Action OnExhaustPileChanged;

    /// <summary>
    /// Initializes the player with a starting deck.
    /// </summary>
    /// <param name="startingDeck">The deck to start with.</param>
    public void Initialize(Deck startingDeck)
    {
        Deck = startingDeck;
        DrawPile = Deck.CreateDrawPile();
        ShuffleDrawPile();
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
                GD.Print($"[Player] Fatigue damage: {FatigueCount}");
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
    /// Moves a card from hand to the exhaust pile.
    /// </summary>
    /// <param name="card">The card to exhaust.</param>
    public void ExhaustCard(Card.Card card)
    {
        if (!Hand.Contains(card))
        {
            return;
        }

        Hand.Remove(card);
        ExhaustPile.Add(card);

        OnHandChanged?.Invoke();
        OnExhaustPileChanged?.Invoke();
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
    /// Checks if the player can spend the specified amount of energy.
    /// </summary>
    /// <param name="amount">The energy amount to check.</param>
    /// <returns>True if the player has enough energy.</returns>
    public bool CanSpendEnergy(int amount)
    {
        return CurrentEnergy >= amount;
    }

    /// <summary>
    /// Discards all cards in hand.
    /// </summary>
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
    /// Adds gold to the player's total.
    /// </summary>
    /// <param name="amount">The amount of gold to add.</param>
    public void AddGold(int amount)
    {
        Gold += amount;
    }

    /// <summary>
    /// Attempts to spend gold.
    /// </summary>
    /// <param name="amount">The amount of gold to spend.</param>
    /// <returns>True if the gold was spent successfully.</returns>
    public bool SpendGold(int amount)
    {
        if (Gold < amount)
        {
            return false;
        }

        Gold -= amount;
        return true;
    }

    /// <summary>
    /// Applies damage to the player's headquarters.
    /// </summary>
    /// <param name="damage">The amount of damage to apply.</param>
    public void TakeHQDamage(int damage)
    {
        HQCurrentHealth -= damage;
        GD.Print($"[Player] HQ took {damage} damage. HQ Health: {HQCurrentHealth}/{HQMaxHealth}");
    }

    /// <summary>
    /// Restores headquarters health to specific values.
    /// </summary>
    /// <param name="currentHealth">The current health to set.</param>
    /// <param name="maxHealth">The maximum health to set.</param>
    public void RestoreHQHealth(int currentHealth, int maxHealth)
    {
        HQCurrentHealth = currentHealth;
        HQMaxHealth = maxHealth;
    }
}

/// <summary>
/// Represents a deck of cards with a maximum size limit.
/// Stores card data resources and can create runtime card instances.
/// </summary>
public class Deck
{
    /// <summary>
    /// Maximum number of cards allowed in the deck.
    /// </summary>
    public const int MaxCards = 30;

    /// <summary>
    /// List of card data resources in this deck.
    /// </summary>
    public List<Resource> Cards { get; private set; } = new();

    /// <summary>
    /// Current number of cards in the deck.
    /// </summary>
    public int CardCount => Cards.Count;

    /// <summary>
    /// Checks if a card can be added to the deck.
    /// </summary>
    /// <returns>True if the deck is not full.</returns>
    public bool CanAddCard()
    {
        return CardCount < MaxCards;
    }

    /// <summary>
    /// Attempts to add a card if the deck is not full.
    /// </summary>
    /// <param name="card">The card resource to add.</param>
    /// <returns>True if the card was added.</returns>
    public bool AddCardWithCheck(Resource card)
    {
        if (!CanAddCard())
        {
            return false;
        }

        Cards.Add(card);
        return true;
    }

    /// <summary>
    /// Checks if the deck exceeds the maximum size.
    /// </summary>
    /// <returns>True if the deck has more than MaxCards.</returns>
    public bool IsOverLimit()
    {
        return CardCount > MaxCards;
    }

    /// <summary>
    /// Adds a unit card to the deck.
    /// </summary>
    /// <param name="unit">The unit data to add.</param>
    public void AddUnit(UnitData unit)
    {
        Cards.Add(unit);
    }

    /// <summary>
    /// Adds an order card to the deck.
    /// </summary>
    /// <param name="order">The order data to add.</param>
    public void AddOrder(OrderData order)
    {
        Cards.Add(order);
    }

    /// <summary>
    /// Removes a card from the deck.
    /// </summary>
    /// <param name="card">The card resource to remove.</param>
    public void RemoveCard(Resource card)
    {
        Cards.Remove(card);
    }

    /// <summary>
    /// Initializes the deck with a list of cards.
    /// </summary>
    /// <param name="initialCards">The initial card list.</param>
    public void Initialize(List<Resource> initialCards)
    {
        Cards = initialCards;
    }

    /// <summary>
    /// Creates a draw pile with runtime card instances from the deck data.
    /// </summary>
    /// <returns>A list of card instances ready for gameplay.</returns>
    public List<Card.Card> CreateDrawPile()
    {
        List<Card.Card> pile = new();
        foreach (Resource cardData in Cards)
        {
            if (cardData is UnitData unitData)
            {
                pile.Add(Card.Unit.Create(unitData));
            }
            else if (cardData is OrderData orderData)
            {
                pile.Add(Card.Order.Create(orderData));
            }
        }
        return pile;
    }
}
