using Godot;
using OdysseyCards.Core;
using OdysseyCards.Localization;
using System.Collections.Generic;
using System.Linq;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.Core;

public partial class OrderData : Resource, ICardData, ILocalizable
{
    public CardType Type => CardType.Order;
    [Export] public string Id { get; set; } = "";
    [Export] public string CardName { get; set; } = "Unnamed Order";
    [Export] public string Description { get; set; } = "";
    [Export] public CardRarity Rarity { get; set; } = CardRarity.Common;
    [Export] public Texture2D Artwork { get; set; }

    [Export] public int Cost { get; set; } = 1;
    [Export] public bool RequiresTarget { get; set; } = false;
    [Export] public string[] RequiredTags { get; set; } = System.Array.Empty<string>();

    [Export] public Godot.Collections.Array<CardTag> Tags { get; set; } = new();
    [Export] public Godot.Collections.Array<CardEffectData> Effects { get; set; } = new();

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

    /// <summary>
    /// Checks if a target character matches this order's required tags.
    /// </summary>
    /// <param name="target">The target character to check.</param>
    /// <param name="caster">The character casting the order.</param>
    /// <returns>True if the target matches all required tags.</returns>
    public bool CanTarget(Character.Character target, Character.Character caster)
    {
        if (!RequiresTarget) return false;
        if (target == null) return false;
        return target.MatchesTags(RequiredTags, caster);
    }
}
