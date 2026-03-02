namespace OdysseyCards.Core
{
    /// <summary>
    /// Defines when a damage modifier is applied in the calculation pipeline.
    /// 伤害修改器在计算管道中的应用阶段。
    /// </summary>
    public enum DamagePhase
    {
        /// <summary>
        /// Addition/subtraction modifiers (e.g., Strength +3, Defense -2).
        /// 加算阶段：力量+3、防御-1等。
        /// </summary>
        ADDITIVE = 1,

        /// <summary>
        /// Multiplication/division modifiers (e.g., Vulnerable 1.5x, Weak 0.75x).
        /// 乘算阶段：易伤1.5x、虚弱0.75x等。
        /// </summary>
        MULTIPLICATIVE = 2,

        /// <summary>
        /// Limit/cap modifiers (e.g., Intangible caps at 1, Immune caps at 0).
        /// 限定阶段：伤害上限限制。
        /// </summary>
        CAPPING = 3
    }
}
