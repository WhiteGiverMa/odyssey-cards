using Godot;
using OdysseyCards.Core;
using OdysseyCards.Localization;
using System.Collections.Generic;

namespace OdysseyCards.Core;

public partial class OrderData : Resource, ICardData
{
    public CardType Type => CardType.Order;
    [Export] public string Id { get; set; } = "";
    [Export] public string CardName { get; set; } = "Unnamed Order";
    [Export] public string Description { get; set; } = "";
    [Export] public CardRarity Rarity { get; set; } = CardRarity.Common;
    [Export] public Texture2D Artwork { get; set; }

    [Export] public int Cost { get; set; } = 1;
    [Export] public CardTarget Target { get; set; } = CardTarget.None;

    [Export] public Godot.Collections.Array<CardTag> Tags { get; set; } = new();
    [Export] public Godot.Collections.Array<CardEffectData> Effects { get; set; } = new();

    public string GetLocalizedName()
    {
        return Localization.T($"cards.{Id}.name", CardName);
    }

    public string GetLocalizedDescription(Dictionary<string, object> parameters = null)
    {
        return Localization.T($"cards.{Id}.description", Description, parameters);
    }

    public bool HasTag(CardTag tag)
    {
        return Tags.Contains(tag);
    }
}
