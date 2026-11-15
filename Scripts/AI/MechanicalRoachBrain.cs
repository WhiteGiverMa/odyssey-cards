using OdysseyCards.Card;

namespace OdysseyCards.AI;

/// <summary>机械小蠊使用近战行动脑；分裂由所属方回合末统一结算，不消费普通攻击。</summary>
public sealed class MechanicalRoachBrain(Minion body) : DefaultAttackMinionBrain(body);
