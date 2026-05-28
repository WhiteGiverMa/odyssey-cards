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
    /// 敌方英雄。
    /// </summary>
    public Hero EnemyHero { get; private set; }

    /// <summary>
    /// 玩家角色（Godot Node 引用，用于场景树交互）。
    /// </summary>
    public Player Player { get; private set; }

    /// <summary>
    /// 当前敌方 AI 遭遇实例。
    /// </summary>
    private EnemyEncounter _currentEnemy = null!;

    /// <summary>
    /// 敌方意图变化事件（参数为意图描述文本）。
    /// </summary>
    public event Action<string>? OnEnemyIntentChanged;

    /// <summary>
    /// 游戏结束事件（参数为 true=胜利, false=失败）。
    /// </summary>
    public event Action<bool>? OnGameOver;

    // ===== 随从攻击追踪 =====

    /// <summary>
    /// 本回合内每个随从的已攻击次数（键为随从实例，值为攻击次数）。
    /// 用于风怒（Windfury）多段攻击判定和攻击上限检查。
    /// </summary>
    private readonly Dictionary<Minion, int> _attackCountThisTurn = new();

    /// <summary>
    /// 本回合内可以攻击的随从集合。
    /// 新召唤的随从默认不可攻击（除非有冲锋）；
    /// 回合开始时所有玩家随从重置为可攻击状态。
    /// </summary>
    private readonly HashSet<Minion> _canAttackThisTurn = new();

    /// <summary>
    /// 本回合内可以攻击的敌方随从集合。
    /// 敌方意图/召唤产生的随从默认不能立即攻击——只有回合开始时已存在的随从可以攻击。
    /// 在 <see cref="ExecuteEnemyTurn"/> 开始时快照，在 <see cref="EnemyMinionsAttack"/> 中使用。
    /// </summary>
    private readonly HashSet<Minion> _enemyMinionsCanAttack = new();

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

        // 4. 创建敌方英雄和 AI 遭遇（从 GameRunState 读取遭遇类型）
        var runState = gm.RunState;
        if (runState != null && runState.SelectedRoom != null &&
            runState.SelectedRoom.Type is RoomType.Monster or RoomType.Elite or RoomType.Boss)
        {
            _currentEnemy = runState.CreateEncounter();
            GD.Print($"[CombatManager] 从 RunState 读取敌人 — {_currentEnemy.Name}（{runState.SelectedRoom.Type}）");
        }
        else
        {
            // 回退：如果没有运行状态（例如直接从 Combat.tscn 启动），使用默认邪教徒
            _currentEnemy = new Cultist();
            GD.Print($"[CombatManager] 回退使用默认敌人 — {_currentEnemy.Name}");
        }

        var enemyCore = new CommanderCore();
        enemyCore.InitializeHealth(_currentEnemy.MaxHealth, _currentEnemy.CurrentHealth);
        enemyCore.SetMana(0, 0);
        var enemyHero = new Hero(enemyCore);
        GD.Print($"[CombatManager] 敌方已创建 — {_currentEnemy.Name}，{enemyHero.CurrentHealth}/{enemyHero.MaxHealth}HP");

        // 5. 初始化战斗管理器（创建 _playerCore、PlayerHero、Board、GameState）
        Initialize(player, enemyHero);

        // 6. 获取 CombatUI 并初始化
        var combatUI = GetNode<CombatUI>("CanvasLayer/CombatUI");
        combatUI.Initialize(player, this);
        GD.Print("[CombatManager] CombatUI 已初始化");

        // 7. 开始战斗
        StartCombat();
        combatUI.RefreshAll(); // StartCombat 中法力变化后刷新 UI

        // 触发初始意图事件
        OnEnemyIntentChanged?.Invoke(_currentEnemy.GetCurrentIntent().Description);

        GD.Print("[CombatManager] BootstrapCombat 完成");
    }

    // ===== 初始化 =====

    /// <summary>
    /// 初始化战斗管理器。
    /// 创建战场和游戏状态，构建玩家英雄包装，存储敌方英雄引用。
    /// </summary>
    /// <param name="player">玩家角色（Godot Node）</param>
    /// <param name="enemyHero">敌方英雄实例</param>
    /// <exception cref="ArgumentNullException">当 player 或 enemyHero 为 null 时抛出</exception>
    public void Initialize(Player player, Hero enemyHero)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        EnemyHero = enemyHero ?? throw new ArgumentNullException(nameof(enemyHero));

        // 创建玩家英雄专用的 CommanderCore，共享牌堆定义
        _playerCore = new CommanderCore();
        _playerCore.Deck = player.Deck;
        _playerCore.InitializeHealth(player.MaxHealth, player.CurrentHealth);
        _playerCore.SetMana(0, 0);
        PlayerHero = new Hero(_playerCore);

        Board = new Board();
        State = new GameState();

        GD.Print($"[CombatManager] 初始化完成 — 玩家 {PlayerHero.CurrentHealth}/{PlayerHero.MaxHealth}，" +
                  $"敌方 {EnemyHero.CurrentHealth}/{EnemyHero.MaxHealth}");
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
        State.StartPlayerTurn();
        _playerCore.SetMana(State.PlayerMana, State.PlayerMaxMana);

        // 回合开始抽 1 张牌
        PlayerHero.DrawCards(1);

        // 重置随从攻击状态
        ResetAttackTracking();

        GD.Print($"[CombatManager] 第 {State.TurnCount} 回合开始（法力 {State.PlayerMana}/{State.PlayerMaxMana}），手牌 {_playerCore.Hand.Count} 张");
    }

    // ===== 随从召唤 =====

    /// <summary>
    /// 玩家打出一张随从牌，将其召唤至战场的指定槽位。
    /// 验证玩家回合、法力值、槽位可用性，处理战吼和冲锋关键词。
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

        // 创建随从运行时实例
        var minion = new Minion(card.Data, isPlayerSide: true);

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

        // 冲锋关键词：召唤的回合即可攻击
        if (minion.HasCharge)
        {
            _canAttackThisTurn.Add(minion);
            GD.Print($"[CombatManager]   ⚡ {minion.CardName} 具有冲锋，本回合可以攻击");
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
        ExecuteEffect(effect, source);
    }

    // ===== 法术施放 =====

    /// <summary>
    /// 玩家打出一张法术牌，对目标施放效果。
    /// 目标可以是随从（Minion）或英雄（Hero），通过 IDamageTarget 接口统一处理伤害。
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

        // 消耗法力值
        PlayerHero.SpendMana(card.Cost);
        GD.Print($"[CombatManager] 施放法术 {card.CardName}，消耗 {card.Cost} 法力值");

        // 解析每个法术效果
        foreach (var effect in card.Data.Effects)
        {
            ResolveSpellEffect(effect, target);
        }

        // 从手牌中弃掉
        PlayerHero.RemoveFromHand(card);

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
    private void ExecuteEffect(CardEffectData effect, object target)
    {
        switch (effect.EffectType)
        {
            // ----- 伤害类 -----
            case CardEffectType.Damage:
            case CardEffectType.DealDamageToTarget:
                if (target is Minion minionTarget)
                {
                    minionTarget.TakeDamage(effect.Value, null);
                    GD.Print($"[CombatManager]   对 {minionTarget.CardName} 造成 {effect.Value} 点伤害");
                }
                else if (target is Hero heroTarget)
                {
                    heroTarget.TakeDamage(effect.Value, null);
                    GD.Print($"[CombatManager]   对英雄造成 {effect.Value} 点伤害");
                }
                else
                {
                    GD.PrintErr($"[CombatManager]   目标类型不支持伤害");
                }
                break;

            case CardEffectType.DealDamageToEnemyHero:
                EnemyHero.TakeDamage(effect.Value, null);
                GD.Print($"[CombatManager]   对敌方英雄造成 {effect.Value} 点伤害（剩余 {EnemyHero.CurrentHealth}）");
                break;

            case CardEffectType.DealDamageToAllEnemies:
                {
                    int hitCount = 0;
                    foreach (var enemyMinion in Board.GetEnemyMinions())
                    {
                        enemyMinion.TakeDamage(effect.Value, null);
                        hitCount++;
                    }
                    GD.Print($"[CombatManager]   对所有敌方随从造成 {effect.Value} 点伤害（命中 {hitCount} 个目标）");
                }
                break;

            // ----- 抽牌 -----
            case CardEffectType.DrawCards:
                PlayerHero.DrawCards(effect.Value);
                GD.Print($"[CombatManager]   抽 {effect.Value} 张牌");
                break;

            // ----- 治疗与护甲 -----
            case CardEffectType.Heal:
                _playerCore.Heal(effect.Value);
                GD.Print($"[CombatManager]   恢复 {effect.Value} 点生命值（当前 {PlayerHero.CurrentHealth}）");
                break;

            case CardEffectType.RestoreHealth:
            case CardEffectType.GainArmor:
                PlayerHero.GainArmor(effect.Value);
                GD.Print($"[CombatManager]   获得 {effect.Value} 点护甲（当前 {PlayerHero.CurrentArmor}）");
                break;

            case CardEffectType.GainMaxHealth:
                _playerCore.InitializeHealth(
                    _playerCore.MaxHealth + effect.Value,
                    _playerCore.CurrentHealth + effect.Value);
                GD.Print($"[CombatManager]   最大生命值 +{effect.Value} 并恢复等量生命值（当前 {PlayerHero.CurrentHealth}/{PlayerHero.MaxHealth}）");
                break;

            // ----- 召唤随从 -----
            case CardEffectType.SummonMinion:
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
                break;

            // ----- 强化随从 -----
            case CardEffectType.BuffMinion:
                if (target is Minion buffTarget)
                {
                    GD.Print($"[CombatManager]   BuffMinion：{effect.GetDescription()} → {buffTarget.CardName}（原型：暂未实现属性修改）");
                }
                else
                {
                    GD.Print($"[CombatManager]   BuffMinion 需要有效的随从目标");
                }
                break;

            // ----- 未处理类型 -----
            default:
                GD.Print($"[CombatManager]   未处理的效果类型：{effect.EffectType}（{effect.GetDescription()}）");
                break;
        }
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
    /// 玩家随从攻击敌方随从。
    /// 双方同时造成伤害（炉石规则），支持嘲讽检测和风怒多次攻击。
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
        var enemyTaunts = Board.GetTaunts(isEnemy: true);
        if (enemyTaunts.Count > 0 && !enemyTaunts.Contains(defender))
        {
            GD.PrintErr($"[CombatManager] MinionAttack 失败 — 敌方有 {enemyTaunts.Count} 个嘲讽随从阻挡，必须先攻击嘲讽目标");
            return false;
        }

        // 双方同时造成伤害
        GD.Print($"[CombatManager] ⚔ {attacker.CardName}（{attacker.Attack}攻/{attacker.CurrentHealth}血）攻击 " +
                  $"{defender.CardName}（{defender.Attack}攻/{defender.CurrentHealth}血）");

        defender.TakeDamage(attacker.Attack, attacker);
        attacker.TakeDamage(defender.Attack, defender);

        // 记录攻击次数
        RecordAttack(attacker);

        GD.Print($"[CombatManager]   交锋后 — {attacker.CardName}：{attacker.CurrentHealth}血，" +
                  $"{defender.CardName}：{defender.CurrentHealth}血");

        // 检查防御方死亡
        if (defender.IsDead)
        {
            GD.Print($"[CombatManager]   ☠ {defender.CardName} 被击杀");
            Board.RemoveMinion(defender);
            TriggerDeathrattle(defender);
        }

        // 检查攻击方死亡
        if (attacker.IsDead)
        {
            GD.Print($"[CombatManager]   ☠ {attacker.CardName} 在攻击中阵亡");
            Board.RemoveMinion(attacker);
            TriggerDeathrattle(attacker);
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
        var enemyTaunts = Board.GetTaunts(isEnemy: true);
        if (enemyTaunts.Count > 0)
        {
            GD.PrintErr($"[CombatManager] MinionAttackHero 失败 — 敌方有 {enemyTaunts.Count} 个嘲讽随从阻挡");
            return false;
        }

        GD.Print($"[CombatManager] ⚔ {attacker.CardName} 攻击敌方英雄，造成 {attacker.Attack} 点伤害");

        hero.TakeDamage(attacker.Attack, attacker);

        // 记录攻击次数
        RecordAttack(attacker);

        GD.Print($"[CombatManager]   敌方英雄剩余生命值：{hero.CurrentHealth}（护甲：{hero.CurrentArmor}）");

        // 检查胜负
        if (hero.IsDead)
        {
            GD.Print("[CombatManager]   ★ 敌方英雄被击败！");
            State.SetVictory();
            OnGameOver?.Invoke(true);
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
            GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 本回合无法攻击（召唤回合无冲锋）");
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
    /// </summary>
    internal void CheckDeaths()
    {
        var deadMinions = Board.GetPlayerMinions()
            .Where(m => m.IsDead)
            .Concat(Board.GetEnemyMinions().Where(m => m.IsDead))
            .ToList();

        foreach (var minion in deadMinions)
        {
            GD.Print($"[CombatManager] ☠ {minion.CardName}（{minion.IsPlayerSide switch { true => "玩家方", false => "敌方" }}）死亡");

            Board.RemoveMinion(minion);
            TriggerDeathrattle(minion);

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
    internal void TriggerDeathrattle(Minion minion)
    {
        if (!minion.HasDeathrattle)
            return;

        GD.Print($"[CombatManager]   ◆ 触发亡语：{minion.CardName}");
        foreach (var effect in minion.DeathrattleEffects)
        {
            GD.Print($"[CombatManager]     亡语效果：{effect.GetDescription()}");
            ExecuteEffect(effect, minion);
        }
    }

    // ===== 胜负判定 =====

    /// <summary>
    /// 检查是否达成胜利或失败条件。
    /// 敌方英雄死亡 → 胜利；玩家英雄死亡 → 失败。
    /// </summary>
    /// <returns>游戏结束返回 true</returns>
    internal bool CheckVictoryOrDefeat()
    {
        if (State.IsGameOver)
            return true;

        if (EnemyHero.IsDead)
        {
            GD.Print("[CombatManager] ★★★ 敌方英雄被击败 — 玩家胜利！★★★");

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

        // 切换到敌方回合
        State.EndPlayerTurn();
        GD.Print($"[CombatManager] ---------- 敌方回合开始（{_currentEnemy.Name}）----------");

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
    /// 执行敌方 AI 回合：执行当前意图 → 推进意图轮转 → 敌方随从攻击 → 死亡检查 → 胜负判定。
    /// </summary>
    private void ExecuteEnemyTurn()
    {
        // 0. 快照本回合开始时已存在的敌方随从——只有它们可以攻击
        _enemyMinionsCanAttack.Clear();
        foreach (var m in Board.GetEnemyMinions())
        {
            if (!m.IsDead) _enemyMinionsCanAttack.Add(m);
        }

        // 1. 执行敌方当前意图（攻击/防御/召唤等）
        _currentEnemy.ExecuteIntent(this);

        // 2. 推进到下一意图
        _currentEnemy.AdvanceIntent();
        GD.Print($"[CombatManager] 敌方下回合意图：{_currentEnemy.GetCurrentIntent().Description}");

        // 3. 敌方随从攻击
        EnemyMinionsAttack();

        // 4. 全局死亡检查（意图/攻击可能造成随从死亡）
        CheckDeaths();

        // 5. 胜负判定
        CheckVictoryOrDefeat();

        // 6. 通知 UI 更新意图显示
        OnEnemyIntentChanged?.Invoke(_currentEnemy.GetCurrentIntent().Description);
    }

    /// <summary>
    /// 敌方所有随从依次攻击：有嘲讽时攻击嘲讽随从，无嘲讽时攻击玩家英雄。
    /// </summary>
    private void EnemyMinionsAttack()
    {
        // 回合开始时已存在的随从可以攻击，有冲锋的新召唤随从也可以
        var enemies = Board.GetEnemyMinions()
            .Where(m => !m.IsDead && (_enemyMinionsCanAttack.Contains(m) || m.HasCharge))
            .ToList();
        if (enemies.Count == 0) return;

        var playerTaunts = Board.GetTaunts(isEnemy: false);
        bool hasPlayerTaunt = playerTaunts.Count > 0;

        foreach (var attacker in enemies)
        {
            if (attacker.IsDead) continue;

            if (hasPlayerTaunt)
            {
                // 攻击随机嘲讽随从
                var tauntTargets = playerTaunts.Where(t => !t.IsDead).ToList();
                if (tauntTargets.Count == 0) continue;
                var defender = tauntTargets[new Random().Next(tauntTargets.Count)];
                GD.Print($"[CombatManager] ⚔ 敌方 {attacker.CardName} 攻击我方嘲讽 {defender.CardName}");
                defender.TakeDamage(attacker.Attack, attacker);
                attacker.TakeDamage(defender.Attack, defender);

                if (defender.IsDead)
                {
                    Board.RemoveMinion(defender);
                    TriggerDeathrattle(defender);
                }
                if (attacker.IsDead)
                {
                    Board.RemoveMinion(attacker);
                    TriggerDeathrattle(attacker);
                }
            }
            else
            {
                // 攻击玩家英雄
                GD.Print($"[CombatManager] ⚔ 敌方 {attacker.CardName} 攻击玩家英雄，造成 {attacker.Attack} 伤");
                PlayerHero.TakeDamage(attacker.Attack, attacker);
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
}
