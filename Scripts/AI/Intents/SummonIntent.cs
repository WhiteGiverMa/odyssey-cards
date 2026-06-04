namespace OdysseyCards.AI.Intents;

/// <summary>
/// 召唤意图——在战场上召唤随从单位。
/// </summary>
public sealed class SummonIntent : AbstractIntent
{
    /// <inheritdoc />
    public override IntentType Type => IntentType.Summon;

    /// <inheritdoc />
    public override string IntentPrefix => "SUMMON";

    /// <inheritdoc />
    public override string? SpritePath => "res://Assets/Intents/summon.png";
}
