namespace OdysseyCards.AI.Intents;

/// <summary>
/// 逃跑意图——脱离当前战斗。
/// </summary>
public sealed class EscapeIntent : AbstractIntent
{
	/// <inheritdoc />
	public override IntentType Type => IntentType.Escape;

	/// <inheritdoc />
	public override string IntentPrefix => "ESCAPE";

	/// <inheritdoc />
	public override string? SpritePath => "res://Assets/Intents/escape.png";
}
