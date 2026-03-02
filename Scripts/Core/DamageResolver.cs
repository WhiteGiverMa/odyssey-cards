using System.Collections.Generic;

namespace OdysseyCards.Core
{
    /// <summary>
    /// Centralized damage calculation resolver.
    /// This is the SINGLE SOURCE OF TRUTH for all damage calculations.
    /// 统一伤害计算解析器，所有伤害计算的"唯一真理之源"。
    /// </summary>
    public static class DamageResolver
    {
        /// <summary>
        /// Resolves the final damage value after applying all modifiers.
        /// 解析应用所有修改器后的最终伤害值。
        /// 
        /// Phase order (CRITICAL - additive before multiplicative before capping):
        /// 1. ADDITIVE: Addition/subtraction (Strength +3, Defense -2)
        /// 2. MULTIPLICATIVE: Multiplication/division (Vulnerable 1.5x, Weak 0.75x)
        /// 3. CAPPING: Limit/cap damage (Immune caps at 0, Intangible caps at 1)
        /// 4. Clamp: Ensure non-negative
        /// </summary>
        /// <param name="baseDamage">The base damage value.</param>
        /// <param name="source">The damage source (attacker).</param>
        /// <param name="target">The damage target (defender, can be null for preview).</param>
        /// <returns>The final resolved damage value.</returns>
        public static int ResolveDamage(int baseDamage, IDamageSource source, IDamageTarget target)
        {
            int damage = baseDamage;
            var context = new DamageContext(source, target);

            // Phase 1: ADDITIVE (加算)
            // Apply source's additive modifiers (e.g., Strength +3)
            if (source != null)
            {
                damage = ApplyModifiers(damage, context, source.DamageModifiers, DamagePhase.ADDITIVE, isDealt: true);
            }

            // Apply target's additive modifiers (e.g., Defense -2)
            if (target != null)
            {
                damage = ApplyModifiers(damage, context, target.DamageModifiers, DamagePhase.ADDITIVE, isDealt: false);
            }

            // Phase 2: MULTIPLICATIVE (乘算)
            // Apply source's multiplicative modifiers
            if (source != null)
            {
                damage = ApplyModifiers(damage, context, source.DamageModifiers, DamagePhase.MULTIPLICATIVE, isDealt: true);
            }

            // Apply target's multiplicative modifiers (e.g., Vulnerable 1.5x)
            if (target != null)
            {
                damage = ApplyModifiers(damage, context, target.DamageModifiers, DamagePhase.MULTIPLICATIVE, isDealt: false);
            }

            // Phase 3: CAPPING (限定)
            // Apply source's capping modifiers
            if (source != null)
            {
                damage = ApplyModifiers(damage, context, source.DamageModifiers, DamagePhase.CAPPING, isDealt: true);
            }

            // Apply target's capping modifiers (e.g., Immune caps at 0)
            if (target != null)
            {
                damage = ApplyModifiers(damage, context, target.DamageModifiers, DamagePhase.CAPPING, isDealt: false);
            }

            // Phase 4: Clamp to non-negative
            return System.Math.Max(0, damage);
        }

        /// <summary>
        /// Resolves damage for preview mode (no target).
        /// 解析预览模式的伤害（无目标）。
        /// </summary>
        /// <param name="baseDamage">The base damage value.</param>
        /// <param name="source">The damage source.</param>
        /// <returns>The preview damage value.</returns>
        public static int ResolvePreviewDamage(int baseDamage, IDamageSource source)
        {
            return ResolveDamage(baseDamage, source, null);
        }

        private static int ApplyModifiers(int damage, DamageContext context, IReadOnlyList<IDamageModifier> modifiers, DamagePhase phase, bool isDealt)
        {
            if (modifiers == null)
            {
                return damage;
            }

            foreach (var modifier in modifiers)
            {
                if (modifier.Phase == phase)
                {
                    damage = isDealt
                        ? modifier.ModifyDamageDealt(damage, context)
                        : modifier.ModifyDamageTaken(damage, context);
                }
            }

            return damage;
        }
    }
}
