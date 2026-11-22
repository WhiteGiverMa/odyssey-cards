using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;
using OdysseyCards.Heat;
using OdysseyCards.Relic;
using OdysseyCards.Roguelike;
using OdysseyCards.UI;

namespace OdysseyCards.Combat;

/// <summary>
/// 战斗管理器 — 炉石传说风格的回合制战斗编排器。
/// 负责战场管理、回合流转、随从召唤与攻击、法术施放以及胜负判定。
/// 继承 Godot.Node 以接入场景树，支持信号驱动的 UI 响应。
/// </summary>
public partial class CombatManager : Node
{
	// ===== 单例 =====

	/// <summary>
	/// 战斗管理器全局单例。
	/// </summary>
	public static CombatManager Instance { get; private set; }

	// ===== 抉择 UI（参考炉石 Choose One） =====

	/// <summary>
	/// 抉择 UI 请求载荷。由 <see cref="BeginChooseOne"/> 触发，<see cref="ChooseOneRequested"/> 事件携带本结构。
	/// UI 监听事件后弹出 <see cref="UI.ChooseOneUI"/>，玩家选完后回调 <c>OnChosen</c>（index 0-based；跳过=-1）。
	/// </summary>
	public sealed class ChooseOneRequest
	{
		public string TitleKey { get; init; } = "";
		public string TitleFallback { get; init; } = "";
		public IReadOnlyList<string> OptionLabels { get; init; } = Array.Empty<string>();
		public IReadOnlyList<string> OptionDescriptions { get; init; } = Array.Empty<string>();
		public Action<int> OnChosen { get; init; } = _ => { };
	}

	/// <summary>
	/// 抉择 UI 事件——请求 UI 弹出抉择界面。CombatUI 订阅。
	/// </summary>
	public event Action<ChooseOneRequest>? ChooseOneRequested;

	/// <summary>
	/// 触发抉择 UI——由 CardEffectDispatcher 经此回调调用。
	/// </summary>
	private void BeginChooseOne(string titleKey, string titleFallback,
		IReadOnlyList<string> optionLabels, IReadOnlyList<string> optionDescriptions,
		Action<int> onChosen)
	{
		var request = new ChooseOneRequest
		{
			TitleKey = titleKey,
			TitleFallback = titleFallback,
			OptionLabels = optionLabels,
			OptionDescriptions = optionDescriptions,
			OnChosen = onChosen,
		};
		GD.Print($"[CombatManager] 触发抉择：{titleFallback}（{optionLabels.Count} 个选项）");
		ChooseOneRequested?.Invoke(request);
	}

	// ===== 内部引用 =====

	/// <summary>
	/// 玩家英雄底层的指挥官核心。
	/// 保留引用以便访问 Hero 类未暴露的方法（如 Heal）。
	/// </summary>
	private CommanderCore _playerCore;

	/// <summary>
	/// 热力值系统——每场战斗生效的全场级 buff。
	/// </summary>
	private HeatSystem _heatSystem;

	/// <summary>
	/// 藏品管理器——持有玩家所有藏品的列表，负责事件分发。
	/// </summary>
	private RelicManager _relicManager;

	// ===== 公开属性 =====

	/// <summary>
	/// 三线棋盘，双方支援线各 4 格、共享独占前线 5 格。
	/// </summary>
	public Board Board { get; private set; }

	/// <summary>
	/// 游戏状态，追踪战斗阶段、回合数和法力水晶。
	/// </summary>
	public GameState State { get; private set; }

	/// <summary>
	/// 玩家英雄。包装 CommanderCore，提供法力值、生命值和护甲管理。
	/// </summary>
	public Hero PlayerHero { get; private set; }

	/// <summary>
	/// 所有敌方战斗单位列表。每个 EnemyUnit 包含一个 Hero 身体和一个 EnemyEncounter 大脑。
	/// </summary>
	public IReadOnlyList<EnemyUnit> EnemyUnits { get; private set; } = new List<EnemyUnit>();

	/// <summary>
	/// 获取当前默认敌方英雄目标：第一个仍存活的敌方单位。
	/// </summary>
	public EnemyUnit GetDefaultEnemyTargetUnit()
	{
		return EnemyUnits.FirstOrDefault(unit => !unit.Body.IsDead);
	}

	/// <summary>
	/// 武器主动技能的目标。IonPulse 等需要选择目标的主动技能在 Execute 时读取此属性。
	/// 由 CombatUI 在进入技能目标选择模式时设置，Execute 完成后清除。
	/// </summary>
	public IDamageTarget? ActiveSkillTarget { get; set; }

	/// <summary>
	/// 玩家角色（Godot Node 引用，用于场景树交互）。
	/// </summary>
	public Player Player { get; private set; }

	/// <summary>
	/// 热力值系统（只读访问）。
	/// </summary>
	public HeatSystem Heat => _heatSystem;

	/// <summary>
	/// 藏品管理器（只读访问）。
	/// </summary>
	public RelicManager Relics => _relicManager;

	/// <summary>
	/// <summary>
	/// 战斗状态变更事件——在随从部署/移除、英雄受伤等任何可能影响意图显示的
	/// 状态变更时触发。UI 监听此事件来刷新意图标签和目标高亮。
	/// 参考 STS2 的 CombatStateTracker.CombatStateChanged。
	/// </summary>
	public event Action? OnCombatStateChanged;

	/// <summary>
	/// 游戏结束事件（参数为 true=胜利, false=失败）。
	/// </summary>
	public event Action<bool>? OnGameOver;

	/// <summary>
	/// 战斗消息事件——玩家/敌方都可发送。
	/// 由 CombatUI 订阅以显示浮动消息文本。
	/// </summary>
	public event Action<CombatEmoteMessage>? OnCombatEmote;

	/// <summary>
	/// 伤害弹道表现事件。
	/// 由规则层在“主动伤害”发生前显式请求，UI 层 fire-and-forget 播放；
	/// 普通反击不主动请求，避免两路弹道打架。
	/// 参数为（视觉来源、伤害目标、结算类型、表现语义）。
	/// </summary>
	public event Action<object, IDamageTarget, DamageKind, CombatDamageVfxKind> OnDamageVfxRequested;

	/// <summary>
	/// 请求播放伤害弹道特效。只表达表现意图，不改变任何战斗状态。
	/// </summary>
	public void RequestDamageVfx(object visualSource, IDamageTarget target, DamageKind kind, CombatDamageVfxKind vfxKind)
	{
		if (target == null)
			return;
		OnDamageVfxRequested?.Invoke(visualSource, target, kind, vfxKind);
	}

	public IDamageTarget? SelectSmartEnemyAttackTarget(IDamageSource source, int baseDamage)
	{
		return SmartTargetingSystem.SelectEnemyAttackTarget(Board, PlayerHero, source, baseDamage);
	}

	public IDamageTarget SelectSmartEnemySpellTarget(IDamageSource source, int baseDamage)
	{
		return SmartTargetingSystem.SelectEnemySpellTarget(Board, PlayerHero, source, baseDamage);
	}

	public bool ExecuteEnemyHeroSmartAttack(Hero attacker, int baseDamage)
	{
		if (attacker.IsDead || attacker.IsIncapacitated || attacker.Weapon is { IsDisabled: true })
			return false;

		var target = SelectSmartEnemyAttackTarget(attacker, baseDamage);
		if (target == null)
		{
			GD.Print($"[CombatManager] 敌方英雄的攻击被烟幕完全阻挡，跳过");
			return false;
		}
		BeginLogAction(CombatLogEntryKind.WeaponAction, attacker, null, target, attacker,
			attacker.Weapon?.Name ?? GetLogHeroName(attacker));
		try
		{
			if (target is Minion minionTarget)
				TriggerBaitTacticsOnAttacked(minionTarget, attacker);
			bool hit = AttackCombatResolver.Resolve(attacker, target, baseDamage,
				() => RequestDamageVfx(attacker, target, DamageKind.Attack, CombatDamageVfxKind.Attack));
			if (hit)
				attacker.Weapon?.PassiveSkill?.OnWeaponHit(target, attacker);
			if (target is Minion defender)
				TryTriggerIdolTwilight(defender);
			CheckDeaths();
			return true;
		}
		finally
		{
			CommitLogAction();
		}
	}

	public bool ExecuteEnemyMinionSmartAttack(Minion attacker)
	{
		if (State.IsGameOver || attacker.IsDead || attacker.IsIncapacitated || attacker.HasStatusEffect("attack_ban") || attacker.Attack <= 0)
			return false;

		var target = SelectSmartEnemyAttackTarget(attacker, attacker.Attack);
		if (target == null)
		{
			GD.Print($"[CombatManager] 随从 {attacker.CardName} 的攻击被烟幕完全阻挡，跳过");
			return false;
		}
		var owner = attacker.OwnerHero ?? GetOwnerHero(attacker.IsPlayerSide);
		BeginLogAction(CombatLogEntryKind.Attack, owner, attacker.ToRuntimeCard(), target, attacker);
		try
		{
			if (target is Minion defender)
			{
				ResolveMinionCombat(attacker, defender);
				if (defender.IsDead)
					Board.RemoveMinion(defender);
				if (attacker.IsDead)
					Board.RemoveMinion(attacker);
				CheckDeaths();
				return true;
			}

			AttackCombatResolver.Resolve(attacker, target, attacker.Attack,
				() => RequestDamageVfx(attacker, target, DamageKind.Attack, CombatDamageVfxKind.Attack));

			if (attacker.IsDead)
				Board.RemoveMinion(attacker);
			CheckDeaths();
			return true;
		}
		finally
		{
			CommitLogAction();
		}
	}

	public bool CanExecuteSmartPlayerAttack()
	{
		if (State.IsGameOver || IsDiscovering || !State.IsPlayerTurn)
			return false;

		int remainingMana = PlayerHero.CurrentMana;
		foreach (var attacker in Board.GetPlayerMinions())
		{
			if (attacker.IsDead || attacker.IsIncapacitated || attacker.Attack <= 0 || !_attackTracker.CanAttack(attacker))
				continue;
			if (attacker.ActionCost <= remainingMana && SmartTargetingSystem.TrySelectPlayerAttackTarget(Board, EnemyUnits,
				attacker, out _, target => CanMinionAttackTarget(attacker, target)))
				return true;
		}

		return false;
	}

	public bool ExecuteSmartPlayerAttack()
	{
		if (!CanExecuteSmartPlayerAttack())
			return false;

		bool attacked = false;
		foreach (var attacker in Board.GetPlayerMinions().ToList())
		{
			if (attacker.IsDead || attacker.IsIncapacitated || attacker.Attack <= 0 || !_attackTracker.CanAttack(attacker))
				continue;
			if (attacker.ActionCost > PlayerHero.CurrentMana)
				continue;

			if (!SmartTargetingSystem.TrySelectPlayerAttackTarget(Board, EnemyUnits, attacker, out var target,
				candidate => CanMinionAttackTarget(attacker, candidate)))
				continue;
			if (target is Minion defender)
			{
				attacked |= MinionAttack(attacker, defender);
			}
			else if (target is Hero hero)
			{
				attacked |= MinionAttackHero(attacker, hero);
			}
		}

		return attacked;
	}

	public bool DealSmartEnemySpellDamage(IDamageSource source, int baseDamage)
	{
		var target = SelectSmartEnemySpellTarget(source, baseDamage);
		RequestDamageVfx(source, target, DamageKind.Effect, CombatDamageVfxKind.Spell);
		target.TakeDamage(baseDamage, source, DamageKind.Effect);
		if (target is Minion minion && minion.IsDead)
			Board.RemoveMinion(minion);
		CheckDeaths();
		return true;
	}

	// ===== 表情系统 =====

	/// <summary>
	/// 表情系统子节点——管理敌人嘲讽表情的定时触发。
	/// 在 <see cref="_Ready"/> 中作为子节点创建并添加。
	/// </summary>
	private EmoteSystem _emoteSystem;

	/// <summary>
	/// 强制从指定敌人发送一条表情文本（由 ChatScreen /emote 命令调用）。
	/// </summary>
	public void SendEnemyEmote(string text, int enemyIndex = 0)
	{
		bool isOfficial = Core.GameManager.Instance?.IsOfficialCollectionEmote(text) ?? false;
		_emoteSystem?.SendEnemyEmote(text, enemyIndex, isOfficial);
	}

	/// <summary>
	/// 强制由玩家发送一条消息/表情。
	/// </summary>
	public void SendPlayerEmote(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return;

		bool isOfficial = Core.GameManager.Instance?.IsOfficialCollectionEmote(text) ?? false;
		OnCombatEmote?.Invoke(new CombatEmoteMessage(
			text.Trim(),
			CombatEmoteSpeaker.Player,
			-1,
			isOfficial));
	}

	/// <summary>
	/// 兼容旧调用方：默认按敌方 0 号发送。
	/// </summary>
	public void SendEmote(string text)
	{
		SendEnemyEmote(text, 0);
	}

	/// <summary>
	/// 获取/设置表情空闲计时器的基础时长（委托给 <see cref="EmoteSystem.EmoteIdleBaseTime"/>）。
	/// </summary>
	public static float EmoteIdleBaseTime
	{
		get => EmoteSystem.EmoteIdleBaseTime;
		set => EmoteSystem.EmoteIdleBaseTime = value;
	}

