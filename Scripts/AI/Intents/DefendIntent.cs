namespace OdysseyCards.AI.Intents;

/// <summary>
/// 防御意图——获得护甲或格挡值。
/// </summary>
public sealed class DefendIntent : AbstractIntent
{
    /// <inheritdoc />
    public override IntentType Type => IntentType.Defend;

    /// <inheritdoc />
    public override string IntentPrefix => "DEFEND";

    /// <inheritdoc />
    public override string? SpritePath => "res://Assets/Intents/defend.png";
}
