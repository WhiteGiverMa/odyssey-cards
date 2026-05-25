namespace OdysseyCards.Core
{
    /// <summary>
    /// 伤害计算上下文，包含所有相关信息。
    /// </summary>
    public readonly struct DamageContext
    {
        /// <summary>
        /// 伤害来源（攻击者）。
        /// </summary>
        public IDamageSource Source { get; }

        /// <summary>
        /// 伤害目标（防御者）。
        /// </summary>
        public IDamageTarget Target { get; }

        /// <summary>
        /// 创建 DamageContext。
        /// </summary>
        public DamageContext(IDamageSource source, IDamageTarget target)
        {
            Source = source;
            Target = target;
        }

        /// <summary>
        /// 创建预览模式的 DamageContext（无目标）。
        /// </summary>
        public static DamageContext ForPreview(IDamageSource source)
        {
            return new DamageContext(source, null);
        }
    }
}
