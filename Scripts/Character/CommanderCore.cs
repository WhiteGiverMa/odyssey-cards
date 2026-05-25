using System;
using System.Collections.Generic;
using Godot;

namespace OdysseyCards.Character;

/// <summary>
/// 指挥官核心逻辑（非 Godot 对象）。
/// 管理生命值、法力水晶（原能量）、牌堆状态和回合流程。
/// 已移除对 Map/Headquarters 的依赖，改为简单的生命值属性。
/// </summary>
public class CommanderCore
{
    public const int MaxManaCrystals = 10;
    public const int HardMaxManaCap = 24;

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public int CurrentHealth { get; private set; } = 30;

    /// <summary>
    /// 最大生命值。
    /// </summary>
    public int MaxHealth { get; private set; } = 30;

    /// <summary>
    /// 牌堆定义。
    /// </summary>
    public Deck Deck { get; internal set; }

    /// <summary>
    /// 战斗中的牌堆状态。
    /// </summary>
    public CombatDeckState CombatDeckState { get; } = new();

    public List<OdysseyCards.Card.Card> Hand => CombatDeckState.Hand;
    public List<OdysseyCards.Card.Card> DrawPile => CombatDeckState.DrawPile;
    public List<OdysseyCards.Card.Card> DiscardPile => CombatDeckState.DiscardPile;

    /// <summary>
    /// 当前法力水晶。
    /// </summary>
    public int CurrentMana { get; private set; }

    /// <summary>
    /// 最大法力水晶。
    /// </summary>
    public int MaxMana { get; private set; } = 1;

    /// <summary>
    /// 最大手牌数。
    /// </summary>
    public int MaxHandSize { get => CombatDeckState.MaxHandSize; set => CombatDeckState.MaxHandSize = value; }

    /// <summary>
    /// 疲劳计数器。
    /// </summary>
    public int FatigueCount => CombatDeckState.FatigueCount;

    /// <summary>
    /// 法力值变化事件。
    /// </summary>
    public event Action<int, int> OnManaChanged;

    /// <summary>
    /// 手牌变化事件。
    /// </summary>
    public event Action OnHandChanged
    {
        add => CombatDeckState.OnHandChanged += value;
        remove => CombatDeckState.OnHandChanged -= value;
    }

    public event Action OnDrawPileChanged
    {
        add => CombatDeckState.OnDrawPileChanged += value;
        remove => CombatDeckState.OnDrawPileChanged -= value;
    }

    public event Action OnDiscardPileChanged
    {
        add => CombatDeckState.OnDiscardPileChanged += value;
        remove => CombatDeckState.OnDiscardPileChanged -= value;
    }

    public CommanderCore()
    {
        Deck = new Deck();
        // 连接疲劳伤害回调：疲劳时对自己造成伤害
        CombatDeckState.SetFatigueCallback(damage =>
        {
            ApplyDamage(damage);
            GD.Print($"[CommanderCore] Fatigue damage: {damage}, remaining HP: {CurrentHealth}");
        });
    }

    /// <summary>
    /// 初始化生命值。
    /// </summary>
    public void InitializeHealth(int maxHealth, int currentHealth = -1)
    {
        MaxHealth = maxHealth;
        CurrentHealth = currentHealth >= 0 ? currentHealth : maxHealth;
    }

    /// <summary>
    /// 应用伤害。
    /// </summary>
    public void ApplyDamage(int amount)
    {
        if (amount <= 0) return;
        CurrentHealth = Math.Max(0, CurrentHealth - amount);
    }

    /// <summary>
    /// 恢复生命值。
    /// </summary>
    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
    }

    /// <summary>
    /// 设置生命值。
    /// </summary>
    public void SetHealth(int current, int max)
    {
        MaxHealth = max;
        CurrentHealth = current;
    }

    /// <summary>
    /// 是否已死亡。
    /// </summary>
    public bool IsDead => CurrentHealth <= 0;

    // ===== 法力水晶管理 =====

    public void SpendMana(int amount)
    {
        CurrentMana = Math.Max(0, CurrentMana - amount);
        OnManaChanged?.Invoke(CurrentMana, MaxMana);
    }

    public void GainMana(int amount)
    {
        CurrentMana = Math.Min(MaxMana + amount, CurrentMana + amount);
        OnManaChanged?.Invoke(CurrentMana, MaxMana);
    }

    public void ResetMana()
    {
        CurrentMana = MaxMana;
        OnManaChanged?.Invoke(CurrentMana, MaxMana);
    }

    public void SetMana(int current, int max)
    {
        MaxMana = max;
        CurrentMana = current;
        OnManaChanged?.Invoke(CurrentMana, MaxMana);
    }

    public bool CanSpendMana(int amount)
    {
        return CurrentMana >= amount;
    }

    // ===== 牌堆操作 =====

    public void DrawCards(int count)
    {
        CombatDeckState.DrawCards(count);
    }

    public void DiscardCard(OdysseyCards.Card.Card card)
    {
        CombatDeckState.DiscardCard(card);
    }

    public void RemoveFromHand(OdysseyCards.Card.Card card)
    {
        CombatDeckState.RemoveFromHand(card);
    }

    public void ReturnToDrawPile(OdysseyCards.Card.Card card)
    {
        CombatDeckState.ReturnToDrawPile(card);
    }

    public void ShuffleDrawPile()
    {
        CombatDeckState.ShuffleDrawPile();
    }

    public void DiscardHand()
    {
        CombatDeckState.DiscardHand();
    }

    // ===== 回合管理 =====

    /// <summary>
    /// 开始回合：增加最大水晶（上限10），填满当前水晶。
    /// </summary>
    public void StartTurn()
    {
        if (MaxMana < MaxManaCrystals)
        {
            MaxMana++;
        }
        CurrentMana = MaxMana;
        OnManaChanged?.Invoke(CurrentMana, MaxMana);
    }

    public void EndTurn()
    {
    }

    public void ResetFatigue()
    {
        CombatDeckState.ResetFatigue();
    }

    public void ClearPiles()
    {
        CombatDeckState.ClearPiles();
    }

    /// <summary>
    /// 从牌堆设置抽牌堆。
    /// </summary>
    public void SetupDrawPile()
    {
        var cards = CreateDrawPileFromDeck();
        CombatDeckState.SetupDrawPile(cards);
    }

    /// <summary>
    /// 从 Deck 创建运行时卡牌列表。
    /// </summary>
    private List<OdysseyCards.Card.Card> CreateDrawPileFromDeck()
    {
        var cards = new List<OdysseyCards.Card.Card>();
        foreach (var cardData in Deck.Cards)
        {
            var card = new OdysseyCards.Card.Card(cardData);
            cards.Add(card);
        }
        return cards;
    }
}
