using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>
/// 伤害弹道特效的表现语义。
/// 与实际 <see cref="DamageKind"/> 结算分离，避免反击等副作用自动继承主动攻击特效。
/// </summary>
public enum CombatDamageVfxKind
{
	/// <summary>普通主动攻击或武器攻击。</summary>
	Attack,

	/// <summary>法术、技能、藏品等效果伤害。</summary>
	Spell,

	/// <summary>简化“战斗”机制的互相伤害。</summary>
	Combat,
}
