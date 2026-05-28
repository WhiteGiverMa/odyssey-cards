using Godot;
using OdysseyCards.Core;
using OdysseyCards.Localization;
using System.Collections.Generic;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.Core;

/// <summary>
/// 统一的卡牌数据资源。
/// 同时支持随从（Minion）和法术（Spell）两种类型，
/// 取代旧的 UnitData 和 OrderData。
/// </summary>
public partial class CardData : Resource, ICardData, ILocalizable
{
    // ===== 公共字段（所有卡牌类型共享） =====

    [Export] public CardType Type { get; set; } = CardType.Minion;
    [Export] public string Id { get; set; } = "";
    [Export] public string CardName { get; set; } = "Unnamed Card";
    [Export] public string Description { get; set; } = "";
    [Export] public CardRarity Rarity { get; set; } = CardRarity.Common;
    [Export] public Texture2D Artwork { get; set; }

    /// <summary>
    /// 法力值消耗。
    /// </summary>
    [Export] public int Cost { get; set; } = 1;

    /// <summary>
    /// 领域标识（仅领域牌有效）。
    /// 同名领域叠加层数，不同领域独立存在。
    /// </summary>
    [Export] public string DomainId { get; set; } = "";

    // ===== 随从专属字段 =====

    /// <summary>
    /// 攻击力（仅随从）。
    /// </summary>
    [Export] public int Attack { get; set; } = 1;

    /// <summary>
    /// 生命值（仅随从）。
    /// </summary>
    [Export] public int Health { get; set; } = 1;

    /// <summary>
    /// 关键词列表（仅随从）。
    /// </summary>
    [Export] public Godot.Collections.Array<Keyword> Keywords { get; set; } = new();

    /// <summary>
    /// 战吼效果（仅随从，打出时触发）。
    /// </summary>
    [Export] public Godot.Collections.Array<CardEffectData> BattlecryEffects { get; set; } = new();

    /// <summary>
    /// 亡语效果（仅随从，死亡时触发）。
    /// </summary>
    [Export] public Godot.Collections.Array<CardEffectData> DeathrattleEffects { get; set; } = new();

    // ===== 法术专属字段 =====

    /// <summary>
    /// 法术效果列表。
    /// </summary>
    [Export] public Godot.Collections.Array<CardEffectData> Effects { get; set; } = new();

    /// <summary>
    /// 是否需要选择目标。
    /// </summary>
    [Export] public bool RequiresTarget { get; set; } = false;

    // ===== 本地化支持 =====

    public string LocalizationPrefix => "cards";

    public string LocalizationId => Id;

    public LocalStr Local(string field, Dictionary<string, object> parameters = null)
    {
        return new LocalStr($"cards.{Id}.{field}", parameters);
    }

    public bool HasLocal(string field)
    {
        return Loc.HasKey($"cards.{Id}.{field}");
    }

    public string GetLocalizedName()
    {
        return this.Local("name").Resolve();
    }

    public string GetLocalizedDescription(Dictionary<string, object> parameters = null)
    {
        return this.Local("description", parameters).Resolve();
    }

    // ===== 便捷查询方法 =====

    /// <summary>
    /// 检查随从是否拥有指定关键词。
    /// </summary>
    public bool HasKeyword(Keyword keyword)
    {
        return Keywords.Contains(keyword);
    }

    /// <summary>
    /// 是否为随从牌。
    /// </summary>
    public bool IsMinion => Type == CardType.Minion;

    /// <summary>
    /// 是否为法术牌。
    /// </summary>
    public bool IsSpell => Type == CardType.Spell;
}
