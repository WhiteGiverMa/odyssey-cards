namespace OdysseyCards.Core
{
    /// <summary>
    /// Context for damage calculation, containing all relevant information.
    /// 伤害计算上下文，包含所有相关信息。
    /// </summary>
    public readonly struct DamageContext
    {
        /// <summary>
        /// The source of the damage (attacker).
        /// 伤害来源（攻击者）。
        /// </summary>
        public IDamageSource Source { get; }

        /// <summary>
        /// The target of the damage (defender).
        /// 伤害目标（防御者）。
        /// </summary>
        public IDamageTarget Target { get; }

        /// <summary>
        /// The card that caused this damage, if any.
        /// 造成此伤害的卡牌，如果有的话。
        /// </summary>
        public Card.Card Card { get; }

        /// <summary>
        /// Creates a new DamageContext.
        /// </summary>
        /// <param name="source">The damage source.</param>
        /// <param name="target">The damage target.</param>
        /// <param name="card">The card that caused the damage (optional).</param>
        public DamageContext(IDamageSource source, IDamageTarget target, Card.Card card = null)
        {
            Source = source;
            Target = target;
            Card = card;
        }

        /// <summary>
        /// Creates a DamageContext for preview mode (no target).
        /// 创建预览模式的 DamageContext（无目标）。
        /// </summary>
        /// <param name="source">The damage source.</param>
        /// <param name="card">The card that would cause the damage (optional).</param>
        public static DamageContext ForPreview(IDamageSource source, Card.Card card = null)
        {
            return new DamageContext(source, null, card);
        }
    }
}
