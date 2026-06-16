namespace OdysseyCards.Core;

/// <summary>
/// 聚焦法术伤害修饰器——仅放大效果伤害（DamageKind.Effect）。
/// 使用加算阶段，允许负层数降低伤害，但最终仍由 DamageResolver Clamp 到非负值。
/// </summary>
public sealed class FocusSpellDamageModifier : IDamageModifier
{
	private readonly System.Func<int> _getStacks;

	public FocusSpellDamageModifier(System.Func<int> getStacks)
	{
		_getStacks = getStacks;
	}

	public DamagePhase Phase => DamagePhase.ADDITIVE;

	public int ModifyDamageDealt(int currentDamage, DamageContext context)
	{
		if (context.Kind != DamageKind.Effect)
			return currentDamage;

		return currentDamage + _getStacks();
	}

	public int ModifyDamageTaken(int currentDamage, DamageContext context) => currentDamage;
}
