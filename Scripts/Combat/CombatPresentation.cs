using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>敌方回合 UI 步进类型；无 UI 同步路径不读取本枚举。</summary>
public enum EnemyStepKind
{
	TurnPrepare,
	PlayCard,
	Deploy,
	Move,
	Attack,
	Weapon,
	HeroPower,
	MinionAction,
	TurnEnd,
}

/// <summary>一次真实指令/领域/反制结算对应的亮牌表现快照。</summary>
public sealed class CardPlayPresentationEvent
{
	public CardPlayPresentationEvent(
		Hero casterHero,
		Card.Card card,
		IDamageTarget? target,
		bool isCountermeasureTrigger = false,
		bool isCanceled = false,
		Card.Card? targetCard = null)
	{
		CasterHero = casterHero;
		Card = card.CreateSnapshot();
		Target = target;
		IsCountermeasureTrigger = isCountermeasureTrigger;
		IsCanceled = isCanceled;
		TargetCard = targetCard?.CreateSnapshot();
	}

	public Hero CasterHero { get; }
	public Card.Card Card { get; }
	public IDamageTarget? Target { get; }
	public bool IsCountermeasureTrigger { get; }
	public bool IsCanceled { get; }
	/// <summary>被主体作用/拦截的目标卡快照；无目标卡时为 null。</summary>
	public Card.Card? TargetCard { get; }
	public bool IsPlayerSide => CasterHero.IsPlayerSide;
}
