namespace OdysseyCards.Core;

/// <summary>
/// 敌意系伤害修改器的统一标记接口——静态「敌意」与运行时枚举的相对敌意都实现它，
/// 效果栏与规则判定只依赖该接口，避免漏检实战挂载的修饰符。
/// </summary>
public interface IAnimosityModifier : IDamageModifier
{
}

/// <summary>
/// 敌意伤害修改器——使目标受到来自玩家阵营的伤害翻倍。
/// 在 MULTIPLICATIVE 阶段生效，仅检查伤害来源的阵营归属。
/// 「玩家」是绝对阵营概念：无论此修改器挂在友方还是敌方随从身上，
/// 只要伤害来源属于玩家方（PlayerHero 或 IsPlayerSide=true 的 Minion），伤害就翻倍。
/// </summary>
public sealed class AnimosityDamageModifier : IAnimosityModifier
{
	public DamagePhase Phase => DamagePhase.MULTIPLICATIVE;

	/// <summary>
	/// 不影响造成的伤害。
	/// </summary>
	public int ModifyDamageDealt(int currentDamage, DamageContext context) => currentDamage;

	/// <summary>
	/// 若伤害来源属于玩家阵营，伤害翻倍。
	/// source 为 null 时（无来源的效果伤害）不会翻倍。
	/// </summary>
	public int ModifyDamageTaken(int currentDamage, DamageContext context)
	{
		if (context.Source is { IsPlayerSide: true })
			return currentDamage * 2;

		return currentDamage;
	}
}
