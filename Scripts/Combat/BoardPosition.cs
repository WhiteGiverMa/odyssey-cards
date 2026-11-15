namespace OdysseyCards.Combat;

/// <summary>棋盘上的三条阵线；场外位置由可空 BoardRow 表示。</summary>
public enum BoardRow
{
	EnemySupport,
	Frontline,
	PlayerSupport,
}

public enum FrontlineOwner
{
	None,
	Player,
	Enemy,
}

/// <summary>阵线与该线内的槽位索引。</summary>
public readonly record struct BoardPosition(BoardRow Row, int SlotIndex);