	/// <summary>
	/// 获取当前敌方遭遇的动态意图（含实时目标和伤害计算）。
	/// UI 在 <see cref="OnCombatStateChanged"/> 触发时调用此方法刷新显示。
	/// </summary>
	/// <returns>包含动态 TargetSelector 和 DamageCalc 的意图结构体</returns>
	/// <summary>
	/// 获取指定敌方单位的当前意图。
	/// </summary>
	public EnemyIntent GetCurrentEnemyIntent(int enemyIndex = 0)
	{
		return EnemyUnits[enemyIndex].GetCurrentIntent(this);
	}

	/// <summary>
	/// 通知战斗状态变化，并在 UI 重新查询意图前失效攻击目标缓存。
	/// 玩家部署/移除嘲讽随从后，敌人意图应重新锁定到最新合法目标，
	/// 但同一次状态刷新内显示与执行仍共用同一个缓存目标。
	/// </summary>
	private void NotifyCombatStateChanged()
	{
		foreach (var unit in EnemyUnits)
			unit.Brain.ResetCachedAttackTarget();

		OnCombatStateChanged?.Invoke();
	}

	// ===== 随从攻击追踪 =====

	/// <summary>
	/// 攻击追踪器——管理本回合内随从的"可否攻击"和"已攻击次数"状态。
	/// 从 CombatManager 拆出为独立类，解除回合流转/攻击系统/死亡处理之间的数据耦合。
	/// </summary>
	private readonly AttackTracker _attackTracker = new();
	private MinionActionService _playerActions = null!;
	public IntelService Intel { get; } = new();
	/// <summary>一步突击已推进落位，可由 UI 选择停稳开火或滑行开火演出。</summary>
	public event Action<Minion>? OnAssaultAdvance;

	/// <summary>供 UI 高亮、两步推进及一步突击动画使用的统一事务入口。</summary>
	public bool CanMoveMinionToFrontline(Minion minion, int slot = -1) => State.IsPlayerTurn && !State.IsGameOver
		&& !IsDiscovering && _playerActions.CanMove(minion, slot);
	public bool MoveMinionToFrontline(Minion minion, int preferredFrontlineSlot = -1)
	{
		if (!CanMoveMinionToFrontline(minion, preferredFrontlineSlot))
			return false;
		bool moved = _playerActions.Move(minion, preferredFrontlineSlot);
		NotifyCombatStateChanged();
		return moved;
	}

	public bool CanMinionAttackTarget(Minion attacker, IDamageTarget target) => State.IsPlayerTurn && !State.IsGameOver
		&& !IsDiscovering && _playerActions.CanAttack(attacker, target, out _);

	public bool CanMinionAct(Minion minion) => State.IsPlayerTurn && !State.IsGameOver && !IsDiscovering
		&& minion.IsPlayerSide && PlayerHero.CanSpendMana(minion.ActionCost)
		&& (_attackTracker.CanAttack(minion) || _attackTracker.CanMove(minion));

	/// <summary>
	/// 本回合内可以攻击的敌方随从集合。
	/// 敌方意图/召唤产生的随从默认不能立即攻击——只有回合开始时已存在的随从可以攻击。
	/// 在 <see cref="ExecuteEnemyTurn"/> 开始时快照，在 <see cref="EnemyMinionsAttack"/> 中使用。
	/// </summary>
	private readonly HashSet<Minion> _enemyMinionsCanAttack = new();

	/// <summary>
	/// 敌方回合执行动画进行中。设为 true 时冻结意图 UI 刷新，
	/// 防止执行动画期间意图数值跳变。参考 STS2 的 NIntent._isFrozen。
	/// </summary>
	private bool _isEnemyTurnAnimating;

	/// <summary>
	/// 公开的冻结状态查询——UI 通过此属性判断是否应跳过意图刷新。
	/// </summary>
	public bool IsEnemyTurnAnimating => _isEnemyTurnAnimating;

	// ===== 发现选牌 / 手牌选择 =====

	/// <summary>
	/// 选择系统——管理发现选牌、手牌选择等所有选择交互。
	/// </summary>
	private SelectionSystem _selectionSystem = null!;

	/// <summary>
	/// 当前正在等待玩家进行发现选牌或手牌选择。
	/// </summary>
	public bool IsDiscovering => _selectionSystem?.IsDiscovering ?? false;

	/// <summary>
	/// 当前发现选牌的 N 张候选卡牌（只读）。
	/// </summary>
	public IReadOnlyList<CardData>? DiscoverOptions => _selectionSystem?.DiscoverOptions;

	/// <summary>
	/// 当前选牌需要选择的张数。
	/// </summary>
	public int DiscoverPickCount => _selectionSystem?.DiscoverPickCount ?? 1;

	/// <summary>
	/// 当前选牌是否使用运行时卡牌实例。
	/// </summary>
	public IReadOnlyList<Card.Card>? DiscoverRuntimeOptions => _selectionSystem?.DiscoverRuntimeOptions;

	public enum PendingSelectionMode
	{
		Discover,
		Discard,
		ChooseDiscard,
		BladeCrisis,
		CopyHandFill,
		RestructureDiscard,
		DevDiscard
	}

	/// <summary>
	/// 当前选牌模式，供 UI 读取以自定义标题/行为。
	/// </summary>
	public PendingSelectionMode CurrentSelectionMode => _selectionSystem?.CurrentSelectionMode ?? PendingSelectionMode.Discover;

	/// <summary>
	/// 是否处于手牌选择模式（STS2 风格）。
	/// </summary>
	public bool IsHandSelecting => _selectionSystem?.IsHandSelecting ?? false;

	/// <summary>
	/// 手牌选择模式的待选卡牌列表（只读）。
	/// </summary>
	public IReadOnlyList<Card.Card>? HandSelectOptions => _selectionSystem?.HandSelectOptions;

	/// <summary>
	/// 手牌选择最少需选张数。
	/// </summary>
	public int HandSelectMin => _selectionSystem?.HandSelectMin ?? 0;

	/// <summary>
	/// 手牌选择最多可选张数。
	/// </summary>
	public int HandSelectMax => _selectionSystem?.HandSelectMax ?? 0;

	// ===== 规则子系统 =====

	/// <summary>
	/// 卡牌效果分发器——持有 EffectType→Handler 注册表与具体处理逻辑。
	/// </summary>
	private CardEffectDispatcher _effectDispatcher = null!;

	/// <summary>
	/// 反制牌规则服务——手中付费激活/取消激活、敌方回合拦截法术。
	/// </summary>
	private CountermeasureService _countermeasures = null!;

	/// <summary>
	/// 领域触发管理器——封装领域在部署/回合/受击等时机的行为。
	/// </summary>
	private DomainTriggerManager _domainTriggerManager = null!;

	/// <summary>
	/// 本回合是否已使用过英雄技能。回合开始时重置。
	/// </summary>
	private bool _heroPowerUsedThisTurn;

	// 机会~被动追踪：每回合各自独立计数
	private bool _chansuTriggeredThisPlayerTurn;
	private bool _chansuTriggeredThisEnemyTurn;

	/// <summary>
	/// 胜负判定器——检查战斗结束条件、发放金币、触发胜负事件。
	/// </summary>
	private VictoryDefeatResolver _victoryResolver = null!;

	/// <summary>
	/// 死亡处理器——管理随从死亡检测、亡语触发、牌堆回收。
	/// </summary>
	private DeathHandler _deathHandler = null!;

	/// <summary>
	/// 武器攻击系统——管理玩家英雄的武器攻击与主动技能。
	/// </summary>
	private WeaponAttackSystem _weaponAttack = null!;

	// ===== Godot 生命周期 =====

	/// <summary>
	/// Godot 节点就绪回调。注册单例引用。
	/// </summary>
	public override void _Ready()
	{
		Instance = this;
		GD.Print("[CombatManager] _Ready — 单例已注册");

		// 表情系统——作为子节点创建并添加，事件转发到 OnCombatEmote
		_emoteSystem = new EmoteSystem { Name = "EmoteSystem" };
		AddChild(_emoteSystem);
		_emoteSystem.OnEmote += message => OnCombatEmote?.Invoke(message);
		GD.Print("[CombatManager] EmoteSystem 子节点已创建");

		// 使用 CallDeferred 延迟到下一帧执行，确保 GameManager.Instance 等 Autoload 已就绪
		CallDeferred(nameof(BootstrapCombat));
	}

	// ===== 自动启动 =====

	/// <summary>
	/// 战斗自动启动引导方法。
	/// 由 <see cref="_Ready"/> 通过 CallDeferred 调用，确保 Autoload 已就绪。
	/// 从 GameManager 获取 Player；若不存在则回退创建，确保初始化链不会静默失败。
	/// </summary>
	private void BootstrapCombat()
	{
		GD.Print("[CombatManager] BootstrapCombat 开始...");

		// 1. 确保 GameManager 可用（Autoload 应在 Bootstrap 之前已就绪）
		var gm = GameManager.Instance;
		if (gm == null)
		{
			GD.PrintErr("[CombatManager] BootstrapCombat 失败 — GameManager.Instance 为 null");
			return;
		}

		// 2. 确保 CurrentPlayer 存在，若未创建则回退
		if (gm.CurrentPlayer == null)
		{
			GD.Print("[CombatManager] CurrentPlayer 为 null，尝试回退创建玩家...");
			gm.CreateNewPlayer();
			if (gm.CurrentPlayer == null)
			{
				GD.PrintErr("[CombatManager] BootstrapCombat 失败 — 回退创建 Player 失败");
				return;
			}
		}

		var player = gm.CurrentPlayer;

		// 3. 检查牌堆是否为空
		if (player.Deck == null || player.Deck.CardCount == 0)
		{
			GD.PrintErr($"[CombatManager] BootstrapCombat 失败 — 牌堆为空（{player.Deck?.CardCount ?? 0} 张牌）");
			return;
		}
		GD.Print($"[CombatManager] 牌堆有 {player.Deck.CardCount} 张牌");

		// 3.5. 保存战斗开始时的牌组快照（用于信息界面"当前卡组"显示）
		gm.SnapshotCombatStartDeck();

		// 4. 创建敌方英雄和 AI 遭遇（FightOverride 优先 → RoomTypeOverride → RunState → 回退）
		IReadOnlyList<EnemyEncounter> encounters;
		if (gm.FightOverride is { Count: > 0 })
		{
			encounters = gm.FightOverride;
			gm.FightOverride = null; // 消费后清空
			GD.Print($"[CombatManager] 从 FightOverride 读取 {encounters.Count} 个敌人 — {string.Join(", ", encounters.Select(e => e.Name))}");
		}
		else if (gm.RoomTypeOverride is Roguelike.RoomType roomType &&
			roomType is Roguelike.RoomType.Monster or Roguelike.RoomType.Elite or Roguelike.RoomType.Boss)
		{
			// /room monster 等命令——用覆写的房间类型创建遭遇
			gm.RoomTypeOverride = null; // 消费后清空
			gm.RunState?.SelectRoom(new Roguelike.RoomDefinition
			{
				Type = roomType,
				DisplayName = roomType.ToString(),
			});
			encounters = gm.RunState!.CreateEncounters();
			GD.Print($"[CombatManager] 从 RoomTypeOverride 读取 {encounters.Count} 个敌人（{roomType}）");
		}
		else if (gm.RunState is { SelectedRoom: not null } runState &&
			runState.SelectedRoom.Type is RoomType.Monster or RoomType.Elite or RoomType.Boss)
		{
			encounters = runState.CreateEncounters();
			GD.Print($"[CombatManager] 从 RunState 读取 {encounters.Count} 个敌人 — {string.Join(", ", encounters.Select(e => e.Name))}" +
					  $"（{runState.SelectedRoom.Type}）");
		}
		else
		{
			// 回退：如果没有运行状态（例如直接从 Combat.tscn 启动），使用默认牌手
			encounters = new EnemyEncounter[] { EnemyDeckProfiles.CreateCultist() };
			GD.Print("[CombatManager] 回退使用默认敌人 — 狂热学徒（卡组牌手）");
		}

		// 为每个 EnemyEncounter 创建对应的 Hero + EnemyUnit
		var enemyUnits = new List<EnemyUnit>();
		bool isBossEncounter = GameManager.Instance.RunState?.SelectedRoom?.Type == Roguelike.RoomType.Boss;
		foreach (var enc in encounters)
		{
			var enemyCore = new CommanderCore();
			enemyCore.InitializeHealth(enc.MaxHealth, enc.MaxHealth);
			enemyCore.SetMana(0, 0);
			var enemyHero = new Hero(enemyCore, false);
			var unit = new EnemyUnit(enemyHero, enc);
			enc.IsBoss = isBossEncounter; // 标记 Boss——斩杀类效果（致命裂痕）对此单位无效但仍提示一次
			enemyUnits.Add(unit);

			// 注入敌方被动技能
			ApplyEnemyPassives(unit);
			GD.Print($"[CombatManager] 敌方已创建 — {enc.Name}，{enemyHero.CurrentHealth}/{enemyHero.MaxHealth}HP");
		}

		// 5. 初始化战斗管理器（创建 _playerCore、PlayerHero、Board、GameState）
		Initialize(player, enemyUnits);

		// 6. 获取 CombatUI 并初始化
		var combatUI = GetNode<CombatUI>("CanvasLayer/CombatUI");
		combatUI.Initialize(player, this);
		GD.Print("[CombatManager] CombatUI 已初始化");

		// 7. 开始战斗
		StartCombat();
		combatUI.RefreshAll(); // StartCombat 中法力变化后刷新 UI

		// 触发初始意图事件（使用动态意图）
		NotifyCombatStateChanged();

		GD.Print("[CombatManager] BootstrapCombat 完成");
	}

