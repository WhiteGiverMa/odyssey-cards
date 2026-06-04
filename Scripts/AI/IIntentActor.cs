using OdysseyCards.Combat;
using OdysseyCards.Card;

namespace OdysseyCards.AI;

/// <summary>
/// 意图执行者接口——统一敌方英雄和敌方随从的意图查询/执行/推进。
/// 参考杀戮尖塔2的 Monster/Creature/MoveStateMachine 层级。
/// </summary>
public interface IIntentActor
{
    /// <summary>所属的 Hero 身体（英雄 actor 时为自身，随从 actor 时为 null）。</summary>
    Hero? OwnerHero { get; }

    /// <summary>查询当前回合意图。</summary>
    EnemyIntent GetCurrentIntent(CombatManager combat);

    /// <summary>执行当前意图。</summary>
    void ExecuteIntent(CombatManager combat);

    /// <summary>推进到下一意图。</summary>
    void AdvanceIntent();

    /// <summary>
    /// 获取当前 MoveState（新意图系统）。
    /// 默认返回 null——随从大脑等不支持 MoveState 的实现可不重写。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    /// <returns>当前 MoveState，若不支持则返回 null</returns>
    MoveState? GetCurrentMove(CombatManager combat) => null;
}
