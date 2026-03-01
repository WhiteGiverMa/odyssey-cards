using Godot;
using System;
using System.Collections.Generic;
using OdysseyCards.Card;
using OdysseyCards.Character;

namespace OdysseyCards.Core;

/// <summary>
/// Global game state manager (Autoload singleton).
/// Manages player progression, deck state, and run persistence across scenes.
/// </summary>
public partial class GameManager : Node
{
    /// <summary>
    /// Singleton instance for global access.
    /// </summary>
    public static GameManager Instance { get; private set; }

    private Deck _playerDeck;

    /// <summary>
    /// The player's current deck configuration.
    /// </summary>
    public Deck PlayerDeck => _playerDeck;

    /// <summary>
    /// The current player character instance.
    /// </summary>
    public Player CurrentPlayer { get; private set; }

    /// <summary>
    /// Current floor number within the act (1-15).
    /// </summary>
    public int CurrentFloor { get; private set; } = 1;

    /// <summary>
    /// Current act number (1-3).
    /// </summary>
    public int CurrentAct { get; private set; } = 1;

    /// <summary>
    /// Maximum number of acts in a run.
    /// </summary>
    public int MaxAct { get; set; } = 3;

    /// <summary>
    /// Current health of the player's headquarters.
    /// </summary>
    public int PlayerHQCurrentHealth { get; set; } = 8;

    /// <summary>
    /// Maximum health of the player's headquarters.
    /// </summary>
    public int PlayerHQMaxHealth { get; set; } = 8;

    /// <summary>
    /// Fired when the player advances to a new floor.
    /// </summary>
    public event Action<int> OnFloorChanged;

    /// <summary>
    /// Fired when the player advances to a new act.
    /// </summary>
    public event Action<int> OnActChanged;

    /// <summary>
    /// Fired when the deck contents change.
    /// </summary>
    public event Action OnDeckChanged;

    /// <summary>
    /// Fired when deck adjustment is needed (deck is full).
    /// </summary>
    public event Action<DeckAdjustment> OnDeckAdjustmentRequired;

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>
    /// Creates a new player with default starting values and deck.
    /// </summary>
    public void CreateNewPlayer()
    {
        CurrentPlayer = new Player();
        CurrentPlayer.CharacterName = "Ironclad";
        CurrentPlayer.MaxHealth = 80;
        CurrentPlayer.MaxEnergy = 3;

        var startingDeck = CreateStartingDeck();
        CurrentPlayer.Initialize(startingDeck);
        _playerDeck = startingDeck;
    }

    /// <summary>
    /// Adds a card to the player's deck if space is available.
    /// </summary>
    /// <param name="card">The card resource to add.</param>
    /// <returns>True if the card was added successfully.</returns>
    public bool AddCardToDeck(Resource card)
    {
        if (_playerDeck == null || card == null)
            return false;

        bool success = _playerDeck.AddCardWithCheck(card);
        if (success)
        {
            OnDeckChanged?.Invoke();
        }
        return success;
    }

    /// <summary>
    /// Attempts to add a card, triggering adjustment flow if deck is full.
    /// </summary>
    /// <param name="card">The card resource to add.</param>
    /// <returns>True if added directly, false if adjustment is required.</returns>
    public bool TryAddCardWithAdjustment(Resource card)
    {
        if (_playerDeck == null || card == null)
            return false;

        if (_playerDeck.CanAddCard())
        {
            return AddCardToDeck(card);
        }

        var adjustment = new DeckAdjustment(card, new List<Resource>(_playerDeck.Cards));
        adjustment.CalculateRemoval();
        OnDeckAdjustmentRequired?.Invoke(adjustment);
        return false;
    }

    /// <summary>
    /// Removes a card from the player's deck.
    /// </summary>
    /// <param name="card">The card resource to remove.</param>
    /// <returns>True if the card was removed successfully.</returns>
    public bool RemoveCardFromDeck(Resource card)
    {
        if (_playerDeck == null || card == null)
            return false;

        if (_playerDeck.Cards.Contains(card))
        {
            _playerDeck.RemoveCard(card);
            OnDeckChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the player's current deck.
    /// </summary>
    /// <returns>The player's deck instance.</returns>
    public Deck GetPlayerDeck()
    {
        return _playerDeck;
    }

    private Deck CreateStartingDeck()
    {
        var deck = new Deck();
        deck.Initialize(CardFactory.GetStarterDeck1());
        return deck;
    }

    /// <summary>
    /// Advances the player to the next floor, potentially advancing acts.
    /// </summary>
    public void AdvanceFloor()
    {
        CurrentFloor++;
        OnFloorChanged?.Invoke(CurrentFloor);

        if (CurrentFloor > GetFloorsPerAct())
        {
            AdvanceAct();
        }
    }

    private void AdvanceAct()
    {
        CurrentAct++;
        CurrentFloor = 1;
        OnActChanged?.Invoke(CurrentAct);
    }

    private int GetFloorsPerAct()
    {
        return 15;
    }

    /// <summary>
    /// Resets the entire run to initial state.
    /// </summary>
    public void ResetRun()
    {
        CurrentFloor = 1;
        CurrentAct = 1;
        CreateNewPlayer();
    }

    /// <summary>
    /// Persists headquarters health between combat encounters.
    /// </summary>
    /// <param name="currentHealth">Current HQ health to save.</param>
    /// <param name="maxHealth">Maximum HQ health to save.</param>
    public void SavePlayerHQHealth(int currentHealth, int maxHealth)
    {
        PlayerHQCurrentHealth = Mathf.Min(currentHealth, maxHealth);
        PlayerHQMaxHealth = maxHealth;
    }

    /// <summary>
    /// Retrieves persisted headquarters health.
    /// </summary>
    /// <returns>Tuple of current and max HQ health.</returns>
    public (int currentHealth, int maxHealth) GetPlayerHQHealth()
    {
        return (PlayerHQCurrentHealth, PlayerHQMaxHealth);
    }

    /// <summary>
    /// Resets headquarters health to default values.
    /// </summary>
    public void ResetPlayerHQHealth()
    {
        PlayerHQCurrentHealth = 8;
        PlayerHQMaxHealth = 8;
    }
}
