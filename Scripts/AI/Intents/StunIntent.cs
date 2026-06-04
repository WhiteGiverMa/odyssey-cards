namespace OdysseyCards.AI.Intents;

/// <summary>
/// 眩晕意图——本回合无法行动。
/// 与睡眠不同，眩晕通常由玩家施加，且不会被攻击唤醒。
/// </summary>
public sealed class StunIntent : AbstractIntent
{
    /// <inheritdoc />
    public override IntentType Type => IntentType.Stun;

    /// <inheritdoc />
    public override string IntentPrefix => "STUN";

    /// <inheritdoc />
    public override string? SpritePath => "res://Assets/Intents/stun.png";
}