	// ===== 初始化 =====

	/// <summary>
	/// 初始化战斗管理器。
	/// 创建战场和游戏状态，构建玩家英雄包装，存储敌方战斗单位引用。
	/// </summary>
	/// <param name="player">玩家角色（Godot Node）</param>
	/// <param name="enemyUnits">敌方战斗单位列表（1..N 个）</param>
	/// <exception cref="ArgumentNullException">当 player 或 enemyUnits 为 null/空时抛出</exception>
	public void Initialize(Player player, IReadOnlyList<EnemyUnit> enemyUnits)
	{
		Player = player ?? throw new ArgumentNullException(nameof(player));
		if (enemyUnits == null || enemyUnits.Count == 0)
			throw new ArgumentNullException(nameof(enemyUnits));
		EnemyUnits = enemyUnits;

		// 创建玩家英雄专用的 CommanderCore，共享牌堆定义
		_playerCore = new CommanderCore();
		_playerCore.Deck = player.Deck;
		_playerCore.InitializeHealth(player.MaxHealth, player.CurrentHealth);
		_playerCore.SetMana(0, 0);
		_playerCore.MaxHandSize = 10; // 统一手牌上限，覆盖 CombatDeckState 默认值 9
		var heroProfile = GameManager.Instance?.SelectedHeroProfile ?? HeroProfile.Get(null);
		PlayerHero = new Hero(_playerCore, true);

		// 从 Player 复制英雄技能设置（由 GameManager.CreateNewPlayer 注入）
		PlayerHero.HeroPower = Player.HeroPower;
		if (heroProfile.StartingDefense != 0)
			PlayerHero.ModifyDefense(heroProfile.StartingDefense);

		// 奇巧关键词回调：被弃牌时自动打出
		_playerCore.CombatDeckState.OnBeforeDiscard = HandleQiqiaoDiscard;

		Board = new Board();
		State = new GameState(_playerCore);

		// 日志接线必须先于任何规则订阅：机械蜈蚣-防空型等“部署即受伤”的拦截
		// 依赖新随从已订阅伤害事件，否则首次拦截伤害会从战报中丢失。
		InitializeCombatLogging();

		// 创建胜负判定器
		_victoryResolver = new VictoryDefeatResolver(Board, State, EnemyUnits, _playerCore);
		_victoryResolver.OnGameOver += (won) =>
		{
			if (won)
			{
				// 跨战斗保存玩家生命值（持久化到 GameManager）
				var gm = GameManager.Instance;
				gm?.SavePlayerHealth(PlayerHero.CurrentHealth, PlayerHero.MaxHealth);
				GD.Print($"[CombatManager] 已保存玩家生命值：{PlayerHero.CurrentHealth}/{PlayerHero.MaxHealth}");
				gm?.SaveRun();
			}
			else
			{
				// 标记运行失败
				var gm = GameManager.Instance;
				gm?.RunState?.FailRun();
				gm?.SaveRun();
			}
			CleanupCombat();
			OnGameOver?.Invoke(won);
		};

		// 事件驱动胜负判定：任何英雄死亡时自动触发 CheckVictoryOrDefeat
		PlayerHero.OnDeath += _ => _victoryResolver.CheckVictoryOrDefeat();
		foreach (var unit in enemyUnits)
			unit.Body.OnDeath += _ =>
			{
				// 敌人死亡时需要刷新 UI：意图箭头清除 + 身份卡显示死亡状态
				NotifyCombatStateChanged();
				_victoryResolver.CheckVictoryOrDefeat();
			};

		Board.OnMinionPlaced += (minion, _) =>
		{
			minion.OwnerHero ??= ResolveDeploymentOwner(minion.IsPlayerSide);
			// 同构牌手随从由评分贪心控制器行动，不挂旧意图脑，避免 UI 画出静态意图箭头。
			if (!minion.IsPlayerSide && !IsCommanderMinion(minion))
				minion.IntentBrain ??= new DefaultAttackMinionBrain(minion);
			_attackTracker.RegisterDeployment(minion);
		};

		// 机械蜈蚣-防空型自动拦截：敌方部署费用 ≤2 的随从时自动触发战斗
		Board.OnMinionPlaced += OnMinionPlacedForCentipede;

		// 装配所选英雄的初始武器
		PlayerHero.Weapon = GameManager.Instance?.CreateSelectedHeroWeapon() ?? new MagicWand();

		// 初始化热力值系统
		_heatSystem = new HeatSystem();

		// 初始化藏品管理器（从 GameManager 获取，或创建空列表）
		_relicManager = GameManager.Instance?.Relics ?? new RelicManager();
		// 藏品修改热力值（如冰袋）
		_relicManager.ModifyHeatSystem(_heatSystem);
		_selectionSystem = new SelectionSystem(
			PlayerHero,
			_playerCore,
			Board,
			State,
			NotifyCombatStateChanged,
			CheckDeaths,
			() => _victoryResolver.CheckVictoryOrDefeat());

		_selectionSystem.OptionRequested = (request, chosen) => BeginChooseOne(request.TitleKey,
			request.TitleFallback, request.Labels, request.Descriptions, chosen);
		_effectDispatcher = new CardEffectDispatcher(PlayerHero, EnemyUnits, Board,
			NotifyCombatStateChanged, RequestDamageVfx, Intel, LoadCombatCard,
			() => GameManager.Instance?.GetEnabledCards() ?? new List<CardData>());
		_countermeasures = new CountermeasureService(LoadCombatCard, NotifyCombatStateChanged);
		_deathHandler = new DeathHandler(Board, PlayerHero, _effectDispatcher, _attackTracker,
			minion => CreateActorContext(minion.OwnerHero ?? GetOwnerHero(minion.IsPlayerSide), minion),
			ExecuteLoggedDeathrattle);
		_weaponAttack = new WeaponAttackSystem(
			Board,
			PlayerHero,
			EnemyUnits,
			State,
			_deathHandler,
			NotifyCombatStateChanged,
			() => IsDiscovering,
			(m) => TriggerBaitTacticsOnAttacked(m, null),
			this);
		_domainTriggerManager = new DomainTriggerManager(
			_playerCore,
			PlayerHero,
			Board,
			State,
			EnemyUnits,
			NotifyCombatStateChanged, LoadCombatCard);
		_playerActions = new MinionActionService(Board, _attackTracker, PlayerHero);
		_playerActions.OnAssaultAdvance += minion => OnAssaultAdvance?.Invoke(minion);
		Board.OnMinionPlaced += (minion, _) =>
		{
			_domainTriggerManager.OnMinionPlaced(minion);
		};
		// 规则订阅之后再广播，UI/意图预览必须看到已应用部署领域与闪击配额的状态。
		Board.OnMinionPlaced += (_, _) => NotifyCombatStateChanged();
		Board.OnMinionRemoved += (_) => NotifyCombatStateChanged();
		Board.OnMinionMoved += (_, _, _) => NotifyCombatStateChanged();
		PlayerHero.OnAttacked += HandlePlayerHeroAttacked;
		foreach (var unit in EnemyUnits)
			unit.Body.OnAttacked += HandlePlayerHeroAttacked;

		// 机会~被动：订阅所有敌方单位的 OnDamageTaken
		foreach (var unit in EnemyUnits)
		{
			unit.Body.OnDamageTaken += HandleEnemyDamageTakenForChansu;
		}

		// 注册热力值伤害修改器到所有敌方单位
		var heatMod = new HeatDamageModifier(_heatSystem);
		foreach (var unit in EnemyUnits)
		{
			unit.Body._damageModifiers.Add(heatMod);
		}

		foreach (var unit in EnemyUnits)
		{
			unit.Body.Weapon = unit.Brain.CreateWeapon();
			if (unit.Controller is IDeckDrivenCommander commander)
			{
				var profile = commander.DeckProfile
					?? LoadThemeDeckProfile(commander.HeroId)
					?? new DeckProfile { HeroId = commander.HeroId, ThemeName = "通用牌组", TargetDeckSize = commander.TargetDeckSize };
				commander.InitializeDeck(unit.Body, GameManager.Instance.GetEnabledCards(), profile, Random.Shared.Next());
				unit.Body.Core.CombatDeckState.OnBeforeDiscard = card => HandleQiqiaoDiscard(unit.Body, card);
			}
			if (unit.Brain.StartingDefense != 0)
				unit.Body.ModifyDefense(unit.Brain.StartingDefense);
			GD.Print($"[CombatManager] {unit.Brain.Name} 武器：{unit.Body.Weapon.Name}" +
					  $"（{unit.Body.Weapon.Attack}攻）");

			// 同步敌人攻击力到意图系统
			unit.Brain.Attack = unit.Body.Weapon?.Attack ?? 0;
		}

		GD.Print($"[CombatManager] 初始化完成 — 玩家 {PlayerHero.CurrentHealth}/{PlayerHero.MaxHealth}，" +
				  $"敌方 {EnemyUnits.Count} 个单位：" +
				  string.Join(", ", EnemyUnits.Select(u => $"{u.Brain.Name} {u.Body.CurrentHealth}/{u.Body.MaxHealth}")));
	}

	/// <summary>
	/// 加载某英雄的主题卡组画像（纯 C# 快照）；资源不存在时返回 null。
	/// 供「牌手未自带 DeckProfile」的宿敌指挥官回退使用。
	/// </summary>
	private static DeckProfile? LoadThemeDeckProfile(string heroId)
	{
		if (string.IsNullOrEmpty(heroId))
			return null;
		string path = $"res://Resources/Themes/ThemeProfile_{char.ToUpperInvariant(heroId[0])}{heroId[1..]}.tres";
		if (!ResourceLoader.Exists(path))
			return null;
		return GD.Load<ThemeProfile>(path)?.ToDeckProfile();
	}

	/// <summary>
	/// 注入敌人被动技能 modifier 到对应的 Hero。
	/// 固璋(3) → DamageCapModifier；不破(1) → HasUnbreakable。
	/// 旧意图敌人（张郎）与同构牌手（DeckDrivenCommander 的被动标记）都走同一入口。
	/// </summary>
	private static void ApplyEnemyPassives(EnemyUnit unit)
	{
		if (unit.Brain is DeckDrivenCommander commander)
		{
			if (commander.DamageCap is int cap)
			{
				unit.Body._damageModifiers.Add(new DamageCapModifier(cap));
				GD.Print($"[CombatManager] {unit.Brain.Name} 固璋({cap}) — 单次伤害上限 {cap}");
			}
			if (commander.Unbreakable)
			{
				unit.Body.HasUnbreakable = true;
				GD.Print($"[CombatManager] {unit.Brain.Name} 不破(1) — 每回合仅受一次生命伤害");
			}
		}

		// 旧意图死代码路径：保留以保证旧单测与 /fight 直接 new ZhangLang() 仍可用。
		if (unit.Brain is ZhangLang)
		{
			unit.Body._damageModifiers.Add(new DamageCapModifier(3));
			GD.Print($"[CombatManager] {unit.Brain.Name} 固璋(3) — 单次伤害上限 3");
		}
	}

	// ===== 战斗开始 =====

	/// <summary>
	/// 开始战斗——仅处理战斗初始化（场景、牌堆、发牌）。
	/// 完成后调用 <see cref="StartPlayerTurn"/> 启动第一个玩家回合。
	/// </summary>
	public void StartCombat()
	{
		GD.Print("[CombatManager] ========== 战斗开始 ==========");
		CombatLog.Clear();

		// 从牌堆定义创建运行时卡牌并洗入抽牌堆
		_playerCore.SetupDrawPile();
		GD.Print($"[CombatManager] 抽牌堆已设置，共 {_playerCore.DrawPile.Count} 张牌");

		State.StartGame();
		foreach (var unit in EnemyUnits.Where(unit => unit.Controller is IDeckDrivenCommander))
		{
			unit.Body.SetMana(GameState.StartingMaxMana, GameState.StartingMaxMana);
			unit.Body.DrawCards(5);
		}

		// 玩家始终先手，发 5 张起始手牌
		PlayerHero.DrawCards(5);
		GD.Print($"[CombatManager] 起手抽 5 张牌 → 共 {_playerCore.Hand.Count} 张手牌");

		// 藏品 — 战斗开始时触发（需在发牌之后，以便好梦抱枕等操作抽牌堆）
		_relicManager.TriggerBattleStart(this);

		// 开始第一个玩家回合
		StartPlayerTurn();
	}

