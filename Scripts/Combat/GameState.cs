using System;

namespace OdysseyCards.Combat;

/// <summary>
/// 战斗阶段枚举。
/// 定义一场战斗从开始到结束的各个阶段。
/// </summary>
public enum CombatPhase
{
    /// <summary>战斗尚未开始。</summary>
    NotStarted,

    /// <summary>起手换牌阶段。</summary>
    Mulligan,

    /// <summary>玩家回合。</summary>
    PlayerTurn,

    /// <summary>敌人回合。</summary>
    EnemyTurn,

    /// <summary>玩家胜利。</summary>
    Victory,

    /// <summary>玩家失败。</summary>
    Defeat
}

/// <summary>
/// 游戏状态管理器。
/// 管理战斗的回合流转、法力水晶增长与消耗。
/// 纯 C# 类，不继承 Godot Node。
/// </summary>
public class GameState
{
    // ===== 常量 =====

    /// <summary>
    /// 法力水晶上限。
    /// </summary>
    public const int MaxManaCrystals = 10;

    /// <summary>
    /// 游戏开始时的最大法力水晶数。
    /// </summary>
    public const int StartingMaxMana = 1;

    // ===== 属性 =====

    /// <summary>
    /// 当前战斗阶段。
    /// </summary>
    public CombatPhase Phase { get; private set; } = CombatPhase.NotStarted;

    /// <summary>
    /// 当前回合数（双方共用计数器）。
    /// 每经过一个玩家回合 + 一个敌人回合，算作一轮完整回合。
    /// </summary>
    public int TurnCount { get; private set; }

    /// <summary>
    /// 玩家当前可用法力水晶。
    /// </summary>
    public int PlayerMana { get; private set; }

    /// <summary>
    /// 玩家最大法力水晶。
    /// </summary>
    public int PlayerMaxMana { get; private set; }

    /// <summary>
    /// 敌人当前可用法力水晶。
    /// </summary>
    public int EnemyMana { get; private set; }

    /// <summary>
    /// 敌人最大法力水晶。
    /// </summary>
    public int EnemyMaxMana { get; private set; }

    /// <summary>
    /// 当前是否为玩家回合。
    /// </summary>
    public bool IsPlayerTurn => Phase == CombatPhase.PlayerTurn;

    /// <summary>
    /// 当前是否为敌人回合。
    /// </summary>
    public bool IsEnemyTurn => Phase == CombatPhase.EnemyTurn;

    /// <summary>
    /// 战斗是否已结束（胜利或失败）。
    /// </summary>
    public bool IsGameOver => Phase == CombatPhase.Victory || Phase == CombatPhase.Defeat;

    // ===== 游戏流程控制 =====

    /// <summary>
    /// 启动游戏，进入起手换牌阶段。
    /// 初始化回合数为 0，双方法力水晶均为 0/1。
    /// </summary>
    public void StartGame()
    {
        Phase = CombatPhase.Mulligan;
        TurnCount = 0;
        PlayerMana = 0;
        PlayerMaxMana = StartingMaxMana;
        EnemyMana = 0;
        EnemyMaxMana = StartingMaxMana;
    }

    /// <summary>
    /// 开始玩家回合。
    /// 回合数 +1，若最大法力水晶未达上限则增长 1 点，
    /// 并将当前法力水晶恢复至最大值。
    /// </summary>
    public void StartPlayerTurn()
    {
        Phase = CombatPhase.PlayerTurn;

        // 回合数递增（每次轮到玩家时递增）
        TurnCount++;

        // 法力水晶增长
        if (PlayerMaxMana < MaxManaCrystals)
            PlayerMaxMana++;
        PlayerMana = PlayerMaxMana;
    }

    /// <summary>
    /// 开始敌人回合。
    /// 若敌人最大法力水晶未达上限则增长 1 点，
    /// 并将当前法力水晶恢复至最大值。
    /// 敌人回合不递增 TurnCount（与玩家回合共用同一轮）。
    /// </summary>
    public void StartEnemyTurn()
    {
        Phase = CombatPhase.EnemyTurn;

        // 法力水晶增长
        if (EnemyMaxMana < MaxManaCrystals)
            EnemyMaxMana++;
        EnemyMana = EnemyMaxMana;
    }

    // ===== 法力消耗 =====

    /// <summary>
    /// 消耗玩家的法力水晶。
    /// </summary>
    /// <param name="amount">消耗量</param>
    /// <returns>成功消耗返回 true；法力不足返回 false</returns>
    public bool SpendPlayerMana(int amount)
    {
        if (PlayerMana < amount)
            return false;
        PlayerMana -= amount;
        return true;
    }

    // ===== 回合结束 =====

    /// <summary>
    /// 结束玩家回合，自动转入敌人回合。
    /// </summary>
    public void EndPlayerTurn()
    {
        StartEnemyTurn();
    }

    /// <summary>
    /// 结束敌人回合，自动转入玩家回合。
    /// </summary>
    public void EndEnemyTurn()
    {
        StartPlayerTurn();
    }

    // ===== 游戏结束 =====

    /// <summary>
    /// 设置战斗结果为玩家胜利。
    /// </summary>
    public void SetVictory()
    {
        Phase = CombatPhase.Victory;
    }

    /// <summary>
    /// 设置战斗结果为玩家失败。
    /// </summary>
    public void SetDefeat()
    {
        Phase = CombatPhase.Defeat;
    }
}
