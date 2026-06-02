using Godot;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Character;
using OdysseyCards.Roguelike;
using OdysseyCards.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdysseyCards.Combat;

/// <summary>
/// 战斗管理器 — 炉石传说风格的回合制战斗编排器。
/// 负责战场管理、回合流转、随从召唤与攻击、法术施放以及胜负判定。
/// 继承 Godot.Node 以接入场景树，支持信号驱动的 UI 响应。
/// </summary>
public partial class CombatManager : Node
{
    // ===== 单例 =====

    /// <summary>
    /// 战斗管理器全局单例。
    /// </summary>
    public static CombatManager Instance { get; private set; }

    // ===== 内部引用 =====

    /// <summary>
    /// 玩家英雄底层的指挥官核心。
    /// 保留引用以便访问 Hero 类未暴露的方法（如 Heal）。
    /// </summary>
    private CommanderCore _playerCore;

    // ===== 公开属性 =====

    /// <summary>
    /// 战场管理器，管理双方各 5 个随从槽位。
    /// </summary>
    public Board Board { get; private set; }

    /// <summary>
    /// 游戏状态，追踪战斗阶段、回合数和法力水晶。
    /// </summary>
    public GameState State { get; private set; }

    /// <summary>
    /// 玩家英雄。包装 CommanderCore，提供法力值、生命值和护甲管理。
    /// </summary>
    public Hero PlayerHero { get; private set; }

    /// <summary>
    /// 敌方英雄（向后兼容单一敌人）。多敌人时返回 EnemyUnits[0].Body。
    /// </summary>
    public Hero EnemyHero => EnemyUnits[0].Body;

    /// <summary>
    /// 所有敌方战斗单位列表。每个 EnemyUnit 包含一个 Hero 身体和一个 EnemyEncounter 大脑。
    /// </summary>
    public IReadOnlyList<EnemyUnit> EnemyUnits { get; private set; } = new List<EnemyUnit>();

    /// <summary>
    /// 武器主动技能的目标。IonPulse 等需要选择目标的主动技能在 Execute 时读取此属性。
    /// 由 CombatUI 在进入技能目标选择模式时设置，Execute 完成后清除。
    /// </summary>
    public IDamageTarget? ActiveSkillTarget { get; set; }

    /// <summary>
    /// 玩家角色（Godot Node 引用，用于场景树交互）。
    /// </summary>
    public Player Player { get; private set; }

    /// <summary>
    /// 当前敌方 AI 遭遇实例（向后兼容单一敌人）。
    /// </summary>
    private EnemyEncounter _currentEnemy => EnemyUnits[0].Brain;

    /// <summary>
    /// 敌方意图变化事件（参数为意图描述文本）。
    /// </summary>
    public event Action<string>? OnEnemyIntentChanged;

    /// <summary>
    /// 战斗状态变更事件——在随从部署/移除、英雄受伤等任何可能影响意图显示的
    /// 状态变更时触发。UI 监听此事件来刷新意图标签和目标高亮。
    /// 参考 STS2 的 CombatStateTracker.CombatStateChanged。
    /// </summary>
    public event Action? OnCombatStateChanged;

    /// <summary>
    /// 游戏结束事件（参数为 true=胜利, false=失败）。
    /// </summary>
    public event Action<bool>? OnGameOver;

    /// <summary>
    /// 获取当前敌方遭遇的动态意图（含实时目标和伤害计算）。
    /// UI 在 <see cref="OnCombatStateChanged"/> 触发时调用此方法刷新显示。
    /// </summary>
    /// <returns>包含动态 TargetSelector 和 DamageCalc 的意图结构体</returns>
    /// <summary>
    /// 获取指定敌方单位的当前意图。
    /// </summary>
    public EnemyIntent GetCurrentEnemyIntent(int enemyIndex = 0)
    {
        return EnemyUnits[enemyIndex].GetCurrentIntent(this);
    }

    /// <summary>
    /// 通知战斗状态变化，并在 UI 重新查询意图前失效攻击目标缓存。
    /// 玩家部署/移除嘲讽随从后，敌人意图应重新锁定到最新合法目标，
    /// 但同一次状态刷新内显示与执行仍共用同一个缓存目标。
    /// </summary>
    private void NotifyCombatStateChanged()
    {
        foreach (var unit in EnemyUnits)
            unit.Brain.ResetCachedAttackTarget();

        OnCombatStateChanged?.Invoke();
    }

    // ===== 随从攻击追踪 =====

    /// <summary>
    /// 本回合内每个随从的已攻击次数（键为随从实例，值为攻击次数）。
    /// 用于风怒（Windfury）多段攻击判定和攻击上限检查。
    /// </summary>
    private readonly Dictionary<Minion, int> _attackCountThisTurn = new();

    /// <summary>
    /// 本回合内可以攻击的随从集合。
        /// 新召唤的随从默认不可攻击（除非有闪击）；
    /// 回合开始时所有玩家随从重置为可攻击状态。
    /// </summary>
    private readonly HashSet<Minion> _canAttackThisTurn = new();

    /// <summary>
    /// 本回合内可以攻击的敌方随从集合。
    /// 敌方意图/召唤产生的随从默认不能立即攻击——只有回合开始时已存在的随从可以攻击。
    /// 在 <see cref="ExecuteEnemyTurn"/> 开始时快照，在 <see cref="EnemyMinionsAttack"/> 中使用。
    /// </summary>
    private readonly HashSet<Minion> _enemyMinionsCanAttack = new();

    /// <summary>
    /// 敌方回合执行动画进行中。设为 true 时冻结意图 UI 刷新，
    /// 防止执行动画期间意图数值跳变。参考 STS2 的 NIntent._isFrozen。
    /// </summary>
    private bool _isEnemyTurnAnimating;

    /// <summary>
    /// 公开的冻结状态查询——UI 通过此属性判断是否应跳过意图刷新。
    /// </summary>
    public bool IsEnemyTurnAnimating => _isEnemyTurnAnimating;

    // ===== 发现选牌状态 =====

    /// <summary>
    /// 当前正在等待玩家进行发现选牌。
    /// </summary>
    public bool IsDiscovering => _pendingDiscoverOptions != null;

    /// <summary>
    /// 当前发现选牌的 N 个候选卡牌（只读）。
    /// </summary>
    public IReadOnlyList<CardData>? DiscoverOptions => _pendingDiscoverOptions?.AsReadOnly();

    /// <summary>
    /// 手牌数量上限（炉石规则：10 张）。
    /// </summary>
    public const int MaxHandSize = 10;

    /// <summary>
    /// 发现选牌候选卡牌列表（null 表示不在发现阶段）。
    /// </summary>
    private List<CardData>? _pendingDiscoverOptions;

    /// <summary>
    /// 当前发现/选牌界面候选的运行时卡牌。用于从弃牌堆选择原卡牌实例。
    /// </summary>
    private List<Card.Card>? _pendingDiscoverRuntimeOptions;

    /// <summary>
    /// 当前选牌需要选择的张数。
    /// </summary>
    public int DiscoverPickCount { get; private set; } = 1;

    /// <summary>
    /// 当前选牌是否使用运行时卡牌实例。
    /// </summary>
    public IReadOnlyList<Card.Card>? DiscoverRuntimeOptions => _pendingDiscoverRuntimeOptions?.AsReadOnly();

    /// <summary>
    /// 触发发现效果的法术牌（选牌完成后从手牌移除）。
    /// </summary>
    private Card.Card? _pendingDiscoverSpellCard;

    public enum PendingSelectionMode
    {
        Discover,
        Discard,
        ChooseDiscard,
        BladeCrisis
    }

    private PendingSelectionMode _pendingSelectionMode = PendingSelectionMode.Discover;

    /// <summary>
    /// 当前选牌模式，供 UI 读取以自定义标题/行为。
    /// </summary>
    public PendingSelectionMode CurrentSelectionMode => _pendingSelectionMode;

    // ===== 效果处理器 =====

    /// <summary>
    /// 效果类型到处理器的映射字典。在 <see cref="InitializeEffectHandlers"/> 中填充。
    /// </summary>
    private Dictionary<CardEffectType, Action<CardEffectData, object, IDamageSource?>> _effectHandlers = null!;

    // ===== Godot 生命周期 =====

    /// <summary>
    /// Godot 节点就绪回调。注册单例引用。
    /// </summary>
    public override void _Ready()
    {
        Instance = this;
        GD.Print("[CombatManager] _Ready — 单例已注册");

        // 使用 CallDeferred 延迟到下一帧执行，确保 GameManager.Instance 等 Autoload 已就绪
        CallDeferred(nameof(BootstrapCombat));
    }

    // ===== 自动启动 =====

    /// <summary>
    /// 战斗自动启动引导方法。
    /// 由 <see cref="_Ready"/> 通过 CallDeferred 调用，确保 Autoload 已就绪。
    /// 从 GameManager 获取 Player；若不存在则回退创建，确保初始化链不会静默失败。
    /// </summary>
    private void BootstrapCombat()
    {
        GD.Print("[CombatManager] BootstrapCombat 开始...");

        // 1. 确保 GameManager 可用（Autoload 应在 Bootstrap 之前已就绪）
        var gm = GameManager.Instance;
        if (gm == null)
        {
            GD.PrintErr("[CombatManager] BootstrapCombat 失败 — GameManager.Instance 为 null");
            return;
        }

        // 2. 确保 CurrentPlayer 存在，若未创建则回退
        if (gm.CurrentPlayer == null)
        {
            GD.Print("[CombatManager] CurrentPlayer 为 null，尝试回退创建玩家...");
            gm.CreateNewPlayer();
            if (gm.CurrentPlayer == null)
            {
                GD.PrintErr("[CombatManager] BootstrapCombat 失败 — 回退创建 Player 失败");
                return;
            }
        }

        var player = gm.CurrentPlayer;

        // 3. 检查牌堆是否为空
        if (player.Deck == null || player.Deck.CardCount == 0)
        {
            GD.PrintErr($"[CombatManager] BootstrapCombat 失败 — 牌堆为空（{player.Deck?.CardCount ?? 0} 张牌）");
            return;
        }
        GD.Print($"[CombatManager] 牌堆有 {player.Deck.CardCount} 张牌");

        // 4. 创建敌方英雄和 AI 遭遇（FightOverride 优先 → RunState → 回退）
        IReadOnlyList<EnemyEncounter> encounters;
        if (gm.FightOverride is { Count: > 0 })
        {
            encounters = gm.FightOverride;
            gm.FightOverride = null; // 消费后清空
            GD.Print($"[CombatManager] 从 FightOverride 读取 {encounters.Count} 个敌人 — {string.Join(", ", encounters.Select(e => e.Name))}");
        }
        else if (gm.RunState is { SelectedRoom: not null } runState &&
            runState.SelectedRoom.Type is RoomType.Monster or RoomType.Elite or RoomType.Boss)
        {
            encounters = runState.CreateEncounters();
            GD.Print($"[CombatManager] 从 RunState 读取 {encounters.Count} 个敌人 — {string.Join(", ", encounters.Select(e => e.Name))}" +
                      $"（{runState.SelectedRoom.Type}）");
        }
        else
        {
            // 回退：如果没有运行状态（例如直接从 Combat.tscn 启动），使用默认邪教徒
            encounters = new EnemyEncounter[] { new Cultist() };
            GD.Print("[CombatManager] 回退使用默认敌人 — 邪教徒");
        }

        // 为每个 EnemyEncounter 创建对应的 Hero + EnemyUnit
        var enemyUnits = new List<EnemyUnit>();
        foreach (var enc in encounters)
        {
            var enemyCore = new CommanderCore();
            enemyCore.InitializeHealth(enc.MaxHealth, enc.MaxHealth);
            enemyCore.SetMana(0, 0);
            var enemyHero = new Hero(enemyCore, false);
            var unit = new EnemyUnit(enemyHero, enc);
            enemyUnits.Add(unit);

            // 注入敌方被动技能
            ApplyEnemyPassives(unit);
            GD.Print($"[CombatManager] 敌方已创建 — {enc.Name}，{enemyHero.CurrentHealth}/{enemyHero.MaxHealth}HP");
        }

        // 5. 初始化战斗管理器（创建 _playerCore、PlayerHero、Board、GameState）
        Initialize(player, enemyUnits);

        // 5.5. 初始化效果处理器字典
        InitializeEffectHandlers();

        // 6. 获取 CombatUI 并初始化
        var combatUI = GetNode<CombatUI>("CanvasLayer/CombatUI");
        combatUI.Initialize(player, this);
        GD.Print("[CombatManager] CombatUI 已初始化");

        // 7. 开始战斗
        StartCombat();
        combatUI.RefreshAll(); // StartCombat 中法力变化后刷新 UI

        // 触发初始意图事件（使用动态意图）
        NotifyCombatStateChanged();

        GD.Print("[CombatManager] BootstrapCombat 完成");
    }

    // ===== 初始化 =====

    /// <summary>
    /// 初始化战斗管理器。
    /// 创建战场和游戏状态，构建玩家英雄包装，存储敌方战斗单位引用。
    /// </summary>
    /// <param name="player">玩家角色（Godot Node）</param>
    /// <param name="enemyUnits">敌方战斗单位列表（1..N 个）</param>
    /// <exception cref="ArgumentNullException">当 player 或 enemyUnits 为 null/空时抛出</exception>
    public void Initialize(Player player, IReadOnlyList<EnemyUnit> enemyUnits)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        if (enemyUnits == null || enemyUnits.Count == 0)
            throw new ArgumentNullException(nameof(enemyUnits));
        EnemyUnits = enemyUnits;

        // 创建玩家英雄专用的 CommanderCore，共享牌堆定义
        _playerCore = new CommanderCore();
        _playerCore.Deck = player.Deck;
        _playerCore.InitializeHealth(player.MaxHealth, player.CurrentHealth);
        _playerCore.SetMana(0, 0);
        PlayerHero = new Hero(_playerCore, true);
        PlayerHero.OnAttacked += HandlePlayerHeroAttacked;