	/// <summary>
	/// 玩家回合开始——法力增长/回满、抽 1 张、重置攻击状态。
	/// 由 <see cref="StartCombat"/>（首回合）和 <see cref="EndPlayerTurn"/>（后续回合）调用。
	/// </summary>
	private void StartPlayerTurn()
	{
		// 反制牌新回合失效：上一轮未触发的已激活反制牌解除激活，需重新付费。
		_countermeasures?.DisarmAll(PlayerHero);

		// 重置英雄技能使用标记
		_heroPowerUsedThisTurn = false;
		_chansuTriggeredThisPlayerTurn = false;

		// 检查英雄是否拥有「无限潜能」领域，决定自然增长上限
		int growthCap = PlayerHero.ActiveDomains.ContainsKey("unlimited_potential")
			? GameState.HardMaxManaCap
			: GameState.MaxManaCrystals;
		State.StartPlayerTurn(growthCap);
		// 在回合开始效果/藏品召唤之前快照：只有回合开始已在场的随从免整备冷却。
		ResetAttackTracking();

		// 状态效果 + 限时 mount 〜 在藏品之前触发：原 sutaraito_spirit 走领域通道时也位于此处，
		// 已迁移到 StatusEffect(PlayerTurnStart) 通道，此处触发替代了原 OnPlayerTurnStart。
		// damage_over_time 状态衰减也走同一路径。
		TickTurnStartStatusEffects(TickTiming.PlayerTurnStart);
		CheckDeaths();
		if (_victoryResolver.CheckVictoryOrDefeat())
			return;

		// 藏品 〜 回合开始时触发，位于 mount 状态之后，以便藏品逻辑可以看到已结算的法力/手牌。
		_relicManager.TriggerTurnStart(this);

		// 领域回合开始 + 先发制人重置
		_domainTriggerManager.OnPlayerTurnStart();

		// 回合开始抽 1 张牌
		PlayerHero.DrawCards(1);

		// 重置伏击状态（所有随从和英雄的伏击在新整轮重新可用）
		foreach (var minion in Board.GetPlayerMinions())
			minion.ResetAmbush();
		foreach (var minion in Board.GetEnemyMinions())
			minion.ResetAmbush();
		PlayerHero.ResetAmbush();
		foreach (var unit in EnemyUnits)
			unit.Body.ResetAmbush();

		// 重置受伤标记（确保上回合武器反击的标记不会跨回合影响新的攻击）
		PlayerHero.ResetDamageTakenThisTurn();
		foreach (var unit in EnemyUnits)
			unit.Body.ResetDamageTakenThisTurn();

		// 重置武器攻击次数 + 冷却衰减
		PlayerHero.ResetWeaponAttacks();
		PlayerHero.TickWeaponCooldown();
		if (PlayerHero.HeroPower is IChargeCooldownSkill chargedHeroPower)
			chargedHeroPower.TickChargeCooldown();

		// 启动表情空闲计时器
		_emoteSystem?.StartIdleTimer();

		CombatLog.RecordTurnStart(State.TurnCount, isPlayerSide: true,
			Localization.Localization.T("ui.combat.player_turn", "玩家回合"));
		GD.Print($"[CombatManager] 第 {State.TurnCount} 回合开始（法力 {State.PlayerMana}/{State.PlayerMaxMana}），手牌 {_playerCore.Hand.Count} 张");
	}

	// ===== 卡牌打出通知（藏品/热力值钩子） =====

	/// <summary>
	/// 卡牌成功打出后的统一通知点。
	/// 通知热力值系统和藏品系统卡牌打出和法力花费事件。
	/// </summary>
	/// <param name="card">打出的卡牌</param>
	/// <param name="actualCost">实际消耗的法力值</param>
	private void NotifyCardPlayed(Card.Card card, int actualCost)
	{
		_heatSystem.OnCardPlayed();
		_heatSystem.OnManaSpent(actualCost);
		_relicManager.TriggerCardPlayed(this, card, actualCost);
		_relicManager.TriggerManaSpent(this, actualCost);

		// 玩家出牌，重置表情空闲计时器
		_emoteSystem?.ResetIdleTimer();
		GD.Print($"[CombatManager] 卡牌「{card.CardName}」已打出，表情计时器已重置");
	}

	/// <summary>
	/// 应用藏品费用修改器，返回修改后的费用。
	/// </summary>
	private int ApplyRelicCostModifiers(Card.Card card)
	{
		return _relicManager.ApplyCostModifiers(card, card.GetEffectiveCost());
	}

	// ===== 随从召唤 =====

	/// <summary>
	/// 玩家打出一张随从牌，将其召唤至战场的指定槽位。
	/// 验证玩家回合、法力值、槽位可用性，处理战吼和闪击关键词。
	/// </summary>
	/// <param name="card">要打出的卡牌（手牌中的运行时实例）</param>
	/// <param name="slotIndex">所属方支援线槽位索引（0-3）</param>
	/// <returns>成功返回 true；验证失败返回 false</returns>
	public bool PlayMinion(Card.Card card, int slotIndex)
	{
		return PlayMinion(CreateActorContext(PlayerHero, PlayerHero, card), card, slotIndex);
	}

	/// <summary>
	/// 处理战吼效果。
	/// 效果分发器统一执行战吼，保留随从作为伤害来源与阵营上下文。
	/// </summary>
	/// <param name="effect">战吼效果数据</param>
	/// <param name="source">战吼来源随从</param>
	private void ResolveBattlecryEffect(CardEffectData effect, Minion source)
	{
		GD.Print($"[CombatManager]     战吼效果：{effect.GetDescription()}（来源：{source.CardName}）");
		ExecuteEffect(effect, source, source);
	}

	/// <summary>
	/// 我的刀盾战吼：复制Token随从到相邻槽位。
	/// 优先尝试左侧（slot-1），再尝试右侧（slot+1），最后回退任意空位。
	/// </summary>
	/// <param name="sourceMinion">触发战吼的源随从</param>
	private void CopyMinionToAdjacentSlot(Minion sourceMinion)
	{
		var tokenData = GD.Load<CardData>("res://Resources/Cards/Minion_WhatTheDogDoing.tres");
		if (tokenData == null)
		{ GD.PrintErr("[CombatManager] CopyToAdjacentSlot: 无法加载Token卡牌"); return; }

		// 优先相邻槽位（先左后右）
		int targetSlot = Board.GetAdjacentSupportSlotIndex(sourceMinion);
		if (targetSlot >= 0)
		{
			var copy = new Minion(tokenData, sourceMinion.IsPlayerSide) { OwnerHero = sourceMinion.OwnerHero };
			Board.PlaceMinion(copy, targetSlot);
			GD.Print($"[CombatManager] [Copy] 我的刀盾复制到槽位{targetSlot}");
		}
		else
			GD.Print("[CombatManager] [Copy] 我的刀盾：棋盘已满，无法复制");
	}

	// ===== 法术施放 =====

	/// <summary>
	/// 玩家打出一张法术牌，对目标施放效果。
	/// 目标可以是随从（Minion）或英雄（Hero），通过 IDamageTarget 接口统一处理伤害。
	/// 如果法术包含 Discover 效果，则暂停清理流程，等待玩家选牌后通过 <see cref="ConfirmDiscoverChoice"/> 完成。
	/// </summary>
	/// <param name="card">要打出的法术牌</param>
	/// <param name="target">法术目标（Minion 或 Hero 实例）</param>
	/// <returns>成功返回 true</returns>
	public bool PlaySpell(Card.Card card, object target)
	{
		return PlaySpell(CreateActorContext(PlayerHero, PlayerHero, card), card, target);
	}

	// ===== 领域卡牌播放 =====

	/// <summary>
	/// 玩家打出一张领域牌，将持久效果附加到玩家英雄上。
	/// 领域不需要选择目标，自动挂在己方英雄上。
	/// 同名领域叠加层数，不同领域独立存在。
	/// 领域不进入弃牌堆。
	/// </summary>
	/// <param name="card">要打出的领域牌</param>
	/// <returns>成功返回 true</returns>
	public bool PlayDomain(Card.Card card)
	{
		return PlayDomain(CreateActorContext(PlayerHero, PlayerHero, card), card);
	}

	// ===== 反制牌（手中激活 / 取消激活） =====

	/// <summary>
	/// 玩家在友方回合内激活或取消激活手中的反制牌。
	/// 首次调用支付费用并置为待命（卡不离开手牌）；再次调用退还费用并解除待命。
	/// </summary>
	public bool PlayCountermeasure(Card.Card card)
	{
		if (IsDiscovering || !State.IsPlayerTurn || State.IsGameOver || card == null
			|| card.Type != CardType.Countermeasure || !PlayerHero.Hand.Contains(card))
			return false;

		bool wasArmed = card.IsCountermeasureArmed;
		int cost = GetPlayCost(CreateActorContext(PlayerHero, PlayerHero, card), card);
		if (!_countermeasures.Toggle(PlayerHero, card, cost))
			return false;

		if (!wasArmed)
			NotifyCardPlayed(card, cost);
		NotifyCombatStateChanged();
		return true;
	}

	/// <summary>AI 牌手能否在回合末激活这张反制牌。</summary>
	internal bool CanArmCountermeasure(Hero owner, Card.Card card)
	{
		if (owner == null || !IsOwnerTurn(owner) || _countermeasures == null)
			return false;
		int cost = GetPlayCost(CreateActorContext(owner, owner, card), card);
		return !card.IsCountermeasureArmed && _countermeasures.CanToggle(owner, card, cost);
	}

	/// <summary>AI 牌手在回合末激活反制牌。</summary>
	internal bool ArmCountermeasure(Hero owner, Card.Card card)
	{
		if (!CanArmCountermeasure(owner, card))
			return false;
		int cost = GetPlayCost(CreateActorContext(owner, owner, card), card);
		bool armed = _countermeasures.Arm(owner, card, cost);
		if (armed && !owner.IsPlayerSide)
			NotifyCombatStateChanged();
		return armed;
	}

	/// <summary>
	/// 执行单个卡牌效果，根据 EffectType 分发到对应的处理逻辑。
	/// 供法术、战吼、亡语等所有效果触发场景共用。
	/// </summary>
	/// <param name="effect">效果数据</param>
	/// <param name="target">效果目标对象（Minion 或 Hero，可为 null）</param>
	private void ExecuteEffect(CardEffectData effect, object? target, IDamageSource source, object? visualSource = null)
	{
		var owner = source is Hero hero ? hero : ((Minion)source).OwnerHero ?? GetOwnerHero(source.IsPlayerSide);
		_effectDispatcher.ExecuteEffect(effect, target, CreateActorContext(owner, source, visualSource as Card.Card));
	}

	/// <summary>
	/// 处理玩家英雄被敌方攻击时触发的领域效果。
	/// </summary>
	/// <param name="target">被攻击英雄</param>
	/// <param name="source">攻击来源</param>
	/// <param name="finalDamage">护甲结算后的实际生命伤害</param>
	private void HandlePlayerHeroAttacked(Hero target, IDamageSource source, int finalDamage)
	{
		_domainTriggerManager.HandlePlayerHeroAttacked(target, source, finalDamage);
	}

	/// <summary>
	/// 敌方单位受击时检查「机会~」被动触发条件。
	/// 每回合首次对热力值 > 100% 的敌人造成伤害时，
	/// 从牌库或弃牌堆中搜索「机会~」并加入手牌。
	/// </summary>
	private void HandleEnemyDamageTakenForChansu(DamageEventInfo info, IDamageSource? source)
	{
		// 必须来自玩家方的伤害
		if (source == null || !source.IsPlayerSide)
			return;

		// 热力值必须 > 100%
		if (_heatSystem.CurrentHeat <= 1.0f)
			return;

		// 本回合已触发过
		bool alreadyTriggered = State.IsPlayerTurn
			? _chansuTriggeredThisPlayerTurn
			: _chansuTriggeredThisEnemyTurn;
		if (alreadyTriggered)
			return;

		// 搜索并取回
		if (TryFetchChansuToHand())
		{
			if (State.IsPlayerTurn)
				_chansuTriggeredThisPlayerTurn = true;
			else
				_chansuTriggeredThisEnemyTurn = true;
		}
	}

	/// <summary>
	/// 从牌库顶或弃牌堆中搜索「机会~」并加入手牌。
	/// 优先从弃牌堆查找（更可能在那里）。
	/// </summary>
	private bool TryFetchChansuToHand()
	{
		const string chaseId = "spell_chansu";

		// 先检查弃牌堆
		foreach (var card in _playerCore.DiscardPile)
		{
			if (card.Data.Id == chaseId)
			{
				_playerCore.DiscardPile.Remove(card);
				_playerCore.AddToHand(card);
				GD.Print($"[CombatManager] 机会~：从弃牌堆加入手牌");
				NotifyCombatStateChanged();
				return true;
			}
		}

		// 再检查牌库
		foreach (var card in _playerCore.DrawPile)
		{
			if (card.Data.Id == chaseId)
			{
				_playerCore.DrawPile.Remove(card);
				_playerCore.AddToHand(card);
				GD.Print($"[CombatManager] 机会~：从牌库加入手牌");
				NotifyCombatStateChanged();
				return true;
			}
		}

		return false;
	}

	// ===== 随从攻击 =====

	/// <summary>
	/// 统一的随从间战斗序列（炉石规则 + 伏击 + 冲击）。
	///
	/// 结算顺序：
	/// 1. 伏击检查 — 防御者有伏击且本回合未消耗时，防御者先手攻击
	/// 2. 冲击检查 — 攻击者有冲击时免疫所有反击伤害（包括伏击先手）
	/// 3. 攻击者造成伤害
	/// 4. 防御者反击（如果存活且攻击者无冲击）
	/// 5. 消耗冲击 + 消耗伏击
	/// 6. 死亡检查
	///
	/// 伏击 vs 冲击交互（KARDS 规则）：冲击免疫伏击的先手伤害，
	/// 伏击仍然消耗，攻击者仍正常造成伤害。
	/// </summary>
	/// <param name="attacker">攻击方随从</param>
	/// <param name="defender">防御方随从</param>
	/// <returns>向调用方返回是否应继续后续处理（若伏击击杀攻击方则为 false）</returns>
	/// <summary>
	/// 执行随从与随从之间的统一战斗序列（伏击 → 攻击 → 反击 → 消耗冲击）。
	/// 内部方法，供 CombatManager 的 MinionAttack 和 AI 系统的 DefaultAttackMinionBrain 调用。
	/// </summary>
	internal bool ResolveMinionCombat(Minion attacker, Minion defender)
	{
		TriggerBaitTacticsOnAttacked(defender);
		bool result = AttackCombatResolver.Resolve(attacker, defender, attacker.Attack,
			() => RequestDamageVfx(attacker, defender, DamageKind.Attack, CombatDamageVfxKind.Attack));
		TryTriggerIdolTwilight(defender);
		return result;
	}

