namespace OdysseyCards.Core;

/// <summary>
/// 虚弱伤害修改器——攻击者造成伤害 × 倍率。
/// 始终注册在 _damageModifiers 中，通过 <see cref="IsActive"/> 控制是否生效。
/// 基础倍率 0.75（STS2 WeakPower），可被「总观效应」降为 0.5（减半后的值）。
/// </summary>
public class WeakDamageModifier : IDamageModifier
{
	/// <summary>虚弱是否激活。由 StatusEffect 系统控制。</summary>
	public bool IsActive { get; set; }

	/// <summary>基础倍率。默认 0.75（25%减伤）。总观效应进一步降低。</summary>
	public float BaseMultiplier { get; set; } = 0.75f;

	/// <summary>额外倍率减益。总观效应设此值为负值。</summary>
	public float ExtraMultiplier { get; set; }

	/// <summary>当前有效倍率 = BaseMultiplier + ExtraMultiplier，最小 0。</summary>
	private float EffectiveMultiplier => System.Math.Max(0f, BaseMultiplier + ExtraMultiplier);

	public DamagePhase Phase => DamagePhase.MULTIPLICATIVE;

	public int ModifyDamageDealt(int currentDamage, DamageContext context)
	{
		if (!IsActive || context.Kind != DamageKind.Attack)
			return currentDamage;
		return (int)(currentDamage * EffectiveMultiplier);
	}

	public int ModifyDamageTaken(int currentDamage, DamageContext context) => currentDamage;
}
