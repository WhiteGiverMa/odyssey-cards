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
/// Defines the target type for card effects.
/// </summary>
public enum CardTarget
{
    /// <summary>
    /// No target required.
    /// </summary>
    None,

    /// <summary>
    /// Targets the card caster.
    /// </summary>
    Self,

    /// <summary>
    /// Targets a single enemy character.
    /// </summary>
    SingleEnemy,

    /// <summary>
    /// Targets all enemy characters.
    /// </summary>
    AllEnemies,

    /// <summary>
    /// Targets all characters on the field.
    /// </summary>
    Everyone,

    /// <summary>
    /// Targets a headquarters.
    /// </summary>
    Headquarters,

    /// <summary>
    /// Targets a single unit on the battle map.
    /// </summary>
    SingleUnit
}
