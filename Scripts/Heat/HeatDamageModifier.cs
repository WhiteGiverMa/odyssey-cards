using System;
using OdysseyCards.Core;

namespace OdysseyCards.Heat;

/// <summary>
/// 热力值伤害乘区修改器。
/// 挂载在敌方伤害来源上，MULTIPLICATIVE 阶段生效。
/// 敌方造成的最终伤害 × (1 + 热力值%)，四舍五入。
/// 
/// 设计为可扩展——未来可添加更多 MULTIPLICATIVE 阶段的修改器，
/// 多个乘区按顺序依次应用，互不影响。
/// </summary>
public class HeatDamageModifier : IDamageModifier
{
	private readonly HeatSystem _heat;

	public DamagePhase Phase => DamagePhase.HEAT;

	public HeatDamageModifier(HeatSystem heat)
	{
		_heat = heat ?? throw new ArgumentNullException(nameof(heat));
	}

	/// <summary>
	/// 修改造成的伤害——乘以热力值倍率。
	/// 例：当前伤害 10，热力值 40% → 10 × 1.4 = 14
	/// </summary>
	public int ModifyDamageDealt(int currentDamage, DamageContext context)
	{
		float multiplier = _heat.DamageMultiplier;
		float result = currentDamage * multiplier;
		int final = (int)MathF.Round(result);
		Godot.GD.Print($"[HeatMod] base={currentDamage} × mult={multiplier:F3}(heat={_heat.CurrentHeat:F3}) = {result:F2} → round={final}");
		return final;
	}

	/// <summary>
	/// 修改受到的伤害——热力值不影响受到伤害，直接返回原值。
	/// </summary>
	public int ModifyDamageTaken(int currentDamage, DamageContext context)
	{
		return currentDamage;
	}
}
