namespace OdysseyCards.Core;

/// <summary>
/// Defines special abilities and behaviors that can be attached to cards.
/// Tags modify how cards behave in combat.
/// </summary>
public enum CardTag
{
    /// <summary>
    /// No special tag.
    /// </summary>
    None,

    /// <summary>
    /// Unit can act immediately when deployed (no deployment sickness).
    /// </summary>
    Blitz,

    /// <summary>
    /// Unit can move when deployed.
    /// </summary>
    Maneuver,

    /// <summary>
    /// Card returns to draw pile after being played.
    /// </summary>
    Rotation,

    /// <summary>
    /// Unit can attack multiple times per turn.
    /// </summary>
    Fury,

    /// <summary>
    /// Unit can protect adjacent allies from attacks.
    /// </summary>
    Guard,

    /// <summary>
    /// Unit triggers an effect when destroyed.
    /// </summary>
    LastWords,

    /// <summary>
    /// Unit triggers an effect when deployed.
    /// </summary>
    Deploy,

    /// <summary>
    /// Unit has increased defensive capabilities.
    /// </summary>
    Defense,

    /// <summary>
    /// Unit strikes first in combat, before the attacker.
    /// </summary>
    Ambush,

    /// <summary>
    /// Unit deals bonus damage on its first attack.
    /// </summary>
    Impact,

    /// <summary>
    /// Unit cannot be damaged.
    /// </summary>
    Immune,

    /// <summary>
    /// Unit can pin enemies, preventing them from moving.
    /// </summary>
    Pin,

    /// <summary>
    /// Unit can suppress enemies, reducing their capabilities.
    /// </summary>
    Suppress,

    /// <summary>
    /// Unit cannot be moved by enemy effects.
    /// </summary>
    Massive,

    /// <summary>
    /// Unit can bypass enemy units when moving.
    /// </summary>
    Infiltrate
}
