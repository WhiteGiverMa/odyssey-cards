using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Card;

namespace OdysseyCards.Combat;

/// <summary>
/// 战场管理器。
/// 管理双方（玩家和敌人）各5个随从槽位，
/// 提供随从放置、移除、查询和嘲讽检测功能。
/// 纯 C# 类，不继承 Godot Node。
/// </summary>
public class Board
{
    // ===== 常量 =====

    /// <summary>
    /// 每方最大随从槽位数。
    /// </summary>
    public const int MaxSlotsPerSide = 5;

    // ===== 事件 =====

    /// <summary>
    /// 随从被放置到战场的某个槽位时触发。
    /// 参数为被放置的随从和其槽位索引。
    /// </summary>
    public event Action<Minion, int>? OnMinionPlaced;

    /// <summary>
    /// 随从从战场被移除时触发。
    /// 参数为被移除的随从。
    /// </summary>
    public event Action<Minion>? OnMinionRemoved;

    // ===== 战场槽位 =====

    /// <summary>
    /// 玩家方随从槽位（5个），空槽位为 null。
    /// </summary>
    public Minion?[] PlayerSlots { get; } = new Minion?[MaxSlotsPerSide];

    /// <summary>
    /// 敌方随从槽位（5个），空槽位为 null。
    /// </summary>
    public Minion?[] EnemySlots { get; } = new Minion?[MaxSlotsPerSide];

    // ===== 槽位查询 =====

    /// <summary>
    /// 获取指定一方的第一个空槽位索引。
    /// </summary>
    /// <param name="isPlayerSide">true 为玩家方，false 为敌方</param>
    /// <returns>第一个空槽位的索引（0-4）；若已满则返回 -1</returns>
    public int GetEmptySlotIndex(bool isPlayerSide)
    {
        var slots = isPlayerSide ? PlayerSlots : EnemySlots;
        for (int i = 0; i < MaxSlotsPerSide; i++)
        {
            if (slots[i] is null)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// 检查指定一方是否还有空槽位可以放置随从。
    /// </summary>
    /// <param name="isPlayerSide">true 为玩家方，false 为敌方</param>
    /// <returns>有空位返回 true，已满返回 false</returns>
    public bool CanPlaceMinion(bool isPlayerSide)
    {
        return GetEmptySlotIndex(isPlayerSide) >= 0;
    }

    /// <summary>
    /// 获取指定槽位上的随从。
    /// </summary>
    /// <param name="index">槽位索引（0-4）</param>
    /// <param name="isPlayerSide">true 为玩家方，false 为敌方</param>
    /// <returns>该槽位的随从；若为空则返回 null</returns>
    public Minion? GetMinionAt(int index, bool isPlayerSide)
    {
        var slots = isPlayerSide ? PlayerSlots : EnemySlots;
        if (index < 0 || index >= MaxSlotsPerSide) return null;
        return slots[index];
    }

    // ===== 随从放置与移除 =====

    /// <summary>
    /// 将随从放置到指定槽位。
    /// 若该槽位已被占用则替换之（触发旧随从的移除事件和新随从的放置事件）。
    /// </summary>
    /// <param name="minion">要放置的随从</param>
    /// <param name="slotIndex">目标槽位索引（0-4）</param>
    /// <exception cref="ArgumentNullException">当 minion 为 null 时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">当 slotIndex 超出有效范围时抛出</exception>
    public void PlaceMinion(Minion minion, int slotIndex)
    {
        ArgumentNullException.ThrowIfNull(minion);
        if (slotIndex < 0 || slotIndex >= MaxSlotsPerSide)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"槽位索引必须在 0 到 {MaxSlotsPerSide - 1} 之间");

        var slots = minion.IsPlayerSide ? PlayerSlots : EnemySlots;

        // 若该位置已有随从，先移除
        var existing = slots[slotIndex];
        if (existing is not null)
        {
            existing.BoardSlotIndex = -1;
            OnMinionRemoved?.Invoke(existing);
        }

        slots[slotIndex] = minion;
        minion.BoardSlotIndex = slotIndex;
        OnMinionPlaced?.Invoke(minion, slotIndex);
    }

    /// <summary>
    /// 从战场上移除指定随从。
    /// 遍历双方槽位查找并清除，同时触发移除事件。
    /// </summary>
    /// <param name="minion">要移除的随从</param>
    /// <exception cref="ArgumentNullException">当 minion 为 null 时抛出</exception>
    public void RemoveMinion(Minion minion)
    {
        ArgumentNullException.ThrowIfNull(minion);

        var slots = minion.IsPlayerSide ? PlayerSlots : EnemySlots;
        for (int i = 0; i < MaxSlotsPerSide; i++)
        {
            if (slots[i] == minion)
            {
                slots[i] = null;
                minion.BoardSlotIndex = -1;
                OnMinionRemoved?.Invoke(minion);
                return;
            }
        }
    }

    // ===== 批量查询 =====

    /// <summary>
    /// 获取玩家方所有存活随从的列表。
    /// </summary>
    /// <returns>玩家方非 null 槽位的随从列表</returns>
    public List<Minion> GetPlayerMinions()
    {
        var result = new List<Minion>(MaxSlotsPerSide);
        for (int i = 0; i < MaxSlotsPerSide; i++)
        {
            if (PlayerSlots[i] is Minion m && !m.IsDead)
                result.Add(m);
        }
        return result;
    }

    /// <summary>
    /// 获取敌方所有存活随从的列表。
    /// </summary>
    /// <returns>敌方非 null 槽位的随从列表</returns>
    public List<Minion> GetEnemyMinions()
    {
        var result = new List<Minion>(MaxSlotsPerSide);
        for (int i = 0; i < MaxSlotsPerSide; i++)
        {
            if (EnemySlots[i] is Minion m && !m.IsDead)
                result.Add(m);
        }
        return result;
    }

    /// <summary>
    /// 获取指定一方所有具有「嘲讽」的存活随从。
    /// 用于攻击时的嘲讽检测——若对方有嘲讽随从，攻击者必须优先攻击嘲讽目标。
    /// </summary>
    /// <param name="isEnemy">true 为敌方随从，false 为玩家方随从</param>
    /// <returns>具有嘲讽的存活随从列表</returns>
    public List<Minion> GetTaunts(bool isEnemy)
    {
        var slots = isEnemy ? EnemySlots : PlayerSlots;
        var result = new List<Minion>(MaxSlotsPerSide);
        for (int i = 0; i < MaxSlotsPerSide; i++)
        {
            if (slots[i] is Minion m && m.HasTaunt && !m.IsDead)
                result.Add(m);
        }
        return result;
    }
}
