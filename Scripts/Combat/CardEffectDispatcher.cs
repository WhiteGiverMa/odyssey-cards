#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>
/// 卡牌效果分发器。
/// 从 CombatManager 中拆出 EffectType→Handler 注册表与具体效果处理逻辑，
/// 让 CombatManager 只保留战斗流转与玩家操作入口。
/// </summary>
internal sealed class CardEffectDispatcher
{
    private readonly CommanderCore _playerCore;
    private readonly Hero _playerHero;
    private readonly Board _board;
    private readonly GameState _state;
    private readonly Action _notifyCombatStateChanged;
    private readonly Action<CardEffectData> _handleDiscoverEffect;
    private readonly Action<List<Card.Card>, int> _beginDiscardDiscoverSelection;
    private readonly Action<List<Card.Card>, int, int, bool> _beginHandDiscardSelection;
    private readonly Dictionary<CardEffectType, Action<CardEffectData, object, IDamageSource?>> _handlers;

    public CardEffectDispatcher(
        CommanderCore playerCore,
        Hero playerHero,
        Board board,
        GameState state,
        Action notifyCombatStateChanged,
        Action<CardEffectData> handleDiscoverEffect,
        Action<List<Card.Card>, int> beginDiscardDiscoverSelection,
        Action<List<Card.Card>, int, int, bool> beginHandDiscardSelection)
    {
        _playerCore = playerCore;
        _playerHero = playerHero;
        _board = board;
        _state = state;
        _notifyCombatStateChanged = notifyCombatStateChanged;
        _handleDiscoverEffect = handleDiscoverEffect;
        _beginDiscardDiscoverSelection = beginDiscardDiscoverSelection;
        _beginHandDiscardSelection = beginHandDiscardSelection;

        _handlers = new Dictionary<CardEffectType, Action<CardEffectData, object, IDamageSource?>>()
        {
            [CardEffectType.Damage] = HandleDamage,
            [CardEffectType.DealDamageToTarget] = HandleDamage,
            [CardEffectType.DealDamageToEnemyHero] = HandleDealDamageToEnemyHero,
            [CardEffectType.DealDamageToFriendlyHero] = HandleDealDamageToFriendlyHero,
            [CardEffectType.DealDamageToAllEnemies] = HandleDealDamageToAllEnemies,
            [CardEffectType.DrawCards] = HandleDrawCards,
            [CardEffectType.Heal] = HandleHeal,
            [CardEffectType.RestoreHealth] = HandleHeal,
            [CardEffectType.GainArmor] = HandleGainArmor,
            [CardEffectType.GainMaxHealth] = HandleGainMaxHealth,
            [CardEffectType.SummonMinion] = HandleSummonMinion,
            [CardEffectType.BuffMinion] = HandleBuffMinion,
            [CardEffectType.GainManaSlot] = HandleGainManaSlot,
            [CardEffectType.RemoveNaturalManaCap] = HandleRemoveNaturalManaCap,
            [CardEffectType.Discover] = HandleDiscoverEffectDispatch,
            [CardEffectType.ReplaceDeathrattleWithDraw] = HandleReplaceDeathrattleWithDraw,
            [CardEffectType.GrantIdolTwilight] = HandleGrantIdolTwilight,
            [CardEffectType.ChooseFromDiscard] = HandleChooseFromDiscard,
            [CardEffectType.DiscardRandom] = HandleDiscardRandom,
            [CardEffectType.DiscardChoose] = HandleDiscardChoose,
            [CardEffectType.ShuffleTribeCards] = HandleShuffleTribeCards,
            [CardEffectType.Custom] = HandleCustomEffect,
        };
    }

    public void ExecuteEffect(CardEffectData effect, object target, IDamageSource? source = null)
    {
        if (_handlers.TryGetValue(effect.EffectType, out var handler))
        {
            handler(effect, target, source);
            return;
        }

        GD.Print($"[CardEffectDispatcher] 未处理的效果类型：{effect.EffectType}（{effect.GetDescription()}）");
    }

    private void HandleDamage(CardEffectData effect, object target, IDamageSource? source)
    {
        if (target is Minion minionTarget)
        {
            minionTarget.TakeDamage(effect.Value, source, DamageKind.Effect);
            GD.Print($"[CardEffectDispatcher] 对 {minionTarget.CardName} 造成 {effect.Value} 点伤害");
        }
        else if (target is Hero heroTarget)
        {
            heroTarget.TakeDamage(effect.Value, source, DamageKind.Effect);
            GD.Print($"[CardEffectDispatcher] 对英雄造成 {effect.Value} 点伤害");
        }
        else
        {
            GD.PrintErr("[CardEffectDispatcher] 目标类型不支持伤害");
        }
    }

