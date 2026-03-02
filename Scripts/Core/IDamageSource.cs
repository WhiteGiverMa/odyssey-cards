using System.Collections.Generic;

namespace OdysseyCards.Core
{
    /// <summary>
    /// Interface for entities that can deal damage.
    /// 可以造成伤害的实体接口。
    /// </summary>
    public interface IDamageSource
    {
        /// <summary>
        /// Gets the damage modifiers applied by this source.
        /// 获取此来源应用的伤害修改器。
        /// </summary>
        IReadOnlyList<IDamageModifier> DamageModifiers { get; }

        /// <summary>
        /// Gets the base attack damage value.
        /// 获取基础攻击伤害值。
        /// </summary>
        int BaseAttack { get; }
    }
}
