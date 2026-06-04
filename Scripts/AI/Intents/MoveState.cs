using System;
using System.Collections.Generic;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Combat;

namespace OdysseyCards.AI;

/// <summary>
/// 敌人的单个行动状态——包含一组意图和一个可选的执行回调。
/// 参考《杀戮尖塔2》的 MoveState 架构：每个 MoveState 可以包含多个意图
/// （例如同时攻击和防御），由战斗管理器统一执行。
/// </summary>
public class MoveState
{
    /// <summary>行动状态的唯一标识符。</summary>
    public string Id { get; }

    /// <summary>此行动状态包含的意图列表（只读）。</summary>
    public IReadOnlyList<AbstractIntent> Intents { get; }

    /// <summary>
    /// 此行动状态的执行回调。若不为 null，则在执行此 MoveState 时调用。
    /// 参数为战斗管理器和执行此行动的敌方英雄身体。
    /// </summary>
    public Action<CombatManager, Hero>? OnPerform { get; }

    /// <summary>
    /// 创建敌人的单个行动状态。
    /// </summary>
    /// <param name="id">行动状态标识符</param>
    /// <param name="perform">执行回调（可选）</param>
    /// <param name="intents">此行动状态包含的意图列表</param>
    public MoveState(string id, Action<CombatManager, Hero>? perform, params AbstractIntent[] intents)
    {
        Id = id;
        OnPerform = perform;
        Intents = intents;
    }
}
