using Godot;
using System;
using System.Collections.Generic;
using OdysseyCards.Character;
using OdysseyCards.Localization;
using OdysseyCards.Roguelike;

namespace OdysseyCards.Core;

/// <summary>
/// 全局游戏状态管理器（Autoload 单例）。
/// 管理玩家进度、牌堆状态和跨场景持久化。
/// 已移除对 Application/Infrastructure 层的依赖。
/// </summary>
public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    private string _currentLanguage = "zh";

    public static string CurrentLanguage => Instance?._currentLanguage ?? "zh";

    /// <summary>
    /// 语言变更事件。所有场景通过此事件感知语言切换并刷新 UI。
    /// GameManager 是语言变更的唯一入口。
    /// </summary>
    public event Action<string> LanguageChanged;

    /// <summary>
    /// 玩家的牌堆定义。
    /// </summary>
    public Deck PlayerDeck => _playerDeck;
    private Deck _playerDeck;

    /// <summary>
    /// 当前玩家角色实例。
    /// </summary>
    public Player CurrentPlayer { get; private set; }

    /// <summary>
    /// 玩家英雄当前生命值（跨战斗保存）。
    /// </summary>
    public int PlayerHealth { get; set; } = 30;

    /// <summary>
    /// 玩家英雄最大生命值。
    /// </summary>
    public int PlayerMaxHealth { get; set; } = 30;

    /// <summary>
    /// 牌堆变化事件。
    /// </summary>
    public event Action OnDeckChanged;

    /// <summary>
    /// 当前游戏运行状态（一次冒险的完整生命周期）。
    /// 跨战斗持久化，由 StartNewRun 创建，Boss 击败或玩家死亡时结束。
    /// </summary>
    public GameRunState? RunState { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        Localization.Localization.Initialize();
        Localization.Localization.SetLanguage(_currentLanguage);
        GD.Print("[GameManager] _Ready called, Instance set");
    }

    public void SetLanguage(string language)
    {
        if (string.IsNullOrEmpty(language) || _currentLanguage == language)
        {
            return;
        }

        _currentLanguage = language;
        Localization.Localization.SetLanguage(language);
        LanguageChanged?.Invoke(language);
        GD.Print($"[GameManager] Language changed to: {language}");
    }

    public void ToggleLanguage()
    {
        string newLang = _currentLanguage == "en" ? "zh" : "en";
        SetLanguage(newLang);
    }

    /// <summary>
    /// 创建新玩家。
    /// </summary>
    public void CreateNewPlayer()
    {
        GD.Print("[GameManager] CreateNewPlayer called");

        CurrentPlayer = new Player();
        CurrentPlayer.CharacterName = "Ironclad";
        CurrentPlayer.InitializeHealth(30);
        CurrentPlayer.SetMana(0, 3);

        var startingDeck = CreateStartingDeck();
        CurrentPlayer.Initialize(startingDeck);
        _playerDeck = startingDeck;

        GD.Print($"[GameManager] Player created: {CurrentPlayer.CharacterName}, deck size: {startingDeck.CardCount}");
    }

    /// <summary>
    /// 添加卡牌到牌堆。
    /// </summary>
    public bool AddCardToDeck(CardData card)
    {
        if (_playerDeck == null || card == null)
            return false;

        bool success = _playerDeck.AddCardWithCheck(card);
        if (success)
        {
            OnDeckChanged?.Invoke();
        }
        return success;
    }

    /// <summary>
    /// 从牌堆移除卡牌。
    /// </summary>
    public bool RemoveCardFromDeck(CardData card)
    {
        if (_playerDeck == null || card == null)
            return false;

        _playerDeck.RemoveCard(card);
        OnDeckChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 创建起始牌堆（使用硬编码的初始卡牌数据）。
    /// 注意：需要 CardData .tres 文件存在才能正常工作。
    /// </summary>
    private Deck CreateStartingDeck()
    {
        var deck = new Deck();
        var cards = new List<CardData>(12);

        string[] cardPaths =
        {
            // 基础卡牌（各2张）
            "res://Resources/Cards/Spell_Alert.tres",
            "res://Resources/Cards/Spell_Assault.tres",
            "res://Resources/Cards/Spell_Strike.tres",
            "res://Resources/Cards/Minion_18thRegiment.tres",
            "res://Resources/Cards/Minion_DetectiveSquad.tres",
            "res://Resources/Cards/Minion_LianshuScout.tres",
            "res://Resources/Cards/Domain_Zhijian.tres",
            "res://Resources/Cards/Domain_InfiniteFire.tres",
        };

        // 新卡牌（各1张）
        string[] newCardPaths =
        {
            "res://Resources/Cards/Spell_Ignite.tres",
            "res://Resources/Cards/Spell_Longtermism.tres",
            "res://Resources/Cards/Domain_UnlimitedPotential.tres",
        };

        foreach (var path in cardPaths)
        {
            if (!ResourceLoader.Exists(path))
            {
                GD.PrintErr($"[GameManager] 警告：未找到 {path}");
                continue;
            }

            var cardData = GD.Load<CardData>(path);
            if (cardData == null)
            {
                GD.PrintErr($"[GameManager] 警告：加载失败 {path}");
                continue;
            }

            cards.Add(cardData);
            cards.Add(cardData);
        }

        foreach (var path in newCardPaths)
        {
            if (!ResourceLoader.Exists(path))
            {
                GD.PrintErr($"[GameManager] 警告：未找到 {path}");
                continue;
            }

            var cardData = GD.Load<CardData>(path);
            if (cardData == null)
            {
                GD.PrintErr($"[GameManager] 警告：加载失败 {path}");
                continue;
            }

            cards.Add(cardData);
        }

        deck.Initialize(cards);
        GD.Print($"[GameManager] 起始牌堆已创建，共 {cards.Count} 张牌");
        return deck;
    }

    /// <summary>
    /// 开始一次新的冒险运行。
    /// 创建玩家角色、初始化 RunState 和第一位面。
    /// </summary>
    public void StartNewRun()
    {
        GD.Print("[GameManager] 开始新冒险！");

        CreateNewPlayer();
        PlayerHealth = 30;
        PlayerMaxHealth = 30;

        RunState = new GameRunState();
        RunState.OnRunCompleted += () => GD.Print("[GameManager] 冒险完成！");
        RunState.OnRunFailed += () => GD.Print("[GameManager] 冒险失败！");
        RunState.StartNewRun();

        GD.Print($"[GameManager] 冒险已初始化 — {RunState.CurrentPlane?.PlaneName}，" +
                  $"{RunState.TotalLayers} 层，首层 {RunState.CurrentChoiceCount} 个可选房间");
    }

    /// <summary>
    /// 重置整局游戏。</summary>
    public void ResetRun()
    {
        PlayerHealth = 30;
        PlayerMaxHealth = 30;
        CreateNewPlayer();
    }

    /// <summary>
    /// 保存英雄生命值（跨战斗）。
    /// </summary>
    public void SavePlayerHealth(int currentHealth, int maxHealth)
    {
        PlayerHealth = Mathf.Min(currentHealth, maxHealth);
        PlayerMaxHealth = maxHealth;
    }

    /// <summary>
    /// 获取保存的英雄生命值。
    /// </summary>
    public (int currentHealth, int maxHealth) GetPlayerHealth()
    {
        return (PlayerHealth, PlayerMaxHealth);
    }

    /// <summary>
    /// 重置英雄生命值为默认值。
    /// </summary>
    public void ResetPlayerHealth()
    {
        PlayerHealth = 30;
        PlayerMaxHealth = 30;
    }
}