        Board = new Board();
        State = new GameState();

        // 亡语驱动：随从从棋盘移除时自动触发亡语，无需在各处手动调用
        Board.OnMinionRemoved += TriggerDeathrattle;

        // 牌堆回收驱动：随从死亡时自动进入弃牌堆或返回抽牌堆（轮战），无需在各处手动调用
        Board.OnMinionRemoved += HandleMinionDeathPile;

        // 状态变更事件：随从部署/移除时触发，驱动意图 UI 实时刷新
        Board.OnMinionPlaced += (_, _) => NotifyCombatStateChanged();
        Board.OnMinionRemoved += (_) => NotifyCombatStateChanged();

        // 机械蜈蚣-防空型自动拦截：敌方部署低费随从时自动触发战斗
        Board.OnMinionPlaced += OnMinionPlacedForCentipede;

        // 装配默认武器
        PlayerHero.Weapon = new IonPistol();
        foreach (var unit in EnemyUnits)
        {
            unit.Body.Weapon = new RollingLog();
            GD.Print($"[CombatManager] {unit.Brain.Name} 武器：{unit.Body.Weapon.Name}" +
                      $"（{unit.Body.Weapon.Attack}攻）");

            // 同步敌人攻击力到意图系统
            unit.Brain.Attack = unit.Body.Weapon?.Attack ?? 0;
        }

