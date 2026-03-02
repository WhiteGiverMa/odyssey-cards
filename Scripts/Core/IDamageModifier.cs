namespace OdysseyCards.Core
{
    /// <summary>
    /// Interface for damage modifiers that can affect damage calculation.
    /// 伤害修改器接口，用于影响伤害计算。
    /// </summary>
    public interface IDamageModifier
    {
        /// <summary>
        /// The phase in which this modifier is applied.
        /// 此修改器应用的阶段。
        /// </summary>
        DamagePhase Phase { get; }

        /// <summary>
        /// Modifies outgoing damage from the source.
        /// 修改攻击者造成的伤害。
        /// </summary>
        /// <param name="currentDamage">Current damage value.</param>
        /// <param name="context">Damage calculation context.</param>
        /// <returns>Modified damage value.</returns>
        int ModifyDamageDealt(int currentDamage, DamageContext context);

        /// <summary>
        /// Modifies incoming damage to the target.
        /// 修改目标受到的伤害。
        /// </summary>
        /// <param name="currentDamage">Current damage value.</param>
        /// <param name="context">Damage calculation context.</param>
        /// <returns>Modified damage value.</returns>
        int ModifyDamageTaken(int currentDamage, DamageContext context);
    }
}