	/// <summary>
	/// 偶像的黄昏领域触发：友方随从被攻击时获得 +N/+N。
	/// 查询玩家英雄的 <see cref="Hero.ActiveDomains"/>——领域在则触发，领域消失则自动停止。
	/// 这就是「持续领域」语义：领域展开后召唤的随从、洗入卡组再抽出的随从，被攻击时都会生效。
	/// </summary>
	/// <param name="defender">被攻击的随从（仅玩家方触发）</param>
	private void TryTriggerIdolTwilight(Minion defender)
	{
		if (defender.IsDead)
			return;
		if (!(defender.OwnerHero ?? GetOwnerHero(defender.IsPlayerSide)).ActiveDomains.TryGetValue("idol_twilight", out var domain))
			return;

		int stacks = Math.Max(1, domain.EffectData?.Value ?? 1) * domain.StackCount;
		defender.GainStats(stacks, stacks);
		GD.Print($"[CombatManager]   [Domain] 偶像的黄昏触发：{defender.CardName} 获得 +{stacks}/+{stacks}（{domain.StackCount}层）");
	}

	/// <summary>
	/// 执行两个随从之间的简化「战斗」——纯相互伤害，不走伏击/冲击/武器反击等复杂逻辑。
	/// 双方各自对对方造成等同于攻击力的伤害，伤害经过完整的 DamageResolver 管线
	/// （防御力、伤害翻倍等 modifier 正常生效）。遵循炉石同时伤害规则：即使一方在
	/// 伤害结算中死亡，仍能造成反击伤害。
	/// 用于机械蜈蚣-防空型等卡牌的自动拦截效果。
	/// </summary>
	/// <param name="a">战斗方 A（先手造成伤害）</param>
	/// <param name="b">战斗方 B（后手造成伤害，遵循同时伤害规则）</param>
	internal void ResolveCombat(Minion a, Minion b)
	{
		if (a.IsDead || b.IsDead)
			return;

		GD.Print($"[CombatManager] [Combat] 战斗：{a.CardName}（{a.Attack}攻/{a.CurrentHealth}血）vs " +
				  $"{b.CardName}（{b.Attack}攻/{b.CurrentHealth}血）");

		// A 对 B 造成伤害（完整 DamageResolver 管线）
		if (!a.IsIncapacitated)
		{
			RequestDamageVfx(a, b, DamageKind.Attack, CombatDamageVfxKind.Combat);
			b.TakeDamage(a.Attack, a);
		}

		// B 对 A 造成伤害（即使 B 已被击杀，仍遵循同时伤害规则）
		if (!b.IsIncapacitated)
		{
			RequestDamageVfx(b, a, DamageKind.Attack, CombatDamageVfxKind.Combat);
			a.TakeDamage(b.Attack, b);
		}

		GD.Print($"[CombatManager]   战斗后 — {a.CardName}：{a.CurrentHealth}血，" +
				  $"{b.CardName}：{b.CurrentHealth}血");
	}

	/// <summary>
	/// 处理随从成为攻击目标时的触发效果。
	/// 「诱饵战术」始终降低攻击方敌人的防御力。
	/// </summary>
	/// <param name="target">被攻击的随从</param>
	/// <param name="attackerHero">攻击方英雄（AI 大脑/意图攻击时传入），为 null 时回退到首个存活敌人</param>
	internal void TriggerBaitTacticsOnAttacked(Minion target, Hero? attackerHero = null)
	{
		if (!target.HasBaitTacticsOnAttacked)
			return;

		// 优先降低具体攻击方英雄的防御力；否则回退到首个存活敌人（如随从互殴场景）
		if (attackerHero != null && attackerHero.IsPlayerSide != target.IsPlayerSide)
		{
			attackerHero.ModifyDefense(-1);
			GD.Print($"[CombatManager] [Trigger] 诱饵战术触发：{target.CardName} 受到敌方英雄攻击，该英雄防御力-1（当前 {attackerHero.Defense}）");
		}
		else
		{
			var enemyBody = GetOpponentHeroes(target.IsPlayerSide).FirstOrDefault();
			if (enemyBody == null)
				return;
			enemyBody.ModifyDefense(-1);
			GD.Print($"[CombatManager] [Trigger] 诱饵战术触发：{target.CardName} 受到攻击，敌方英雄防御力-1（当前 {enemyBody.Defense}）");
		}
	}

	/// <summary>
	/// 玩家随从攻击敌方随从。
	/// 通过统一战斗序列 <see cref="ResolveMinionCombat"/> 处理伤害交互，
	/// 自动支持伏击、冲击、嘲讽检测和风怒多次攻击。
	/// </summary>
	/// <param name="attacker">攻击方（必须是玩家随从）</param>
	/// <param name="defender">防御方（敌方随从）</param>
	/// <returns>攻击成功返回 true</returns>
	public bool MinionAttack(Minion attacker, Minion defender)
	{
		return ExecutePlayerMinionAttack(attacker, defender);
	}

	/// <summary>
	/// 玩家随从攻击敌方英雄。
	/// 通过三线目标规则与行动事务校验，突击可自动推进后攻击。
	/// </summary>
	/// <param name="attacker">攻击方（必须是玩家随从）</param>
	/// <param name="hero">目标英雄</param>
	/// <returns>攻击成功返回 true</returns>
	public bool MinionAttackHero(Minion attacker, Hero hero)
	{
		return EnemyUnits.Any(unit => ReferenceEquals(unit.Body, hero)) && ExecutePlayerMinionAttack(attacker, hero);
	}

	private bool ExecutePlayerMinionAttack(Minion attacker, IDamageTarget target)
	{
		if (attacker == null || target == null || !CanMinionAttackTarget(attacker, target))
			return false;
		bool success = _playerActions.Attack(attacker, target, () =>
		{
			BeginLogAction(CombatLogEntryKind.Attack, PlayerHero, attacker.ToRuntimeCard(), target, attacker);
			try
			{
				if (target is Minion defender)
					ResolveMinionCombat(attacker, defender);
				else
					AttackCombatResolver.Resolve(attacker, target, attacker.Attack,
						() => RequestDamageVfx(attacker, target, DamageKind.Attack, CombatDamageVfxKind.Attack));
			}
			finally
			{
				CommitLogAction();
			}
		}, () => TriggerFirstAttackPassive(attacker, PlayerHero));
		CheckDeaths();
		NotifyCombatStateChanged();
		return success;
	}

	private static void TriggerFirstAttackPassive(Minion attacker, Hero owner)
	{
		if (!attacker.HasSmokeGrantOnFirstAttack || attacker._firstAttackDone)
			return;
		attacker._firstAttackDone = true;
		owner.AddStatusEffect(new StatusEffect(StatusEffect.SmokescreenId, 1,
			owner.IsPlayerSide ? TickTiming.PlayerTurnEnd : TickTiming.EnemyTurnEnd, StatusEffectPolarity.NonNegative));
	}

	// ===== 武器攻击 =====

	/// <summary>
	/// 玩家英雄使用武器攻击敌方英雄。
	/// 对敌方英雄造成武器攻击力伤害，同时受到敌方武器反击伤害。
	/// </summary>
	/// <returns>攻击成功返回 true</returns>
	public bool HeroWeaponAttackHero(Hero target)
	{
		return _weaponAttack.HeroWeaponAttackHero(target);
	}

	/// <summary>
	/// 玩家英雄使用武器攻击敌方随从。
	/// 对敌方随从造成武器攻击力伤害，同时受到随从攻击力反击（互砍）。
	/// 武器反击在此流程中被抑制，避免无限循环。
	/// </summary>
	/// <param name="target">目标敌方随从</param>
	/// <returns>攻击成功返回 true</returns>
	public bool HeroWeaponAttackMinion(Minion target)
	{
		return _weaponAttack.HeroWeaponAttackMinion(target);
	}

	/// <summary>
	/// 执行武器主动技能。
	/// 由 CombatUI 的技能按钮触发。
	/// </summary>
	/// <returns>执行成功返回 true</returns>
	public bool UseWeaponActiveSkill()
	{
		return _weaponAttack.UseWeaponActiveSkill();
	}

	// ===== 死亡检测与亡语处理（已委托给 DeathHandler） =====

	/// <summary>
	/// 遍历战场双方所有随从，移除已死亡随从并触发亡语效果。
	/// 委托给 <see cref="DeathHandler.CheckDeaths"/>。
	/// </summary>
	internal void CheckDeaths()
	{
		_deathHandler.CheckDeaths();
	}

	// ===== 胜负判定（已委托给 VictoryDefeatResolver） =====
	// CheckVictoryOrDefeat 和 AwardGold 逻辑已移至 VictoryDefeatResolver。
	// 胜负事件的后续处理（SavePlayerHealth/SaveRun/CleanupCombat）
	// 通过 _victoryResolver.OnGameOver 回调在 Initialize 中接线。

	/// <summary>
	/// 公开的胜负判定入口——委托给 VictoryDefeatResolver。
	/// 供 CombatUI 和 ChatScreen 调用。
	/// </summary>
	public bool CheckVictoryOrDefeat() => _victoryResolver.CheckVictoryOrDefeat();

	/// <summary>
	/// 战斗结束清理——移除所有状态牌，重置热力值等。
	/// </summary>
	private void CleanupCombat()
	{
		CancelEnemyTurnStepping();
		// 清理状态牌——从手牌、抽牌堆、弃牌堆中移除所有 Status 类型的卡牌
		VictoryDefeatResolver.RemoveStatusCardsFromList(_playerCore.Hand);
		VictoryDefeatResolver.RemoveStatusCardsFromList(_playerCore.DrawPile);
		VictoryDefeatResolver.RemoveStatusCardsFromList(_playerCore.DiscardPile);

		GD.Print("[CombatManager] 战斗结束——状态牌已清理，热力值已重置");
	}

	// ===== 回合管理 =====

	/// <summary>
	/// 结束玩家回合。清理攻击状态 → 执行敌方 AI 回合 → 开始新玩家回合。
	/// </summary>
	public void EndPlayerTurn()
	{
		if (!CanEndPlayerTurn())
			return;
		if (!RunPlayerTurnEndPhase())
			return;

		// 切换到敌方回合；同步入口保持零等待，供单测与运行时 QA 使用。
		State.EndPlayerTurn();
		ExecuteEnemyTurn();
		CompletePlayerRoundAfterEnemyTurn();
	}

	private bool CanEndPlayerTurn()
	{
		if (IsDiscovering)
		{
			GD.PrintErr("[CombatManager] EndPlayerTurn 失败 — 正在发现选牌阶段");
			return false;
		}
		if (!State.IsPlayerTurn)
		{
			GD.PrintErr("[CombatManager] EndPlayerTurn 失败 — 当前不是玩家回合");
			return false;
		}
		return true;
	}

	/// <summary>玩家回合末规则；同步/异步入口共用，不在 UI 复制结算。</summary>
	private bool RunPlayerTurnEndPhase()
	{
		GD.Print("[CombatManager] ========== 玩家回合结束 ==========");
		_emoteSystem?.StopIdleTimer();
		_attackTracker.Reset();
		_domainTriggerManager.OnPlayerTurnEnd();
		TriggerPlayerTurnEndMinionEffects();
		CheckDeaths();
		if (_victoryResolver.CheckVictoryOrDefeat())
			return false;

		_relicManager.TriggerTurnEnd(this);
		PlayerHero.TickStatusEffects(TickTiming.PlayerTurnEnd);
		PlayerHero.ResetDamageTakenThisTurn();
		foreach (var unit in EnemyUnits)
			unit.Body.TickStatusEffects(TickTiming.PlayerTurnEnd);
		foreach (var unit in EnemyUnits)
			unit.Body.ResetDamageTakenThisTurn();
		foreach (var minion in Board.GetPlayerMinions())
			minion.TickStatusEffects(TickTiming.PlayerTurnEnd);
		foreach (var minion in Board.GetEnemyMinions())
			minion.TickStatusEffects(TickTiming.PlayerTurnEnd);
		CheckDeaths();
		return !_victoryResolver.CheckVictoryOrDefeat();
	}

	private void CompletePlayerRoundAfterEnemyTurn()
	{
		if (State.IsGameOver)
			return;
		State.EndEnemyTurn();
		StartPlayerTurn();
		_victoryResolver.CheckVictoryOrDefeat();
	}


	private void TriggerPlayerTurnEndMinionEffects(bool isPlayerSide = true)
	{
		foreach (var minion in Board.GetAllMinions().Where(m => m.IsPlayerSide == isPlayerSide).ToList())
		{
			if (minion.IsDead)
				continue;

			if (minion.Id == "minion_centurion")
				TriggerCenturionTurnEndBlock(minion.OwnerHero ?? GetOwnerHero(isPlayerSide));

			if (minion.Id == "minion_lianshu_transport")
				HandleLianshuTransportTurnEnd(minion);
		}
		TriggerRoachCopies(isPlayerSide);
	}