        GD.Print($"[CombatManager] 初始化完成 — 玩家 {PlayerHero.CurrentHealth}/{PlayerHero.MaxHealth}，" +
                  $"敌方 {EnemyUnits.Count} 个单位：" +
                  string.Join(", ", EnemyUnits.Select(u => $"{u.Brain.Name} {u.Body.CurrentHealth}/{u.Body.MaxHealth}")));
    }

    /// <summary>
    /// 初始化效果处理器字典。在各 Wave 迁移中逐步填充。
    /// </summary>
    private void InitializeEffectHandlers()
    {
        _effectHandlers = new Dictionary<CardEffectType, Action<CardEffectData, object, IDamageSource?>>();

        // Wave 2: 伤害类型
        _effectHandlers[CardEffectType.Damage] = HandleDamage;
        _effectHandlers[CardEffectType.DealDamageToTarget] = HandleDamage;
        _effectHandlers[CardEffectType.DealDamageToEnemyHero] = HandleDealDamageToEnemyHero;
        _effectHandlers[CardEffectType.DealDamageToFriendlyHero] = HandleDealDamageToFriendlyHero;
        _effectHandlers[CardEffectType.DealDamageToAllEnemies] = HandleDealDamageToAllEnemies;
        _effectHandlers[CardEffectType.DrawCards] = HandleDrawCards;
        _effectHandlers[CardEffectType.Heal] = HandleHeal;
        _effectHandlers[CardEffectType.RestoreHealth] = HandleHeal;
        _effectHandlers[CardEffectType.GainArmor] = HandleGainArmor;
        _effectHandlers[CardEffectType.GainMaxHealth] = HandleGainMaxHealth;
        _effectHandlers[CardEffectType.SummonMinion] = HandleSummonMinion;
        _effectHandlers[CardEffectType.BuffMinion] = HandleBuffMinion;
        _effectHandlers[CardEffectType.GainManaSlot] = HandleGainManaSlot;
        _effectHandlers[CardEffectType.RemoveNaturalManaCap] = HandleRemoveNaturalManaCap;
        _effectHandlers[CardEffectType.Discover] = HandleDiscoverEffectDispatch;
        _effectHandlers[CardEffectType.ReplaceDeathrattleWithDraw] = HandleReplaceDeathrattleWithDraw;
        _effectHandlers[CardEffectType.GrantIdolTwilight] = HandleGrantIdolTwilight;
        _effectHandlers[CardEffectType.ChooseFromDiscard] = HandleChooseFromDiscard;
        _effectHandlers[CardEffectType.DiscardRandom] = HandleDiscardRandom;
        _effectHandlers[CardEffectType.DiscardChoose] = HandleDiscardChoose;
        _effectHandlers[CardEffectType.ShuffleTribeCards] = HandleShuffleTribeCards;
        _effectHandlers[CardEffectType.Custom] = HandleCustomEffect;
    }

    // ===== 效果处理器 (Effect Handlers) =====

    /// <summary>
    /// 对目标（随从或英雄）造成效果伤害。
    /// </summary>
    private void HandleDamage(CardEffectData effect, object target, IDamageSource? source)
    {
        if (target is Minion minionTarget)
        {
            minionTarget.TakeDamage(effect.Value, source, DamageKind.Effect);
            GD.Print($"[CombatManager]   对 {minionTarget.CardName} 造成 {effect.Value} 点伤害");
        }
        else if (target is Hero heroTarget)
        {
            heroTarget.TakeDamage(effect.Value, source, DamageKind.Effect);
            GD.Print($"[CombatManager]   对英雄造成 {effect.Value} 点伤害");
        }
        else
        {
            GD.PrintErr("[CombatManager]   目标类型不支持伤害");
        }
    }

    /// <summary>
    /// 对敌方英雄造成效果伤害。
    /// </summary>
    private void HandleDealDamageToEnemyHero(CardEffectData effect, object target, IDamageSource? source)
    {
        EnemyHero.TakeDamage(effect.Value, source, DamageKind.Effect);
        GD.Print($"[CombatManager]   对敌方英雄造成 {effect.Value} 点伤害（剩余 {EnemyHero.CurrentHealth}）");
    }

    /// <summary>
    /// 对友方英雄造成效果伤害。
    /// </summary>
    private void HandleDealDamageToFriendlyHero(CardEffectData effect, object target, IDamageSource? source)
    {
        PlayerHero.TakeDamage(effect.Value, source, DamageKind.Effect);
        GD.Print($"[CombatManager]   对友方英雄造成 {effect.Value} 点伤害（剩余 {PlayerHero.CurrentHealth}）");
    }

    /// <summary>
    /// 对所有敌方随从造成效果伤害。
    /// </summary>
    private void HandleDealDamageToAllEnemies(CardEffectData effect, object target, IDamageSource? source)
    {
        int hitCount = 0;
        foreach (var enemyMinion in Board.GetEnemyMinions())
        {
            enemyMinion.TakeDamage(effect.Value, source, DamageKind.Effect);
            hitCount++;
        }
        GD.Print($"[CombatManager]   对所有敌方随从造成 {effect.Value} 点伤害（命中 {hitCount} 个目标）");
    }

    /// <summary>
    /// 抽牌。
    /// </summary>
    private void HandleDrawCards(CardEffectData effect, object target, IDamageSource? source)
    {
        PlayerHero.DrawCards(effect.Value);
        GD.Print($"[CombatManager]   抽 {effect.Value} 张牌");
    }

    /// <summary>
    /// 恢复生命值（上限内）。
    /// </summary>
    private void HandleHeal(CardEffectData effect, object target, IDamageSource? source)
    {
        _playerCore.Heal(effect.Value);
        GD.Print($"[CombatManager]   恢复 {effect.Value} 点生命值（当前 {PlayerHero.CurrentHealth}）");
    }

    /// <summary>
    /// 获得护甲值。
    /// </summary>
    private void HandleGainArmor(CardEffectData effect, object target, IDamageSource? source)
    {
        PlayerHero.GainArmor(effect.Value);
        GD.Print($"[CombatManager]   获得 {effect.Value} 点护甲（当前 {PlayerHero.CurrentArmor}）");
    }

    /// <summary>
    /// 获得最大生命值（同步回复等量生命值）。
    /// </summary>
    private void HandleGainMaxHealth(CardEffectData effect, object target, IDamageSource? source)
    {
        _playerCore.InitializeHealth(
            _playerCore.MaxHealth + effect.Value,
            _playerCore.CurrentHealth + effect.Value);
        GD.Print($"[CombatManager]   最大生命值 +{effect.Value} 并恢复等量生命值（当前 {PlayerHero.CurrentHealth}/{PlayerHero.MaxHealth}）");
    }

    /// <summary>
    /// 召唤随从（原型：仅记录日志）。
    /// </summary>
    private void HandleSummonMinion(CardEffectData effect, object target, IDamageSource? source)
    {
        int emptySlot = Board.GetEmptySlotIndex(isPlayerSide: true);
        if (emptySlot >= 0)
        {
            GD.Print($"[CombatManager]   召唤随从效果：{effect.GetDescription()}（原型：仅记录日志）");
        }
        else
        {
            GD.Print($"[CombatManager]   召唤随从失败 — 战场已满");
        }
    }

    /// <summary>
    /// 强化随从（原型：暂未实现属性修改）。
    /// </summary>
    private void HandleBuffMinion(CardEffectData effect, object target, IDamageSource? source)
    {
        if (target is Minion buffTarget)
        {
            GD.Print($"[CombatManager]   BuffMinion：{effect.GetDescription()} → {buffTarget.CardName}（原型：暂未实现属性修改）");
        }
        else
        {
            GD.Print($"[CombatManager]   BuffMinion 需要有效的随从目标");
        }
    }

    /// <summary>
    /// 获得额外的法力水晶槽。
    /// </summary>
    private void HandleGainManaSlot(CardEffectData effect, object target, IDamageSource? source)
    {
        State.GainManaSlot(effect.Value);
        _playerCore.SetMana(_playerCore.CurrentMana, State.PlayerMaxMana);
        GD.Print($"[CombatManager]   获得 {effect.Value} 个法力水晶槽（总上限 {State.PlayerMaxMana}）");
    }

    /// <summary>
    /// 解除自然增长的法力水晶上限。
    /// </summary>
    private void HandleRemoveNaturalManaCap(CardEffectData effect, object target, IDamageSource? source)
    {
        GD.Print("[CombatManager]   无限潜能领域已展开，自然增长上限提升至 30");
    }

    /// <summary>
    /// 发现选牌——委托给现有的 HandleDiscoverEffect 方法。
    /// </summary>
    private void HandleDiscoverEffectDispatch(CardEffectData effect, object target, IDamageSource? source)
    {
        HandleDiscoverEffect(effect);
    }

    /// <summary>
    /// 替换目标随从亡语为「玩家英雄抽牌」。
    /// </summary>
    private void HandleReplaceDeathrattleWithDraw(CardEffectData effect, object target, IDamageSource? source)
    {
        if (target is not Minion minionTarget)
        {
            GD.Print("[CombatManager]   替换亡语需要有效的随从目标");
            return;
        }

        int drawCount = Math.Max(1, effect.Value);
        var drawEffect = new CardEffectData
        {
            EffectType = CardEffectType.DrawCards,
            Value = drawCount,
        };
        minionTarget.ReplaceDeathrattleEffects(new[] { drawEffect });
        GD.Print($"[CombatManager]   {minionTarget.CardName} 获得亡语：抽 {drawCount} 张牌");
    }

    /// <summary>
    /// 偶像的黄昏：玩家所有区域中的随从获得被攻击后 +1/+1。
    /// </summary>
    private void HandleGrantIdolTwilight(CardEffectData effect, object target, IDamageSource? source)
    {
        int stacks = Math.Max(1, effect.Value);
        int grantCount = 0;

        foreach (var card in PlayerHero.Hand)
            grantCount += GrantIdolTwilightToCard(card, stacks);
        foreach (var card in PlayerHero.DeckState.DrawPile)
            grantCount += GrantIdolTwilightToCard(card, stacks);
        foreach (var card in PlayerHero.DeckState.DiscardPile)
            grantCount += GrantIdolTwilightToCard(card, stacks);
        foreach (var minion in Board.GetPlayerMinions())
        {
            minion.GrantIdolTwilightOnAttacked(stacks);
            grantCount++;
        }

        GD.Print($"[CombatManager] ◆ 偶像的黄昏：为 {grantCount} 个玩家随从/随从牌授予被攻击后 +{stacks}/+{stacks}");
        NotifyCombatStateChanged();
    }

    private static int GrantIdolTwilightToCard(Card.Card card, int stacks)
    {
        if (card.Type != CardType.Minion) return 0;

        card.GrantIdolTwilightOnAttacked(stacks);
        return 1;
    }

    /// <summary>
    /// 捞月：从弃牌堆展示 N 张牌，选择 M 张移回手牌。
    /// </summary>
    private void HandleChooseFromDiscard(CardEffectData effect, object target, IDamageSource? source)
    {
        int optionCount = effect.Value > 0 ? effect.Value : 5;
        int pickCount = effect.SecondaryValue > 0 ? effect.SecondaryValue : 2;
        var options = GetRandomCardsFromDiscard(optionCount);

        if (options.Count == 0)
        {
            GD.Print("[CombatManager] 捞月：弃牌堆为空，无牌可选");
            return;
        }

        _pendingDiscoverRuntimeOptions = options;
        _pendingDiscoverOptions = options.Select(c => c.Data).ToList();
        DiscoverPickCount = Math.Min(pickCount, options.Count);
        _pendingSelectionMode = PendingSelectionMode.Discard;
        State.SetDiscovering();

        GD.Print($"[CombatManager] ◆ 捞月：从弃牌堆展示 {options.Count} 张，选择 {DiscoverPickCount} 张");
        NotifyCombatStateChanged();
    }

    /// <summary>
    /// 随机弃牌：从手牌中随机弃掉 N 张牌。
    /// </summary>
    private void HandleDiscardRandom(CardEffectData effect, object target, IDamageSource? source)
    {
        int discardCount = effect.Value;
        var hand = PlayerHero.Hand.ToList();

        if (hand.Count == 0)
        {
            GD.Print("[CombatManager] 随机弃牌：手牌为空，无法弃牌");
            return;
        }

        int actualDiscard = Math.Min(discardCount, hand.Count);
        using var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int i = 0; i < actualDiscard; i++)
        {
            int randomIndex = rng.RandiRange(0, hand.Count - 1);
            var card = hand[randomIndex];
            GD.Print($"[CombatManager]   随机弃掉: {card.GetLocalizedName()}");
            PlayerHero.DiscardCard(card);
            hand.RemoveAt(randomIndex);
        }

        GD.Print($"[CombatManager] ◆ 随机弃牌完成：弃掉 {actualDiscard}/{discardCount} 张牌");
        NotifyCombatStateChanged();
    }

    /// <summary>
    /// 主动弃牌：从手牌中选择 N 张牌弃掉（异步，等待玩家选择）。
    /// 遵循与捞月相同的异步选牌模式。
    /// </summary>
    private void HandleDiscardChoose(CardEffectData effect, object target, IDamageSource? source)
    {
        int mustDiscard = effect.Value;
        var handCopy = PlayerHero.Hand.ToList();

        if (handCopy.Count == 0)
        {
            GD.Print("[CombatManager] 主动弃牌：手牌为空，无法弃牌");
            return;
        }

        if (handCopy.Count < mustDiscard)
        {
            GD.Print($"[CombatManager] 主动弃牌：手牌数量({handCopy.Count})不足，需要弃{mustDiscard}张");
            return;
        }

        _pendingDiscoverRuntimeOptions = handCopy;
        _pendingDiscoverOptions = handCopy.Select(c => c.Data).ToList();
        DiscoverPickCount = mustDiscard;
        _pendingSelectionMode = PendingSelectionMode.ChooseDiscard;
        State.SetDiscovering();

        GD.Print($"[CombatManager] ◆ 主动弃牌：从手牌 {handCopy.Count} 张中选择弃掉 {mustDiscard} 张");
        NotifyCombatStateChanged();
    }

    /// <summary>
    /// 种族洗牌：将 N 张随机指定种族的随从卡牌洗入抽牌堆。
    /// 从全卡牌池中加载指定标签的随从，可重复选取（with replacement）。
    /// </summary>
    private void HandleShuffleTribeCards(CardEffectData effect, object target, IDamageSource? source)
    {
        int insertCount = effect.Value;

        // 解析目标种族标签
        if (!Enum.TryParse<CardTag>(effect.TargetType, out var targetTag) || targetTag == CardTag.None)
        {
            GD.PrintErr($"[CombatManager] 种族洗牌：无法识别的种族标签 '{effect.TargetType}'");
            return;
        }

        // 加载全卡牌池并过滤
        var pool = new List<CardData>();
        using var dir = DirAccess.Open("res://Resources/Cards/");
        if (dir != null)
        {
            dir.ListDirBegin();
            string fileName = dir.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (!dir.CurrentIsDir() && fileName.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
                {
                    var cardData = GD.Load<CardData>($"res://Resources/Cards/{fileName}");
                    if (cardData != null && !string.IsNullOrEmpty(cardData.Id)
                        && cardData.Tags.HasFlag(targetTag)
                        && cardData.Type == CardType.Minion)
                    {
                        pool.Add(cardData);
                    }
                }
                fileName = dir.GetNext();
            }
            dir.ListDirEnd();
        }

        if (pool.Count == 0)
        {
            GD.Print($"[CombatManager] 种族洗牌：没有符合条件的 {effect.TargetType} 随从卡牌");
            return;
        }

        // 有放回随机选取
        using var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int i = 0; i < insertCount; i++)
        {
            int randomIndex = rng.RandiRange(0, pool.Count - 1);
            var cardData = pool[randomIndex];
            var card = new OdysseyCards.Card.Card(cardData);
            PlayerHero.InsertCardToDrawPile(card);
            GD.Print($"[CombatManager]   洗入抽牌堆: {card.GetLocalizedName()}");
        }

        PlayerHero.ShuffleDrawPile();
        GD.Print($"[CombatManager] ◆ 种族洗牌完成：将 {insertCount} 张随机 {effect.TargetType} 随从洗入抽牌堆");
        NotifyCombatStateChanged();
    }

    /// <summary>
    /// 自定义效果——根据 CustomEffectName 分发到子逻辑。
    /// </summary>
    private void HandleCustomEffect(CardEffectData effect, object target, IDamageSource? source)
    {
        if (effect.CustomEffectName == "AddPlanToHand")
        {
            var planData = GD.Load<CardData>("res://Resources/Cards/Spell_Plan.tres");
            if (planData != null)
            {
                var planCard = new OdysseyCards.Card.Card(planData);
                _playerCore.AddToHand(planCard);
                GD.Print("[CombatManager]   将「计划」加入手牌");
            }
            else
            {
                GD.PrintErr("[CombatManager]   无法加载计划卡牌资源");
            }
        }
        else if (effect.CustomEffectName == "FlyingAway")
        {
            PlayerHero.GainArmor(effect.Value);
            GD.Print($"[CombatManager]   飞远：获得 {effect.Value} 点格挡（护甲）");
        }
        else if (effect.CustomEffectName == "StripArmor")
        {
            if (target is Hero heroTarget)
            {
                int armorLost = heroTarget.CurrentArmor;
                heroTarget.RemoveArmor();
                GD.Print($"[CombatManager]   移除目标所有护甲（失去 {armorLost} 点）");
            }
            else
            {
                GD.Print("[CombatManager]   StripArmor 目标无护甲（非英雄单位），无效果");
            }
        }
        else if (effect.CustomEffectName == "BaitTactics")
        {
            if (target is Minion minionTarget)
            {
                minionTarget.GrantBaitTactics();
                GD.Print($"[CombatManager]   诱饵战术：{minionTarget.CardName} 获得伏击、冲击与被攻击触发");
            }
            else
            {
                GD.Print("[CombatManager]   诱饵战术需要有效的随从目标");
            }
        }
        else if (effect.CustomEffectName == "Animosity")
        {
            if (target is Minion minionTarget)
            {
                // 1. 授予嘲讽
                minionTarget.HasTaunt = true;
                // 2. 注册敌意伤害翻倍修改器（受到来自玩家阵营的伤害翻倍）
                minionTarget._damageModifiers.Add(new AnimosityDamageModifier());
                // 3. 追加亡语：玩家抽一张牌
                var drawEffect = new CardEffectData
                {
                    EffectType = CardEffectType.DrawCards,
                    Value = 1,
                };
                minionTarget.AddDeathrattleEffect(drawEffect);
                GD.Print($"[CombatManager]   敌意：{minionTarget.CardName} 获得嘲讽、伤害翻倍（玩家阵营）和亡语抽牌");
            }
            else
            {
                GD.Print("[CombatManager]   敌意需要有效的随从目标");
            }
        }
        else if (effect.CustomEffectName == "BladeCrisis")
        {
            int maxDiscard = effect.Value > 0 ? effect.Value : 5;
            var hand = PlayerHero.Hand.ToList();
            if (hand.Count == 0) { GD.Print("[CombatManager] 刀盾危机：手牌为空"); return; }

            _pendingDiscoverRuntimeOptions = hand.Select(c => (Card.Card)c).ToList();
            _pendingDiscoverOptions = hand.Select(c => c.Data).ToList();
            DiscoverPickCount = Math.Min(maxDiscard, hand.Count);
            _pendingSelectionMode = PendingSelectionMode.BladeCrisis;
            State.SetDiscovering();
            GD.Print($"[CombatManager] ◆ 刀盾危机：可选最多{DiscoverPickCount}张手牌弃掉");
            NotifyCombatStateChanged();
        }
        else
        {
            GD.Print($"[CombatManager]   未处理的Custom效果：{effect.CustomEffectName}");
        }
    }

    /// <summary>
    /// 注入敌人被动技能 modifier 到对应的 Hero。
    /// 固璋(3) → DamageCapModifier；不破(1) 由 Hero._hasTakenDamageThisTurn 门控处理。
    /// </summary>
    private static void ApplyEnemyPassives(EnemyUnit unit)
    {
        if (unit.Brain is ZhangLang)
        {
            unit.Body._damageModifiers.Add(new DamageCapModifier(3));
            GD.Print($"[CombatManager] {unit.Brain.Name} 固璋(3) — 单次伤害上限 3");
        }
        // 不破(1)：需在 ApplyEnemyPassives 中设置 unit.Body.HasUnbreakable = true，
        // 并由 Hero._hasTakenDamageThisTurn 守卫 + StartPlayerTurn/EndPlayerTurn 的 ResetDamageTakenThisTurn 处理
    }

    // ===== 战斗开始 =====

    /// <summary>
    /// 开始战斗——仅处理战斗初始化（场景、牌堆、发牌）。
    /// 完成后调用 <see cref="StartPlayerTurn"/> 启动第一个玩家回合。
    /// </summary>
    public void StartCombat()
    {
        GD.Print("[CombatManager] ========== 战斗开始 ==========");

        // 从牌堆定义创建运行时卡牌并洗入抽牌堆
        _playerCore.SetupDrawPile();
        GD.Print($"[CombatManager] 抽牌堆已设置，共 {_playerCore.DrawPile.Count} 张牌");

        State.StartGame();

        // 玩家始终先手，发 5 张起始手牌
        PlayerHero.DrawCards(5);
        GD.Print($"[CombatManager] 起手抽 5 张牌 → 共 {_playerCore.Hand.Count} 张手牌");

        // 开始第一个玩家回合
        StartPlayerTurn();
    }

    /// <summary>
    /// 玩家回合开始——法力增长/回满、抽 1 张、重置攻击状态。
    /// 由 <see cref="StartCombat"/>（首回合）和 <see cref="EndPlayerTurn"/>（后续回合）调用。
    /// </summary>
    private void StartPlayerTurn()
    {
        // 检查英雄是否拥有「无限潜能」领域，决定自然增长上限
        int growthCap = PlayerHero.ActiveDomains.ContainsKey("unlimited_potential")
            ? GameState.HardMaxManaCap
            : GameState.MaxManaCrystals;
        State.StartPlayerTurn(growthCap);
        _playerCore.SetMana(State.PlayerMana, State.PlayerMaxMana);

        // 回合开始抽 1 张牌
        PlayerHero.DrawCards(1);

        // 重置随从攻击状态
        ResetAttackTracking();

        // 重置伏击状态（所有随从和英雄的伏击在新整轮重新可用）
        foreach (var minion in Board.GetPlayerMinions())
            minion.ResetAmbush();
        foreach (var minion in Board.GetEnemyMinions())
            minion.ResetAmbush();
        PlayerHero.ResetAmbush();
        foreach (var unit in EnemyUnits)
            unit.Body.ResetAmbush();

        // 重置受伤标记（确保上回合武器反击的标记不会跨回合影响新的攻击）
        PlayerHero.ResetDamageTakenThisTurn();
        foreach (var unit in EnemyUnits)
            unit.Body.ResetDamageTakenThisTurn();

        // 重置武器攻击次数 + 冷却衰减
        PlayerHero.ResetWeaponAttacks();
        PlayerHero.TickWeaponCooldown();

        GD.Print($"[CombatManager] 第 {State.TurnCount} 回合开始（法力 {State.PlayerMana}/{State.PlayerMaxMana}），手牌 {_playerCore.Hand.Count} 张");
    }

    // ===== 随从召唤 =====

    /// <summary>
    /// 玩家打出一张随从牌，将其召唤至战场的指定槽位。
        /// 验证玩家回合、法力值、槽位可用性，处理战吼和闪击关键词。
    /// </summary>
    /// <param name="card">要打出的卡牌（手牌中的运行时实例）</param>
    /// <param name="slotIndex">目标槽位索引（0-4）</param>
    /// <returns>成功返回 true；验证失败返回 false</returns>
    public bool PlayMinion(Card.Card card, int slotIndex)
    {
        // 验证：玩家回合
        if (!State.IsPlayerTurn)
        {
            GD.PrintErr("[CombatManager] PlayMinion 失败 — 不是玩家回合");
            return false;
        }

        // 验证：卡牌有效性
        if (card == null)
        {
            GD.PrintErr("[CombatManager] PlayMinion 失败 — 卡牌为 null");
            return false;
        }

        // 验证：是随从牌
        if (card.Type != CardType.Minion)
        {
            GD.PrintErr($"[CombatManager] PlayMinion 失败 — {card.CardName} 不是随从牌");
            return false;
        }

        // 验证：法力值充足
        if (!PlayerHero.CanSpendMana(card.Cost))
        {
            GD.PrintErr($"[CombatManager] PlayMinion 失败 — 法力值不足（需 {card.Cost}，现有 {PlayerHero.CurrentMana}）");
            return false;
        }

        // 验证：槽位可用
        if (!Board.CanPlaceMinion(isPlayerSide: true))
        {
            GD.PrintErr("[CombatManager] PlayMinion 失败 — 玩家战场已满（最多 5 个随从）");
            return false;
        }

        // 消耗法力值
        PlayerHero.SpendMana(card.Cost);
        GD.Print($"[CombatManager] 消耗 {card.Cost} 法力值（剩余 {PlayerHero.CurrentMana}）");

        // 创建随从运行时实例，并保留牌堆中已有的运行时修饰
        var minion = new Minion(card, isPlayerSide: true);

        // 处理战吼效果
        if (minion.HasBattlecry)
        {
            GD.Print($"[CombatManager]   ◆ 触发战吼：{minion.CardName}");
            foreach (var effect in minion.BattlecryEffects)
            {
                ResolveBattlecryEffect(effect, minion);
            }
        }

        // 放置随从到战场
        Board.PlaceMinion(minion, slotIndex);

        // 我的刀盾：战吼复制到相邻槽位
        if (minion.HasBattlecry && minion.BattlecryEffects.Any(e => e.CustomEffectName == "CopyToAdjacentSlot"))
        {
            CopyMinionToAdjacentSlot(minion, slotIndex);
        }

        // 闪击关键词：召唤的回合即可攻击
        if (minion.HasCharge)
        {
            _canAttackThisTurn.Add(minion);
            GD.Print($"[CombatManager]   ⚡ {minion.CardName} 具有闪击，本回合可以攻击");
        }

        // 从手牌中移除
        PlayerHero.RemoveFromHand(card);

        GD.Print($"[CombatManager] 玩家召唤了 {minion.CardName}（{minion.Attack}/{minion.CurrentHealth}）到槽位 {slotIndex}");

        // 触发领域效果 — 友方随从部署后
        TriggerDomainsOnMinionPlaced(minion);

        return true;
    }

    /// <summary>
    /// 处理战吼效果。
    /// 原型阶段仅输出日志，后续可扩展为完整的效果解析。
    /// </summary>
    /// <param name="effect">战吼效果数据</param>
    /// <param name="source">战吼来源随从</param>
    private void ResolveBattlecryEffect(CardEffectData effect, Minion source)
    {
        GD.Print($"[CombatManager]     战吼效果：{effect.GetDescription()}（来源：{source.CardName}）");
        ExecuteEffect(effect, source, source);
    }

    /// <summary>
    /// 我的刀盾战吼：复制Token随从到相邻槽位。
    /// 优先尝试左侧（slot-1），再尝试右侧（slot+1），最后回退任意空位。
    /// </summary>
    /// <param name="sourceMinion">触发战吼的源随从</param>
    /// <param name="sourceSlotIndex">源随从所在槽位</param>
    private void CopyMinionToAdjacentSlot(Minion sourceMinion, int sourceSlotIndex)
    {
        var tokenData = GD.Load<CardData>("res://Resources/Cards/Minion_WhatTheDogDoing.tres");
        if (tokenData == null) { GD.PrintErr("[CombatManager] CopyToAdjacentSlot: 无法加载Token卡牌"); return; }

        // 优先相邻槽位（先左后右）
        int? targetSlot = null;
        if (sourceSlotIndex > 0 && Board.PlayerSlots[sourceSlotIndex - 1] == null)
            targetSlot = sourceSlotIndex - 1;
        else if (sourceSlotIndex < 4 && Board.PlayerSlots[sourceSlotIndex + 1] == null)
            targetSlot = sourceSlotIndex + 1;

        // 回退：任意空位
        if (!targetSlot.HasValue)
            targetSlot = Board.GetEmptySlotIndex(isPlayerSide: true);

        if (targetSlot.HasValue)
        {
            var copy = new Minion(tokenData, isPlayerSide: true);
            Board.PlaceMinion(copy, targetSlot.Value);
            GD.Print($"[CombatManager] 🐕 我的刀盾复制到槽位{targetSlot.Value}");
        }
        else
            GD.Print("[CombatManager] 🐕 我的刀盾：棋盘已满，无法复制");
    }

    // ===== 法术施放 =====

    /// <summary>
    /// 玩家打出一张法术牌，对目标施放效果。
    /// 目标可以是随从（Minion）或英雄（Hero），通过 IDamageTarget 接口统一处理伤害。
    /// 如果法术包含 Discover 效果，则暂停清理流程，等待玩家选牌后通过 <see cref="ConfirmDiscoverChoice"/> 完成。
    /// </summary>
    /// <param name="card">要打出的法术牌</param>
    /// <param name="target">法术目标（Minion 或 Hero 实例）</param>
    /// <returns>成功返回 true</returns>
    public bool PlaySpell(Card.Card card, object target)
    {
        // 验证：玩家回合
        if (!State.IsPlayerTurn)
        {
            GD.PrintErr("[CombatManager] PlaySpell 失败 — 不是玩家回合");
            return false;
        }

        // 验证：卡牌有效性
        if (card == null)
        {
            GD.PrintErr("[CombatManager] PlaySpell 失败 — 卡牌为 null");
            return false;
        }

        // 验证：是法术牌
        if (card.Type != CardType.Spell)
        {
            GD.PrintErr($"[CombatManager] PlaySpell 失败 — {card.CardName} 不是法术牌");
            return false;
        }

        // 验证：法力值充足
        if (!PlayerHero.CanSpendMana(card.Cost))
        {
            GD.PrintErr($"[CombatManager] PlaySpell 失败 — 法力值不足（需 {card.Cost}，现有 {PlayerHero.CurrentMana}）");
            return false;
        }

        // 验证：目标合法性（tag 子集匹配）
        if (!ValidateTarget(card, target))
        {
            GD.PrintErr($"[CombatManager] PlaySpell 失败 — 目标不合法（{card.CardName} 的目标过滤：{card.Data.TargetFilter}）");
            return false;
        }

        // 消耗法力值
        PlayerHero.SpendMana(card.Cost);
        GD.Print($"[CombatManager] 施放法术 {card.CardName}，消耗 {card.Cost} 法力值");

        // 解析每个法术效果
        bool selectionTriggered = false;
        foreach (var effect in card.Data.Effects)
        {
            ResolveSpellEffect(effect, target);
            if (IsDiscovering)
            {
                selectionTriggered = true;
                _pendingDiscoverSpellCard = card;
            }
        }

        // 如果触发了选牌效果，延迟清理——确认选择后处理 RemoveFromHand/CheckDeaths
        if (selectionTriggered)
        {
            GD.Print("[CombatManager]   选牌效果已触发，等待玩家选择...");
            return true;
        }

        // 轮战法术：回到抽牌堆底部；普通法术：进入弃牌堆
        if (card.HasRecycle)
        {
            PlayerHero.ReturnToDrawPile(card);
            GD.Print($"[CombatManager]   ♻ {card.CardName}（轮战）回到抽牌堆底部");
        }
        else
        {
            PlayerHero.DiscardCard(card);
            GD.Print($"[CombatManager]   🗑 {card.CardName} 进入弃牌堆");
        }

        // 法术可能造成随从死亡
        CheckDeaths();

        // 检查胜负
        CheckVictoryOrDefeat();

        return true;
    }

    // ===== 领域卡牌播放 =====

    /// <summary>
    /// 玩家打出一张领域牌，将持久效果附加到玩家英雄上。
    /// 领域不需要选择目标，自动挂在己方英雄上。
    /// 同名领域叠加层数，不同领域独立存在。
    /// 领域不进入弃牌堆。
    /// </summary>
    /// <param name="card">要打出的领域牌</param>
    /// <returns>成功返回 true</returns>
    public bool PlayDomain(Card.Card card)
    {
        // 验证：玩家回合
        if (!State.IsPlayerTurn)
        {
            GD.PrintErr("[CombatManager] PlayDomain 失败 — 不是玩家回合");
            return false;
        }

        // 验证：卡牌有效性
        if (card == null)
        {
            GD.PrintErr("[CombatManager] PlayDomain 失败 — 卡牌为 null");
            return false;
        }

        // 验证：是领域牌
        if (card.Type != CardType.Domain)
        {
            GD.PrintErr($"[CombatManager] PlayDomain 失败 — {card.CardName} 不是领域牌");
            return false;
        }

        // 验证：法力值充足
        if (!PlayerHero.CanSpendMana(card.Cost))
        {
            GD.PrintErr($"[CombatManager] PlayDomain 失败 — 法力值不足（需 {card.Cost}，现有 {PlayerHero.CurrentMana}）");
            return false;
        }

        // 消耗法力值
        PlayerHero.SpendMana(card.Cost);
        GD.Print($"[CombatManager] 展开领域 {card.CardName}，消耗 {card.Cost} 法力值");

        // 将领域效果附加到英雄
        string domainId = card.Data.DomainId;
        if (string.IsNullOrEmpty(domainId))
        {
            domainId = card.Data.Id; // fallback 用卡牌ID作为领域标识
        }

        foreach (var effect in card.Data.Effects)
        {
            // 先执行即时效果（如 RemoveNaturalManaCap），再存储为持久领域效果
            ExecuteEffect(effect, null);
            PlayerHero.AddDomain(domainId, effect);
        }

        // 从手牌中移除（领域不进入弃牌堆）
        PlayerHero.RemoveFromHand(card);

        return true;
    }

    // ===== 领域触发 =====

    /// <summary>
    /// 触发「友方随从部署」相关领域效果。
    /// 在 <see cref="PlayMinion"/> 中随从放置到棋盘后调用。
    /// </summary>
    /// <param name="minion">刚被部署的随从</param>
    private void TriggerDomainsOnMinionPlaced(Minion minion)
    {
        foreach (var domain in PlayerHero.ActiveDomains.Values)
        {
            switch (domain.DomainId)
            {
                case "zhijian":
                    int bonusAtk = domain.EffectData.Value * domain.StackCount;
                    minion.ModifyAttack(bonusAtk);
                    GD.Print($"[CombatManager] ◆ 「执锐」触发：{minion.CardName} 攻击力 +{bonusAtk}（{domain.StackCount}层）");
                    break;
            }
        }
    }

    /// <summary>
    /// 触发「友方回合结束」相关领域效果。
    /// 在 <see cref="EndPlayerTurn"/> 中回合结束清理后调用。
    /// </summary>
    private void TriggerDomainsOnTurnEnd()
    {
        foreach (var domain in PlayerHero.ActiveDomains.Values)
        {
            switch (domain.DomainId)
            {
                case "infinite_fire":
                    int shuffleCount = domain.EffectData.Value * domain.StackCount;
                    for (int i = 0; i < shuffleCount; i++)
                    {
                        var strikeData = GD.Load<CardData>("res://Resources/Cards/Spell_Strike.tres");
                        var strikeCard = new OdysseyCards.Card.Card(strikeData);
                        PlayerHero.InsertCardToDrawPile(strikeCard);
                    }
                    GD.Print($"[CombatManager] ◆ 「无限火力」触发：洗入 {shuffleCount} 张打击（{domain.StackCount}层）");
                    break;
            }
        }
    }

    /// <summary>
    /// 执行单个卡牌效果，根据 EffectType 分发到对应的处理逻辑。
    /// 供法术、战吼、亡语等所有效果触发场景共用。
    /// </summary>
    /// <param name="effect">效果数据</param>
    /// <param name="target">效果目标对象（Minion 或 Hero，可为 null）</param>
    private void ExecuteEffect(CardEffectData effect, object target, IDamageSource? source = null)
    {
        // 字典优先分发：已注册的效果类型直接调用对应 handler
        if (_effectHandlers.TryGetValue(effect.EffectType, out var handler))
        {
            handler(effect, target, source);
            return;
        }

        // 未注册的效果类型 — 输出日志
        GD.Print($"[CombatManager]   未处理的效果类型：{effect.EffectType}（{effect.GetDescription()}）");
    }

    /// <summary>
    /// 处理玩家英雄被敌方攻击时触发的领域效果。
    /// </summary>
    /// <param name="target">被攻击英雄</param>
    /// <param name="source">攻击来源</param>
    /// <param name="finalDamage">护甲结算后的实际生命伤害</param>
    private void HandlePlayerHeroAttacked(Hero target, IDamageSource source, int finalDamage)
    {
        if (!ReferenceEquals(target, PlayerHero)) return;
        if (State.IsPlayerTurn) return;

        bool isEnemyAttackSource = ReferenceEquals(source, EnemyHero)
            || source is Minion { IsPlayerSide: false };
        if (!isEnemyAttackSource) return;

        if (!PlayerHero.ActiveDomains.TryGetValue("flying_away", out var domain)) return;
        if (domain.LastTriggeredTurn == State.TurnCount) return;

        domain.LastTriggeredTurn = State.TurnCount;

        int drawCount = domain.EffectData.SecondaryValue > 0 ? domain.EffectData.SecondaryValue : 2;
        PlayerHero.DrawCards(drawCount);

        string tokenPath = string.IsNullOrWhiteSpace(domain.EffectData.TargetType)
            ? "res://Resources/Cards/Spell_Shoushen.tres"
            : domain.EffectData.TargetType;
        var tokenData = GD.Load<CardData>(tokenPath);
        if (tokenData != null)
        {
            _playerCore.AddToHand(new OdysseyCards.Card.Card(tokenData));
            GD.Print($"[CombatManager] ◆ 「飞远」触发：抽 {drawCount} 张牌，将「{tokenData.GetLocalizedName()}」加入手牌");
        }
        else
        {
            GD.PrintErr($"[CombatManager] ◆ 「飞远」触发失败：无法加载受身卡牌 {tokenPath}");
        }

        if (domain.StackCount <= 1)
        {
            PlayerHero.RemoveDomain("flying_away");
        }
        else
        {
            domain.StackCount--;
            GD.Print($"[CombatManager] ◆ 「飞远」剩余 {domain.StackCount} 层");
        }

        NotifyCombatStateChanged();
    }

    /// <summary>
    /// 解析单个法术效果，委托给共享的 ExecuteEffect 执行。
    /// </summary>
    /// <param name="effect">效果数据</param>
    /// <param name="target">法术目标对象</param>
    private void ResolveSpellEffect(CardEffectData effect, object target)
    {
        ExecuteEffect(effect, target);
    }

    // ===== 随从攻击 =====

    /// <summary>
    /// 统一的随从间战斗序列（炉石规则 + 伏击 + 冲击）。
    ///
    /// 结算顺序：
    /// 1. 伏击检查 — 防御者有伏击且本回合未消耗时，防御者先手攻击
    /// 2. 冲击检查 — 攻击者有冲击时免疫所有反击伤害（包括伏击先手）
    /// 3. 攻击者造成伤害
    /// 4. 防御者反击（如果存活且攻击者无冲击）
    /// 5. 消耗冲击 + 消耗伏击
    /// 6. 死亡检查
    ///
    /// 伏击 vs 冲击交互（KARDS 规则）：冲击免疫伏击的先手伤害，
    /// 伏击仍然消耗，攻击者仍正常造成伤害。
    /// </summary>
    /// <param name="attacker">攻击方随从</param>
    /// <param name="defender">防御方随从</param>
    /// <returns>向调用方返回是否应继续后续处理（若伏击击杀攻击方则为 false）</returns>
    /// <summary>
    /// 执行随从与随从之间的统一战斗序列（伏击 → 攻击 → 反击 → 消耗冲击）。
    /// 内部方法，供 CombatManager 的 MinionAttack 和 AI 系统的 DefaultAttackMinionBrain 调用。
    /// </summary>
    internal bool ResolveMinionCombat(Minion attacker, Minion defender)
    {
        TriggerBaitTacticsOnAttacked(defender);

        bool ambushTriggers = defender.HasAmbush && !defender.AmbushUsedThisTurn;
        bool impactActive = attacker.HasImpact;

        GD.Print($"[CombatManager] ⚔ {attacker.CardName}（{attacker.Attack}攻/{attacker.CurrentHealth}血）攻击 " +
                  $"{defender.CardName}（{defender.Attack}攻/{defender.CurrentHealth}血）" +
                  (ambushTriggers ? " [伏击触发！]" : "") +
                  (impactActive ? " [冲击激活]" : ""));

        // ===== Phase 1: 伏击先手（防御者先于攻击者造成伤害） =====
        if (ambushTriggers)
        {
            defender.AmbushUsedThisTurn = true;

            if (impactActive)
            {
                // KARDS 规则：冲击免疫伏击的先手伤害
                GD.Print($"[CombatManager]   🛡 冲击免疫了 {defender.CardName} 的伏击伤害（{defender.Attack}）");
            }
            else
            {
                // 伏击先手：防御者先对攻击者造成伤害
                GD.Print($"[CombatManager]   ⚡ {defender.CardName} 伏击先手，造成 {defender.Attack} 伤害");
                attacker.TakeDamage(defender.Attack, defender);
            }

            // 伏击击杀攻击者 → 战斗结束，攻击被取消
            if (attacker.IsDead)
            {
                GD.Print($"[CombatManager]   ☠ {attacker.CardName} 被伏击击杀，攻击被取消（不造成伤害）");
                return false;
            }
        }

        // ===== Phase 2: 攻击者造成伤害 =====
        defender.TakeDamage(attacker.Attack, attacker);

        // ===== Phase 3: 防御者反击（炉石规则：双方同时伤害，防御者被击杀仍能反击） =====
        // 注意：即使防御者在 Phase 2 中被击杀，也要造成反击伤害（炉石同时伤害规则）
        {
            if (impactActive)
            {
                // 冲击免疫正常反击伤害
                GD.Print($"[CombatManager]   🛡 冲击免疫了 {defender.CardName} 的反击伤害（{defender.Attack}）");
            }
            else
            {
                attacker.TakeDamage(defender.Attack, defender);
            }
        }

        // ===== Phase 4: 消耗冲击（一次性效果） =====
        if (impactActive)
        {
            attacker.HasImpact = false;
            GD.Print($"[CombatManager]   ✨ {attacker.CardName} 的冲击已被消耗");
        }

        defender.TriggerIdolTwilightOnAttacked();

        GD.Print($"[CombatManager]     交锋后 — {attacker.CardName}：{attacker.CurrentHealth}血，" +
                  $"{defender.CardName}：{defender.CurrentHealth}血");

        return true;
    }

    /// <summary>
    /// 执行两个随从之间的简化「战斗」——纯相互伤害，不走伏击/冲击/武器反击等复杂逻辑。
    /// 双方各自对对方造成等同于攻击力的伤害，伤害经过完整的 DamageResolver 管线
    /// （防御力、伤害翻倍等 modifier 正常生效）。遵循炉石同时伤害规则：即使一方在
    /// 伤害结算中死亡，仍能造成反击伤害。
    /// 用于机械蜈蚣-防空型等卡牌的自动拦截效果。
    /// </summary>
    /// <param name="a">战斗方 A（先手造成伤害）</param>
    /// <param name="b">战斗方 B（后手造成伤害，遵循同时伤害规则）</param>
    internal void ResolveCombat(Minion a, Minion b)
    {
        if (a.IsDead || b.IsDead) return;

        GD.Print($"[CombatManager] ⚔ 战斗：{a.CardName}（{a.Attack}攻/{a.CurrentHealth}血）vs " +
                  $"{b.CardName}（{b.Attack}攻/{b.CurrentHealth}血）");

        // A 对 B 造成伤害（完整 DamageResolver 管线）
        b.TakeDamage(a.Attack, a);

        // B 对 A 造成伤害（即使 B 已被击杀，仍遵循同时伤害规则）
        a.TakeDamage(b.Attack, b);

        GD.Print($"[CombatManager]   战斗后 — {a.CardName}：{a.CurrentHealth}血，" +
                  $"{b.CardName}：{b.CurrentHealth}血");
    }

    /// <summary>
    /// 处理随从成为攻击目标时的触发效果。
    /// 「诱饵战术」总是降低玩家敌方英雄的防御力，不根据被攻击随从阵营改向。
    /// </summary>
    /// <param name="target">被攻击的随从</param>
    internal void TriggerBaitTacticsOnAttacked(Minion target)
    {
        if (!target.HasBaitTacticsOnAttacked) return;

        EnemyHero.ModifyDefense(-1);
        GD.Print($"[CombatManager] ◆ 诱饵战术触发：{target.CardName} 受到攻击，敌方英雄防御力-1（当前 {EnemyHero.Defense}）");
    }

    /// <summary>
    /// MCP QA 入口：验证「诱饵战术」在友方/敌方随从目标上都降低玩家敌方的英雄防御力。
    /// </summary>
    public string RunBaitTacticsQa()
    {
        var baitData = GD.Load<CardData>("res://Resources/Cards/Spell_BaitTactics.tres");
        var playerMinionData = GD.Load<CardData>("res://Resources/Cards/Minion_18thRegiment.tres");
        var enemyMinionData = GD.Load<CardData>("res://Resources/Cards/Minion_Slime.tres");

        if (baitData == null || playerMinionData == null || enemyMinionData == null)
        {
            return "诱饵战术QA失败：无法加载所需卡牌资源";
        }

        PlayerHero.GainMana(20);
        int initialDefense = EnemyHero.Defense;

        var friendlyTarget = new Minion(playerMinionData, isPlayerSide: true);
        var enemyAttacker = new Minion(enemyMinionData, isPlayerSide: false);
        var friendlySpell = new OdysseyCards.Card.Card(baitData);
        AddCardToHand(friendlySpell);
        bool friendlySpellPlayed = PlaySpell(friendlySpell, friendlyTarget);
        bool friendlyBuffApplied = friendlyTarget.HasAmbush && friendlyTarget.HasImpact && friendlyTarget.HasBaitTacticsOnAttacked;
        ResolveMinionCombat(enemyAttacker, friendlyTarget);
        bool friendlyTriggerWorked = EnemyHero.Defense == initialDefense - 1;

        var enemyTarget = new Minion(enemyMinionData, isPlayerSide: false);
        var playerAttacker = new Minion(playerMinionData, isPlayerSide: true);
        var enemySpell = new OdysseyCards.Card.Card(baitData);
        AddCardToHand(enemySpell);
        bool enemySpellPlayed = PlaySpell(enemySpell, enemyTarget);
        bool enemyBuffApplied = enemyTarget.HasAmbush && enemyTarget.HasImpact && enemyTarget.HasBaitTacticsOnAttacked;
        ResolveMinionCombat(playerAttacker, enemyTarget);
        bool enemyTriggerWorked = EnemyHero.Defense == initialDefense - 2;

        NotifyCombatStateChanged();

        bool passed = friendlySpellPlayed
            && friendlyBuffApplied
            && friendlyTriggerWorked
            && enemySpellPlayed
            && enemyBuffApplied
            && enemyTriggerWorked;

        string result = passed
            ? $"诱饵战术QA通过：友方目标触发、敌方目标触发，玩家敌方的英雄防御 {initialDefense}->{EnemyHero.Defense}"
            : $"诱饵战术QA失败：friendlySpell={friendlySpellPlayed}, friendlyBuff={friendlyBuffApplied}, friendlyTrigger={friendlyTriggerWorked}, enemySpell={enemySpellPlayed}, enemyBuff={enemyBuffApplied}, enemyTrigger={enemyTriggerWorked}, defense={EnemyHero.Defense}";
        GD.Print($"[CombatManager] {result}");
        return result;
    }

    /// <summary>
    /// MCP QA 入口：验证本批新增三张卡的核心规则行为。
    /// </summary>
    public string RunNewCardsQa()
    {
        var nanoData = GD.Load<CardData>("res://Resources/Cards/Spell_NanoCorpseArt.tres");
        var idolData = GD.Load<CardData>("res://Resources/Cards/Domain_IdolTwilight.tres");
        var moonData = GD.Load<CardData>("res://Resources/Cards/Spell_MoonFishing.tres");
        var scoutData = GD.Load<CardData>("res://Resources/Cards/Minion_LianshuScout.tres");
        var slimeData = GD.Load<CardData>("res://Resources/Cards/Minion_Slime.tres");
        var alertData = GD.Load<CardData>("res://Resources/Cards/Spell_Alert.tres");
        var strikeData = GD.Load<CardData>("res://Resources/Cards/Spell_Strike.tres");
        var assaultData = GD.Load<CardData>("res://Resources/Cards/Spell_Assault.tres");
        var regimentData = GD.Load<CardData>("res://Resources/Cards/Minion_18thRegiment.tres");

        if (nanoData == null || idolData == null || moonData == null || scoutData == null || slimeData == null
            || alertData == null || strikeData == null || assaultData == null || regimentData == null)
        {
            return "新增卡牌QA失败：资源加载不完整";
        }

        PlayerHero.GainMana(50);
        PlayerHero.AddToDrawPileBottom(new Card.Card(alertData));

        var nanoTarget = new Minion(scoutData, isPlayerSide: true);
        var nanoCard = new Card.Card(nanoData);
        AddCardToHand(nanoCard);
        bool nanoPlayed = PlaySpell(nanoCard, nanoTarget);
        bool nanoReplaced = nanoTarget.HasDeathrattle
            && nanoTarget.DeathrattleEffects.Count == 1
            && nanoTarget.DeathrattleEffects[0].EffectType == CardEffectType.DrawCards
            && nanoTarget.DeathrattleEffects[0].Value == 1;

        var handMinionCard = new Card.Card(regimentData);
        var drawMinionCard = new Card.Card(scoutData);
        var discardMinionCard = new Card.Card(scoutData);
        AddCardToHand(handMinionCard);
        PlayerHero.AddToDrawPileBottom(drawMinionCard);
        PlayerHero.AddToDiscardPile(discardMinionCard);
        var boardMinion = new Minion(regimentData, isPlayerSide: true);
        Board.PlaceMinion(boardMinion, Board.GetEmptySlotIndex(isPlayerSide: true));

        var idolCard = new Card.Card(idolData);
        AddCardToHand(idolCard);
        bool idolPlayed = PlayDomain(idolCard);
        bool idolGrantedZones = handMinionCard.IdolTwilightOnAttackedStacks == 1
            && drawMinionCard.IdolTwilightOnAttackedStacks == 1
            && discardMinionCard.IdolTwilightOnAttackedStacks == 1
            && boardMinion.IdolTwilightOnAttackedStacks == 1;
        int beforeAttack = boardMinion.Attack;
        int beforeHealth = boardMinion.CurrentHealth;
        ResolveMinionCombat(new Minion(slimeData, isPlayerSide: false), boardMinion);
        bool idolTriggered = boardMinion.Attack == beforeAttack + 1
            && boardMinion.CurrentHealth == beforeHealth - slimeData.Attack + 1;

        var discardA = new Card.Card(strikeData);
        var discardB = new Card.Card(assaultData);
        var discardC = new Card.Card(alertData);
        PlayerHero.AddToDiscardPile(discardA);
        PlayerHero.AddToDiscardPile(discardB);
        PlayerHero.AddToDiscardPile(discardC);
        int discardBeforeMoon = PlayerHero.DeckState.DiscardPile.Count;
        var moonCard = new Card.Card(moonData);
        AddCardToHand(moonCard);
        bool moonPlayed = PlaySpell(moonCard, PlayerHero);
        var moonOptions = DiscoverRuntimeOptions?.Take(2).ToList() ?? new List<Card.Card>();
        while (PlayerHero.Hand.Count > 8)
        {
            PlayerHero.RemoveFromHand(PlayerHero.Hand[0]);
        }
        ConfirmDiscoverCards(moonOptions);
        bool moonMovedCards = moonOptions.Count == 2
            && moonOptions.All(c => PlayerHero.Hand.Contains(c))
            && PlayerHero.DeckState.DiscardPile.Count == discardBeforeMoon - 2 + 1;

        bool passed = nanoPlayed && nanoReplaced && idolPlayed && idolGrantedZones && idolTriggered && moonPlayed && moonMovedCards;
        string result = passed
            ? "新增卡牌QA通过：纳米散尸术替换亡语并抽牌；偶像的黄昏授予跨区域触发且被攻击后+1/+1；捞月从弃牌堆2选加入手牌"
            : $"新增卡牌QA失败：nanoPlayed={nanoPlayed}, nanoReplaced={nanoReplaced}, idolPlayed={idolPlayed}, idolGrantedZones={idolGrantedZones}, idolTriggered={idolTriggered}, moonPlayed={moonPlayed}, moonOptions={moonOptions.Count}, moonMovedCards={moonMovedCards}";
        GD.Print($"[CombatManager] {result}");
        return result;
    }

    /// <summary>
    /// 玩家随从攻击敌方随从。
    /// 通过统一战斗序列 <see cref="ResolveMinionCombat"/> 处理伤害交互，
    /// 自动支持伏击、冲击、嘲讽检测和风怒多次攻击。
    /// </summary>
    /// <param name="attacker">攻击方（必须是玩家随从）</param>
    /// <param name="defender">防御方（敌方随从）</param>
    /// <returns>攻击成功返回 true</returns>
    public bool MinionAttack(Minion attacker, Minion defender)
    {
        // 验证：玩家回合
        if (!State.IsPlayerTurn)
        {
            GD.PrintErr("[CombatManager] MinionAttack 失败 — 不是玩家回合");
            return false;
        }

        // 验证：攻击方合法性
        if (!ValidateAttacker(attacker))
            return false;

        // 消耗行动花费法力
        if (attacker.ActionCost > 0)
        {
            PlayerHero.SpendMana(attacker.ActionCost);
            GD.Print($"[CombatManager]   {attacker.CardName} 行动花费 {attacker.ActionCost} 法力，剩余法力：{PlayerHero.CurrentMana}");
        }

        // 验证：防御方有效性
        if (defender == null || defender.IsDead)
        {
            GD.PrintErr("[CombatManager] MinionAttack 失败 — 目标随从未知或已死亡");
            return false;
        }

        if (defender.IsPlayerSide)
        {
            GD.PrintErr("[CombatManager] MinionAttack 失败 — 不能攻击己方随从");
            return false;
        }

        // 嘲讽检测：敌方有嘲讽随从时，只能攻击嘲讽目标
        var enemyTaunts = Board.GetTaunts(ofEnemy: true);
        if (enemyTaunts.Count > 0 && !enemyTaunts.Contains(defender))
        {
            GD.PrintErr($"[CombatManager] MinionAttack 失败 — 敌方有 {enemyTaunts.Count} 个嘲讽随从阻挡，必须先攻击嘲讽目标");
            return false;
        }

        // 通过统一战斗序列执行随从间战斗（自动处理伏击、冲击）
        bool combatContinues = ResolveMinionCombat(attacker, defender);

        // 记录攻击次数（即使被伏击击杀也算消耗）
        RecordAttack(attacker);

        // 检查防御方死亡
        if (defender.IsDead)
        {
            GD.Print($"[CombatManager]   ☠ {defender.CardName} 被击杀");
            Board.RemoveMinion(defender);
        }

        // 检查攻击方死亡
        if (attacker.IsDead)
        {
            GD.Print($"[CombatManager]   ☠ {attacker.CardName} 在攻击中阵亡");
            Board.RemoveMinion(attacker);
            _canAttackThisTurn.Remove(attacker);
            _attackCountThisTurn.Remove(attacker);
        }

        // 全局死亡检查与胜负判定
        CheckDeaths();
        CheckVictoryOrDefeat();

        return true;
    }

    /// <summary>
    /// 玩家随从攻击敌方英雄。
    /// 需要敌方无嘲讽随从阻挡。原型阶段仅支持玩家攻击敌方英雄（敌方 AI 攻击在 Phase 4 实现）。
    /// </summary>
    /// <param name="attacker">攻击方（必须是玩家随从）</param>
    /// <param name="hero">目标英雄</param>
    /// <returns>攻击成功返回 true</returns>
    public bool MinionAttackHero(Minion attacker, Hero hero)
    {
        // 验证：玩家回合
        if (!State.IsPlayerTurn)
        {
            GD.PrintErr("[CombatManager] MinionAttackHero 失败 — 不是玩家回合");
            return false;
        }

        // 验证：攻击方合法性
        if (!ValidateAttacker(attacker))
            return false;

        // 消耗行动花费法力
        if (attacker.ActionCost > 0)
        {
            PlayerHero.SpendMana(attacker.ActionCost);
            GD.Print($"[CombatManager]   {attacker.CardName} 行动花费 {attacker.ActionCost} 法力，剩余法力：{PlayerHero.CurrentMana}");
        }

        // 嘲讽检测（攻击英雄）
        var enemyTaunts = Board.GetTaunts(ofEnemy: true);
        if (enemyTaunts.Count > 0)
        {
            GD.PrintErr($"[CombatManager] MinionAttackHero 失败 — 敌方有 {enemyTaunts.Count} 个嘲讽随从阻挡");
            return false;
        }

        GD.Print($"[CombatManager] ⚔ {attacker.CardName} 攻击敌方英雄，造成 {attacker.Attack} 点伤害" +
                  (attacker.HasImpact ? " [冲击]" : ""));

        // 冲击：攻击时免疫反击伤害（抑制敌方英雄武器反击）
        bool impactActive = attacker.HasImpact;
        if (impactActive)
            hero.SuppressWeaponCounter = true;

        hero.TakeDamage(attacker.Attack, attacker);

        if (impactActive)
        {
            hero.SuppressWeaponCounter = false;
            attacker.HasImpact = false;
            GD.Print($"[CombatManager]   🛡 冲击免疫了反击伤害，冲击已消耗");
        }

        // 记录攻击次数
        RecordAttack(attacker);

        GD.Print($"[CombatManager]   敌方英雄剩余生命值：{hero.CurrentHealth}（护甲：{hero.CurrentArmor}）");

        // 检查攻击方是否因敌方武器反击而死亡
        if (attacker.IsDead)
        {
            GD.Print($"[CombatManager]   ☠ {attacker.CardName} 在攻击英雄时被反击击杀");
            Board.RemoveMinion(attacker);
            _canAttackThisTurn.Remove(attacker);
            _attackCountThisTurn.Remove(attacker);
        }

        // 检查胜负
        if (hero.IsDead)
        {
            GD.Print("[CombatManager]   ★ 敌方英雄被击败！");
            State.SetVictory();
            OnGameOver?.Invoke(true);
        }

        return true;
    }

    // ===== 武器攻击 =====

    /// <summary>
    /// 玩家英雄使用武器攻击敌方英雄。
    /// 对敌方英雄造成武器攻击力伤害，同时受到敌方武器反击伤害。
    /// </summary>
    /// <returns>攻击成功返回 true</returns>
    public bool HeroWeaponAttackHero()
    {
        if (!State.IsPlayerTurn)
        {
            GD.PrintErr("[CombatManager] HeroWeaponAttackHero 失败 — 不是玩家回合");
            return false;
        }

        if (!PlayerHero.CanWeaponAttack())
        {
            GD.PrintErr("[CombatManager] HeroWeaponAttackHero 失败 — 武器不可用");
            return false;
        }

        if (!PlayerHero.CanSpendMana(PlayerHero.Weapon!.AttackCost))
        {
            GD.PrintErr($"[CombatManager] HeroWeaponAttackHero 失败 — 法力不足（需 {PlayerHero.Weapon.AttackCost}，现有 {PlayerHero.CurrentMana}）");
            return false;
        }

        // 消耗法力
        PlayerHero.SpendMana(PlayerHero.Weapon.AttackCost);

        // 计算武器伤害
        int weaponDamage = PlayerHero.Weapon.GetModifiedDamage(PlayerHero.Weapon.Attack);

        GD.Print($"[CombatManager] ⚔ 玩家英雄使用 {PlayerHero.Weapon.Name} 攻击敌方英雄，造成 {weaponDamage} 点伤害");

        // 对敌方英雄造成伤害（敌方英雄的武器反击由 Hero.TakeDamage → CounterAttack 自动处理）
        EnemyHero.TakeDamage(weaponDamage, PlayerHero);

        // 触发武器被动命中效果（如熔毁：目标防御-1）
        PlayerHero.Weapon?.PassiveSkill?.OnWeaponHit(EnemyHero, PlayerHero);

        // 记录武器攻击
        PlayerHero.RecordWeaponAttack();

        GD.Print($"[CombatManager]   敌方英雄剩余生命值：{EnemyHero.CurrentHealth}（护甲：{EnemyHero.CurrentArmor}）");

        // 检查我方英雄是否被敌方武器反击致死
        if (PlayerHero.IsDead)
        {
            GD.Print("[CombatManager]   ☠ 玩家英雄在武器攻击时被敌方武器反击击杀！");
            GameManager.Instance?.RunState?.FailRun();
            State.SetDefeat();
            OnGameOver?.Invoke(false);
            return true;
        }

        // 检查胜负
        if (EnemyHero.IsDead)
        {
            GD.Print("[CombatManager]   ★ 敌方英雄被击败！");
            State.SetVictory();
            OnGameOver?.Invoke(true);
        }

        return true;
    }

    /// <summary>
    /// 玩家英雄使用武器攻击敌方随从。
    /// 对敌方随从造成武器攻击力伤害，同时受到随从攻击力反击（互砍）。
    /// 武器反击在此流程中被抑制，避免无限循环。
    /// </summary>
    /// <param name="target">目标敌方随从</param>
    /// <returns>攻击成功返回 true</returns>
    public bool HeroWeaponAttackMinion(Minion target)
    {
        if (!State.IsPlayerTurn)
        {
            GD.PrintErr("[CombatManager] HeroWeaponAttackMinion 失败 — 不是玩家回合");
            return false;
        }

        if (!PlayerHero.CanWeaponAttack())
        {
            GD.PrintErr("[CombatManager] HeroWeaponAttackMinion 失败 — 武器不可用");
            return false;
        }

        if (!PlayerHero.CanSpendMana(PlayerHero.Weapon!.AttackCost))
        {
            GD.PrintErr($"[CombatManager] HeroWeaponAttackMinion 失败 — 法力不足（需 {PlayerHero.Weapon.AttackCost}，现有 {PlayerHero.CurrentMana}）");
            return false;
        }

        if (target == null || target.IsDead)
        {
            GD.PrintErr("[CombatManager] HeroWeaponAttackMinion 失败 — 目标无效");
            return false;
        }

        if (target.IsPlayerSide)
        {
            GD.PrintErr("[CombatManager] HeroWeaponAttackMinion 失败 — 不能攻击己方随从");
            return false;
        }

        // 嘲讽检测：武器攻击也受嘲讽限制
        var enemyTaunts = Board.GetTaunts(ofEnemy: true);
        if (enemyTaunts.Count > 0 && !enemyTaunts.Contains(target))
        {
            GD.PrintErr($"[CombatManager] HeroWeaponAttackMinion 失败 — 敌方有 {enemyTaunts.Count} 个嘲讽随从阻挡");
            return false;
        }

        // 消耗法力
        PlayerHero.SpendMana(PlayerHero.Weapon.AttackCost);

        // 计算武器伤害
        int weaponDamage = PlayerHero.Weapon.GetModifiedDamage(PlayerHero.Weapon.Attack);

        GD.Print($"[CombatManager] ⚔ 玩家英雄使用 {PlayerHero.Weapon.Name} 攻击 {target.CardName}，造成 {weaponDamage} 点伤害");

        TriggerBaitTacticsOnAttacked(target);

        // 伏击检查：目标有伏击且本回合未消耗时，目标先手攻击英雄
        bool targetAmbush = target.HasAmbush && !target.AmbushUsedThisTurn;
        if (targetAmbush)
        {
            target.AmbushUsedThisTurn = true;
            GD.Print($"[CombatManager]   ⚡ {target.CardName} 伏击先手，对英雄造成 {target.Attack} 伤害");
            PlayerHero.SuppressWeaponCounter = true;
            PlayerHero.TakeDamage(target.Attack, target);
            PlayerHero.SuppressWeaponCounter = false;

            // 伏击击杀英雄 → 攻击被取消
            if (PlayerHero.IsDead)
            {
                GD.Print($"[CombatManager]   ☠ 玩家英雄被 {target.CardName} 伏击击杀，攻击被取消");
                CheckVictoryOrDefeat();
                return false;
            }
        }

        // 英雄武器攻击随从（第一次伤害：英雄→随从）
        target.TakeDamage(weaponDamage, PlayerHero);

        // 触发武器被动命中效果（如熔毁：目标防御-1）
        PlayerHero.Weapon?.PassiveSkill?.OnWeaponHit(target, PlayerHero);

        // 随从反击英雄（第二次伤害：随从→英雄）。
        // 如果伏击已触发则跳过——伏击先手已经完成了随从的反击。
        // 抑制武器反击，避免英雄武器对随从的反击再次触发。
        if (!target.IsDead && !targetAmbush)
        {
            PlayerHero.SuppressWeaponCounter = true;
            PlayerHero.TakeDamage(target.Attack, target);
            PlayerHero.SuppressWeaponCounter = false;
        }

        target.TriggerIdolTwilightOnAttacked();

        // 记录武器攻击
        PlayerHero.RecordWeaponAttack();

        GD.Print($"[CombatManager]   交锋后 — 英雄剩余 {PlayerHero.CurrentHealth}HP，" +
                  $"{target.CardName}：{target.CurrentHealth}血");

        // 检查随从死亡
        if (target.IsDead)
        {
            GD.Print($"[CombatManager]   ☠ {target.CardName} 被击杀");
            Board.RemoveMinion(target);
        }

        // 全局死亡检查与胜负判定
        CheckDeaths();
        CheckVictoryOrDefeat();

        return true;
    }

    /// <summary>
    /// 执行武器主动技能。
    /// 由 CombatUI 的技能按钮触发。
    /// </summary>
    /// <returns>执行成功返回 true</returns>
    public bool UseWeaponActiveSkill()
    {
        if (!State.IsPlayerTurn)
        {
            GD.PrintErr("[CombatManager] UseWeaponActiveSkill 失败 — 不是玩家回合");
            return false;
        }

        var active = PlayerHero.Weapon?.ActiveSkill;
        if (active == null)
        {
            GD.PrintErr("[CombatManager] UseWeaponActiveSkill 失败 — 武器无主动技能");
            return false;
        }

        if (!active.CanUse(PlayerHero))
        {
            GD.PrintErr($"[CombatManager] UseWeaponActiveSkill 失败 — 技能不可用（冷却 {active.CurrentCooldown}，法力 {PlayerHero.CurrentMana}/{active.Cost}）");
            return false;
        }

        // 清除上一帧残留的目标选择
        ActiveSkillTarget = null;

        GD.Print($"[CombatManager] ★ 使用武器主动技能：{active.Name}");
        active.Execute(PlayerHero, this);

        // 触发武器被动命中效果（如熔毁：目标防御-1）
        if (ActiveSkillTarget != null)
        {
            PlayerHero.Weapon?.PassiveSkill?.OnWeaponHit(ActiveSkillTarget, PlayerHero);
            ActiveSkillTarget = null;
        }

        return true;
    }

    /// <summary>
    /// 验证攻击方随从是否可以攻击。
    /// 检查：非 null、是玩家随从、本回合可攻击、未达攻击上限、风怒判定。
    /// </summary>
    /// <param name="attacker">待验证的随从</param>
    /// <returns>可以攻击返回 true</returns>
    private bool ValidateAttacker(Minion attacker)
    {
        if (attacker == null)
        {
            GD.PrintErr("[CombatManager] 攻击验证失败 — 攻击者为 null");
            return false;
        }

        if (!attacker.IsPlayerSide)
        {
            GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 不是玩家随从");
            return false;
        }

        if (attacker.IsDead)
        {
            GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 已死亡");
            return false;
        }

        if (!_canAttackThisTurn.Contains(attacker))
        {
            GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 本回合无法攻击（召唤回合无闪击）");
            return false;
        }

        int attacks = _attackCountThisTurn.GetValueOrDefault(attacker, 0);

        if (attacks >= 2)
        {
            GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 本回合已达攻击上限");
            return false;
        }

        if (attacks >= 1 && !attacker.HasWindfury)
        {
            GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 无风怒，本回合已攻击过");
            return false;
        }

        // 行动花费检查：随从攻击需要消耗法力值
        if (attacker.ActionCost > 0 && PlayerHero.CurrentMana < attacker.ActionCost)
        {
            GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 行动花费 {attacker.ActionCost}，当前法力不足（{PlayerHero.CurrentMana}）");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 记录随从的一次攻击。根据风怒关键词决定是否保留可攻击状态。
    /// </summary>
    /// <param name="attacker">完成攻击的随从</param>
    private void RecordAttack(Minion attacker)
    {
        int newCount = _attackCountThisTurn.GetValueOrDefault(attacker, 0) + 1;
        _attackCountThisTurn[attacker] = newCount;

        // 无风怒或有风怒但已达 2 次上限 → 移除可攻击状态
        if (!attacker.HasWindfury || newCount >= 2)
        {
            _canAttackThisTurn.Remove(attacker);
        }

        GD.Print($"[CombatManager]   {attacker.CardName} 本回合攻击次数：{newCount}" +
                  (attacker.HasWindfury && newCount < 2 ? "（风怒：还可以攻击）" : ""));
    }

    // ===== 死亡检测与亡语处理 =====

    /// <summary>
    /// 遍历战场双方所有随从，移除已死亡随从并触发亡语效果。
    /// 先收集再处理以避免迭代中修改集合。
    /// 死亡随从从槽位直接收集（GetPlayerMinions 会过滤死亡随从，不能用）。
    /// </summary>
    internal void CheckDeaths()
    {
        var deadMinions = new List<Minion>();
        for (int i = 0; i < Board.MaxSlotsPerSide; i++)
        {
            if (Board.PlayerSlots[i] is Minion m && m.IsDead)
                deadMinions.Add(m);
            if (Board.EnemySlots[i] is Minion em && em.IsDead)
                deadMinions.Add(em);
        }

        foreach (var minion in deadMinions)
        {
            GD.Print($"[CombatManager] ☠ {minion.CardName}（{minion.IsPlayerSide switch { true => "玩家方", false => "敌方" }}）死亡");

            // Board.RemoveMinion 自动触发：
            //   - TriggerDeathrattle（亡语）
            //   - HandleMinionDeathPile（轮战回收 / 进入弃牌堆）
            //   - NotifyCombatStateChanged（UI 刷新）
            Board.RemoveMinion(minion);

            // 清理攻击追踪
            _canAttackThisTurn.Remove(minion);
            _attackCountThisTurn.Remove(minion);
        }
    }

    /// <summary>
    /// 触发随从的亡语效果。
    /// 原型阶段仅输出日志；后续可扩展为完整效果解析。
    /// </summary>
    /// <param name="minion">已死亡的随从</param>
    private void TriggerDeathrattle(Minion minion)
    {
        if (!minion.HasDeathrattle)
            return;

        GD.Print($"[CombatManager]   ◆ 触发亡语：{minion.CardName}");
        foreach (var effect in minion.DeathrattleEffects)
        {
            GD.Print($"[CombatManager]     亡语效果：{effect.GetDescription()}");
            ExecuteEffect(effect, minion, minion);
        }
    }

    /// <summary>
    /// 处理随从死亡后的牌堆流转（订阅 <see cref="Board.OnMinionRemoved"/> 事件自动触发）。
    /// 玩家方随从：轮战→返回抽牌堆底部，否则→进入弃牌堆。
    /// 敌方随从不参与牌堆流转。
    /// </summary>
    /// <param name="minion">已从棋盘移除的随从</param>
    private void HandleMinionDeathPile(Minion minion)
    {
        if (!minion.IsPlayerSide)
            return;

        var cardFromMinion = minion.ToRuntimeCard();
        if (minion.HasRecycle)
        {
            PlayerHero.AddToDrawPileBottom(cardFromMinion);
            GD.Print($"[CombatManager]   ♻ {minion.CardName}（轮战）返回抽牌堆底部");
        }
        else
        {
            PlayerHero.AddToDiscardPile(cardFromMinion);
            GD.Print($"[CombatManager]   🗑 {minion.CardName} 进入弃牌堆");
        }
    }

    // ===== 胜负判定 =====

    /// <summary>
    /// 检查是否达成胜利或失败条件。
    /// 所有敌方英雄死亡 → 胜利；玩家英雄死亡 → 失败。
    /// </summary>
    /// <returns>游戏结束返回 true</returns>
    internal bool CheckVictoryOrDefeat()
    {
        if (State.IsGameOver)
            return true;

        // 胜利 = 所有敌方英雄均已死亡
        if (EnemyUnits.All(u => u.Body.IsDead))
        {
            GD.Print("[CombatManager] ★★★ 敌方全部被击败 — 玩家胜利！★★★");

            // 跨战斗保存玩家生命值（持久化到 GameManager）
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SavePlayerHealth(PlayerHero.CurrentHealth, PlayerHero.MaxHealth);
                GD.Print($"[CombatManager] 已保存玩家生命值：{PlayerHero.CurrentHealth}/{PlayerHero.MaxHealth}");
            }

            State.SetVictory();
            OnGameOver?.Invoke(true);
            return true;
        }

        if (PlayerHero.IsDead)
        {
            GD.Print("[CombatManager] ☠☠☠ 玩家英雄被击败 — 玩家失败 ☠☠☠");

            // 标记运行失败
            var gm = GameManager.Instance;
            gm?.RunState?.FailRun();

            State.SetDefeat();
            OnGameOver?.Invoke(false);
            return true;
        }

        return false;
    }

    // ===== 回合管理 =====

    /// <summary>
    /// 结束玩家回合。清理攻击状态 → 执行敌方 AI 回合 → 开始新玩家回合。
    /// </summary>
    public void EndPlayerTurn()
    {
        if (!State.IsPlayerTurn)
        {
            GD.PrintErr("[CombatManager] EndPlayerTurn 失败 — 当前不是玩家回合");
            return;
        }

        GD.Print("[CombatManager] ========== 玩家回合结束 ==========");

        // 清理本回合攻击追踪
        _canAttackThisTurn.Clear();
        _attackCountThisTurn.Clear();

        // 触发领域效果 — 友方回合结束时
        TriggerDomainsOnTurnEnd();

        // 状态效果衰减 — 友方回合结束时
        PlayerHero.TickStatusEffects(TickTiming.PlayerTurnEnd);
        PlayerHero.ResetDamageTakenThisTurn(); // 玩家每回合重置受伤标记
        foreach (var unit in EnemyUnits)
            unit.Body.TickStatusEffects(TickTiming.PlayerTurnEnd);
        // 重置每回合受伤标记（不破等被动用）
        foreach (var unit in EnemyUnits)
            unit.Body.ResetDamageTakenThisTurn();

        // Minion 状态效果衰减 — 友方回合结束时
        foreach (var minion in Board.GetPlayerMinions())
            minion.TickStatusEffects(TickTiming.PlayerTurnEnd);
        foreach (var minion in Board.GetEnemyMinions())
            minion.TickStatusEffects(TickTiming.PlayerTurnEnd);

        // 切换到敌方回合
        State.EndPlayerTurn();
        GD.Print($"[CombatManager] ---------- 敌方回合开始（{EnemyUnits.Count} 个敌人）----------");

        // 执行敌方回合
        ExecuteEnemyTurn();

        // 敌方回合结束，切换回玩家回合
        State.EndEnemyTurn();

        // 开始玩家新回合
        StartPlayerTurn();

        // 检查胜负
        CheckVictoryOrDefeat();
    }

    /// <summary>
    /// 执行敌方 AI 回合：依次执行每个敌人的意图 → 推进意图轮转 → 敌方随从攻击 → 死亡检查 → 胜负判定。
    /// 执行期间冻结意图 UI 刷新（_isEnemyTurnAnimating），防止动画中数值跳变。
    /// </summary>
    private void ExecuteEnemyTurn()
    {
        // 冻结意图 UI 刷新——防止执行动画期间数值跳变
        _isEnemyTurnAnimating = true;

        // 0. 快照本回合开始时已存在的敌方随从——只有它们可以攻击
        _enemyMinionsCanAttack.Clear();
        foreach (var m in Board.GetEnemyMinions())
        {
            if (!m.IsDead) _enemyMinionsCanAttack.Add(m);
        }

        // 1. 依次执行每个敌人的当前意图（攻击/防御/召唤等）——使用动态目标选择
        foreach (var unit in EnemyUnits)
        {
            // 跳过已死亡的敌人
            if (unit.Body.IsDead) continue;

            // 同步敌方攻击力到意图系统（考虑武器禁用等状态）
            unit.Brain.Attack = unit.Body.Weapon is { IsDisabled: false } ? unit.Body.Weapon.Attack : 0;
            unit.Brain.ExecuteIntent(this, unit.Body);

            // 2. 推进到下一意图
            unit.Brain.AdvanceIntent();
            GD.Print($"[CombatManager] {unit.Brain.Name} 下回合意图：{unit.Brain.GetCurrentIntent(this, unit.Body).Description}");

            // 每次执行后检查死亡（攻击意图可能杀死敌人自身或玩家）
            CheckDeaths();
            if (CheckVictoryOrDefeat())
            {
                _isEnemyTurnAnimating = false;
                return;
            }
        }

        // 3. 敌方随从攻击
        EnemyMinionsAttack();

        // 4. 全局死亡检查（意图/攻击可能造成随从死亡）
        CheckDeaths();

        // 5. 胜负判定
        CheckVictoryOrDefeat();

        // 6. 解冻——允许 UI 重新响应状态变更
        _isEnemyTurnAnimating = false;

        // 7. 状态效果衰减 — 敌方回合结束时（武器禁用等 debuff 在此触发）
        PlayerHero.TickStatusEffects(TickTiming.EnemyTurnEnd);
        foreach (var unit in EnemyUnits)
            unit.Body.TickStatusEffects(TickTiming.EnemyTurnEnd);

        // Minion 状态效果衰减 — 敌方回合结束时
        foreach (var minion in Board.GetPlayerMinions())
            minion.TickStatusEffects(TickTiming.EnemyTurnEnd);
        foreach (var minion in Board.GetEnemyMinions())
            minion.TickStatusEffects(TickTiming.EnemyTurnEnd);

        // 8. 通知 UI 刷新意图显示（解冻后触发）
        NotifyCombatStateChanged();
    }

    /// <summary>
    /// 敌方所有随从依次攻击：有嘲讽时攻击嘲讽随从，无嘲讽时攻击玩家英雄。
    /// </summary>
    private void EnemyMinionsAttack()
    {
            // 回合开始时已存在的随从可以攻击，有闪击的新召唤随从也可以
        var enemies = Board.GetEnemyMinions()
            .Where(m => !m.IsDead && (_enemyMinionsCanAttack.Contains(m) || m.HasCharge))
            .ToList();
        if (enemies.Count == 0) return;

        var playerTaunts = Board.GetTaunts(ofEnemy: false);
        bool hasPlayerTaunt = playerTaunts.Count > 0;

        foreach (var attacker in enemies)
        {
            if (attacker.IsDead) continue;

            // 确保所有敌方随从有意图大脑（供 UI 意图显示使用）
            attacker.IntentBrain ??= new DefaultAttackMinionBrain(attacker);

            // 自定义随从大脑？优先使用
            if (attacker.IntentBrain != null)
            {
                attacker.IntentBrain.ExecuteIntent(this);
                attacker.IntentBrain.AdvanceIntent();
                continue;
            }

            // 默认行为：嘲讽随从优先攻击嘲讽，否则攻击英雄
            if (hasPlayerTaunt)
            {
                // 攻击随机嘲讽随从
                var tauntTargets = playerTaunts.Where(t => !t.IsDead).ToList();
                if (tauntTargets.Count == 0) continue;
                var defender = tauntTargets[new Random().Next(tauntTargets.Count)];

                // 通过统一战斗序列执行（自动处理伏击、冲击）
                ResolveMinionCombat(attacker, defender);

                if (defender.IsDead)
                {
                    Board.RemoveMinion(defender);
                }
                if (attacker.IsDead)
                {
                    Board.RemoveMinion(attacker);
                }
            }
            else
            {
                // 攻击玩家英雄
                GD.Print($"[CombatManager] ⚔ 敌方 {attacker.CardName} 攻击玩家英雄，造成 {attacker.Attack} 伤" +
                          (attacker.HasImpact ? " [冲击]" : ""));

                // 冲击：攻击时免疫武器反击
                bool impactActive = attacker.HasImpact;
                if (impactActive)
                    PlayerHero.SuppressWeaponCounter = true;

                PlayerHero.TakeDamage(attacker.Attack, attacker);

                if (impactActive)
                {
                    PlayerHero.SuppressWeaponCounter = false;
                    attacker.HasImpact = false;
                    GD.Print($"[CombatManager]   🛡 冲击免疫了武器反击，冲击已消耗");
                }
            }
        }
    }

    /// <summary>
    /// 重置本回合攻击追踪：清空计数器并将所有玩家随从设为可攻击。
    /// </summary>
    private void ResetAttackTracking()
    {
        _canAttackThisTurn.Clear();
        _attackCountThisTurn.Clear();

        foreach (var minion in Board.GetPlayerMinions())
        {
            // 上回合已存在（非新召唤）的随从可以攻击
            _canAttackThisTurn.Add(minion);
        }
    }

    /// <summary>
    /// 将一张卡牌直接加入玩家手牌（用于 DevConsole /token 命令）。
    /// </summary>
    public void AddCardToHand(OdysseyCards.Card.Card card)
    {
        _playerCore.AddToHand(card);
    }

    // ===== 发现选牌系统 =====

    /// <summary>
    /// 处理发现效果——从卡牌池中随机生成 N 张候选卡牌，进入发现选牌阶段。
    /// </summary>
    /// <param name="effect">Discover 效果数据。Value=选项数量，TargetType=稀有度过滤（可选，"all"=全部）</param>
    private void HandleDiscoverEffect(CardEffectData effect)
    {
        int count = effect.Value > 0 ? effect.Value : 3;
        var pool = GetRandomCardsFromPool(count);
        if (pool.Count == 0)
        {
            GD.PrintErr("[CombatManager] HandleDiscoverEffect 失败 — 卡牌池为空");
            return;
        }

        _pendingDiscoverOptions = pool;
        _pendingDiscoverRuntimeOptions = null;
        DiscoverPickCount = 1;
        _pendingSelectionMode = PendingSelectionMode.Discover;
        State.SetDiscovering();
        GD.Print($"[CombatManager] ◆ 发现：展示 {pool.Count} 张候选卡牌");
        foreach (var c in pool)
            GD.Print($"[CombatManager]     {c.GetLocalizedName()} — {c.Description}");

        NotifyCombatStateChanged();
    }

    /// <summary>
    /// 确认发现选牌结果——由 DiscoverUI 在选择/跳过时调用。
    /// </summary>
    /// <param name="chosen">玩家选中的卡牌数据，null 表示跳过</param>
    public void ConfirmDiscoverChoice(CardData? chosen)
    {
        if (!IsDiscovering)
        {
            GD.PrintErr("[CombatManager] ConfirmDiscoverChoice 失败 — 不在发现阶段");
            return;
        }

        if (chosen != null)
        {
            GD.Print($"[CombatManager] ◆ 发现选牌：{chosen.GetLocalizedName()}");

            // 检查手牌上限
            if (_playerCore.Hand.Count >= MaxHandSize)
            {
                GD.Print($"[CombatManager]   手牌已满（{MaxHandSize}张），{chosen.GetLocalizedName()} 被烧毁！");
            }
            else
            {
                var card = new OdysseyCards.Card.Card(chosen);
                _playerCore.AddToHand(card);
                GD.Print($"[CombatManager]   已将 {chosen.GetLocalizedName()} 加入手牌（共 {_playerCore.Hand.Count} 张）");
            }
        }
        else
        {
            GD.Print("[CombatManager] ◆ 发现选牌：跳过");
        }

        // 移除触发发现的法术牌
        if (_pendingDiscoverSpellCard != null)
        {
            PlayerHero.RemoveFromHand(_pendingDiscoverSpellCard);
            _pendingDiscoverSpellCard = null;
        }

        // 清除发现状态
        _pendingDiscoverOptions = null;
        _pendingDiscoverRuntimeOptions = null;
        DiscoverPickCount = 1;
        _pendingSelectionMode = PendingSelectionMode.Discover;
        State.ResumePlayerTurn();

        // 检查死亡和胜负
        CheckDeaths();
        CheckVictoryOrDefeat();

        NotifyCombatStateChanged();
        GD.Print("[CombatManager] 发现选牌完成，恢复玩家回合");
    }

    /// <summary>
    /// 取消发现选牌（等同跳过）。
    /// </summary>
    public void CancelDiscover()
    {
        ConfirmDiscoverChoice(null);
    }

    /// <summary>
    /// 确认运行时卡牌选择结果。当前用于「捞月」从弃牌堆移牌回手牌。
    /// </summary>
    public void ConfirmDiscoverCards(IReadOnlyList<Card.Card> chosenCards)
    {
        if (!IsDiscovering)
        {
            GD.PrintErr("[CombatManager] ConfirmDiscoverCards 失败 — 不在选牌阶段");
            return;
        }

        if (_pendingDiscoverSpellCard != null)
        {
            PlayerHero.DiscardCard(_pendingDiscoverSpellCard);
            _pendingDiscoverSpellCard = null;
        }

        if (_pendingSelectionMode == PendingSelectionMode.Discard)
        {
            int moved = 0;
            foreach (var card in chosenCards)
            {
                if (moved >= DiscoverPickCount) break;
                if (_playerCore.Hand.Count >= MaxHandSize)
                {
                    GD.Print($"[CombatManager]   手牌已满（{MaxHandSize}张），停止加入弃牌堆卡牌");
                    break;
                }

                if (PlayerHero.DeckState.MoveFromDiscardToHand(card))
                    moved++;
            }
            GD.Print($"[CombatManager] ◆ 捞月完成：加入 {moved} 张牌");
        }
        else if (_pendingSelectionMode == PendingSelectionMode.ChooseDiscard)
        {
            int discarded = 0;
            foreach (var card in chosenCards)
            {
                if (discarded >= DiscoverPickCount) break;
                PlayerHero.DiscardCard(card);
                discarded++;
            }
            GD.Print($"[CombatManager] ◆ 弃牌完成：弃掉 {discarded} 张牌");
        }
        else if (_pendingSelectionMode == PendingSelectionMode.BladeCrisis)
        {
            int discarded = 0;
            foreach (var card in chosenCards)
            {
                PlayerHero.DiscardCard(card);
                discarded++;
            }
            GD.Print($"[CombatManager]   刀盾危机弃牌：弃掉{discarded}张");

            var tokenData = GD.Load<CardData>("res://Resources/Cards/Minion_WhatTheDogDoing.tres");
            if (tokenData != null)
            {
                for (int i = 0; i < discarded; i++)
                {
                    int? emptySlot = Board.GetEmptySlotIndex(isPlayerSide: true);
                    if (emptySlot.HasValue)
                    {
                        var tokenMinion = new Minion(tokenData, isPlayerSide: true);
                        Board.PlaceMinion(tokenMinion, emptySlot.Value);
                        GD.Print($"[CombatManager]   刀盾危机：在槽位{emptySlot.Value}放置我的刀盾");
                    }
                    else
                    {
                        GD.Print($"[CombatManager]   刀盾危机：棋盘已满，停止放置（已放{i}个）");
                        break;
                    }
                }
            }
            else GD.PrintErr("[CombatManager] 刀盾危机：无法加载我的刀盾Token卡牌");

            PlayerHero.DrawCards(discarded);
            GD.Print($"[CombatManager] ◆ 刀盾危机完成：弃{discarded}张，抽{discarded}张");
        }

        _pendingDiscoverOptions = null;
        _pendingDiscoverRuntimeOptions = null;
        DiscoverPickCount = 1;
        _pendingSelectionMode = PendingSelectionMode.Discover;
        State.ResumePlayerTurn();

        CheckDeaths();
        CheckVictoryOrDefeat();
        NotifyCombatStateChanged();
        GD.Print("[CombatManager] 选牌完成，恢复玩家回合");
    }

    /// <summary>
    /// 从全卡牌池中随机抽取不重复的 N 张卡牌。
    /// 加载 Resources/Cards/ 下所有 .tres 文件，Fisher-Yates 洗牌后取前 N 张。
    /// </summary>
    /// <param name="count">需要的卡牌数量</param>
    /// <returns>随机卡牌列表</returns>
    private List<CardData> GetRandomCardsFromPool(int count)
    {
        var pool = new List<CardData>();

        // 加载 Resources/Cards/ 下所有 .tres 文件
        using var dir = DirAccess.Open("res://Resources/Cards/");
        if (dir != null)
        {
            dir.ListDirBegin();
            string fileName = dir.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (!dir.CurrentIsDir() && fileName.EndsWith(".tres", System.StringComparison.OrdinalIgnoreCase))
                {
                    var cardData = GD.Load<CardData>($"res://Resources/Cards/{fileName}");
                    if (cardData != null && !string.IsNullOrEmpty(cardData.Id))
                    {
                        pool.Add(cardData);
                    }
                }
                fileName = dir.GetNext();
            }
            dir.ListDirEnd();
        }

        GD.Print($"[CombatManager] GetRandomCardsFromPool: 卡牌池共 {pool.Count} 张，请求 {count} 张");

        // 排除不可发现的卡牌（如「发现」自身不能发现「发现」）
        var nonDiscoverableIds = new System.Collections.Generic.HashSet<string>
        {
            "spell_Discover",
        };
        pool.RemoveAll(c => nonDiscoverableIds.Contains(c.Id));
        GD.Print($"[CombatManager]   过滤后池共 {pool.Count} 张");

        if (pool.Count <= count)
            return pool;

        // Fisher-Yates 洗牌后取前 count 张
        using var rng = new RandomNumberGenerator();
        rng.Randomize();
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.RandiRange(0, i);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.Take(count).ToList();
    }

    /// <summary>
    /// 从弃牌堆中随机抽取不重复的 N 张运行时卡牌。
    /// </summary>
    private List<Card.Card> GetRandomCardsFromDiscard(int count)
    {
        var pool = PlayerHero.DeckState.DiscardPile.ToList();
        if (pool.Count <= count)
            return pool;

        using var rng = new RandomNumberGenerator();
        rng.Randomize();
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.RandiRange(0, i);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.Take(count).ToList();
    }

    /// <summary>
    /// 机械蜈蚣-防空型自动拦截触发器。
    /// 订阅 <see cref="Board.OnMinionPlaced"/> 事件，当敌方部署费用 ≤2 的随从时，
    /// 玩家方的每只机械蜈蚣-防空型自动与该随从战斗。
    /// 战斗后清理死亡随从并触发死亡检查。
    /// </summary>
    /// <param name="placedMinion">刚被部署的随从</param>
    /// <param name="slotIndex">部署槽位</param>
    private void OnMinionPlacedForCentipede(Minion placedMinion, int slotIndex)
    {
        // 仅敌方部署时触发
        if (placedMinion.IsPlayerSide)
            return;

        // 仅拦截费用 ≤2 的低费随从
        if (placedMinion.Cost > 2)
            return;

        // 收集玩家方所有存活的机械蜈蚣-防空型（战斗前快照，防止迭代中修改集合）
        var centipedes = new List<Minion>();
        for (int i = 0; i < Board.MaxSlotsPerSide; i++)
        {
            if (Board.PlayerSlots[i] is Minion m
                && !m.IsDead
                && m.Id == "minion_centipede_aa")
            {
                centipedes.Add(m);
            }
        }

        if (centipedes.Count == 0)
            return;

        foreach (var centipede in centipedes)
        {
            if (centipede.IsDead) continue;
            if (placedMinion.IsDead) break;

            GD.Print($"[CombatManager] ◆ 机械蜈蚣-防空型拦截 {placedMinion.CardName}（{placedMinion.Cost}费）！");
            ResolveCombat(centipede, placedMinion);
        }

        // 清理战斗产生的死亡随从
        if (placedMinion.IsDead)
            Board.RemoveMinion(placedMinion);

        var deadPlayerMinions = new List<Minion>();
        for (int i = 0; i < Board.MaxSlotsPerSide; i++)
        {
            if (Board.PlayerSlots[i] is Minion m && m.IsDead)
                deadPlayerMinions.Add(m);
        }
        foreach (var m in deadPlayerMinions)
            Board.RemoveMinion(m);

        // 全局清理：蜈蚣战斗可能连锁触发其他死亡
        CheckDeaths();
        CheckVictoryOrDefeat();
    }

    /// <summary>
    /// 验证目标是否满足卡牌的目标过滤条件。
    /// 使用子集匹配规则：card.TargetFilter 必须是 entity.GetTargetTags() 的子集。
    /// TargetFilter 为 None 时放行所有目标。
    /// </summary>
    /// <param name="card">要打出的卡牌</param>
    /// <param name="target">目标实体（Minion 或 Hero）</param>
    /// <returns>目标合法返回 true</returns>
    private static bool ValidateTarget(OdysseyCards.Card.Card card, object target)
    {
        var require = card.Data.TargetFilter;
        var exclude = card.Data.ExcludeFilter;

        if (require == TargetTags.None && exclude == TargetTags.None)
            return true;

        // 已死亡的随从不可选为目标
        if (target is Minion minion && minion.IsDead)
            return false;

        TargetTags entityTags = target switch
        {
            Hero hero => hero.GetTargetTags(),
            Minion m => m.GetTargetTags(),
            _ => TargetTags.None
        };

        // 如果目标类型无法识别（entityTags 为 None），仅当无过滤条件时才放行
        if (entityTags == TargetTags.None)
            return require == TargetTags.None && exclude == TargetTags.None;

        return TargetTagsHelper.IsValidTarget(entityTags, require, exclude);
    }
}
