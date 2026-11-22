using System;
using System.Collections.Generic;
using OdysseyCards.Card;

namespace OdysseyCards.Combat;

public enum IntelOverflowReason
{
	NoEnemyHand,
	NotEnoughHiddenCards,
}

public readonly record struct IntelResult(int Revealed, int Overflow);

/// <summary>
/// 情报按手牌顺序公开实例。未用完的情报点数直接溢出：
/// 不产生额外回血、格挡或直伤收益，只通过 OnIntelOverflow 发出纯通知事件。
/// </summary>
public sealed class IntelService
{
	public event Action<Hero, int, IntelOverflowReason>? OnIntelOverflow;
	public event Action<int>? OnIntelRevealed;

	public IntelResult Resolve(Hero owner, int count, IReadOnlyList<Card.Card>? enemyHand = null)
	{
		ArgumentNullException.ThrowIfNull(owner);
		if (count <= 0 || owner.IsDead)
			return default;
		int revealed = 0;
		if (enemyHand != null)
		{
			foreach (var card in enemyHand)
			{
				if (revealed == count)
					break;
				if (card.IsRevealedByIntel)
					continue;
				card.IsRevealedByIntel = true;
				revealed++;
			}
		}
		int overflow = count - revealed;
		if (revealed > 0)
			OnIntelRevealed?.Invoke(revealed);
		if (overflow > 0)
			OnIntelOverflow?.Invoke(owner, overflow, enemyHand == null ? IntelOverflowReason.NoEnemyHand : IntelOverflowReason.NotEnoughHiddenCards);
		return new IntelResult(revealed, overflow);
	}
}