    private void HandleDealDamageToEnemyHero(CardEffectData effect, object target, IDamageSource? source)
    {
        if (target is not Hero hero) return;
        hero.TakeDamage(effect.Value, source, DamageKind.Effect);
        GD.Print($"[CardEffectDispatcher] 对敌方英雄造成 {effect.Value} 点伤害（剩余 {hero.CurrentHealth}）");
    }

    private void HandleDealDamageToFriendlyHero(CardEffectData effect, object target, IDamageSource? source)
    {
        _playerHero.TakeDamage(effect.Value, source, DamageKind.Effect);
        GD.Print($"[CardEffectDispatcher] 对友方英雄造成 {effect.Value} 点伤害（剩余 {_playerHero.CurrentHealth}）");
    }

    private void HandleDealDamageToAllEnemies(CardEffectData effect, object target, IDamageSource? source)
    {
        int hitCount = 0;
        foreach (var enemyMinion in _board.GetEnemyMinions())
        {
            enemyMinion.TakeDamage(effect.Value, source, DamageKind.Effect);
            hitCount++;
        }
        GD.Print($"[CardEffectDispatcher] 对所有敌方随从造成 {effect.Value} 点伤害（命中 {hitCount} 个目标）");
    }

    private void HandleDrawCards(CardEffectData effect, object target, IDamageSource? source)
    {
        _playerHero.DrawCards(effect.Value);
        GD.Print($"[CardEffectDispatcher] 抽 {effect.Value} 张牌");
    }

    private void HandleHeal(CardEffectData effect, object target, IDamageSource? source)
    {
        _playerCore.Heal(effect.Value);
        GD.Print($"[CardEffectDispatcher] 恢复 {effect.Value} 点生命值（当前 {_playerHero.CurrentHealth}）");
    }

    private void HandleGainArmor(CardEffectData effect, object target, IDamageSource? source)
    {
        _playerHero.GainArmor(effect.Value);
        GD.Print($"[CardEffectDispatcher] 获得 {effect.Value} 点护甲（当前 {_playerHero.CurrentArmor}）");
    }

    private void HandleGainMaxHealth(CardEffectData effect, object target, IDamageSource? source)
    {
        _playerCore.InitializeHealth(_playerCore.MaxHealth + effect.Value, _playerCore.CurrentHealth + effect.Value);
        GD.Print($"[CardEffectDispatcher] 最大生命值 +{effect.Value} 并恢复等量生命值（当前 {_playerHero.CurrentHealth}/{_playerHero.MaxHealth}）");
    }

    private void HandleSummonMinion(CardEffectData effect, object target, IDamageSource? source)
    {
        int emptySlot = _board.GetEmptySlotIndex(isPlayerSide: true);
        if (emptySlot >= 0)
        {
            GD.Print($"[CardEffectDispatcher] 召唤随从效果：{effect.GetDescription()}（原型：仅记录日志）");
        }
        else
        {
            GD.Print("[CardEffectDispatcher] 召唤随从失败 — 战场已满");
        }
    }

    private void HandleBuffMinion(CardEffectData effect, object target, IDamageSource? source)
    {
        if (target is Minion buffTarget)
        {
            GD.Print($"[CardEffectDispatcher] BuffMinion：{effect.GetDescription()} → {buffTarget.CardName}（原型：暂未实现属性修改）");
        }
        else
        {
            GD.Print("[CardEffectDispatcher] BuffMinion 需要有效的随从目标");
        }
    }

    private void HandleGainManaSlot(CardEffectData effect, object target, IDamageSource? source)
    {
        _state.GainManaSlot(effect.Value);
        _playerCore.SetMana(_playerCore.CurrentMana, _state.PlayerMaxMana);
        GD.Print($"[CardEffectDispatcher] 获得 {effect.Value} 个法力水晶槽（总上限 {_state.PlayerMaxMana}）");
    }

    private static void HandleRemoveNaturalManaCap(CardEffectData effect, object target, IDamageSource? source)
    {
        GD.Print("[CardEffectDispatcher] 无限潜能领域已展开，自然增长上限提升至 30");
    }

    private void HandleDiscoverEffectDispatch(CardEffectData effect, object target, IDamageSource? source)
    {
        _handleDiscoverEffect(effect);
    }

