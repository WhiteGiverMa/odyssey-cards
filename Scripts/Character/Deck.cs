using System.Collections.Generic;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Character;

public class Deck
{
    public const int MaxCards = 30;

    public List<Resource> Cards { get; private set; } = new();

    public int CardCount => Cards.Count;

    public bool CanAddCard()
    {
        return CardCount < MaxCards;
    }

    public bool AddCardWithCheck(Resource card)
    {
        if (!CanAddCard())
        {
            return false;
        }

        if (card is not UnitData and not OrderData)
        {
            return false;
        }

        Cards.Add(card);
        return true;
    }

    public bool IsOverLimit()
    {
        return CardCount > MaxCards;
    }

    public void AddUnit(UnitData unit)
    {
        Cards.Add(unit);
    }

    public void AddOrder(OrderData order)
    {
        Cards.Add(order);
    }

    public void RemoveCard(Resource card)
    {
        Cards.Remove(card);
    }

    public void Initialize(List<Resource> initialCards)
    {
        Cards = new List<Resource>(initialCards);
    }
}