	private void TriggerRoachCopies(bool isPlayerSide)
	{
		foreach (var minion in Board.GetAllMinions().Where(m => m.IsPlayerSide == isPlayerSide && !m.IsDead && m.Id == "minion_roach").ToList())
		{
			int slot = Board.GetEmptySlotIndex(isPlayerSide);
			if (slot < 0)
				break;
			var clone = new Minion(minion.Data, isPlayerSide) { OwnerHero = minion.OwnerHero };
			if (!isPlayerSide && clone.OwnerHero == null)
				clone.IntentBrain = new MechanicalRoachBrain(clone);
			Board.PlaceMinion(clone, slot);
		}
	}

	/// <summary>
	/// 随从是否由同构牌手的评分贪心控制器指挥；与 UI/单测共用同一判定。
	/// 指挥官随从不能再走意图脑，否则会在指挥官回合造成二次行动。
	/// </summary>
	public bool IsCommanderMinion(Minion minion)
		=> EnemyIntentVisibility.IsCommanderMinion(minion, EnemyUnits);

	private void TriggerCenturionTurnEndBlock(Hero owner)
	{
		const int turnEndBlock = 1;
		const int heroMultiplier = 2;
		foreach (var ally in Board.GetAllMinions().Where(m => !m.IsDead && m.IsPlayerSide == owner.IsPlayerSide))
			ally.GainArmor(turnEndBlock);
		owner.GainArmor(turnEndBlock * heroMultiplier);
		GD.Print($"[CombatManager] 百机长回合结束：友方随从获得 {turnEndBlock} 点格挡，英雄获得 {turnEndBlock * heroMultiplier} 点格挡");
	}

	private void HandleLianshuTransportTurnEnd(Minion minion)
	{
		var card = minion.ToRuntimeCard();
		if (minion.EndTurnDrawPileReturnsRemaining > 0)
		{
			card.EndTurnDrawPileReturnsRemaining = minion.EndTurnDrawPileReturnsRemaining - 1;
			if (Board.RecallMinion(minion))
			{
				_attackTracker.Remove(minion);
				(minion.OwnerHero ?? GetOwnerHero(minion.IsPlayerSide)).AddToDrawPileBottom(card);
				GD.Print($"[CombatManager] 联树机器犬运输型回合结束：返回抽牌堆，剩余返回次数 {card.EndTurnDrawPileReturnsRemaining}");
			}
			return;
		}

		if (Board.RecallMinion(minion))
		{
			_attackTracker.Remove(minion);
			(minion.OwnerHero ?? GetOwnerHero(minion.IsPlayerSide)).AddToDiscardPile(card);
			GD.Print("[CombatManager] 联树机器犬运输型回合结束：返回次数耗尽，进入弃牌堆");
		}
	}

	private void TriggerDirectDamageSpellMinionEffects(Card.Card spell, Hero owner)
	{
		foreach (var minion in Board.GetAllMinions().Where(m => m.IsPlayerSide == owner.IsPlayerSide).ToList())
		{
			if (minion.Id != "minion_mingshan_walking_support" || minion.IsDead)
				continue;

			var targets = GetOpponentTargets(owner.IsPlayerSide);
			var target = targets.Count == 0 ? null : targets[Random.Shared.Next(targets.Count)];
			if (target == null)
				return;

			BeginLogAction(CombatLogEntryKind.EffectTriggered, owner, minion.ToRuntimeCard(), target, minion);
			try
			{
				RequestDamageVfx(minion, target, DamageKind.Effect, CombatDamageVfxKind.Spell);
				target.TakeDamage(minion.Attack, minion, DamageKind.Effect);
			}
			finally
			{
				CommitLogAction();
			}
			GD.Print($"[CombatManager] {minion.CardName} 响应直伤法术「{spell.CardName}」：造成 {minion.Attack} 点法术伤害");
		}
	}

	private IDamageTarget? SelectRandomEnemyTarget()
	{
		var candidates = new List<IDamageTarget>();
		foreach (var enemy in EnemyUnits)
		{
			if (!enemy.Body.IsDead)
				candidates.Add(enemy.Body);
		}
		candidates.AddRange(Board.GetEnemyMinions());
		if (candidates.Count == 0)
			return null;

		using var rng = new RandomNumberGenerator();
		rng.Randomize();
		return candidates[rng.RandiRange(0, candidates.Count - 1)];
	}

	/// <summary>
	/// 尝试使用英雄技能。
	/// 检查是否玩家回合、未使用过、不在发现阶段、英雄技能可用。
	/// </summary>
	/// <returns>成功使用时返回 true</returns>
	public bool TryUseHeroPower()
	{
		return TryUseHeroPower(PlayerHero);
	}

	/// <summary>
	/// 本回合是否已使用过英雄技能。供 CombatUI 刷新按钮状态。
	/// </summary>
	public bool HeroPowerUsedThisTurn => _heroPowerUsedThisTurn;

	/// <summary>
	/// ChatScreen 强制胜利——瞬间击杀所有敌方单位，触发正常胜利流程。
	/// 可通过 grantReward 跳过金币奖励。
	/// </summary>
	public void ForceVictory(bool grantReward = true)
	{
		if (State.IsGameOver)
			return;
		_victoryResolver.DevSkipGoldReward = !grantReward;

		// 击杀所有敌方随从 —— 绕过伤害管线，直接用 ChatScreen 直伤确保击杀
		foreach (var minion in Board.GetEnemyMinions())
		{
			if (!minion.IsDead)
				minion.ApplyDevDamage(minion.CurrentHealth);
		}

		// 击杀所有敌方英雄 —— 绕过伤害管线（CAPPING 会截断大额伤害）
		foreach (var unit in EnemyUnits)
		{
			if (!unit.Body.IsDead)
				unit.Body.ApplyDevDamage(unit.Body.CurrentHealth);
		}

		// 触发胜负判定（含奖励/保存/OnGameOver）
		_victoryResolver.CheckVictoryOrDefeat();
		GD.Print($"[CombatManager] ChatScreen 强制胜利（{(grantReward ? "含" : "跳过")}奖励）");
	}

	/// <summary>
	/// 执行敌方 AI 回合：依次执行每个敌人的意图 → 推进意图轮转 → 敌方随从攻击 → 死亡检查 → 胜负判定。
	/// 执行期间冻结意图 UI 刷新（_isEnemyTurnAnimating），防止动画中数值跳变。
	/// </summary>
	private void ExecuteEnemyTurn()
	{
		if (!BeginEnemyTurnPhase())
			return;
		foreach (var unit in EnemyUnits)
		{
			if (!ExecuteEnemyActorTurn(unit))
				return;
		}
		EnemyMinionsAttack();
		RunEnemyTurnEndPhase();
	}

	/// <summary>敌方回合头：快照可行动随从、状态结算与回合日志。</summary>
	private bool BeginEnemyTurnPhase()
	{
		_isEnemyTurnAnimating = true;
		CombatLog.RecordTurnStart(State.TurnCount, isPlayerSide: false,
			Localization.Localization.T("ui.combat.enemy_turn", "敌人回合"));
		GD.Print($"[CombatManager] ---------- 敌方回合开始（{EnemyUnits.Count} 个敌人）----------");
		_domainTriggerManager.OnEnemyTurnStart();
		_chansuTriggeredThisEnemyTurn = false;
		_enemyMinionsCanAttack.Clear();
		foreach (var minion in Board.GetEnemyMinions())
			_enemyMinionsCanAttack.Add(minion);
		TickTurnStartStatusEffects(TickTiming.EnemyTurnStart);
		CheckDeaths();
		if (_victoryResolver.CheckVictoryOrDefeat())
		{
			_isEnemyTurnAnimating = false;
			return false;
		}
		return true;
	}

	/// <summary>同步执行一个敌方 actor；异步 UI 仅对指挥官拆分此阶段，旧意图怪共用本方法。</summary>
	private bool ExecuteEnemyActorTurn(EnemyUnit unit)
	{
		if (unit.Body.IsDead)
			return true;
		if (unit.Controller is IDeckDrivenCommander commander)
		{
			PrepareCommanderTurn(unit.Body);
			commander.ExecuteTurn(this, unit);
			GD.Print($"[CombatManager] {unit.Brain.Name} 指挥官回合结束（法力 {unit.Body.CurrentMana}/{unit.Body.MaxMana}，手牌 {unit.Body.Hand.Count}）");
		}
		else
		{
			unit.Brain.Attack = !unit.Body.IsIncapacitated && unit.Body.Weapon is { IsDisabled: false } ? unit.Body.Weapon.Attack : 0;
			unit.Controller.ExecuteTurn(this, unit);
			GD.Print($"[CombatManager] {unit.Brain.Name} 下回合意图：{unit.Brain.GetCurrentIntent(this, unit.Body).Description}");
		}
		CheckDeaths();
		if (_victoryResolver.CheckVictoryOrDefeat())
		{
			_isEnemyTurnAnimating = false;
			return false;
		}
		return true;
	}

	/// <summary>敌方回合末规则；调用前所有 actor 与随从行动已完成。</summary>
	private bool RunEnemyTurnEndPhase()
	{
		CheckDeaths();
		if (_victoryResolver.CheckVictoryOrDefeat())
		{
			_isEnemyTurnAnimating = false;
			return false;
		}
		_isEnemyTurnAnimating = false;
		PlayerHero.TickStatusEffects(TickTiming.EnemyTurnEnd);
		foreach (var unit in EnemyUnits)
			unit.Body.TickStatusEffects(TickTiming.EnemyTurnEnd);
		foreach (var minion in Board.GetPlayerMinions())
			minion.TickStatusEffects(TickTiming.EnemyTurnEnd);
		foreach (var minion in Board.GetEnemyMinions())
			minion.TickStatusEffects(TickTiming.EnemyTurnEnd);
		TriggerPlayerTurnEndMinionEffects(isPlayerSide: false);
		_heatSystem.OnEnemyTurnEnd();
		_relicManager.TriggerEnemyTurnEnd(this);
		_domainTriggerManager.OnEnemyTurnEnd();
		NotifyCombatStateChanged();
		return true;
	}


	private void TickTurnStartStatusEffects(TickTiming timing)
	{
		ApplyTurnStartStatusDamage(PlayerHero, timing);
		foreach (var unit in EnemyUnits)
			ApplyTurnStartStatusDamage(unit.Body, timing);

		foreach (var minion in Board.GetPlayerMinions())
			ApplyTurnStartStatusDamage(minion, timing);
		foreach (var minion in Board.GetEnemyMinions())
			ApplyTurnStartStatusDamage(minion, timing);

		PlayerHero.TickStatusEffects(timing);
		foreach (var unit in EnemyUnits)
			unit.Body.TickStatusEffects(timing);

		foreach (var minion in Board.GetPlayerMinions())
			minion.TickStatusEffects(timing);
		foreach (var minion in Board.GetEnemyMinions())
			minion.TickStatusEffects(timing);
	}

	private static void ApplyTurnStartStatusDamage(Hero hero, TickTiming timing)
	{
		if (hero.StatusEffects.TryGetValue("damage_over_time", out var effect)
			&& effect.TickOn == timing
			&& effect.Stacks > 0)
		{
			hero.ApplyStatusDamage(1, effect.Id);
		}
	}

	private static void ApplyTurnStartStatusDamage(Minion minion, TickTiming timing)
	{
		if (minion.StatusEffects.TryGetValue("damage_over_time", out var effect)
			&& effect.TickOn == timing
			&& effect.Stacks > 0)
		{
			minion.ApplyStatusDamage(1, effect.Id);
		}
	}

	/// <summary>
	/// 敌方所有随从依次攻击：优先走 MoveState.OnPerform 统一路径，无 MoveState 时回退 ExecuteIntent。
	/// </summary>
	private void EnemyMinionsAttack()
	{
		foreach (var attacker in GetLegacyEnemyMinionsForTurn())
			ExecuteLegacyEnemyMinionAction(attacker);
	}

	private List<Minion> GetLegacyEnemyMinionsForTurn()
	{
		return Board.GetEnemyMinions()
			.Where(m => !m.IsDead && !m.IsIncapacitated && (_enemyMinionsCanAttack.Contains(m) || m.HasCharge))
			.Where(m => !IsCommanderMinion(m))
			.ToList();
	}

	/// <summary>执行一只旧意图随从的移动/攻击；供同步与 UI 逐只步进共用。</summary>
	private bool ExecuteLegacyEnemyMinionAction(Minion attacker)
	{
		if (State.IsGameOver || attacker.IsDead || attacker.IsIncapacitated)
			return false;
		if (attacker.UnitClass is UnitClass.Vanguard or UnitClass.Assault
			&& Board.MoveMinionToFrontline(attacker) && attacker.UnitClass != UnitClass.Assault)
			return true;

		attacker.IntentBrain ??= new DefaultAttackMinionBrain(attacker);
		if (attacker.IntentBrain != null)
		{
			var move = attacker.IntentBrain.GetCurrentMove(this);
			if (move?.OnPerform != null)
				move.OnPerform(this, null);
			else
				attacker.IntentBrain.ExecuteIntent(this);
			attacker.IntentBrain.AdvanceMove();
			if (attacker.UnitClass == UnitClass.Assault && attacker.HasWindfury && !attacker.IsDead && !State.IsGameOver)
				ExecuteEnemyMinionSmartAttack(attacker);
			return true;
		}
		return ExecuteEnemyMinionSmartAttack(attacker);
	}


	/// <summary>
	/// 重置本回合攻击追踪：清空计数器并将所有玩家随从设为可攻击。
	/// </summary>
	private void ResetAttackTracking()
	{
		_attackTracker.Reset();

		foreach (var minion in Board.GetPlayerMinions())
		{
			// 上回合已存在（非新召唤）的随从可以攻击
			_attackTracker.AddCharged(minion);
		}
	}

