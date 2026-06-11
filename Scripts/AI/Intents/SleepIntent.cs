namespace OdysseyCards.AI.Intents;

/// <summary>
/// 睡眠意图——本回合不行动，进入休眠状态。
/// 睡眠中的敌人可能在特定条件下被唤醒。
/// </summary>
public sealed class SleepIntent : AbstractIntent
{
	/// <inheritdoc />
	public override IntentType Type => IntentType.Sleep;

	/// <inheritdoc />
	public override string IntentPrefix => "SLEEP";

	/// <inheritdoc />
	public override string? SpritePath => "res://Assets/Intents/sleep.png";
}
