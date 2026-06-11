namespace OdysseyCards.AI.Intents;

/// <summary>
/// 未知意图——意图类型尚未确定。
/// 通常用于战斗初始化阶段或特殊过渡状态。
/// </summary>
public sealed class UnknownIntent : AbstractIntent
{
	/// <inheritdoc />
	public override IntentType Type => IntentType.Unknown;

	/// <inheritdoc />
	public override string IntentPrefix => "UNKNOWN";
}
