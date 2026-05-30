using Godot;
using OdysseyCards.Core;
using OdysseyCards.Character;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdysseyCards.Roguelike;

/// <summary>
/// 战斗后事件类型枚举。
/// </summary>
public enum EventType
{
    /// <summary>
    /// 卡牌奖励：从多个卡牌选项中选择一张加入牌堆。
    /// </summary>
    CardReward,

    /// <summary>
    /// 治疗事件：恢复英雄生命值（原型阶段暂未实现）。
    /// </summary>
    Heal
}

/// <summary>
/// 战斗后事件选择器。
/// 负责生成战后事件类型和卡牌奖励选项。
/// 纯 C# 逻辑类，不继承 Node，可直接在任何上下文中使用。
/// </summary>
public sealed class EventSelector : IDisposable
{
    /// <summary>
    /// Godot 随机数生成器，用于事件选择和奖励抽选。
    /// </summary>
    private readonly RandomNumberGenerator _random = new();

    /// <summary>
    /// 奖励卡牌池（原型硬编码）。
    /// 包含 6 张迁移卡牌：3 张随从 + 3 张法术。
    /// </summary>
    private readonly List<CardData> _rewardPool;

    /// <summary>
    /// 创建事件选择器并随机化种子。
    /// </summary>
    public EventSelector()
    {
        _random.Randomize();
        _rewardPool = CreateRewardPool();
    }

    /// <summary>
    /// 获取一个随机事件类型。
    /// 原型阶段始终返回 CardReward；
    /// 后续将支持基于权重的随机选择。
    /// </summary>
    /// <returns>当前仅返回 <see cref="EventType.CardReward"/>。</returns>
    public EventType GetRandomEvent()
    {
        // 原型阶段：固定返回卡牌奖励
        return EventType.CardReward;

        // 未来扩展：基于权重的随机选择
        // var roll = _random.RandfRange(0f, 1f);
        // if (roll < 0.7f) return EventType.CardReward;
        // return EventType.Heal;
    }

    /// <summary>
    /// 从奖励池中随机生成不重复的卡牌选择项。
    /// </summary>
    /// <param name="count">需要生成的选项数量，默认为 3。</param>
    /// <returns>不重复的卡牌选项列表。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="count"/> 超过奖励池大小时抛出。</exception>
    public List<CardData> GenerateRewardChoices(int count = 3)
    {
        if (count > _rewardPool.Count)
        {
            throw new ArgumentException(
                $"请求的奖励数量 ({count}) 超过奖励池大小 ({_rewardPool.Count})。",
                nameof(count));
        }

        // Fisher-Yates 洗牌后取前 count 个，保证不重复
        var shuffled = _rewardPool
            .OrderBy(_ => _random.Randi())
            .Take(count)
            .ToList();

        return shuffled;
    }

    /// <summary>
    /// 将玩家选择的奖励卡牌加入牌堆。
    /// </summary>
    /// <param name="chosen">玩家选择的卡牌数据。</param>
    /// <param name="player">目标玩家角色。</param>
    public void ApplyReward(CardData chosen, Player player)
    {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(player);

        player.AddCardToDeck(chosen);
    }

    /// <summary>
    /// 释放随机数生成器资源。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing)
            return;
        _random.Dispose();
    }

    /// <summary>
    /// 构建原型奖励卡牌池。
    /// 包含 6 张迁移卡牌：3 张随从 + 3 张法术。
    /// </summary>
    /// <returns>硬编码的奖励卡牌列表。</returns>
    private static List<CardData> CreateRewardPool()
    {
        return new List<CardData>
        {
            // ===== 随从牌 =====

            /// <summary>
            /// 第18团 — 嘲讽随从，高血量低攻，适合防御。
            /// </summary>
            new CardData
            {
                Id = "minion_18thRegiment",
                CardName = "第18团",
                Description = "嘲讽",
                Cost = 1,
                Type = CardType.Minion,
                Attack = 1,
                Health = 4,
                Keywords = new Godot.Collections.Array<Keyword> { Keyword.Taunt },
                Rarity = CardRarity.Common
            },

            /// <summary>
            /// 联树侦察犬 — 冲锋随从，亡语抽牌，节奏型卡牌。
            /// </summary>
            new CardData
            {
                Id = "minion_LianshuScout",
                CardName = "联树侦察犬",
                Description = "冲锋，亡语：抽1张牌",
                Cost = 0,
                Type = CardType.Minion,
                Attack = 1,
                Health = 1,
                Keywords = new Godot.Collections.Array<Keyword> { Keyword.Charge },
                DeathrattleEffects = new Godot.Collections.Array<CardEffectData>
                {
                    new CardEffectData
                    {
                        EffectType = CardEffectType.DrawCards,
                        Value = 1
                    }
                },
                Rarity = CardRarity.Common
            },

            /// <summary>
            /// 武侦小组 — 冲锋随从，高攻低血，适合快攻。
            /// </summary>
            new CardData
            {
                Id = "minion_DetectiveSquad",
                CardName = "武侦小组",
                Description = "冲锋",
                Cost = 1,
                Type = CardType.Minion,
                Attack = 3,
                Health = 1,
                Keywords = new Godot.Collections.Array<Keyword> { Keyword.Charge },
                Rarity = CardRarity.Common
            },

            // ===== 法术牌 =====

            /// <summary>
            /// 打击 — 低费直伤法术。
            /// </summary>
            new CardData
            {
                Id = "spell_Strike",
                CardName = "打击",
                Description = "造成3点伤害",
                Cost = 1,
                Type = CardType.Spell,
                RequiresTarget = true,
                Effects = new Godot.Collections.Array<CardEffectData>
                {
                    new CardEffectData
                    {
                        EffectType = CardEffectType.Damage,
                        Value = 3
                    }
                },
                Rarity = CardRarity.Common
            },

            /// <summary>
            /// 出击 — 中费直伤+抽牌法术。
            /// </summary>
            new CardData
            {
                Id = "spell_Assault",
                CardName = "出击",
                Description = "造成2点伤害，抽1张牌",
                Cost = 2,
                Type = CardType.Spell,
                RequiresTarget = true,
                Effects = new Godot.Collections.Array<CardEffectData>
                {
                    new CardEffectData
                    {
                        EffectType = CardEffectType.Damage,
                        Value = 2
                    },
                    new CardEffectData
                    {
                        EffectType = CardEffectType.DrawCards,
                        Value = 1
                    }
                },
                Rarity = CardRarity.Common
            },

            /// <summary>
            /// 警戒 — 中费抽牌+治疗法术。
            /// </summary>
            new CardData
            {
                Id = "spell_Alert",
                CardName = "警戒",
                Description = "抽1张牌，回复2点生命值",
                Cost = 2,
                Type = CardType.Spell,
                Effects = new Godot.Collections.Array<CardEffectData>
                {
                    new CardEffectData
                    {
                        EffectType = CardEffectType.DrawCards,
                        Value = 1
                    },
                    new CardEffectData
                    {
                        EffectType = CardEffectType.Heal,
                        Value = 2
                    }
                },
                Rarity = CardRarity.Common
            }
        };
    }
}
