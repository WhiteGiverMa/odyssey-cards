using System;
using System.Collections.Generic;

namespace OdysseyCards.Character;

/// <summary>
/// 指挥官接口。定义所有指挥官的公共契约。
/// 已移除对 Map/Headquarters 的依赖。
/// </summary>
public interface ICommander
{
    int CommanderId { get; }
    string CharacterName { get; }

    int CurrentHealth { get; }
    int MaxHealth { get; }
    bool IsDefeated { get; }

    int CurrentMana { get; }
    int MaxMana { get; }
    event Action<int, int> OnManaChanged;

    Deck Deck { get; }
    IReadOnlyList<OdysseyCards.Card.Card> Hand { get; }
    IReadOnlyList<OdysseyCards.Card.Card> DrawPile { get; }
    IReadOnlyList<OdysseyCards.Card.Card> DiscardPile { get; }
    int MaxHandSize { get; }
    int FatigueCount { get; }

    event Action OnHandChanged;
    event Action OnDrawPileChanged;
    event Action OnDiscardPileChanged;

    void InitializeHealth(int maxHealth, int currentHealth = -1);
    void SpendMana(int amount);
    void GainMana(int amount);
    void ResetMana();
    void SetMana(int current, int max);
    IReadOnlyList<OdysseyCards.Card.Card> DrawCards(int count);
    void DiscardCard(OdysseyCards.Card.Card card);
    void RemoveFromHand(OdysseyCards.Card.Card card);
    void ReturnToDrawPile(OdysseyCards.Card.Card card);
    void AddToDrawPileBottom(OdysseyCards.Card.Card card);
    void AddToDiscardPile(OdysseyCards.Card.Card card);
    void ShuffleDrawPile();
    void DiscardHand();
    bool CanSpendMana(int amount);
    void StartTurn();
    void EndTurn();
}
