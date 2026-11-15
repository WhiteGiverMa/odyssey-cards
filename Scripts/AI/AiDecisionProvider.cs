using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>稳定的单卡评分选择器；不打开 UI，不写战斗阶段，不读取对手暗牌。</summary>
public sealed class AiDecisionProvider(Func<Card.Card, double>? cardScore = null) : IDecisionProvider
{
	public static double ScoreCard(Card.Card card)
	{
		if (card.HasUnplayable)
			return -20;
		double score = 4 + Math.Min(8, card.GetEffectiveCost()) * 0.6;
		if (card.Data != null)
		{
			if (card.Type == CardType.Minion)
				score += card.Data.Attack + card.Data.Health * 0.5;
			if (card.Data.HasMechanicTag(CardMechanicTag.Draw))
				score += 3;
		}
		if (card.HasQiqiao)
			score -= 3;
		return score;
	}

	public void ChooseCards(CardDecisionRequest request, Action<IReadOnlyList<Card.Card>> onChosen)
	{
		var options = request.Options.Distinct().ToList();
		bool discard = request.Kind is CardDecisionKind.Discard or CardDecisionKind.RestructureDiscard or CardDecisionKind.BladeCrisis;
		var ranked = discard
			? options.OrderBy(card => (cardScore ?? ScoreCard)(card))
			: options.OrderByDescending(card => (cardScore ?? ScoreCard)(card));
		int count = Math.Min(options.Count, Math.Max(0, request.Max));
		if (request.Kind == CardDecisionKind.BladeCrisis)
			count = Math.Min(count, Math.Max(request.Min, 2));
		onChosen(ranked.Take(count).ToArray());
	}

	public void ChooseOption(OptionDecisionRequest request, Action<int> onChosen)
	{
		int best = request.Labels.Count == 0 ? -1 : 0;
		for (int i = 1; i < request.Labels.Count; i++)
			if (i < request.Scores.Count && request.Scores[i] > request.Scores[best])
				best = i;
		onChosen(best);
	}
}
