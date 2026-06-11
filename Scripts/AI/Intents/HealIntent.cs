namespace OdysseyCards.AI.Intents;

/// <summary>
/// 治疗意图——恢复自身或友方单位的生命值。
/// </summary>
public sealed class HealIntent : AbstractIntent
{
	/// <inheritdoc />
	public override IntentType Type => IntentType.Heal;

	/// <inheritdoc />
	public override string IntentPrefix => "HEAL";

	/// <inheritdoc />
	public override string? SpritePath => "res://Assets/Intents/heal.png";
}
