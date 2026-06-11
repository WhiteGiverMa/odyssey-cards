namespace OdysseyCards.AI.Intents;

/// <summary>
/// 隐藏意图——意图对玩家不可见。
/// 用于神秘敌人或特殊遭遇，意图图标和提示均不显示。
/// </summary>
public sealed class HiddenIntent : AbstractIntent
{
	/// <inheritdoc />
	public override IntentType Type => IntentType.Hidden;

	/// <inheritdoc />
	public override bool HasIntentTip => false;

	/// <inheritdoc />
	public override string? SpritePath => null;

	/// <inheritdoc />
	public override string IntentPrefix => "HIDDEN";
}