	/// <summary>
	/// 将一张卡牌直接加入玩家手牌（用于 ChatScreen /token 命令）。
	/// </summary>
	public void AddCardToHand(OdysseyCards.Card.Card card)
	{
		_playerCore.AddToHand(card);
	}

	// ===== 发现选牌系统 =====

	/// <summary>
	/// 处理发现效果——委托给 <see cref="SelectionSystem"/>。
	/// </summary>
	public void ConfirmDiscoverChoice(CardData? chosen)
	{
		_selectionSystem.ConfirmDiscoverChoice(chosen);
	}

	/// <summary>
	/// 取消发现选牌（等同跳过）。
	/// </summary>
	public void CancelDiscover()
	{
		_selectionSystem.CancelDiscover();
	}

	/// <summary>
	/// 确认运行时卡牌选择结果——委托给 <see cref="SelectionSystem"/>。
	/// </summary>
	public void ConfirmDiscoverCards(IReadOnlyList<Card.Card> chosenCards)
	{
		_selectionSystem.ConfirmDiscoverCards(chosenCards);
	}

	// ===== 手牌选择系统（STS2 风格） =====

	/// <summary>
	/// 确认手牌选择——委托给 <see cref="SelectionSystem"/>。
	/// </summary>
	public void ConfirmHandDiscardSelection(IReadOnlyList<Card.Card> selectedCards)
	{
		_selectionSystem.ConfirmHandDiscardSelection(selectedCards);
	}

	/// <summary>
	/// 开始一次强制手牌弃牌选择。用于技能已支付后必须弃牌的效果。
	/// </summary>
	public void BeginForcedHandDiscardSelection(
		List<Card.Card> handOptions,
		int count,
		PendingSelectionMode mode,
		Action<IReadOnlyList<Card.Card>> onConfirmed)
	{
		_selectionSystem.BeginCustomHandDiscardSelection(handOptions, count, count, mode, canCancel: false, onConfirmed);
	}

	/// <summary>按英雄阵营分派强制弃牌：玩家走异步 UI，指挥官走同步 AI 决策。</summary>
	internal void BeginForcedHandDiscardSelection(
		Hero owner,
		List<Card.Card> handOptions,
		int count,
		PendingSelectionMode mode,
		Action<IReadOnlyList<Card.Card>> onConfirmed)
	{
		if (owner.IsPlayerSide)
		{
			_selectionSystem.BeginCustomHandDiscardSelection(handOptions, count, count, mode, canCancel: false, onConfirmed);
			return;
		}
		var kind = mode == PendingSelectionMode.RestructureDiscard
			? CardDecisionKind.RestructureDiscard : CardDecisionKind.Discard;
		ResolveDecisionProvider(owner).ChooseCards(
			new CardDecisionRequest(owner, kind, handOptions, count, count), onConfirmed);
	}

	/// <summary>
	/// 开发者命令用手牌弃牌选择。允许取消，可选择任意范围张数。
	/// </summary>
	public void BeginDevHandDiscardSelection(
		List<Card.Card> handOptions,
		int min,
		int max,
		Action<IReadOnlyList<Card.Card>> onConfirmed)
	{
		_selectionSystem.BeginCustomHandDiscardSelection(handOptions, min, max, PendingSelectionMode.DevDiscard, canCancel: true, onConfirmed);
	}

	/// <summary>
	/// 取消手牌选择——委托给 <see cref="SelectionSystem"/>。
	/// </summary>
	public void CancelHandDiscardSelection()
	{
		_selectionSystem.CancelHandDiscardSelection();
	}

	/// <summary>
	/// 机械蜈蚣-防空型自动拦截触发器。
	/// 订阅 <see cref="Board.OnMinionPlaced"/> 事件，当敌方部署费用 ≤2 的随从时，
	/// 玩家方的每只机械蜈蚣-防空型自动与该随从战斗。
	/// 战斗后清理死亡随从并触发死亡检查。
	/// </summary>
	/// <param name="placedMinion">刚被部署的随从</param>
	/// <param name="slotIndex">部署槽位</param>
	private void OnMinionPlacedForCentipede(Minion placedMinion, BoardPosition position)
	{
		// 仅拦截费用 ≤2 的随从；这是卡牌专属阈值，不跟随全局低费分档。
		if (placedMinion.Cost > 2)
			return;

		// 收集玩家方所有存活的机械蜈蚣-防空型（战斗前快照，防止迭代中修改集合）
		var centipedes = Board.GetAllMinions().Where(m => !m.IsDead && m.IsPlayerSide != placedMinion.IsPlayerSide && m.Id == "minion_centipede_aa").ToList();

		if (centipedes.Count == 0)
			return;

		foreach (var centipede in centipedes)
		{
			if (centipede.IsDead)
				continue;
			if (placedMinion.IsDead)
				break;

			GD.Print($"[CombatManager] [Intercept] 机械蜈蚣-防空型拦截 {placedMinion.CardName}（{placedMinion.Cost}费）！");
			BeginLogAction(CombatLogEntryKind.EffectTriggered,
				centipede.OwnerHero ?? GetOwnerHero(centipede.IsPlayerSide), centipede.ToRuntimeCard(), placedMinion, centipede);
			try
			{
				ResolveCombat(centipede, placedMinion);
			}
			finally
			{
				CommitLogAction();
			}
		}

		// 清理战斗产生的死亡随从
		if (placedMinion.IsDead)
			Board.RemoveMinion(placedMinion);

		var deadPlayerMinions = Board.GetAllMinions().Where(m => m.IsDead).ToList();
		foreach (var m in deadPlayerMinions)
			Board.RemoveMinion(m);

		// 全局清理：蜈蚣战斗可能连锁触发其他死亡
		CheckDeaths();
		_victoryResolver.CheckVictoryOrDefeat();
	}

	/// <summary>
	/// 验证目标是否满足卡牌的目标过滤条件。
	/// 使用子集匹配规则：card.TargetFilter 必须是 entity.GetTargetTags() 的子集。
	/// TargetFilter 为 None 时放行所有目标。
	/// </summary>
	/// <param name="card">要打出的卡牌</param>
	/// <param name="target">目标实体（Minion 或 Hero）</param>
	/// <returns>目标合法返回 true</returns>
	public bool ValidateTarget(OdysseyCards.Card.Card card, object target)
	{
		return ValidateTarget(CreateActorContext(PlayerHero, PlayerHero, card), card, target);
	}

	/// <summary>
	/// 奇巧关键词回调——卡牌被弃掉时自动打出。
	/// 参考 STS2 的 Sly 机制：CardCmd 收集 Sly 卡牌 → AutoPlay(SlyDiscard)。
	/// 返回 true 表示卡牌已处理（不进入弃牌堆），false 表示正常弃牌。
	/// </summary>
	/// <summary>
	/// 奇巧关键词回调——卡牌被弃掉时自动打出。由牌堆所属指挥官决定阵营与资源。
	/// </summary>
	private bool HandleQiqiaoDiscard(OdysseyCards.Card.Card card)
	{
		return HandleQiqiaoDiscard(PlayerHero, card);
	}

	private bool HandleQiqiaoDiscard(Hero owner, OdysseyCards.Card.Card card)
	{
		if (!card.HasQiqiao)
			return false;

		GD.Print($"[CombatManager] [Qiqiao] 奇巧触发：{(owner.IsPlayerSide ? "玩家" : "敌方")} 自动打出「{card.GetLocalizedName()}」");
		BeginLogAction(CombatLogEntryKind.EffectTriggered, owner, card, primarySource: owner);
		try
		{
			owner.RemoveFromHand(card);
			switch (card.Type)
			{
				case CardType.Minion:
					AutoSummonQiqiaoMinion(owner, card);
					break;
				case CardType.Domain:
					AutoPlayQiqiaoDomain(owner, card);
					break;
				default:
					AutoPlayQiqiaoSpell(owner, card);
					break;
			}
		}
		finally
		{
			CommitLogAction();
		}
		return true;
	}

	private void AutoSummonQiqiaoMinion(Hero owner, OdysseyCards.Card.Card card)
	{
		if (!Board.CanPlaceMinion(owner.IsPlayerSide))
		{
			GD.Print($"[CombatManager] 奇巧召唤失败——{(owner.IsPlayerSide ? "玩家" : "敌方")}支援线已满，「{card.CardName}」消失");
			return;
		}
		var minion = new Minion(card, owner.IsPlayerSide) { OwnerHero = owner };
		Board.PlaceMinion(minion, Board.GetEmptySlotIndex(owner.IsPlayerSide));
	}

	private void AutoPlayQiqiaoDomain(Hero owner, OdysseyCards.Card.Card card)
	{
		var actor = CreateActorContext(owner, owner, card);
		string domainId = string.IsNullOrEmpty(card.Data.DomainId) ? card.Data.Id : card.Data.DomainId;
		foreach (var effect in card.Data.Effects)
		{
			_effectDispatcher.ExecuteEffect(effect, null, actor);
			owner.AddDomain(domainId, effect);
		}
		_domainTriggerManager.OnDomainDeployed(owner, domainId);
	}

	private void AutoPlayQiqiaoSpell(Hero owner, OdysseyCards.Card.Card card)
	{
		var actor = CreateActorContext(owner, owner, card);
		object? target = GetOpponentTargets(owner.IsPlayerSide).FirstOrDefault();
		foreach (var effect in card.Data.Effects)
			_effectDispatcher.ExecuteEffect(effect, target, actor);
		CheckDeaths();
	}

	private void HandleExplainReturnToHand(Hero owner, OdysseyCards.Card.Card card)
	{
		owner.RemoveFromHand(card);
		if (owner.Hand.Count >= owner.DeckState.MaxHandSize)
		{
			owner.DiscardCard(card);
			return;
		}
		owner.DeckState.AddToHand(card);
	}

	// ===== M3 施法者相对核心 =====

	internal Hero GetOwnerHero(bool isPlayerSide)
	{
		if (isPlayerSide)
			return PlayerHero;
		return EnemyUnits.FirstOrDefault(unit => !unit.Body.IsDead)?.Body ?? EnemyUnits[0].Body;
	}

	/// <summary>部署归属：玩家随从归玩家；只有主题卡组指挥官才把敌方随从纳入自身牌堆流转。</summary>
	private Hero? ResolveDeploymentOwner(bool isPlayerSide)
	{
		if (isPlayerSide)
			return PlayerHero;
		return EnemyUnits.FirstOrDefault(unit => !unit.Body.IsDead && unit.Controller is IDeckDrivenCommander)?.Body;
	}

	internal List<Hero> GetOpponentHeroes(bool fromPlayerSide)
	{
		if (fromPlayerSide)
			return EnemyUnits.Where(unit => !unit.Body.IsDead).Select(unit => unit.Body).ToList();
		return PlayerHero.IsDead ? new List<Hero>() : new List<Hero> { PlayerHero };
	}

	internal List<IDamageTarget> GetOpponentTargets(bool fromPlayerSide)
		=> GetOpponentHeroes(fromPlayerSide).Cast<IDamageTarget>()
			.Concat(fromPlayerSide ? Board.GetEnemyMinions() : Board.GetPlayerMinions())
			.ToList();

	internal List<IDamageTarget> GetFriendlyTargets(bool fromPlayerSide)
		=> (fromPlayerSide ? new List<Hero> { PlayerHero } : EnemyUnits.Where(u => !u.Body.IsDead).Select(u => u.Body).ToList())
			.Cast<IDamageTarget>()
			.Concat(fromPlayerSide ? Board.GetPlayerMinions() : Board.GetEnemyMinions())
			.Where(target => target is not Hero hero || !hero.IsDead)
			.ToList();

	private IDecisionProvider ResolveDecisionProvider(Hero caster)
	{
		if (caster.IsPlayerSide)
			return _selectionSystem;
		var commander = EnemyUnits.FirstOrDefault(unit => ReferenceEquals(unit.Body, caster))?.Controller as IDeckDrivenCommander;
		return commander?.DecisionProvider ?? new AiDecisionProvider();
	}

	internal EffectActorContext CreateActorContext(Hero caster, IDamageSource source, Card.Card? card = null, bool applyPreemptive = false)
	{
		var actor = new EffectActorContext(caster, source, card, ResolveDecisionProvider(caster));
		if (applyPreemptive && card?.Type == CardType.Spell
			&& card.Data?.HasMechanicTag(CardMechanicTag.DirectDamage) == true)
			actor.SpellDamageMultiplier = _domainTriggerManager.ConsumePreemptiveStrikeMultiplier(caster);
		return actor;
	}

	internal CardData? LoadCombatCard(string idOrPath)
	{
		if (string.IsNullOrWhiteSpace(idOrPath))
			return null;
		if (idOrPath.StartsWith("res://", StringComparison.Ordinal))
			return GD.Load<CardData>(idOrPath);
		return GameManager.Instance?.GetCardById(idOrPath);
	}

	private int GetPlayCost(EffectActorContext actor, Card.Card card)
		=> actor.IsPlayerSide ? ApplyRelicCostModifiers(card) : card.GetEffectiveCost();

	private bool IsActorTurn(EffectActorContext actor)
		=> actor.IsPlayerSide ? State.IsPlayerTurn : State.IsEnemyTurn;

