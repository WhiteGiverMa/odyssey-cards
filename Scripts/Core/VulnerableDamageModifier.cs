namespace OdysseyCards.Core;

/// <summary>
/// 易伤伤害修改器——目标受到攻击伤害 × 倍率。
/// 始终注册在 _damageModifiers 中，通过 <see cref="IsActive"/> 控制是否生效。
/// 基础倍率 1.5（STS2 VulnerablePower），可被「总观效应」翻倍至 3.0。
/// </summary>
public class VulnerableDamageModifier : IDamageModifier
{
	/// <summary>易伤是否激活。由 StatusEffect 系统控制。</summary>
	public bool IsActive { get; set; }

	/// <summary>基础倍率。默认 1.5（50%增伤）。总观效应将其翻倍。</summary>
	public float BaseMultiplier { get; set; } = 1.5f;

	/// <summary>额外倍率加成。总观效应设此值为 BaseMultiplier。</summary>
	public float ExtraMultiplier { get; set; }

	/// <summary>当前有效倍率 = BaseMultiplier + ExtraMultiplier。</summary>
	private float EffectiveMultiplier => BaseMultiplier + ExtraMultiplier;

	public DamagePhase Phase => DamagePhase.MULTIPLICATIVE;

	public int ModifyDamageDealt(int currentDamage, DamageContext context) => currentDamage;

	public int ModifyDamageTaken(int currentDamage, DamageContext context)
	{
		if (!IsActive || context.Kind != DamageKind.Attack)
			return currentDamage;
		return (int)(currentDamage * EffectiveMultiplier);
	}
}
