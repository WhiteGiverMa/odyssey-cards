using System;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>一次效果结算的必填操作者；伤害来源可以是随从，但资源始终归属于施法英雄。</summary>
public sealed class EffectActorContext
{
	public Hero CasterHero { get; }
	public CommanderCore OwnerCore => CasterHero.Core;
	public bool IsPlayerSide => CasterHero.IsPlayerSide;
	public IDamageSource ActualDamageSource { get; }
	public Card.Card? PlayedCard { get; }
	public IDecisionProvider DecisionProvider { get; }
	internal float SpellDamageMultiplier { get; set; } = 1;
	public TickTiming OwnTurnStart => IsPlayerSide ? TickTiming.PlayerTurnStart : TickTiming.EnemyTurnStart;
	public TickTiming OwnTurnEnd => IsPlayerSide ? TickTiming.PlayerTurnEnd : TickTiming.EnemyTurnEnd;
	public TickTiming OpponentTurnEnd => IsPlayerSide ? TickTiming.EnemyTurnEnd : TickTiming.PlayerTurnEnd;

	public EffectActorContext(Hero casterHero, IDamageSource actualDamageSource,
		Card.Card? playedCard, IDecisionProvider decisionProvider)
	{
		CasterHero = casterHero ?? throw new ArgumentNullException(nameof(casterHero));
		ActualDamageSource = actualDamageSource ?? throw new ArgumentNullException(nameof(actualDamageSource));
		DecisionProvider = decisionProvider ?? throw new ArgumentNullException(nameof(decisionProvider));
		if (actualDamageSource.IsPlayerSide != casterHero.IsPlayerSide)
			throw new ArgumentException("伤害来源与施法英雄必须属于同一阵营。", nameof(actualDamageSource));
		PlayedCard = playedCard;
	}

	/// <summary>资源中的友敌标签是相对于施法者，而非固定相对于玩家。</summary>
	public TargetTags GetRelativeTags(IDamageTarget target)
	{
		bool playerSide = target switch
		{
			Hero hero => hero.IsPlayerSide,
			Minion minion => minion.IsPlayerSide,
			_ => throw new ArgumentException("目标必须是英雄或随从。", nameof(target)),
		};
		return (playerSide == IsPlayerSide ? TargetTags.Friendly : TargetTags.Enemy)
			| (target is Hero ? TargetTags.Hero : TargetTags.Minion);
	}
}
