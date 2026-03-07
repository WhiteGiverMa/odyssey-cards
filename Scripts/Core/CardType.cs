namespace OdysseyCards.Core;

/// <summary>
/// Defines the type of a card.
/// </summary>
public enum CardType
{
    /// <summary>
    /// A unit card that can be deployed on the battle map.
    /// </summary>
    Unit,

    /// <summary>
    /// An order card that provides instant effects when played.
    /// </summary>
    Order
}

/// <summary>
/// Defines the rarity tier of a card.
/// </summary>
public enum CardRarity
{
    /// <summary>
    /// Common cards are frequently found and have basic effects.
    /// </summary>
    Common,

    /// <summary>
    /// Uncommon cards are less frequent and have moderate effects.
    /// </summary>
    Uncommon,

    /// <summary>
    /// Rare cards are uncommon and have powerful effects.
    /// </summary>
    Rare,

    /// <summary>
    /// Legendary cards are very rare and have unique, powerful effects.
    /// </summary>
    Legendary
}

/// <summary>
/// Common target tags for card targeting system.
/// </summary>
public static class TargetTags
{
    public const string Unit = "Unit";
    public const string HQ = "HQ";
    public const string Ally = "Ally";
    public const string Enemy = "Enemy";
}
