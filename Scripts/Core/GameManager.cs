using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Character;
using OdysseyCards.Relic;
using OdysseyCards.Roguelike;

namespace OdysseyCards.Core;

/// <summary>
/// 全局游戏状态管理器（Autoload 单例）。
/// 管理玩家进度、牌堆状态、卡牌收藏和跨场景持久化。
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
    /// 玩家的牌堆定义（运行中构建）。
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
    /// 收藏变化事件（解锁新卡、牌组增删时触发）。
    /// </summary>
    public event Action OnCollectionChanged;

    /// <summary>
    /// 当前游戏运行状态（一次冒险的完整生命周期）。
    /// </summary>
    public GameRunState? RunState { get; private set; }

    /// <summary>
    /// 战斗覆盖——由 /fight 命令设置，允许跳过 Roguelike 地图直接开战。
    /// BootstrapCombat 优先读取此值，其次读取 RunState。
    /// 进入战斗后自动清空。
    /// </summary>
    public IReadOnlyList<AI.EnemyEncounter>? FightOverride { get; set; }

    /// <summary>
    /// 房间类型覆盖——由 /room 命令设置，强制当前层使用指定房间类型。
    /// MapUI 和 CombatManager 优先读取此值。消费后自动清空。
    /// 参考 STS2 RoomConsoleCmd 模式。
    /// </summary>
    public Roguelike.RoomType? RoomTypeOverride { get; set; }

    /// <summary>
    /// 事件 ID 覆盖——配合 RoomTypeOverride=Event 使用，指定具体叙事事件。
    /// null 时随机选择。
    /// </summary>
    public string? EventIdOverride { get; set; }

	/// <summary>
	/// 藏品管理器——持有玩家所有藏品的列表，跨战斗持久化。
	/// </summary>
	public RelicManager Relics { get; private set; } = new();

	/// <summary>
	/// 当前冒险中的金币数量。每局重置为0，击败敌人获得，在商店消费。
	/// </summary>
	public int RunGold { get; set; }

	/// <summary>
	/// 获得金币。
	/// </summary>
	public void AddGold(int amount)
	{
		if (amount <= 0) return;
		RunGold += amount;
		GD.Print($"[GameManager] 获得 {amount} 金币（当前 {RunGold}）");
	}

	/// <summary>
	/// 消费金币。返回是否成功（余额不足时失败）。
	/// </summary>
	public bool SpendGold(int amount)
	{
		if (RunGold < amount) return false;
		RunGold -= amount;
		GD.Print($"[GameManager] 消费 {amount} 金币（剩余 {RunGold}）");
		return true;
	}

	/// <summary>
	/// 战斗开始时牌组的快照（只读，用于信息界面展示"当前卡组"）。
	/// 此快照在每场战斗开始前由 CombatManager 调用 SnapshotCombatStartDeck() 更新。
	/// 战斗外为 null。
	/// </summary>
	public Deck? CombatStartDeckSnapshot { get; private set; }

	// ===== 收藏与持久化 =====

    /// <summary>
    /// 已解锁的卡牌 ID 集合。
    /// Demo 阶段默认解锁全部 14 张卡。
    /// </summary>
    public HashSet<string> OwnedCardIds { get; private set; } = new();

    /// <summary>
    /// 所有已保存的牌组。
    /// </summary>
    public List<Deck> Decks { get; private set; } = new();

    /// <summary>
    /// 当前激活的牌组索引。-1 表示无。
    /// </summary>
    public int ActiveDeckIndex { get; set; } = -1;

    /// <summary>
    /// 表情空闲计时器时长（秒）。玩家不出牌超过此时间后敌人发送嘲讽表情。
    /// </summary>
    public float EmoteIdleTimeSeconds { get; set; } = 5.0f;

    /// <summary>
    /// 空闲计时器随机浮动的最小倍率（clamped：0.1 ≤ min ≤ max）。
    /// </summary>
    public float EmoteIdleVariationMin { get; set; } = 0.7f;

    /// <summary>
    /// 空闲计时器随机浮动的最大倍率（clamped：min ≤ max ≤ 3.0）。
    /// </summary>
    public float EmoteIdleVariationMax { get; set; } = 1.3f;

    /// <summary>
    /// 所有卡牌资源的注册表（Id → CardData）。
    /// </summary>
    private readonly Dictionary<string, CardData> _allCardRegistry = new();

    /// <summary>
    /// 卡牌资源路径硬编码列表——作为 DirAccess 在导出版本中失败时的回退方案。
    /// 新增卡牌时需同步更新此列表。
    /// </summary>
    internal static readonly string[] CardResourcePaths =
    {
        "res://Resources/Cards/Domain_FlyingAway.tres",
        "res://Resources/Cards/Domain_IdolTwilight.tres",
        "res://Resources/Cards/Domain_InfiniteFire.tres",
        "res://Resources/Cards/Domain_UnlimitedPotential.tres",
        "res://Resources/Cards/Domain_Zhijian.tres",
        "res://Resources/Cards/Minion_18thRegiment.tres",
        "res://Resources/Cards/Minion_CentipedeAA.tres",
        "res://Resources/Cards/Minion_CentipedeSiege.tres",
        "res://Resources/Cards/Minion_DetectiveSquad.tres",
        "res://Resources/Cards/Minion_KnightType1.tres",
        "res://Resources/Cards/Minion_LianshuScout.tres",
        "res://Resources/Cards/Minion_Mech_Lancer.tres",
        "res://Resources/Cards/Minion_Roach.tres",
        "res://Resources/Cards/Minion_Slime.tres",
        "res://Resources/Cards/Minion_Tombstone.tres",
        "res://Resources/Cards/Minion_WhatTheDogDoing.tres",
        "res://Resources/Cards/Spell_Alert.tres",
        "res://Resources/Cards/Spell_Animosity.tres",
        "res://Resources/Cards/Spell_Assault.tres",
        "res://Resources/Cards/Spell_BaitTactics.tres",
        "res://Resources/Cards/Spell_BladeCrisis.tres",
        "res://Resources/Cards/Spell_Discover.tres",
        "res://Resources/Cards/Spell_Expose.tres",
        "res://Resources/Cards/Spell_FullAssault.tres",
        "res://Resources/Cards/Spell_Ignite.tres",
        "res://Resources/Cards/Spell_Longtermism.tres",
        "res://Resources/Cards/Spell_MoonFishing.tres",
        "res://Resources/Cards/Spell_NanoCorpseArt.tres",
        "res://Resources/Cards/Spell_Plan.tres",
        "res://Resources/Cards/Spell_Shoushen.tres",
        "res://Resources/Cards/Spell_Strike.tres",
        "res://Resources/Cards/Spell_WhiteLegion.tres",
        "res://Resources/Cards/Status_Fatigue.tres",
    };

    /// <summary>
    /// JSON 持久化管理器。
    /// </summary>
    private readonly SaveDataManager _saveManager = new();

    /// <summary>
    /// 当前激活的牌组（用于带进战斗）。
    /// 如果没有选中牌组则返回 null。
    /// </summary>
    public Deck? ActiveDeck
    {
        get
        {
            if (ActiveDeckIndex < 0 || ActiveDeckIndex >= Decks.Count)
                return null;
            return Decks[ActiveDeckIndex];
        }
    }

    // ===== 生命周期 =====

    public override void _Ready()
    {
        Instance = this;
        Localization.Localization.Initialize();
        Localization.Localization.SetLanguage(_currentLanguage);
        GD.Print($"[GameManager] _Ready — language: {_currentLanguage}");

        // 1. 加载所有卡牌资源到注册表
        LoadAllCardResources();
        GD.Print($"[GameManager] 注册表卡片数: {_allCardRegistry.Count}, IDs: [{string.Join(", ", _allCardRegistry.Keys.Take(3))}...]");

        // 2. 加载存档
        LoadFromDisk();
        GD.Print($"[GameManager] 存档加载后 — OwnedCardIds: {OwnedCardIds.Count}, Decks: {Decks.Count}, ActiveDeckIndex: {ActiveDeckIndex}");

        // 3. Demo 默认：如果没有存档，解锁全部卡牌 + 创建默认牌组
        if (OwnedCardIds.Count == 0)
        {
            GD.Print("[GameManager] OwnedCardIds 为空，执行首次初始化...");
            UnlockAllCards();
            CreateDefaultDeck();
            SaveToDisk();
            GD.Print($"[GameManager] 首次初始化完成 — OwnedCardIds: {OwnedCardIds.Count}, Decks: {Decks.Count}, Deck[0] cards: {Decks[0]?.CardCount ?? 0}");
        }
        else
        {
            GD.Print($"[GameManager] 从存档恢复 — OwnedCardIds: {OwnedCardIds.Count}, Decks: {Decks.Count}");
            // 确保至少有一个牌组
            if (Decks.Count == 0)
            {
                GD.Print("[GameManager] 警告：存档中无牌组，创建默认牌组");
                CreateDefaultDeck();
                SaveToDisk();
            }
            if (ActiveDeckIndex < 0 || ActiveDeckIndex >= Decks.Count)
            {
                GD.Print($"[GameManager] 修正 ActiveDeckIndex: {ActiveDeckIndex} → 0");
                ActiveDeckIndex = 0;
            }
        }

        // 4. 最终验证修复
        VerifyAndRepairCollection();

        GD.Print("[GameManager] _Ready — 初始化完成");
    }

    /// <summary>
    /// 验证并修复收藏数据：确保所有注册表中的卡牌都在 OwnedCardIds 中。
    /// 如果激活牌组无效，创建默认牌组。
    /// </summary>
    public void VerifyAndRepairCollection()
    {
        // 修复 OwnedCardIds：确保所有注册表卡牌都已解锁
        int addedCount = 0;
        foreach (var id in _allCardRegistry.Keys)
        {
            if (OwnedCardIds.Add(id))
                addedCount++;
        }
        if (addedCount > 0)
            GD.Print($"[GameManager] 修复：解锁了 {addedCount} 张缺失的卡牌");

        // 修复牌组：确保存在且激活
        if (Decks.Count == 0)
        {
            GD.Print("[GameManager] 修复：创建默认牌组");
            CreateDefaultDeck();
        }

        // Day1 重构：不再静默截断超限牌组。
        // 改为诊断+警告——超限牌组由 UI 层标记为 invalid 并提示用户手动处理。
        foreach (var deck in Decks)
        {
            var result = DeckValidityService.DiagnoseDeck(deck);
            if (!result.IsValid)
            {
                GD.PushWarning($"[GameManager] 牌组「{deck.Name}」状态异常：" +
                               $"{result.DefaultMessage} ({result.CurrentCount} 张)");
            }
        }

        if (ActiveDeckIndex < 0 || ActiveDeckIndex >= Decks.Count)
        {
            GD.Print($"[GameManager] 修复：ActiveDeckIndex {ActiveDeckIndex} → 0");
            ActiveDeckIndex = 0;
        }

        SaveToDisk();
    }

    // ===== 卡牌注册表 =====

    /// <summary>
    /// 扫描 Resources/Cards/ 目录，将所有 .tres 卡牌加载到注册表。
    /// </summary>
    private void LoadAllCardResources()
    {
        _allCardRegistry.Clear();

        // 先尝试 DirAccess（编辑器内正常工作）
        bool loadedViaDir = TryLoadCardsViaDirAccess();

        // 回退：硬编码路径列表（导出版本中 DirAccess 可能失败）
        if (!loadedViaDir || _allCardRegistry.Count == 0)
        {
            if (!loadedViaDir)
                GD.Print("[GameManager] DirAccess 枚举失败，使用硬编码卡牌路径回退");
            LoadCardsFromPaths(CardResourcePaths);
        }

        GD.Print($"[GameManager] 卡牌注册表已初始化 — {_allCardRegistry.Count} 张卡牌");
    }

    /// <summary>
    /// 尝试通过 DirAccess 枚举加载卡牌。在导出版本中可能失败。
    /// </summary>
    /// <returns>是否成功加载了卡牌</returns>
    private bool TryLoadCardsViaDirAccess()
    {
        using var dir = DirAccess.Open("res://Resources/Cards/");
        if (dir == null)
            return false;

        dir.ListDirBegin();
        string fileName;
        int count = 0;
        while ((fileName = dir.GetNext()) != "")
        {
            if (dir.CurrentIsDir()) continue;
            if (!fileName.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)) continue;

            var fullPath = $"res://Resources/Cards/{fileName}";
            var cardData = GD.Load<CardData>(fullPath);
            if (cardData == null || string.IsNullOrEmpty(cardData.Id)) continue;

            _allCardRegistry[cardData.Id] = cardData;
            count++;
        }
        dir.ListDirEnd();
        return count > 0;
    }

    /// <summary>
    /// 从硬编码路径列表加载卡牌资源（导出版本回退方案）。
    /// 新增卡牌时需同步更新 <see cref="CardResourcePaths"/>。
    /// </summary>
    private void LoadCardsFromPaths(string[] paths)
    {
        int count = 0;
        foreach (var path in paths)
        {
            if (!ResourceLoader.Exists(path))
            {
                GD.PushWarning($"[GameManager] 卡牌资源不存在: {path}");
                continue;
            }

            var cardData = GD.Load<CardData>(path);
            if (cardData == null || string.IsNullOrEmpty(cardData.Id))
            {
                GD.PushWarning($"[GameManager] 卡牌资源加载失败: {path}");
                continue;
            }

            _allCardRegistry[cardData.Id] = cardData;
            count++;
        }
        GD.Print($"[GameManager] 从硬编码路径加载了 {count} 张卡牌");
    }

    /// <summary>
    /// 通过 Id 从注册表获取卡牌数据。
    /// </summary>
    public CardData? GetCardById(string id)
    {
        _allCardRegistry.TryGetValue(id, out var card);
        return card;
    }

    /// <summary>
    /// 获取所有已加载的卡牌资源。
    /// </summary>
    public IReadOnlyList<CardData> GetAllCards()
    {
        return _allCardRegistry.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// 获取所有已解锁且可出现在奖励中的卡牌。
    /// </summary>
    public List<CardData> GetRewardEligibleCards()
    {
        return _allCardRegistry.Values
            .Where(c => OwnedCardIds.Contains(c.Id) && c.Rarity.CanAppearInReward())
            .ToList();
    }

    // ===== 收藏管理 =====

    /// <summary>
    /// 解锁全部卡牌（demo 用）。
    /// </summary>
    public void UnlockAllCards()
    {
        OwnedCardIds.Clear();
        foreach (var id in _allCardRegistry.Keys)
        {
            OwnedCardIds.Add(id);
        }
        GD.Print($"[GameManager] 已解锁全部 {OwnedCardIds.Count} 张卡牌");
    }

    /// <summary>
    /// 解锁指定卡牌。
    /// </summary>
    public void UnlockCard(string cardId)
    {
        if (!_allCardRegistry.ContainsKey(cardId))
        {
            GD.PushWarning($"[GameManager] 尝试解锁未知卡牌: {cardId}");
            return;
        }

        if (OwnedCardIds.Add(cardId))
        {
            GD.Print($"[GameManager] 解锁卡牌: {cardId}");
            OnCollectionChanged?.Invoke();
        }
    }

    // ===== 牌组管理 =====

    /// <summary>
    /// 创建默认牌组（首次启动时自动调用）。
    /// 使用硬编码的起始牌堆。
    /// </summary>
    private void CreateDefaultDeck()
    {
        Decks.Clear();
        var defaultDeck = CreateStartingDeck();
        defaultDeck.Name = Localization.Localization.T("ui.collection.default_deck_name", "默认牌组");
        Decks.Add(defaultDeck);
        ActiveDeckIndex = 0;
        GD.Print("[GameManager] 默认牌组已创建");
    }

    /// <summary>
    /// 创建新牌组并设为激活。
    /// </summary>
    public Deck CreateDeck(string name)
    {
        var deck = new Deck { Name = name };
        Decks.Add(deck);
        ActiveDeckIndex = Decks.Count - 1;
        OnCollectionChanged?.Invoke();
        return deck;
    }

    /// <summary>
    /// 删除指定牌组。
    /// </summary>
    public bool DeleteDeck(int index)
    {
        if (index < 0 || index >= Decks.Count) return false;

        Decks.RemoveAt(index);

        if (ActiveDeckIndex == index)
            ActiveDeckIndex = Decks.Count > 0 ? 0 : -1;
        else if (ActiveDeckIndex > index)
            ActiveDeckIndex--;

        OnCollectionChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 设置当前激活的牌组索引。
    /// </summary>
    public void SetActiveDeck(int index)
    {
        if (index >= 0 && index < Decks.Count)
        {
            ActiveDeckIndex = index;
            OnCollectionChanged?.Invoke();
        }
    }

    /// <summary>
    /// 获取当前激活牌组的卡牌数量统计（按卡牌 Id 分组）。
    /// </summary>
    public Dictionary<string, int> GetActiveDeckCardCounts()
    {
        var counts = new Dictionary<string, int>();
        var deck = ActiveDeck;
        if (deck == null) return counts;

        foreach (var card in deck.Cards)
        {
            if (counts.ContainsKey(card.Id))
                counts[card.Id]++;
            else
                counts[card.Id] = 1;
        }
        return counts;
    }

    // ===== 卡牌添加/移除（运行中） =====

    /// <summary>
    /// 构筑时添加卡牌到当前牌组（带上限检查）。
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
    /// 战斗中通过奖励添加卡牌（带上限 999 检查）。
    /// 同时解锁该卡牌。
    /// </summary>
    public bool AddCardToDeckInCombat(CardData card)
    {
        if (_playerDeck == null || card == null)
            return false;

        bool success = _playerDeck.AddCardInCombat(card);
        if (success)
        {
            UnlockCard(card.Id);
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
    /// 向激活的收藏牌组添加卡牌（构筑界面用）。
    /// </summary>
    public bool AddCardToActiveCollectionDeck(CardData card)
    {
        var deck = ActiveDeck;
        if (deck == null) return false;
        deck.AddCard(card);
        return true;
    }

    /// <summary>
    /// 从激活的收藏牌组移除卡牌（构筑界面用）。
    /// </summary>
    public bool RemoveCardFromActiveCollectionDeck(CardData card)
    {
        var deck = ActiveDeck;
        if (deck == null) return false;
        deck.RemoveCard(card);
        return true;
    }

    // ===== 牌堆创建 =====

    /// <summary>
    /// 创建起始牌堆。
    /// 优先使用玩家选中的激活牌组；若无效则回退到硬编码默认牌堆。
    /// </summary>
    private Deck CreateStartingDeck()
    {
        // 优先使用激活牌组
        var activeDeck = ActiveDeck;
        if (activeDeck != null && activeDeck.CardCount >= Deck.MinCards)
        {
            GD.Print($"[GameManager] 使用激活牌组「{activeDeck.Name}」— {activeDeck.CardCount} 张");
            return activeDeck.Clone();
        }

        // 回退到硬编码默认牌堆
        GD.Print("[GameManager] 回退到硬编码起始牌堆");
        return CreateHardcodedStartingDeck();
    }

    /// <summary>
    /// 硬编码的起始牌堆（回退用）。
    /// </summary>
    private static Deck CreateHardcodedStartingDeck()
    {
        var deck = new Deck();
        var cards = new List<CardData>(12);

        string[] cardPaths =
        {
            "res://Resources/Cards/Spell_Alert.tres",
            "res://Resources/Cards/Spell_Assault.tres",
            "res://Resources/Cards/Spell_Strike.tres",
            "res://Resources/Cards/Minion_18thRegiment.tres",
            "res://Resources/Cards/Minion_DetectiveSquad.tres",
            "res://Resources/Cards/Minion_LianshuScout.tres",
            "res://Resources/Cards/Domain_Zhijian.tres",
            "res://Resources/Cards/Domain_InfiniteFire.tres",
        };

        string[] newCardPaths =
        {
            "res://Resources/Cards/Spell_Ignite.tres",
            "res://Resources/Cards/Spell_Longtermism.tres",
            "res://Resources/Cards/Domain_UnlimitedPotential.tres",
            "res://Resources/Cards/Spell_Discover.tres",
            "res://Resources/Cards/Minion_KnightType1.tres",
            "res://Resources/Cards/Minion_CentipedeSiege.tres",
            "res://Resources/Cards/Minion_Tombstone.tres",
            "res://Resources/Cards/Spell_NanoCorpseArt.tres",
            "res://Resources/Cards/Domain_IdolTwilight.tres",
            "res://Resources/Cards/Spell_MoonFishing.tres",
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

        // 移动端/首次启动默认牌组必须满足构筑上限 20 张。
        // 当前回退起始牌组原始列表共有 26 张（8*2 + 10），会直接导致主菜单无法开始游戏。
        // 这里截断到前 20 张，保证新安装用户开局可玩。
        deck.Initialize(cards.Take(Deck.MaxDeckSize).ToList());
        return deck;
    }

    // ===== 玩家管理 =====

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
        CurrentPlayer.HeroPower = new Card.HeroPowers.IronWillHeroPower();

        var startingDeck = CreateStartingDeck();
        CurrentPlayer.Initialize(startingDeck);
        _playerDeck = startingDeck;

        GD.Print($"[GameManager] Player created: {CurrentPlayer.CharacterName}, deck size: {startingDeck.CardCount}");
    }

	/// <summary>
	/// 保存当前牌组快照（战斗开始时调用）。
	/// 深拷贝当前 PlayerDeck 的 CardData 列表，用于信息界面展示"当前卡组"。
	/// </summary>
	public void SnapshotCombatStartDeck()
	{
		CombatStartDeckSnapshot = _playerDeck?.Clone();
	}

	/// <summary>
	/// 清除战斗牌组快照（战斗结束时调用）。
	/// </summary>
	public void ClearCombatDeckSnapshot()
	{
		CombatStartDeckSnapshot = null;
	}

	/// <summary>
	/// 开始一次新的冒险运行。
	/// </summary>
    public void StartNewRun()
    {
        GD.Print("[GameManager] 开始新冒险！");

        CreateNewPlayer();
        PlayerHealth = 30;
        PlayerMaxHealth = 30;
        RunGold = 0;

        RunState = new GameRunState();
        RunState.OnRunCompleted += () => GD.Print("[GameManager] 冒险完成！");
        RunState.OnRunFailed += () => GD.Print("[GameManager] 冒险失败！");
        RunState.StartNewRun();

        GD.Print($"[GameManager] 冒险已初始化 — {RunState.CurrentPlane?.PlaneName}，" +
                  $"{RunState.TotalLayers} 层，首层 {RunState.CurrentChoiceCount} 个可选房间");
    }

    /// <summary>
    /// 重置整局游戏。
    /// </summary>
    public void ResetRun()
    {
        PlayerHealth = 30;
        PlayerMaxHealth = 30;
        CreateNewPlayer();
    }

    /// <summary>
    /// 检查当前激活牌组是否满足最小卡牌数要求。
    /// </summary>
    public bool IsActiveDeckValid()
    {
        var deck = ActiveDeck;
        return deck != null && deck.MeetsMinimum() && !deck.IsOverLimit();
    }

    // ===== 生命值管理 =====

    public void SavePlayerHealth(int currentHealth, int maxHealth)
    {
        PlayerHealth = Mathf.Min(currentHealth, maxHealth);
        PlayerMaxHealth = maxHealth;
    }

    public (int currentHealth, int maxHealth) GetPlayerHealth()
    {
        return (PlayerHealth, PlayerMaxHealth);
    }

    public void ResetPlayerHealth()
    {
        PlayerHealth = 30;
        PlayerMaxHealth = 30;
    }

    // ===== 语言管理 =====

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

    // ===== 持久化 =====

    /// <summary>
    /// 将当前状态保存到磁盘。
    /// </summary>
    public void SaveToDisk()
    {
        var data = new GameSaveData
        {
            Version = 1,
            Language = _currentLanguage,
            OwnedCardIds = OwnedCardIds.ToList(),
            Decks = Decks.Select(d => DeckSaveData.FromDeck(d)).ToList(),
            ActiveDeckIndex = ActiveDeckIndex,
            EmoteIdleTimeSeconds = EmoteIdleTimeSeconds,
            EmoteIdleVariationMin = EmoteIdleVariationMin,
            EmoteIdleVariationMax = EmoteIdleVariationMax,
            RunGold = RunGold,
        };

        _saveManager.Save(data);
    }

    /// <summary>
    /// 从磁盘加载存档。
    /// </summary>
    public void LoadFromDisk()
    {
        var data = _saveManager.Load();
        if (data == null) return;

        // 恢复语言
        if (!string.IsNullOrEmpty(data.Language))
        {
            _currentLanguage = data.Language;
            Localization.Localization.SetLanguage(_currentLanguage);
        }

        // 恢复收藏
        OwnedCardIds = new HashSet<string>(data.OwnedCardIds);

        // 恢复牌组
        Decks.Clear();
        foreach (var deckData in data.Decks)
        {
            var deck = deckData.ToDeck(this);
            if (deck != null)
                Decks.Add(deck);
        }

        ActiveDeckIndex = data.ActiveDeckIndex;
        EmoteIdleTimeSeconds = data.EmoteIdleTimeSeconds;
        EmoteIdleVariationMin = data.EmoteIdleVariationMin;
        EmoteIdleVariationMax = data.EmoteIdleVariationMax;
        RunGold = data.RunGold;

        GD.Print($"[GameManager] 存档已加载 — {Decks.Count} 个牌组，" +
                  $"{OwnedCardIds.Count} 张已解锁");
    }

    /// <summary>
    /// 导出当前激活牌组到指定路径。
    /// </summary>
    public bool ExportActiveDeck(string path)
    {
        var deck = ActiveDeck;
        if (deck == null) return false;
        var data = DeckSaveData.FromDeck(deck);
        return _saveManager.ExportDeck(data, path);
    }

    /// <summary>
    /// 从指定路径导入牌组并添加到牌组列表。
    /// </summary>
    public bool ImportDeck(string path)
    {
        var data = _saveManager.ImportDeck(path);
        if (data == null) return false;

        var deck = data.ToDeck(this);
        if (deck == null) return false;

        Decks.Add(deck);
        ActiveDeckIndex = Decks.Count - 1;
        OnCollectionChanged?.Invoke();
        return true;
    }
}
