using System;
using Godot;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>
/// 机械小蠊 — 随从意图大脑。使用 MoveState 动态选择当前意图。
/// 意图：A.部署回合沉睡 → B.攻击随机敌方目标 → C.友方有空槽时复制自身 → 循环。
/// 参考 STS2 SneakyGremlin 的 SPAWNED_MOVE → TACKLE_MOVE 模式。
/// </summary>
public class MechanicalRoachBrain : IIntentActor
{
	private readonly Minion _body;
	private int _cycleStep; // 0=sleep, 1=attack, 2=copy (if available)
	private bool _hasSlept;
	private IDamageTarget? _cachedTarget;
	private int _cachedDamage;

	private readonly MoveState _moveSleep;
	private readonly MoveState _moveAttack;
	private readonly MoveState _moveCopy;

	public Hero? OwnerHero => null;
	public bool HasMoveStates => true;

	/// <summary>
	/// 创建机械小蠊意图大脑。
	/// </summary>
	/// <param name="body">该大脑控制的随从身体</param>
	public MechanicalRoachBrain(Minion body)
	{
		_body = body ?? throw new ArgumentNullException(nameof(body));

		_moveSleep = new MoveState("SLEEP", null, new BuffIntent());
		_moveAttack = new MoveState("ATTACK", null,
			new SingleAttackIntent(_ => _body.Attack));
		_moveCopy = new MoveState("COPY", null, new BuffIntent());
	}

	// ── MoveState 入口（动态选择，参考 ZhangLang.GetCurrentMove 模式）──

	/// <summary>
	/// 根据当前步数和棋盘状态动态返回当前 MoveState。
	/// </summary>
	public MoveState? GetCurrentMove(CombatManager combat)
	{
		if (!_hasSlept)
			return _moveSleep;
		if (_cycleStep == 2 && combat.Board.CanPlaceMinion(isPlayerSide: false))
			return _moveCopy;
		return _moveAttack;
	}

	public void AdvanceMove()
	{
		_cachedTarget = null;
		_cycleStep = (_cycleStep + 1) % 3;
	}

	// ── 旧系统兼容（执行逻辑 / 箭头绘制）──

	public EnemyIntent GetCurrentIntent(CombatManager combat)
	{
		if (!_hasSlept)
		{
			return new EnemyIntent(IntentType.Buff, 0, "沉睡中…");
		}

		if (_cycleStep == 2 && combat.Board.CanPlaceMinion(isPlayerSide: false))
		{
			return new EnemyIntent(IntentType.Buff, 1, "分裂复制");
		}

		// Attack: resolve target
		var target = ResolveRoachTarget(combat);
		_cachedTarget = target;
		_cachedDamage = DamageResolver.ResolvePreviewDamage(_body.Attack, _body, target);
		string targetName = target switch
		{
			Hero => "英雄",
			Minion m => m.GetLocalizedName(),
			_ => "目标"
		};
		return new EnemyIntent(IntentType.Attack, _cachedDamage, $"攻击 {targetName} 造成 {_cachedDamage} 点伤害");
	}

	public void ExecuteIntent(CombatManager combat)
	{
		if (!_hasSlept)
		{
			_hasSlept = true;
			_cycleStep = 1;
			GD.Print($"[机械小蠊] 部署回合：沉睡");
			return;
		}

		if (_cycleStep == 2 && combat.Board.CanPlaceMinion(isPlayerSide: false))
		{
			ExecuteCopy(combat);
			_cycleStep = 1;
			return;
		}

		GD.Print($"[机械小蠊] 智能攻击，造成 {_body.Attack} 点伤害");
		combat.ExecuteEnemyMinionSmartAttack(_body);
	}

	public void AdvanceIntent()
	{
		_cachedTarget = null;
		_cycleStep = (_cycleStep + 1) % 3;
	}

	private IDamageTarget ResolveRoachTarget(CombatManager combat)
	{
		return combat.SelectSmartEnemyAttackTarget(_body, _body.Attack);
	}

	private void ExecuteCopy(CombatManager combat)
	{
		int slot = combat.Board.GetEmptySlotIndex(isPlayerSide: false);
		if (slot < 0)
			return;

		var clone = new Minion(_body.Data, isPlayerSide: false);
		combat.Board.PlaceMinion(clone, slot);
		GD.Print($"[机械小蠊] 在敌方槽位 {slot} 分裂复制！");
	}
}