    private static void HandleReplaceDeathrattleWithDraw(CardEffectData effect, object target, IDamageSource? source)
    {
        if (target is not Minion minionTarget)
        {
            GD.Print("[CardEffectDispatcher] 替换亡语需要有效的随从目标");
            return;
        }

        int drawCount = Math.Max(1, effect.Value);
        var drawEffect = new CardEffectData { EffectType = CardEffectType.DrawCards, Value = drawCount };
        minionTarget.ReplaceDeathrattleEffects(new[] { drawEffect });
        GD.Print($"[CardEffectDispatcher] {minionTarget.CardName} 获得亡语：抽 {drawCount} 张牌");
    }

    private void HandleGrantIdolTwilight(CardEffectData effect, object target, IDamageSource? source)
    {
        int stacks = Math.Max(1, effect.Value);
        int grantCount = 0;

        foreach (var card in _playerHero.Hand)
            grantCount += GrantIdolTwilightToCard(card, stacks);
        foreach (var card in _playerHero.DeckState.DrawPile)
            grantCount += GrantIdolTwilightToCard(card, stacks);
        foreach (var card in _playerHero.DeckState.DiscardPile)
            grantCount += GrantIdolTwilightToCard(card, stacks);
        foreach (var minion in _board.GetPlayerMinions())
        {
            minion.GrantIdolTwilightOnAttacked(stacks);
            grantCount++;
        }

        GD.Print($"[CardEffectDispatcher] 偶像的黄昏：为 {grantCount} 个玩家随从/随从牌授予被攻击后 +{stacks}/+{stacks}");
        _notifyCombatStateChanged();
    }

    private void HandleChooseFromDiscard(CardEffectData effect, object target, IDamageSource? source)
    {
        int optionCount = effect.Value > 0 ? effect.Value : 5;
        int pickCount = effect.SecondaryValue > 0 ? effect.SecondaryValue : 2;
        var options = GetRandomCardsFromDiscard(optionCount);

        if (options.Count == 0)
        {
            GD.Print("[CardEffectDispatcher] 捞月：弃牌堆为空，无牌可选");
            return;
        }

        _beginDiscardDiscoverSelection(options, pickCount);
        GD.Print($"[CardEffectDispatcher] 捞月：从弃牌堆展示 {options.Count} 张，选择 {Math.Min(pickCount, options.Count)} 张");
    }

    private void HandleDiscardRandom(CardEffectData effect, object target, IDamageSource? source)
    {
        int discardCount = effect.Value;
        var hand = _playerHero.Hand.ToList();

        if (hand.Count == 0)
        {
            GD.Print("[CardEffectDispatcher] 随机弃牌：手牌为空，无法弃牌");
            return;
        }

        int actualDiscard = Math.Min(discardCount, hand.Count);
        using var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int i = 0; i < actualDiscard; i++)
        {
            int randomIndex = rng.RandiRange(0, hand.Count - 1);
            var card = hand[randomIndex];
            GD.Print($"[CardEffectDispatcher] 随机弃掉: {card.GetLocalizedName()}");
            _playerHero.DiscardCard(card);
            hand.RemoveAt(randomIndex);
        }

