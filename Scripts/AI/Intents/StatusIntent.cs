namespace OdysseyCards.AI.Intents;

/// <summary>
/// 状态牌意图——向玩家牌堆中添加状态牌（如伤口、晕眩等）。
/// </summary>
public sealed class StatusIntent : AbstractIntent
{
	/// <inheritdoc />
	public override IntentType Type => IntentType.StatusCard;

	/// <inheritdoc />
	public override string IntentPrefix => "STATUS_CARD";

	/// <inheritdoc />
	public override string? SpritePath => "res://Assets/Intents/status.png";

	/// <summary>将添加的状态牌数量。</summary>
	public int CardCount { get; }

	/// <summary>
	/// 创建状态牌意图。
	/// </summary>
	/// <param name="cardCount">状态牌数量</param>
	public StatusIntent(int cardCount)
	{
		CardCount = cardCount;
	}

	/// <inheritdoc />
	/// <summary>返回状态牌数量，供 UI 展示。</summary>
	public override string GetIntentLabel(Combat.CombatManager combat)
	{
		return CardCount.ToString();
	}
}
