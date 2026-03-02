using Godot;
using System.Collections.Generic;
using OdysseyCards.Core;
using OdysseyCards.Localization;

namespace OdysseyCards.Card;

/// <summary>
/// Abstract base class for all card types in the game.
/// Provides common properties and methods for Unit and Order cards.
/// </summary>
public abstract partial class Card : Node
{
    protected ICardData _data;
    protected string _fallbackName;
    protected string _fallbackDescription;

    /// <summary>
    /// Unique identifier for this card instance.
    /// </summary>
    public string Id { get; protected set; }

    /// <summary>
    /// Display name of the card (localized if available).
    /// </summary>
    public string CardName
    {
        get => _data?.GetLocalizedName() ?? _fallbackName ?? "Unknown";
        protected set => _fallbackName = value;
    }

    /// <summary>
    /// Description text shown on the card (localized if available).
    /// </summary>
    public string Description
    {
        get => _data?.GetLocalizedDescription() ?? _fallbackDescription ?? "";
        protected set => _fallbackDescription = value;
    }

    /// <summary>
    /// Rarity tier of the card.
    /// </summary>
    public CardRarity Rarity { get; protected set; }

    /// <summary>
    /// Artwork texture for the card.
    /// </summary>
    public Texture2D Artwork { get; protected set; }

    /// <summary>
    /// Type classification (Unit or Order).
    /// </summary>
    public CardType Type { get; protected set; }

    /// <summary>
    /// List of tags attached to this card.
    /// </summary>
    public Godot.Collections.Array<CardTag> Tags { get; protected set; }

    protected List<CardEffectData> _effects = new();

    /// <summary>
    /// Checks if the card has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to check for.</param>
    /// <returns>True if the card has the tag.</returns>
    public bool HasTag(CardTag tag)
    {
        return Tags != null && Tags.Contains(tag);
    }

    /// <summary>
    /// Counts the number of occurrences of a specific tag.
    /// </summary>
    /// <param name="tag">The tag to count.</param>
    /// <returns>The number of times the tag appears.</returns>
    public int GetTagCount(CardTag tag)
    {
        if (Tags == null)
            return 0;
        int count = 0;
        foreach (var t in Tags)
        {
            if (t == tag)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Returns a formatted string with card information for debugging.
    /// </summary>
    /// <returns>A string representation of the card.</returns>
    public abstract string GetCardInfo();
}
