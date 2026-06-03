using OdysseyCards.Core;
using System.Collections.Generic;

namespace OdysseyCards.Card;

/// <summary>
/// 运行时法术类。
/// 代表从手牌中使用的法术，继承自 Card 基类，
/// 是一个简单的数据持有类，不继承 Godot Node。
/// 法术使用后立即生效，不会进入战场。
/// </summary>
public class Spell : Card
{
    /// <summary>
    /// 法术效果列表（来源自卡牌数据）。
    /// </summary>
    public IReadOnlyList<CardEffectData> Effects => Data.Effects;

    /// <summary>
    /// 是否需要选择目标才能使用。
    /// </summary>
    public bool RequiresTarget => Data.RequiresTarget;

    /// <summary>
    /// 创建法术运行时实例。
    /// </summary>
    /// <param name="data">卡牌数据资源（必须是法术类型）</param>
    public Spell(CardData data)
        : base(data)
    {
    }

}
