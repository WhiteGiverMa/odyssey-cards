using System;
using System.Collections.Generic;
using OdysseyCards.Map;

namespace OdysseyCards.Character;

public interface ICommander
{
    int CommanderId { get; }
    string CharacterName { get; }

    Headquarters HQ { get; }
    bool IsDefeated { get; }

    int CurrentEnergy { get; }
    int MaxEnergy { get; }
    event Action<int, int> OnEnergyChanged;

    Deck Deck { get; }
    IReadOnlyList<Card.Card> Hand { get; }
    IReadOnlyList<Card.Card> DrawPile { get; }
    IReadOnlyList<Card.Card> DiscardPile { get; }
    int MaxHandSize { get; }
    int FatigueCount { get; }

    event Action OnHandChanged;
    event Action OnDrawPileChanged;
    event Action OnDiscardPileChanged;

    void InitializeHQ(int maxHealth, int currentHealth = -1, int deploymentNodeId = -1);
    void SpendEnergy(int amount);
    void GainEnergy(int amount);
    void ResetEnergy();
    void SetEnergy(int current, int max);
    void IncreaseMaxEnergy(int amount);
    void DrawCards(int count);
    void DiscardCard(Card.Card card);
    void RemoveFromHand(Card.Card card);
    void ReturnToDrawPile(Card.Card card);
    void ShuffleDrawPile();
    void DiscardHand();
    bool CanSpendEnergy(int amount);
    void StartTurn();
    void EndTurn();
}
