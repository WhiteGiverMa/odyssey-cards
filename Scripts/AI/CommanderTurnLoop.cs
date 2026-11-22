using System;
using OdysseyCards.Card;

namespace OdysseyCards.AI;

/// <summary>
/// 指挥官回合的贪心动作循环——同步 <see cref="DeckDrivenCommander.ExecuteTurn"/> 与
/// UI 异步单步（<c>CombatManager.EndPlayerTurnAsync</c>）共用同一份
/// 「最多 <see cref="MaxActionsPerTurn"/> 步、失败即停、结束必收反制」定义，
/// 保证同步整回合与逐帧单步两条路径的回合语义完全一致。
/// </summary>
internal static class CommanderTurnLoop
{
	/// <summary>单回合动作上限；防止零消耗或互相触发的贪心动作死循环。</summary>
	public const int MaxActionsPerTurn = 80;

	/// <summary>
	/// 循环执行单步动作直到无有效动作或触达上限，最后静默激活富余反制牌。
	/// </summary>
	/// <param name="body">指挥官身体（仅用于参数校验，规则状态由委托持有）</param>
	/// <param name="tryExecuteSingleAction">执行当前评分最高的一个动作；无动作可做时返回 false</param>
	/// <param name="armRemainingCountermeasures">回合末收尾；即使中途失败也必须执行</param>
	/// <returns>实际执行的动作数量</returns>
	public static int Run(Hero body, Func<bool> tryExecuteSingleAction, Action armRemainingCountermeasures)
	{
		ArgumentNullException.ThrowIfNull(body);
		ArgumentNullException.ThrowIfNull(tryExecuteSingleAction);
		ArgumentNullException.ThrowIfNull(armRemainingCountermeasures);

		int executed = 0;
		while (executed < MaxActionsPerTurn && tryExecuteSingleAction())
			executed++;
		armRemainingCountermeasures();
		return executed;
	}
}
