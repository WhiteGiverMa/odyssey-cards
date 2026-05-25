using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Character;
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
    /// 从 GameManager 获取 Player，创建敌方英雄，执行完整初始化链。
    /// </summary>
    private void BootstrapCombat()
    {
        GD.Print("[CombatManager] BootstrapCombat 开始...");

        // 1. 从 GameManager 获取当前 Player
        var player = GameManager.Instance?.CurrentPlayer;
        if (player == null)
        {
            GD.PrintErr("[CombatManager] BootstrapCombat 失败 — CurrentPlayer 为 null");
            return;
        }

        // 2. 检查牌堆是否为空
        if (player.Deck == null || player.Deck.CardCount == 0)
        {
            GD.PrintErr($"[CombatManager] BootstrapCombat 失败 — 牌堆为空（{player.Deck?.CardCount ?? 0} 张牌）");
            return;
        }
        GD.Print($"[CombatManager] 牌堆有 {player.Deck.CardCount} 张牌");

        // 3. 创建敌方英雄（默认 30HP）
        var enemyHero = new Hero(new CommanderCore());
        GD.Print($"[CombatManager] 敌方英雄已创建 — {enemyHero.CurrentHealth}/{enemyHero.MaxHealth}");

        // 4. 初始化战斗管理器（创建 _playerCore、PlayerHero、Board、GameState）
        Initialize(player, enemyHero);

        // 5. 获取 CombatUI 并初始化
        var combatUI = GetNode<CombatUI>("CanvasLayer/CombatUI");
        combatUI.Initialize(player, this);
        GD.Print("[CombatManager] CombatUI 已初始化");

        // 6. 开始战斗
        StartCombat();
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
        _playerCore.SetMana(0, 1);
        PlayerHero = new Hero(_playerCore);

        Board = new Board();
        State = new GameState();

        GD.Print($"[CombatManager] 初始化完成 — 玩家 {PlayerHero.CurrentHealth}/{PlayerHero.MaxHealth}，" +
                  $"敌方 {EnemyHero.CurrentHealth}/{EnemyHero.MaxHealth}");
    }

    // ===== 战斗开始 =====

    /// <summary>
    /// 开始战斗。
    /// 设置抽牌堆、进入起手调度阶段、决定先后手并为双方抽起始手牌。
    /// </summary>
    public void StartCombat()
    {
        GD.Print("[CombatManager] ========== 战斗开始 ==========");

        // 从牌堆定义创建运行时卡牌并洗入抽牌堆
        _playerCore.SetupDrawPile();
        GD.Print($"[CombatManager] 抽牌堆已设置，共 {_playerCore.DrawPile.Count} 张牌");

        State.StartGame();

        // 硬币决定先后手
        bool playerGoesFirst = GD.Randi() % 2 == 0;
        GD.Print($"[CombatManager] 硬币结果：{(playerGoesFirst ? "玩家先手" : "敌人先手")}");

        if (playerGoesFirst)
        {
            // 先手：起手 3 张，回合开始再抽 1 张 → 共 4 张
            PlayerHero.DrawCards(3);
            GD.Print("[CombatManager] 先手 → 起手抽 3 张牌");

            State.StartPlayerTurn();
            _playerCore.SetMana(State.PlayerMana, State.PlayerMaxMana);
            PlayerHero.DrawCards(1);
            GD.Print($"[CombatManager] 第 {State.TurnCount} 回合开始（法力值 {State.PlayerMana}/{State.PlayerMaxMana}），抽 1 张牌 → 共 {_playerCore.Hand.Count} 张手牌");

            // 重置随从攻击状态
            ResetAttackTracking();
        }
        else
        {
            // 后手：起手 4 张牌（3 + 幸运币额外 1 张）
            PlayerHero.DrawCards(4);
            GD.Print("[CombatManager] 后手 → 起手抽 4 张牌（含幸运币）");

            State.StartEnemyTurn();
            GD.Print("[CombatManager] 敌方回合开始（原型：无操作）");

            // 敌方回合直接结束，进入玩家回合
            State.EndEnemyTurn();
            _playerCore.SetMana(State.PlayerMana, State.PlayerMaxMana);
            GD.Print($"[CombatManager] 玩家第 {State.TurnCount} 回合开始（法力值 {State.PlayerMana}/{State.PlayerMaxMana}）");

            // 重置随从攻击状态
            ResetAttackTracking();
        }
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
        // 未来扩展：根据 EffectType 解析具体战吼逻辑
        // 例如：DealDamageToTarget 可对指定目标造成伤害
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

    /// <summary>
    /// 解析单个法术效果，根据 EffectType 执行对应逻辑。
    /// </summary>
    /// <param name="effect">效果数据</param>
    /// <param name="target">法术目标对象</param>
    private void ResolveSpellEffect(CardEffectData effect, object target)
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

        // 嘲讽检测
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
    private void CheckDeaths()
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
    private void TriggerDeathrattle(Minion minion)
    {
        if (!minion.HasDeathrattle)
            return;

        GD.Print($"[CombatManager]   ◆ 触发亡语：{minion.CardName}");
        foreach (var effect in minion.DeathrattleEffects)
        {
            GD.Print($"[CombatManager]     亡语效果：{effect.GetDescription()}");
            // 未来扩展：根据 EffectType 解析具体亡语逻辑
            // 例如：SummonMinion 可召唤新随从
        }
    }

    // ===== 胜负判定 =====

    /// <summary>
    /// 检查是否达成胜利或失败条件。
    /// 敌方英雄死亡 → 胜利；玩家英雄死亡 → 失败。
    /// </summary>
    /// <returns>游戏结束返回 true</returns>
    private bool CheckVictoryOrDefeat()
    {
        if (State.IsGameOver)
            return true;

        if (EnemyHero.IsDead)
        {
            GD.Print("[CombatManager] ★★★ 敌方英雄被击败 — 玩家胜利！★★★");
            State.SetVictory();
            return true;
        }

        if (PlayerHero.IsDead)
        {
            GD.Print("[CombatManager] ☠☠☠ 玩家英雄被击败 — 玩家失败 ☠☠☠");
            State.SetDefeat();
            return true;
        }

        return false;
    }

    // ===== 回合管理 =====

    /// <summary>
    /// 结束玩家回合。
    /// 清理攻击状态 → 切换到敌方回合 → 敌方原型无操作直接结束 → 开始新玩家回合。
    /// 法力水晶增长、抽牌、重置随从攻击状态。
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

        // 切换到敌方回合
        State.EndPlayerTurn();
        GD.Print("[CombatManager] ---------- 敌方回合开始 ----------（原型：无操作）");

        // 原型：敌方不做任何事（Phase 4 将添加 AI）
        // 直接结束敌方回合，开始玩家新回合
        State.EndEnemyTurn();

        // 同步法力水晶到 CommanderCore
        _playerCore.SetMana(State.PlayerMana, State.PlayerMaxMana);

        // 抽 1 张牌
        PlayerHero.DrawCards(1);

        // 重置所有玩家随从为可攻击状态
        ResetAttackTracking();

        GD.Print($"[CombatManager] ========== 玩家第 {State.TurnCount} 回合开始 ==========");
        GD.Print($"[CombatManager] 法力值：{PlayerHero.CurrentMana}/{PlayerHero.MaxMana}，手牌：{_playerCore.Hand.Count} 张");

        // 检查胜负
        CheckVictoryOrDefeat();
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