	internal bool PlayMinion(EffectActorContext actor, Card.Card card, int slotIndex)
	{
		if (IsDiscovering || !IsActorTurn(actor) || card == null || card.Type != CardType.Minion)
			return false;
		if (!actor.OwnerCore.Hand.Contains(card) || card.HasUnplayable)
			return false;
		int cost = GetPlayCost(actor, card);
		if (!actor.CasterHero.CanSpendMana(cost))
			return false;
		bool side = actor.IsPlayerSide;
		if (slotIndex < 0 || slotIndex >= Board.SupportCapacity || Board.GetMinionAt(slotIndex, side) != null || !Board.CanPlaceMinion(side))
			return false;
		if (!actor.OwnerCore.TrySpendMana(cost))
			return false;
		if (side)
			NotifyCardPlayed(card, cost);

		var minion = new Minion(card, side) { OwnerHero = actor.CasterHero };
		BeginLogAction(CombatLogEntryKind.DeployUnit, actor.CasterHero, card, primarySource: minion);
		try
		{
			Board.PlaceMinion(minion, slotIndex);
			if (minion.HasBattlecry)
				foreach (var effect in minion.BattlecryEffects)
					ResolveBattlecryEffect(effect, minion);
			if (minion.HasBattlecry && minion.BattlecryEffects.Any(effect => effect.CustomEffectName == "CopyToAdjacentSlot"))
				CopyMinionToAdjacentSlot(minion);
			if (minion.HasCharge)
				_attackTracker.AddCharged(minion);
			actor.CasterHero.RemoveFromHand(card);
		}
		finally
		{
			CommitLogAction();
		}
		NotifyCombatStateChanged();
		return true;
	}

	internal bool PlaySpell(EffectActorContext actor, Card.Card card, object? target)
	{
		if (IsDiscovering || !IsActorTurn(actor) || card == null || card.Type is not (CardType.Spell or CardType.Status))
			return false;
		if (!actor.OwnerCore.Hand.Contains(card) || card.HasUnplayable)
			return false;
		int cost = GetPlayCost(actor, card);
		if (!actor.CasterHero.CanSpendMana(cost) || !ValidateTarget(actor, card, target))
			return false;

		// 敌方回合（或玩家法术被敌方反制牌拦截）时，先让防守方已激活的反制牌判定。
		// 被「俄亥俄级」取消的法术：不结算效果、不进弃牌堆，只消耗法力并从手中移除。
		var defender = ResolveCountermeasureDefender(actor.CasterHero);
		var interception = _countermeasures?.Intercept(actor.CasterHero, defender, card) ?? default;
		foreach (var counter in interception.TriggeredCards ?? Array.Empty<Card.Card>())
		{
			RecordCountermeasureTriggered(defender, counter, card,
				interception.Canceled && counter.Id == "counter_ohio_ssbn", counter.Id == "counter_borei");
		}
		if (interception.Canceled)
		{
			BeginLogAction(CombatLogEntryKind.PlayOrder, actor.CasterHero, card, target, actor.ActualDamageSource);
			CombatLog.RecordCanceled();
			// 亮牌由反制牌本身的 Present（带“已被反制”标签）承担，这里不再重复展示同一张法术，避免两卡叠在同一展示位。
			actor.OwnerCore.TrySpendMana(cost);
			actor.CasterHero.RemoveFromHand(card);
			if (actor.IsPlayerSide)
				NotifyCardPlayed(card, cost);
			CommitLogAction();
			GD.Print($"[CombatManager] 「{card.CardName}」被反制拦截，效果未结算");
			NotifyCombatStateChanged();
			return true;
		}

		if (!actor.OwnerCore.TrySpendMana(cost))
			return false;
		// 先发制人按施法者阵营消费：同一张直伤法术只放大其首个施法者当回合的第一张。
		actor = CreateActorContext(actor.CasterHero, actor.ActualDamageSource, card, applyPreemptive: true);
		if (actor.IsPlayerSide)
			NotifyCardPlayed(card, cost);
		PresentCardPlay(actor.CasterHero, card, target);
		var logScope = BeginLogAction(CombatLogEntryKind.PlayOrder, actor.CasterHero, card, target, actor.ActualDamageSource);
		try
		{
			ResolveSpellEffects(actor, card, target, 0, logScope);
		}
		catch
		{
			// 异常路径兜底提交；正常路径已由 FinishSpell 提交，按作用域引用提交不会误伤外层。
			CommitLogAction(logScope);
			throw;
		}
		return true;
	}

	/// <summary>反制判定的防守方：敌方施法→玩家英雄；玩家施法→首个存活敌方英雄。</summary>
	private Hero ResolveCountermeasureDefender(Hero caster)
	{
		if (!caster.IsPlayerSide)
			return PlayerHero;
		return EnemyUnits.FirstOrDefault(unit => !unit.Body.IsDead && ReferenceEquals(unit.Body, caster))?.Body
			?? EnemyUnits.FirstOrDefault(unit => !unit.Body.IsDead)?.Body
			?? PlayerHero;
	}

	private void ResolveSpellEffects(EffectActorContext actor, Card.Card card, object? target, int index,
		CombatLogEntry? logScope)
	{
		if (index >= card.Data.Effects.Count)
		{
			FinishSpell(actor, card, logScope);
			return;
		}
		_effectDispatcher.ExecuteEffect(card.Data.Effects[index], target, actor,
			() => ResolveSpellEffects(actor, card, target, index + 1, logScope));
	}

	private void FinishSpell(EffectActorContext actor, Card.Card card, CombatLogEntry? logScope = null)
	{
		var owner = actor.CasterHero;
		_logSilentDiscardCards.Push(card);
		try
		{
			if (card.HasRecycle)
				owner.ReturnToDrawPile(card);
			else if (card.Id == "status_explain")
				HandleExplainReturnToHand(owner, card);
			else
				owner.DiscardCard(card);
		}
		finally
		{
			// 栈顶可能因嵌套自动出牌而改变；按引用移除本次入栈的卡，恢复外层状态。
			RemoveLogSilentDiscard(card);
		}

		if (card.Type == CardType.Spell && card.Data.HasMechanicTag(CardMechanicTag.DirectDamage))
			TriggerDirectDamageSpellMinionEffects(card, owner);
		CheckDeaths();
		CommitLogAction(logScope);
		NotifyCombatStateChanged();
	}

	internal bool PlayDomain(EffectActorContext actor, Card.Card card)
	{
		if (IsDiscovering || !IsActorTurn(actor) || card == null || card.Type != CardType.Domain)
			return false;
		if (!actor.OwnerCore.Hand.Contains(card) || card.HasUnplayable)
			return false;
		int cost = GetPlayCost(actor, card);
		if (!actor.CasterHero.CanSpendMana(cost) || !actor.OwnerCore.TrySpendMana(cost))
			return false;
		if (actor.IsPlayerSide)
			NotifyCardPlayed(card, cost);

		string domainId = string.IsNullOrEmpty(card.Data.DomainId) ? card.Data.Id : card.Data.DomainId;
		BeginLogAction(CombatLogEntryKind.PlayDomain, actor.CasterHero, card, primarySource: actor.ActualDamageSource);
		PresentCardPlay(actor.CasterHero, card, null);
		try
		{
			foreach (var effect in card.Data.Effects)
			{
				_effectDispatcher.ExecuteEffect(effect, null, actor);
				actor.CasterHero.AddDomain(domainId, effect);
			}
			_domainTriggerManager.OnDomainDeployed(actor.CasterHero, domainId);
			actor.CasterHero.RemoveFromHand(card);
		}
		finally
		{
			CommitLogAction();
		}
		NotifyCombatStateChanged();
		return true;
	}

	internal bool ValidateTarget(EffectActorContext actor, Card.Card card, object? target)
	{
		if (card.Data.FrontlineOnlySpell && (target is not IDamageTarget frontlineTarget
			|| !AttackTargetRules.CanTargetFrontlineSpell(Board, actor.IsPlayerSide, frontlineTarget)))
			return false;
		var require = card.Data.TargetFilter;
		var exclude = card.Data.ExcludeFilter;
		if (require == TargetTags.None && exclude == TargetTags.None)
			return true;
		if (target is Minion { IsDead: true })
			return false;
		if (target is not IDamageTarget damageTarget)
			return false;
		return TargetTagsHelper.IsValidTarget(actor.GetRelativeTags(damageTarget), require, exclude);
	}

	// ===== 指挥官操作入口（AI 与 UI 共用同一规则层） =====

	internal bool CanCommanderPlay(EffectActorContext actor, Card.Card card)
		=> card != null && actor.OwnerCore.Hand.Contains(card) && !card.HasUnplayable
			&& actor.CasterHero.CanSpendMana(GetPlayCost(actor, card));

	internal bool CommanderMove(Hero owner, Minion minion, int slot = -1)
	{
		if (State.IsGameOver || IsDiscovering || !IsOwnerTurn(owner) || minion.IsDead)
			return false;
		var actions = new MinionActionService(Board, _attackTracker, owner);
		if (!actions.Move(minion, slot))
			return false;
		NotifyCombatStateChanged();
		return true;
	}

	internal bool CanCommanderMove(Hero owner, Minion minion, int slot = -1)
	{
		if (State.IsGameOver || IsDiscovering || !IsOwnerTurn(owner) || minion.IsDead)
			return false;
		return new MinionActionService(Board, _attackTracker, owner).CanMove(minion, slot);
	}

	private bool IsOwnerTurn(Hero owner) => owner.IsPlayerSide ? State.IsPlayerTurn : State.IsEnemyTurn;

	internal List<IDamageTarget> GetCommanderWeaponTargets(Hero owner)
		=> _weaponAttack.GetLegalTargets(owner);

	internal bool CanCommanderAttack(Hero owner, Minion minion, IDamageTarget target)
	{
		if (State.IsGameOver || IsDiscovering || !IsOwnerTurn(owner) || minion.IsDead)
			return false;
		var actions = new MinionActionService(Board, _attackTracker, owner);
		return actions.CanAttack(minion, target, out _);
	}

	internal List<IDamageTarget> GetCommanderAttackTargets(Hero owner, Minion minion)
		=> GetOpponentTargets(owner.IsPlayerSide).Where(target => CanCommanderAttack(owner, minion, target)).ToList();

	internal bool CommanderAttack(Hero owner, Minion attacker, IDamageTarget target)
	{
		if (!CanCommanderAttack(owner, attacker, target))
			return false;
		var actions = new MinionActionService(Board, _attackTracker, owner);
		bool success = actions.Attack(attacker, target, () =>
		{
			BeginLogAction(CombatLogEntryKind.Attack, owner, attacker.ToRuntimeCard(), target, attacker);
			try
			{
				if (target is Minion defender)
					ResolveMinionCombat(attacker, defender);
				else
					AttackCombatResolver.Resolve(attacker, target, attacker.Attack,
						() => RequestDamageVfx(attacker, target, DamageKind.Attack, CombatDamageVfxKind.Attack));
			}
			finally
			{
				CommitLogAction();
			}
		}, () => TriggerFirstAttackPassive(attacker, owner));
		CheckDeaths();
		NotifyCombatStateChanged();
		return success;
	}

	internal bool CommanderWeaponAttack(Hero owner, IDamageTarget target)
		=> _weaponAttack.Attack(owner, target);

	internal bool CommanderWeaponActive(Hero owner, IDamageTarget? target)
		=> _weaponAttack.UseWeaponActiveSkill(owner, target);

	internal bool CommanderUseHeroPower(Hero owner)
		=> TryUseHeroPower(owner);

	private bool TryUseHeroPower(Hero owner)
	{
		if (State.IsGameOver || IsDiscovering)
			return false;
		if (owner.IsPlayerSide ? !State.IsPlayerTurn || _heroPowerUsedThisTurn : !State.IsEnemyTurn)
			return false;
		var power = owner.HeroPower;
		if (power == null || !power.CanUse(owner))
			return false;
		BeginLogAction(CombatLogEntryKind.HeroPower, owner, primarySource: owner, actorName: power.Name);
		try
		{
			power.Execute(owner, this);
			CheckDeaths();
		}
		finally
		{
			CommitLogAction();
		}
		if (owner.IsPlayerSide)
			_heroPowerUsedThisTurn = true;
		NotifyCombatStateChanged();
		return true;
	}

	/// <summary>指挥官回合开始：与玩家对等的自然法力增长、抽牌、武器与技能冷却。</summary>
	internal void PrepareCommanderTurn(Hero owner)
	{
		// 敌方新回合开始：上一轮未触发的已激活反制牌失效。
		_countermeasures?.DisarmAll(owner);

		int growthCap = owner.ActiveDomains.ContainsKey("unlimited_potential")
			? GameState.HardMaxManaCap : GameState.MaxManaCrystals;
		owner.Core.StartTurn(growthCap);
		owner.ResetWeaponAttacks();
		owner.TickWeaponCooldown();
		if (owner.HeroPower is IChargeCooldownSkill charged)
			charged.TickChargeCooldown();
		owner.DrawCards(1);
		owner.ResetAmbush();
		owner.ResetDamageTakenThisTurn();
		foreach (var minion in Board.GetAllMinions().Where(m => !m.IsDead && m.IsPlayerSide == owner.IsPlayerSide))
			_attackTracker.AddCharged(minion);
	}

	/// <summary>供 UI/QA 读取指挥官实时手牌与法力（不新增 UI 依赖）。</summary>
	public IReadOnlyList<Card.Card> GetEnemyHand(int enemyIndex = 0)
		=> EnemyUnits.Count > enemyIndex ? EnemyUnits[enemyIndex].Body.Hand : Array.Empty<Card.Card>();
}
