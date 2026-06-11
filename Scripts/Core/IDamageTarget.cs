using System.Collections.Generic;

namespace OdysseyCards.Core;

/// <summary>
/// Interface for entities that can receive damage.
/// 可以受到伤害的实体接口。
/// </summary>
public interface IDamageTarget
{
	/// <summary>
	/// 当前防御力。正值减免攻击伤害，负值会放大攻击伤害。
	/// </summary>
	int Defense { get; }

	/// <summary>
	/// Gets the damage modifiers applied when receiving damage.
	/// 获取受到伤害时应用的伤害修改器。
	/// </summary>
	IReadOnlyList<IDamageModifier> DamageModifiers { get; }

	/// <summary>
	/// 承受基础伤害——处理护甲吸收、武器反击等前置逻辑后，
	/// 调用 <see cref="ApplyDamage"/> 施加最终伤害。
	/// 这是外部调用者应使用的公共伤害入口。
	/// </summary>
	/// <param name="baseDamage">基础伤害值</param>
	/// <param name="source">伤害来源（可为 null）</param>
	void TakeDamage(int baseDamage, IDamageSource? source);

	/// <summary>
	/// 承受指定类型的基础伤害。
	/// </summary>
	/// <param name="baseDamage">基础伤害值</param>
	/// <param name="source">伤害来源（可为 null）</param>
	/// <param name="kind">伤害结算类型</param>
	void TakeDamage(int baseDamage, IDamageSource? source, DamageKind kind);

	/// <summary>
	/// Applies the final calculated damage to this target.
	/// 将最终计算的伤害应用到此目标。
	/// </summary>
	/// <param name="finalDamage">The final damage amount after all modifiers.</param>
	/// <param name="source">The source of the damage.</param>
	void ApplyDamage(int finalDamage, IDamageSource source);
}
