using Godot;
using System;
using System.Collections.Generic;
using OdysseyCards.Character;
using OdysseyCards.Card;

namespace OdysseyCards.Core;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    public Player CurrentPlayer { get; private set; }
    public int CurrentFloor { get; private set; } = 1;
    public int CurrentAct { get; private set; } = 1;
    public int MaxAct { get; set; } = 3;

    public event Action<int> OnFloorChanged;
    public event Action<int> OnActChanged;

    public override void _Ready()
    {
        Instance = this;
    }

    public void CreateNewPlayer()
    {
        CurrentPlayer = new Player();
        CurrentPlayer.CharacterName = "Ironclad";
        CurrentPlayer.MaxHealth = 80;
        CurrentPlayer.MaxEnergy = 3;
        
        var startingDeck = CreateStartingDeck();
        CurrentPlayer.Initialize(startingDeck);
    }

    private Deck CreateStartingDeck()
    {
        var deck = new Deck();
        deck.Initialize(CardFactory.GetStarterDeck());
        return deck;
    }

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

    public void ResetRun()
    {
        CurrentFloor = 1;
        CurrentAct = 1;
        CreateNewPlayer();
    }
}
