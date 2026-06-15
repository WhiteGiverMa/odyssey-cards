using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.Card;

/// <summary>
/// 状态效果的计时触发时机。
/// </summary>
public enum TickTiming
{
	/// <summary>友方回合开始时触发。</summary>
	PlayerTurnStart,

	/// <summary>友方回合结束时触发。</summary>
	PlayerTurnEnd,

	/// <summary>敌方回合开始时触发。</summary>
	EnemyTurnStart,

	/// <summary>敌方回合结束时触发。</summary>
	EnemyTurnEnd,
}

/// <summary>
/// 状态效果阵营：负面效果可被净化；非负面效果不会被随机净化选中。
/// </summary>
public enum StatusEffectPolarity
{
	/// <summary>非负面效果，包括增益、中性标记或纯显示状态。</summary>
	NonNegative,

	/// <summary>负面效果，可被净化类技能移除。</summary>
	Negative,
}

/// <summary>
/// 英雄状态效果（增益/减益）。
/// 支持同 ID 叠加层数和定时衰减。
/// 纯 C# 类，不继承 Godot Node。
/// 实现 <see cref="ITemporaryEffect"/>——随时间衰减，层数归零时自动移除。
/// 参考 STS2 的 ITemporaryPower（如 VulnerablePower/WeakPower）。
///
/// 与 <see cref="ActiveDomain"/> 的区别：
///   - StatusEffect：临时，TickOn 驱动衰减 → ITemporaryEffect
///   - ActiveDomain：永久，不衰减，仅通过 Counter 消耗 → IPermanentEffect
/// </summary>
public class StatusEffect : ITemporaryEffect
{
	/// <summary>
	/// 效果标识符。相同 ID 的效果叠加层数。
	/// </summary>
	public string Id { get; }

	/// <summary>
	/// 当前层数。每 tick 减 1，归零时移除。
	/// </summary>
	public int Stacks { get; set; }

	/// <summary>
	/// 每 tick 减少的层数。默认 1。
	/// </summary>
	public int DecayPerTick { get; init; } = 1;

	/// <summary>
	/// 层数衰减的计时触发时机。
	/// </summary>
	public TickTiming TickOn { get; }

	/// <summary>
	/// 状态效果是否为负面效果。
	/// </summary>
	public StatusEffectPolarity Polarity { get; }

	/// <summary>
	/// 是否为负面效果。武器/技能净化逻辑以此筛选。
	/// </summary>
	public bool IsNegative => Polarity == StatusEffectPolarity.Negative;

	/// <summary>
	/// 效果是否已过期（层数归零）。
	/// </summary>
	public bool IsExpired => Stacks <= 0;

	/// <summary>
	/// 创建状态效果实例。
	/// </summary>
	/// <param name="id">效果标识符</param>
	/// <param name="stacks">初始层数</param>
	/// <param name="tickOn">衰减触发时机</param>
	public StatusEffect(string id, int stacks, TickTiming tickOn, StatusEffectPolarity? polarity = null)
	{
		Id = id;
		Stacks = stacks;
		TickOn = tickOn;
		Polarity = polarity ?? InferPolarity(id);
	}

	private static StatusEffectPolarity InferPolarity(string id)
	{
		return id switch
		{
			"attack_zero" => StatusEffectPolarity.Negative,
			"meltdown" => StatusEffectPolarity.Negative,
			"weapon_disabled" => StatusEffectPolarity.Negative,
			"animosity" => StatusEffectPolarity.Negative,
			"vulnerable" => StatusEffectPolarity.Negative,
			"weak" => StatusEffectPolarity.Negative,
			"fragile" => StatusEffectPolarity.Negative,
			"total_observation" => StatusEffectPolarity.Negative,
			"attack_ban" => StatusEffectPolarity.Negative,
			"damage_over_time" => StatusEffectPolarity.Negative,
			_ => StatusEffectPolarity.NonNegative,
		};
	}

	/// <summary>
	/// 执行一次计时衰减。返回衰减后的剩余层数。
	/// </summary>
	public int Tick()
	{
		if (IsExpired)
			return 0;
		Stacks -= DecayPerTick;
		if (Stacks < 0)
			Stacks = 0;
		GD.Print($"[StatusEffect] {Id} 衰减 {DecayPerTick} 层，剩余 {Stacks} 层");
		return Stacks;
	}
}
