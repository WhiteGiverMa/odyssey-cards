using Godot;

namespace OdysseyCards.Card;

/// <summary>
/// 状态效果的计时触发时机。
/// </summary>
public enum TickTiming
{
    /// <summary>友方回合开始时触发。</summary>
    PlayerTurnStart,

    /// <summary>友方回合结束时触发。</summary>
    PlayerTurnEnd,

    /// <summary>敌方回合开始时触发。</summary>
    EnemyTurnStart,

    /// <summary>敌方回合结束时触发。</summary>
    EnemyTurnEnd,
}

/// <summary>
/// 英雄状态效果（增益/减益）。
/// 支持同 ID 叠加层数和定时衰减。
/// 纯 C# 类，不继承 Godot Node。
/// </summary>
public class StatusEffect
{
    /// <summary>
    /// 效果标识符。相同 ID 的效果叠加层数。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 当前层数。每 tick 减 1，归零时移除。
    /// </summary>
    public int Stacks { get; set; }

    /// <summary>
    /// 每 tick 减少的层数。默认 1。
    /// </summary>
    public int DecayPerTick { get; init; } = 1;

    /// <summary>
    /// 层数衰减的计时触发时机。
    /// </summary>
    public TickTiming TickOn { get; }

    /// <summary>
    /// 效果是否已过期（层数归零）。
    /// </summary>
    public bool IsExpired => Stacks <= 0;

    /// <summary>
    /// 创建状态效果实例。
    /// </summary>
    /// <param name="id">效果标识符</param>
    /// <param name="stacks">初始层数</param>
    /// <param name="tickOn">衰减触发时机</param>
    public StatusEffect(string id, int stacks, TickTiming tickOn)
    {
        Id = id;
        Stacks = stacks;
        TickOn = tickOn;
    }

    /// <summary>
    /// 执行一次计时衰减。返回衰减后的剩余层数。
    /// </summary>
    public int Tick()
    {
        if (IsExpired) return 0;
        Stacks -= DecayPerTick;
        if (Stacks < 0) Stacks = 0;
        GD.Print($"[StatusEffect] {Id} 衰减 {DecayPerTick} 层，剩余 {Stacks} 层");
        return Stacks;
    }
}
