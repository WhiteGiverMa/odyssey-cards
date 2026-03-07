using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Map;

namespace OdysseyCards.Character;

public interface IEnemyAI
{
    List<Card.Card> SelectCardsToPlay(ICommander enemy, Combat.CombatManager combat);
    void ExecuteTurn(ICommander enemy, Combat.CombatManager combat);
}

public partial class Enemy : Node, ICommander
{
    private readonly CommanderCore _core = new();
    private IEnemyAI _ai;

    public IEnemyAI AI => _ai;

    public EnemyDeckData EnemyData { get; private set; }

    public string EnemyType => EnemyData?.EnemyName ?? "Unknown";

    public int CommanderId { get; set; } = 1;
    public string CharacterName { get; set; } = "Enemy";
    public Headquarters HQ => _core.HQ;
    public bool IsDefeated => HQ?.IsDestroyed ?? true;

    public int CurrentEnergy => _core.CurrentEnergy;
    public int MaxEnergy => _core.MaxEnergy;
    public event Action<int, int> OnEnergyChanged
    {
        add => _core.OnEnergyChanged += value;
        remove => _core.OnEnergyChanged -= value;
    }

    public Deck Deck => _core.Deck;
    public IReadOnlyList<Card.Card> Hand => _core.Hand;
    public IReadOnlyList<Card.Card> DrawPile => _core.DrawPile;
    public IReadOnlyList<Card.Card> DiscardPile => _core.DiscardPile;
    public int MaxHandSize { get => _core.MaxHandSize; set => _core.MaxHandSize = value; }
    public int FatigueCount => _core.FatigueCount;

    public event Action OnHandChanged
    {
        add => _core.OnHandChanged += value;
        remove => _core.OnHandChanged -= value;
    }
    public event Action OnDrawPileChanged
    {
        add => _core.OnDrawPileChanged += value;
        remove => _core.OnDrawPileChanged -= value;
    }
    public event Action OnDiscardPileChanged
    {
        add => _core.OnDiscardPileChanged += value;
        remove => _core.OnDiscardPileChanged -= value;
    }

    public event Action<Enemy> OnEnemyTurnStart;
    public event Action<Enemy> OnEnemyTurnEnd;
    public event Action<Enemy> OnEnemyDeath;

    public override void _Ready()
    {
        CharacterName = "Enemy";
    }

    public void Initialize(EnemyDeckData data, int deploymentNodeId = -1)
    {
        EnemyData = data;

        if (data != null)
        {
            CharacterName = data.EnemyName;
            InitializeHQ(data.StartingHealth, -1, deploymentNodeId);
            _core.SetMaxEnergy(data.MaxEnergy);
            _core.SetCurrentEnergy(data.StartingEnergy);

            List<Resource> cards = data.GetAllCards();
            foreach (Resource cardData in cards)
            {
                if (cardData is UnitData unitData)
                {
                    Deck.AddUnit(unitData);
                }
                else if (cardData is OrderData orderData)
                {
                    Deck.AddOrder(orderData);
                }
            }

            _ai = CreateAI("balanced");
        }
        else
        {
            InitializeHQ(50, -1, deploymentNodeId);
        }
    }

    private IEnemyAI CreateAI(string aiType)
    {
        return aiType?.ToLower() switch
        {
            "aggressive" => new AggressiveAI(),
            "defensive" => new DefensiveAI(),
            "balanced" => new BalancedAI(),
            _ => new BalancedAI()
        };
    }

    public void InitializeHQ(int maxHealth, int currentHealth = -1, int deploymentNodeId = -1)
    {
        _core.InitializeHQ(maxHealth, currentHealth, deploymentNodeId);
    }

    public void SetupDrawPile()
    {
        _core.SetupDrawPile();
    }

    public void ResetForCombat()
    {
        _core.ClearPiles();
        _core.ResetFatigue();
        SetupDrawPile();
    }

    public void StartTurn()
    {
        _core.StartTurn();
        OnEnemyTurnStart?.Invoke(this);
    }

    public void EndTurn()
    {
        _core.EndTurn();
        OnEnemyTurnEnd?.Invoke(this);
    }

    public void ExecuteTurn(Combat.CombatManager combat)
    {
        _ai?.ExecuteTurn(this, combat);
    }

    public List<Card.Card> SelectCardsToPlay(Combat.CombatManager combat)
    {
        return _ai?.SelectCardsToPlay(this, combat) ?? new List<Card.Card>();
    }

