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

/// <summary>情报按手牌顺序公开实例；每点溢出独立结算回血，满血转格挡。</summary>
public sealed class IntelService
{
	public event Action<Hero, int, IntelOverflowReason>? OnIntelOverflow;

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
		for (int i = 0; i < overflow; i++)
		{
			if (owner.CurrentHealth < owner.MaxHealth)
				owner.Heal(1);
			else
				owner.GainArmor(1);
		}
		if (overflow > 0)
			OnIntelOverflow?.Invoke(owner, overflow, enemyHand == null ? IntelOverflowReason.NoEnemyHand : IntelOverflowReason.NotEnoughHiddenCards);
		return new IntelResult(revealed, overflow);
	}
}
