using Godot;
using OdysseyCards.Core;
using OdysseyCards.Localization;
using System.Collections.Generic;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.Core;

public partial class UnitData : Resource, ICardData, ILocalizable
{
    public CardType Type => CardType.Unit;
    [Export] public string Id { get; set; } = "";
    [Export] public string CardName { get; set; } = "Unnamed Unit";
    [Export] public string Description { get; set; } = "";
    [Export] public CardRarity Rarity { get; set; } = CardRarity.Common;
    [Export] public Texture2D Artwork { get; set; }

    [Export] public int DeployCost { get; set; } = 1;
    [Export] public int ActionCost { get; set; } = 0;
    [Export] public int Attack { get; set; } = 1;
    [Export] public int MaxHealth { get; set; } = 1;
    [Export] public int Range { get; set; } = 1;

    [Export] public Godot.Collections.Array<CardTag> Tags { get; set; } = new();
    [Export] public Godot.Collections.Array<CardEffectData> Effects { get; set; } = new();
    [Export] public Godot.Collections.Array<CardEffectData> DeployEffects { get; set; } = new();
    [Export] public Godot.Collections.Array<CardEffectData> LastWordsEffects { get; set; } = new();

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

    public bool HasTag(CardTag tag)
    {
        return Tags.Contains(tag);
    }

    public int GetTagCount(CardTag tag)
    {
        int count = 0;
        foreach (var t in Tags)
        {
            if (t == tag)
                count++;
        }
        return count;
    }
}
