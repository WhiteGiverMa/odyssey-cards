using System;

namespace OdysseyCards.Heat;

/// <summary>
/// 热力值系统：每场战斗生效的全场级 buff。
/// 敌方造成的最终伤害乘以 (1 + 热力值百分比)。
/// 非整数的伤害四舍五入。
/// 
/// 设计意图：鼓励快速跳费建立优势，同时限制跳费后费用充裕的慢速厚卡组。
/// </summary>
public class HeatSystem
{
    /// <summary>默认初始热力值。</summary>
    public const float DefaultInitialHeat = 0.4f;

    /// <summary>自然增长上限——到达此值后不再自动增长。</summary>
    public const float NaturalGrowthCap = 1.2f;

    /// <summary>默认硬上限（可被藏品降低）。</summary>
    public const float DefaultHardCap = 3.0f;

    /// <summary>每回合自然增长量。</summary>
    public const float GrowthPerTurn = 0.1f;

    /// <summary>120% 后每出一张牌的增长量。</summary>
    public const float GrowthPerCardPlayed = 0.005f;

    /// <summary>120% 后每花费 1 点费用的增长量。</summary>
    public const float GrowthPerManaSpent = 0.005f;

    /// <summary>当前热力值（0.4 = 40%）。</summary>
    public float CurrentHeat { get; private set; } = DefaultInitialHeat;

    /// <summary>当前硬上限（可被藏品修改）。</summary>
    public float HardCap { get; set; } = DefaultHardCap;

    /// <summary>
    /// 伤害倍率。敌方造成的伤害 × 此值。
    /// 例：40% 热力值 → 0.4x 伤害（削弱）；200% → 2.0x 伤害（增强）。
    /// </summary>
    public float DamageMultiplier => CurrentHeat;

    /// <summary>
    /// 初始化（战斗开始时调用）。
    /// </summary>
    /// <param name="initialHeat">初始热力值，默认 0.4 (40%)</param>
    public void Initialize(float initialHeat = DefaultInitialHeat)
    {
        CurrentHeat = Math.Clamp(initialHeat, 0f, HardCap);
    }

    /// <summary>
    /// 敌方回合结束时调用——自然增长。
    /// 增长规则：回合结束时如果 < 120%，则 +10%。
    /// </summary>
    public void OnEnemyTurnEnd()
    {
        if (CurrentHeat >= NaturalGrowthCap)
            return;

        CurrentHeat += GrowthPerTurn;
        if (CurrentHeat > NaturalGrowthCap)
            CurrentHeat = NaturalGrowthCap;
    }

    /// <summary>
    /// 玩家打出一张卡牌——120% 后触发额外增长。
    /// </summary>
    public void OnCardPlayed()
    {
        GrowBeyondNatural(GrowthPerCardPlayed);
    }

    /// <summary>
    /// 玩家花费法力值——120% 后触发额外增长。
    /// </summary>
    /// <param name="amount">花费的法力值</param>
    public void OnManaSpent(int amount)
    {
        if (amount <= 0) return;
        GrowBeyondNatural(amount * GrowthPerManaSpent);
    }

    /// <summary>
    /// 藏品施加的固定偏移量（如冰袋 -20%）。
    /// 同时降低当前热力值和硬上限。
    /// </summary>
    /// <param name="flatReduction">减少的百分点数（正数=降低）</param>
    public void ApplyFlatReduction(float flatReduction)
    {
        CurrentHeat = Math.Max(0f, CurrentHeat - flatReduction);
        HardCap = Math.Max(0f, HardCap - flatReduction);
    }

    private void GrowBeyondNatural(float amount)
    {
        if (CurrentHeat < NaturalGrowthCap) return;
        CurrentHeat = Math.Min(HardCap, CurrentHeat + amount);
    }
}
