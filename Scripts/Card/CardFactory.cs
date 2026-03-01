using Godot;
using System.Collections.Generic;
using OdysseyCards.Core;

namespace OdysseyCards.Card;

public static class CardFactory
{
    public static Unit CreateDetectiveSquad()
    {
        var data = GD.Load<UnitData>("res://Resources/Cards/Unit_DetectiveSquad.tres");
        return Unit.Create(data);
    }

    public static Unit Create18thRegiment()
    {
        var data = GD.Load<UnitData>("res://Resources/Cards/Unit_18thRegiment.tres");
        return Unit.Create(data);
    }

    public static Unit CreateLianshuScout()
    {
        var data = GD.Load<UnitData>("res://Resources/Cards/Unit_LianshuScout.tres");
        return Unit.Create(data);
    }

    public static Order CreateStrike()
    {
        var data = GD.Load<OrderData>("res://Resources/Cards/Order_Strike.tres");
        return Order.Create(data);
    }

    public static Order CreateAssault()
    {
        var data = GD.Load<OrderData>("res://Resources/Cards/Order_Assault.tres");
        return Order.Create(data);
    }

    public static Order CreateAlert()
    {
        var data = GD.Load<OrderData>("res://Resources/Cards/Order_Alert.tres");
        return Order.Create(data);
    }

    public static List<Resource> GetStarterDeck1()
    {
        var deck = new List<Resource>();

        for (int i = 0; i < 4; i++)
            deck.Add(GD.Load<OrderData>("res://Resources/Cards/Order_Strike.tres"));

        for (int i = 0; i < 4; i++)
            deck.Add(GD.Load<OrderData>("res://Resources/Cards/Order_Assault.tres"));

        for (int i = 0; i < 4; i++)
            deck.Add(GD.Load<UnitData>("res://Resources/Cards/Unit_18thRegiment.tres"));

        return deck;
    }

    public static List<Resource> GetStarterDeck2()
    {
        var deck = new List<Resource>();

        for (int i = 0; i < 4; i++)
            deck.Add(GD.Load<UnitData>("res://Resources/Cards/Unit_LianshuScout.tres"));

        for (int i = 0; i < 4; i++)
            deck.Add(GD.Load<UnitData>("res://Resources/Cards/Unit_DetectiveSquad.tres"));

        for (int i = 0; i < 4; i++)
            deck.Add(GD.Load<OrderData>("res://Resources/Cards/Order_Alert.tres"));

        return deck;
    }

    public static List<Resource> GetDemoDeck()
    {
        var deck = new List<Resource>();

        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_AssaultInfantry.tres"));
        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_ScoutVehicle.tres"));
        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_Veteran.tres"));
        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_HeavyArmor.tres"));
        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_Guardian.tres"));
        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_Ambusher.tres"));
        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_ShockTrooper.tres"));
        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_Immortal.tres"));
        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_JumboTank.tres"));
        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_Infiltrator.tres"));
        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_RotationInfantry.tres"));
        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_Engineer.tres"));
        deck.Add(GD.Load<UnitData>("res://Resources/Cards/Demo/Unit_Martyr.tres"));

        deck.Add(GD.Load<OrderData>("res://Resources/Cards/Demo/Order_RotationStrike.tres"));
        deck.Add(GD.Load<OrderData>("res://Resources/Cards/Demo/Order_Heal.tres"));
        deck.Add(GD.Load<OrderData>("res://Resources/Cards/Demo/Order_Supply.tres"));

        return deck;
    }
}
