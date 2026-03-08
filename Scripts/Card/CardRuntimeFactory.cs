using System.Collections.Generic;
using Godot;
using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Card;

public static class CardRuntimeFactory
{
    public static Card CreateCard(Resource cardData)
    {
        if (cardData is UnitData unitData)
        {
            return Unit.Create(unitData);
        }
        else if (cardData is OrderData orderData)
        {
            return Order.Create(orderData);
        }

        GD.PrintErr($"[CardRuntimeFactory] Unknown card data type: {cardData?.GetType().Name}");
        return null;
    }

    public static List<Card> CreateDrawPile(Deck deck)
    {
        List<Card> pile = new();
        foreach (Resource cardData in deck.Cards)
        {
            Card card = CreateCard(cardData);
            if (card != null)
            {
                pile.Add(card);
            }
        }
        return pile;
    }
}
