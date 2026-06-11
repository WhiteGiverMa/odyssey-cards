namespace OdysseyCards.AI.Intents;

/// <summary>
/// 增益意图——强化自身或友方单位（如获得力量、增加护甲等）。
/// </summary>
public sealed class BuffIntent : AbstractIntent
{
	/// <inheritdoc />
	public override IntentType Type => IntentType.Buff;

	/// <inheritdoc />
	public override string IntentPrefix => "BUFF";

	/// <inheritdoc />
	public override string? SpritePath => "res://Assets/Intents/buff.png";
}
