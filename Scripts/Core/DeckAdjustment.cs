using System.Collections.Generic;
using Godot;
using OdysseyCards.Character;

namespace OdysseyCards.Core;

public class DeckAdjustment
{
    public int CardsToRemove { get; private set; }
    public Resource NewCard { get; private set; }
    public List<Resource> CurrentCards { get; private set; }

    public DeckAdjustment(Resource newCard, List<Resource> currentCards)
    {
        NewCard = newCard;
        CurrentCards = currentCards;
        CardsToRemove = 0;
    }

    public static int GetCardsToRemove(Deck deck)
    {
        if (deck == null)
        {
            return 0;
        }

        if (!deck.IsOverLimit())
        {
            return 0;
        }

        return deck.CardCount - Deck.MaxCards;
    }

    public void CalculateRemoval()
    {
        if (CurrentCards == null)
        {
            CardsToRemove = 0;
            return;
        }

        int projectedCount = CurrentCards.Count + 1;
        if (projectedCount > Deck.MaxCards)
        {
            CardsToRemove = projectedCount - Deck.MaxCards;
        }
        else
        {
            CardsToRemove = 0;
        }
    }

    public bool NeedsAdjustment => CardsToRemove > 0;

    public bool CanAddWithoutRemoval => CurrentCards != null && CurrentCards.Count < Deck.MaxCards;
}
