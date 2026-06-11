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
	Defeat,

	/// <summary>发现选牌阶段——暂停回合流转，等待玩家从 N 张卡牌中选择。</summary>
	Discovering
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
	/// 法力水晶自然增长上限（每回合+1直到此值）。
	/// </summary>
	public const int MaxManaCrystals = 12;

	/// <summary>
	/// 法力水晶硬上限（任何方式均不可超过）。
	/// </summary>
	public const int HardMaxManaCap = 30;

	/// <summary>
	/// 游戏开始时的最大法力水晶数。
	/// </summary>
	public const int StartingMaxMana = 3;

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
	/// 当前是否为玩家回合（含发现选牌阶段，玩家仍可交互）。
	/// </summary>
	public bool IsPlayerTurn => Phase == CombatPhase.PlayerTurn || Phase == CombatPhase.Discovering;

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
	/// 启动游戏，进入起手调度阶段。
	/// 初始化回合数为 0，玩家法力水晶为 0/0（回合开始后增长至 1/1）。
	/// 敌人使用尖塔式意图系统，不依赖法力水晶。
	/// </summary>
	public void StartGame()
	{
		Phase = CombatPhase.Mulligan;
		TurnCount = 0;
		PlayerMaxMana = StartingMaxMana;
		PlayerMana = StartingMaxMana;
	}

	/// <summary>
	/// 开始玩家回合。
	/// 回合数 +1，法力水晶增长 1 点（不超过 naturalGrowthCap），
	/// 并将当前法力水晶恢复至最大值。
	/// </summary>
	/// <param name="naturalGrowthCap">当前有效的自然增长上限（默认 12，无限潜能领域下为 30）。</param>
	public void StartPlayerTurn(int naturalGrowthCap)
	{
		Phase = CombatPhase.PlayerTurn;

		// 回合数递增（每次轮到玩家时递增）
		TurnCount++;

		// 法力水晶增长：不超过自然增长上限和硬上限
		if (PlayerMaxMana < naturalGrowthCap && PlayerMaxMana < HardMaxManaCap)
			PlayerMaxMana++;
		PlayerMana = PlayerMaxMana;
	}

	/// <summary>
	/// 开始敌人回合。
	/// 敌人使用尖塔式意图系统，不依赖法力水晶增长。
	/// </summary>
	public void StartEnemyTurn()
	{
		Phase = CombatPhase.EnemyTurn;
	}

	/// <summary>
	/// 结束敌人回合，转入玩家回合。
	/// 法力水晶增长和回合数递增由 CombatManager.StartPlayerTurn 统一处理。
	/// </summary>
	public void EndEnemyTurn()
	{
		Phase = CombatPhase.PlayerTurn;
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

	/// <summary>
	/// 获得额外的法力水晶槽（永久增加法力上限）。
	/// 只增加最大法力值，不增加当前法力（当前回合无法使用）。
	/// 可突破自然增长上限，但不超过硬上限 HardMaxManaCap(30)。
	/// </summary>
	/// <param name="amount">增加的槽数</param>
	public void GainManaSlot(int amount)
	{
		PlayerMaxMana = Math.Min(PlayerMaxMana + amount, HardMaxManaCap);
		// 只增加上限，不增加当前法力（本回合无法使用新槽）
	}

	// ===== 回合结束 =====

	/// <summary>
	/// 结束玩家回合，自动转入敌人回合。
	/// </summary>
	public void EndPlayerTurn()
	{
		StartEnemyTurn();
	}

	// ===== 发现选牌阶段 =====

	/// <summary>
	/// 进入发现选牌阶段。暂停回合流转，玩家不可进行攻击/出牌等操作。
	/// 仅保留发现 UI 的交互。
	/// </summary>
	public void SetDiscovering()
	{
		Phase = CombatPhase.Discovering;
	}

	/// <summary>
	/// 退出发现选牌阶段，恢复到玩家回合。
	/// </summary>
	public void ResumePlayerTurn()
	{
		Phase = CombatPhase.PlayerTurn;
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