        GD.Print($"[CardEffectDispatcher] 随机弃牌完成：弃掉 {actualDiscard}/{discardCount} 张牌");
        _notifyCombatStateChanged();
    }

    private void HandleDiscardChoose(CardEffectData effect, object target, IDamageSource? source)
    {
        int mustDiscard = effect.Value;
        var handCopy = _playerHero.Hand.ToList();

        if (handCopy.Count == 0)
        {
            GD.Print("[CardEffectDispatcher] 主动弃牌：手牌为空，无法弃牌");
            return;
        }

        if (handCopy.Count < mustDiscard)
        {
            GD.Print($"[CardEffectDispatcher] 主动弃牌：手牌数量({handCopy.Count})不足，需要弃{mustDiscard}张");
            return;
        }

        _beginHandDiscardSelection(handCopy, mustDiscard, mustDiscard, false);
        GD.Print($"[CardEffectDispatcher] 主动弃牌：从手牌 {handCopy.Count} 张中选择弃掉 {mustDiscard} 张");
    }

    private void HandleShuffleTribeCards(CardEffectData effect, object target, IDamageSource? source)
    {
        int insertCount = effect.Value;
        if (!Enum.TryParse<CardTag>(effect.TargetType, out var targetTag) || targetTag == CardTag.None)
        {
            GD.PrintErr($"[CardEffectDispatcher] 种族洗牌：无法识别的种族标签 '{effect.TargetType}'");
            return;
        }

        var pool = GameManager.Instance.GetAllCards()
            .Where(cardData => cardData.Tags.HasFlag(targetTag) && cardData.Type == CardType.Minion)
            .ToList();

        if (pool.Count == 0)
        {
            GD.Print($"[CardEffectDispatcher] 种族洗牌：没有符合条件的 {effect.TargetType} 随从卡牌");
            return;
        }

        using var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int i = 0; i < insertCount; i++)
        {
            int randomIndex = rng.RandiRange(0, pool.Count - 1);
            var cardData = pool[randomIndex];
            var card = new Card.Card(cardData);
            _playerHero.InsertCardToDrawPile(card);
            GD.Print($"[CardEffectDispatcher] 洗入抽牌堆: {card.GetLocalizedName()}");
        }

        _playerHero.ShuffleDrawPile();
        GD.Print($"[CardEffectDispatcher] 种族洗牌完成：将 {insertCount} 张随机 {effect.TargetType} 随从洗入抽牌堆");
        _notifyCombatStateChanged();
    }

    private void HandleCustomEffect(CardEffectData effect, object target, IDamageSource? source)
    {
        switch (effect.CustomEffectName)
        {
            case "AddPlanToHand":
                var planData = GD.Load<CardData>("res://Resources/Cards/Spell_Plan.tres");
                if (planData != null)
                {
                    _playerCore.AddToHand(new Card.Card(planData));
                    GD.Print("[CardEffectDispatcher] 将「计划」加入手牌");
                }
                else
                {
                    GD.PrintErr("[CardEffectDispatcher] 无法加载计划卡牌资源");
                }
                break;

            case "FlyingAway":
                _playerHero.GainArmor(effect.Value);
                GD.Print($"[CardEffectDispatcher] 飞远：获得 {effect.Value} 点格挡（护甲）");
                break;

            case "StripArmor":
                if (target is Hero heroTarget)
                {
                    int armorLost = heroTarget.CurrentArmor;
                    heroTarget.RemoveArmor();
                    GD.Print($"[CardEffectDispatcher] 移除目标所有护甲（失去 {armorLost} 点）");
                }
                else
                {
                    GD.Print("[CardEffectDispatcher] StripArmor 目标无护甲（非英雄单位），无效果");
                }
                break;

            case "BaitTactics":
                if (target is Minion baitTarget)
                {
                    baitTarget.GrantBaitTactics();
                    GD.Print($"[CardEffectDispatcher] 诱饵战术：{baitTarget.CardName} 获得伏击、冲击与被攻击触发");
                }
                else
                {
                    GD.Print("[CardEffectDispatcher] 诱饵战术需要有效的随从目标");
                }
                break;

            case "Animosity":
                if (target is Minion animosityTarget)
                {
                    animosityTarget.HasTaunt = true;
                    animosityTarget._damageModifiers.Add(new AnimosityDamageModifier());
                    animosityTarget.AddDeathrattleEffect(new CardEffectData
                    {
                        EffectType = CardEffectType.DrawCards,
                        Value = 1,
                    });
                    _notifyCombatStateChanged();
                    GD.Print($"[CardEffectDispatcher] 敌意：{animosityTarget.CardName} 获得嘲讽、伤害翻倍（玩家阵营）和亡语抽牌");
                }
                else
                {
                    GD.Print("[CardEffectDispatcher] 敌意需要有效的随从目标");
                }
                break;

            case "BladeCrisis":
                int maxDiscard = effect.Value > 0 ? effect.Value : 5;
                var hand = _playerHero.Hand.ToList();
                if (hand.Count == 0)
                {
                    GD.Print("[CardEffectDispatcher] 刀盾危机：手牌为空");
                    return;
                }

                _beginHandDiscardSelection(hand, 0, Math.Min(maxDiscard, hand.Count), true);
                GD.Print($"[CardEffectDispatcher] 刀盾危机：可选最多{Math.Min(maxDiscard, hand.Count)}张手牌弃掉");
                break;

            default:
                GD.Print($"[CardEffectDispatcher] 未处理的Custom效果：{effect.CustomEffectName}");
                break;
        }
    }

    private static int GrantIdolTwilightToCard(Card.Card card, int stacks)
    {
        if (card.Type != CardType.Minion) return 0;
        card.GrantIdolTwilightOnAttacked(stacks);
        return 1;
    }

    private List<Card.Card> GetRandomCardsFromDiscard(int count)
    {
        var pool = _playerHero.DeckState.DiscardPile.ToList();
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
}
