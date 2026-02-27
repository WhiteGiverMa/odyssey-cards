using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

public enum CombatState
{
    NotStarted,
    PlayerTurn,
    EnemyTurn,
    Victory,
    Defeat
}

public partial class CombatManager : Node
{
    public static CombatManager Instance { get; private set; }

    public CombatState State { get; private set; } = CombatState.NotStarted;
    public Character.Player Player { get; private set; }
    public List<Character.Enemy> Enemies { get; private set; } = new();
    public int TurnCount { get; private set; }

    public event Action OnCombatStart;
    public event Action OnTurnStart;
    public event Action OnTurnEnd;
    public event Action<CombatState> OnCombatEnd;

    public override void _Ready()
    {
        Instance = this;
        CallDeferred(nameof(InitializeCombat));
    }

    private void InitializeCombat()
    {
        if (GameManager.Instance?.CurrentPlayer == null)
        {
            GD.PrintErr("CombatManager: No player found! Creating fallback player.");
            Player = new Player();
            Player.CharacterName = "Ironclad";
            Player.MaxHealth = 80;
            Player.MaxEnergy = 3;
            
            var deck = new Deck();
            deck.Initialize(CardFactory.GetStarterDeck());
            Player.Initialize(deck);
        }
        else
        {
            Player = GameManager.Instance.CurrentPlayer;
        }

        Enemies = new List<Character.Enemy>();
        var slimeData = GD.Load<EnemyData>("res://Resources/Enemies/Slime.tres");
        if (slimeData != null)
        {
            var slime = EnemyFactory.FromData(slimeData);
            Enemies.Add(slime);
        }

        var ui = GetTree().GetFirstNodeInGroup("CombatUI") as OdysseyCards.UI.CombatUI;
        if (ui != null)
        {
            ui.Initialize(Player, this);
            OnCombatEnd += (state) => ui.ShowCombatResult(state == CombatState.Victory);
        }

        StartCombat();
    }

    public void StartCombat()
    {
        State = CombatState.PlayerTurn;
        TurnCount = 1;

        foreach (var enemy in Enemies)
        {
            enemy.DecideNextAction();
        }

        Player.StartTurn();
        Player.DrawCards(5);
        
        OnCombatStart?.Invoke();
        OnTurnStart?.Invoke();
    }

    public void EndPlayerTurn()
    {
        if (State != CombatState.PlayerTurn)
            return;

        Player.DiscardHand();
        Player.EndTurn();
        
        State = CombatState.EnemyTurn;
        OnTurnEnd?.Invoke();
        
        ExecuteEnemyTurns();
    }

    private void ExecuteEnemyTurns()
    {
        foreach (var enemy in Enemies)
        {
            if (enemy.IsDead)
                continue;

            enemy.ExecuteAction(Player);
            
            if (Player.IsDead)
            {
                EndCombat(CombatState.Defeat);
                return;
            }
        }

        StartNewTurn();
    }

    private void StartNewTurn()
    {
        TurnCount++;
        State = CombatState.PlayerTurn;

        foreach (var enemy in Enemies)
        {
            if (!enemy.IsDead)
                enemy.StartTurn();
        }

        Player.StartTurn();
        Player.ResetEnergy();
        Player.DrawCards(5);
        
        OnTurnStart?.Invoke();
    }

    public void CheckCombatEnd()
    {
        bool allEnemiesDead = true;
        foreach (var enemy in Enemies)
        {
            if (!enemy.IsDead)
            {
                allEnemiesDead = false;
                break;
            }
        }

        if (allEnemiesDead)
        {
            EndCombat(CombatState.Victory);
        }
        else if (Player.IsDead)
        {
            EndCombat(CombatState.Defeat);
        }
    }

    private void EndCombat(CombatState result)
    {
        State = result;
        OnCombatEnd?.Invoke(result);
    }

    public void PlayCard(Card.Card card, Character.Character target)
    {
        if (State != CombatState.PlayerTurn)
            return;

        if (!card.CanPlay(Player, target))
            return;

        card.Play(Player, target);

        if (card.Data.Exhausts)
        {
            Player.ExhaustCard(card);
        }
        else
        {
            Player.DiscardCard(card);
        }

        CheckCombatEnd();
    }
}