    public void Die()
    {
        OnEnemyDeath?.Invoke(this);
        QueueFree();
    }

    public void SpendEnergy(int amount) => _core.SpendEnergy(amount);
    public void GainEnergy(int amount) => _core.GainEnergy(amount);
    public void ResetEnergy() => _core.ResetEnergy();
    public void SetEnergy(int current, int max) => _core.SetEnergy(current, max);
    public void IncreaseMaxEnergy(int amount) => _core.IncreaseMaxEnergy(amount);
    public void DrawCards(int count) => _core.DrawCards(count);
    public void DiscardCard(Card.Card card) => _core.DiscardCard(card);
    public void RemoveFromHand(Card.Card card) => _core.RemoveFromHand(card);
    public void ReturnToDrawPile(Card.Card card) => _core.ReturnToDrawPile(card);
    public void ShuffleDrawPile() => _core.ShuffleDrawPile();
    public void DiscardHand() => _core.DiscardHand();
    public bool CanSpendEnergy(int amount) => _core.CanSpendEnergy(amount);
}

public class AggressiveAI : IEnemyAI
{
    public List<Card.Card> SelectCardsToPlay(ICommander enemy, Combat.CombatManager combat)
    {
        List<Card.Card> result = new();
        int energy = enemy.CurrentEnergy;

        foreach (Card.Card card in enemy.Hand)
        {
            int cost = GetCardCost(card);
            if (cost > 0 && cost <= energy)
            {
                result.Add(card);
                energy -= cost;
            }
        }

        return result;
    }

    public void ExecuteTurn(ICommander enemy, Combat.CombatManager combat)
    {
        List<Card.Card> cardsToPlay = SelectCardsToPlay(enemy, combat);
        foreach (Card.Card card in cardsToPlay)
        {
            GD.Print($"[AggressiveAI] Playing card: {card.CardName}");
        }
    }

    private int GetCardCost(Card.Card card)
    {
        if (card is Unit unit && unit.Data != null)
            return unit.Data.DeployCost;
        if (card is Order order && order.Data != null)
            return order.Data.Cost;
        return 0;
    }
}

public class DefensiveAI : IEnemyAI
{
    public List<Card.Card> SelectCardsToPlay(ICommander enemy, Combat.CombatManager combat)
    {
        List<Card.Card> result = new();
        int energy = enemy.CurrentEnergy;

        foreach (Card.Card card in enemy.Hand)
        {
            int cost = GetCardCost(card);
            if (cost > 0 && cost <= energy)
            {
                if (card is Order)
                {
                    result.Add(card);
                    energy -= cost;
                }
            }
        }

        foreach (Card.Card card in enemy.Hand)
        {
            int cost = GetCardCost(card);
            if (cost > 0 && cost <= energy && !result.Contains(card))
            {
                result.Add(card);
                energy -= cost;
            }
        }

        return result;
    }

    public void ExecuteTurn(ICommander enemy, Combat.CombatManager combat)
    {
        List<Card.Card> cardsToPlay = SelectCardsToPlay(enemy, combat);
        foreach (Card.Card card in cardsToPlay)
        {
            GD.Print($"[DefensiveAI] Playing card: {card.CardName}");
        }
    }

    private int GetCardCost(Card.Card card)
    {
        if (card is Unit unit && unit.Data != null)
            return unit.Data.DeployCost;
        if (card is Order order && order.Data != null)
            return order.Data.Cost;
        return 0;
    }
}

public class BalancedAI : IEnemyAI
{
    public List<Card.Card> SelectCardsToPlay(ICommander enemy, Combat.CombatManager combat)
    {
        List<Card.Card> result = new();
        int energy = enemy.CurrentEnergy;

        foreach (Card.Card card in enemy.Hand)
        {
            int cost = GetCardCost(card);
            if (cost > 0 && cost <= energy)
            {
                result.Add(card);
                energy -= cost;
            }
        }

        return result;
    }

    public void ExecuteTurn(ICommander enemy, Combat.CombatManager combat)
    {
        List<Card.Card> cardsToPlay = SelectCardsToPlay(enemy, combat);
        foreach (Card.Card card in cardsToPlay)
        {
            GD.Print($"[BalancedAI] Playing card: {card.CardName}");
        }
    }

    private int GetCardCost(Card.Card card)
    {
        if (card is Unit unit && unit.Data != null)
            return unit.Data.DeployCost;
        if (card is Order order && order.Data != null)
            return order.Data.Cost;
        return 0;
    }
}
