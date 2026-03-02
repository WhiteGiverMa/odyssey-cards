using Godot;
using System.Collections.Generic;

namespace OdysseyCards.Core;

/// <summary>
/// Interface defining the core data contract for all card types.
/// Implemented by UnitData and OrderData resource classes.
/// </summary>
public interface ICardData
{
    /// <summary>
    /// Unique identifier for the card.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name of the card.
    /// </summary>
    string CardName { get; }

    /// <summary>
    /// Description text shown on the card.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Rarity tier of the card.
    /// </summary>
    CardRarity Rarity { get; }

    /// <summary>
    /// Artwork texture for the card.
    /// </summary>
    Texture2D Artwork { get; }

    /// <summary>
    /// Type classification (Unit or Order).
    /// </summary>
    CardType Type { get; }

    /// <summary>
    /// Gets the localized display name of the card.
    /// </summary>
    string GetLocalizedName();

    /// <summary>
    /// Gets the localized description with parameter substitution.
    /// </summary>
    string GetLocalizedDescription(Dictionary<string, object> parameters = null);
}
