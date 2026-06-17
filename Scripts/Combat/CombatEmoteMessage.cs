namespace OdysseyCards.Combat;

/// <summary>
/// 战斗中的一条消息/表情。
/// </summary>
public readonly record struct CombatEmoteMessage(
	string Text,
	CombatEmoteSpeaker Speaker,
	int EnemyIndex,
	bool IsOfficialCollection);

public enum CombatEmoteSpeaker
{
	Player,
	Enemy,
}
