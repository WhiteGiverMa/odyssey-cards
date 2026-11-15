using System;
using System.Collections.Generic;
using OdysseyCards.Card;

namespace OdysseyCards.Combat;

public enum CardDecisionKind
{
	Discover,
	RecoverDiscard,
	Discard,
	BladeCrisis,
	CopyHandFill,
	RestructureDiscard,
}

/// <summary>选择仅返回实例，不代替规则层移动牌堆。回调可由 UI 异步调用，亦可由 AI 当场调用。</summary>
public sealed record CardDecisionRequest(Hero Owner, CardDecisionKind Kind,
	IReadOnlyList<Card.Card> Options, int Min, int Max, bool CanCancel = false);

public sealed record OptionDecisionRequest(string TitleKey, string TitleFallback,
	IReadOnlyList<string> Labels, IReadOnlyList<string> Descriptions,
	IReadOnlyList<double> Scores);

public interface IDecisionProvider
{
	void ChooseCards(CardDecisionRequest request, Action<IReadOnlyList<Card.Card>> onChosen);
	void ChooseOption(OptionDecisionRequest request, Action<int> onChosen);
}
