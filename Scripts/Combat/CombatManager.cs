using Godot;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Character;
using OdysseyCards.Heat;
using OdysseyCards.Relic;
using OdysseyCards.Roguelike;
using OdysseyCards.UI;
using System;
using System.Collections.Generic;
using System.Linq;

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
	/// 战场管理器，管理双方各 5 个随从槽位。
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
	/// 敌方表情事件——当敌人发送嘲讽表情时触发（参数为表情文本）。
	/// 由 CombatUI 订阅以显示浮动表情文本。
	/// </summary>
	public event Action<string>? OnEnemyEmote;

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

	// ===== 表情系统 =====

	/// <summary>
	/// 表情系统子节点——管理敌人嘲讽表情的定时触发。
	/// 在 <see cref="_Ready"/> 中作为子节点创建并添加。
	/// </summary>
	private EmoteSystem _emoteSystem;

	/// <summary>
	/// 强制从指定敌人发送一条表情文本（由 DevConsole /emote 命令调用）。
	/// 委托给 <see cref="EmoteSystem.SendEmote"/>。
	/// </summary>
	public void SendEmote(string text)
	{
		_emoteSystem?.SendEmote(text);
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
		BladeCrisis
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
	/// 领域触发管理器——封装领域在部署/回合/受击等时机的行为。
	/// </summary>
	private DomainTriggerManager _domainTriggerManager = null!;

	/// <summary>
	/// 本回合是否已使用过英雄技能。回合开始时重置。
	/// </summary>
	private bool _heroPowerUsedThisTurn;

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

		// 表情系统——作为子节点创建并添加，事件转发到 OnEnemyEmote
		_emoteSystem = new EmoteSystem { Name = "EmoteSystem" };
		AddChild(_emoteSystem);
		_emoteSystem.OnEmote += (text) => OnEnemyEmote?.Invoke(text);
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
			// 回退：如果没有运行状态（例如直接从 Combat.tscn 启动），使用默认邪教徒
			encounters = new EnemyEncounter[] { new Cultist() };
			GD.Print("[CombatManager] 回退使用默认敌人 — 邪教徒");
		}

		// 为每个 EnemyEncounter 创建对应的 Hero + EnemyUnit
		var enemyUnits = new List<EnemyUnit>();
		foreach (var enc in encounters)
		{
			var enemyCore = new CommanderCore();
			enemyCore.InitializeHealth(enc.MaxHealth, enc.MaxHealth);
			enemyCore.SetMana(0, 0);
			var enemyHero = new Hero(enemyCore, false);
			var unit = new EnemyUnit(enemyHero, enc);
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
		PlayerHero = new Hero(_playerCore, true);

		// 从 Player 复制英雄技能设置（由 GameManager.CreateNewPlayer 注入）
		PlayerHero.HeroPower = Player.HeroPower;

		// 奇巧关键词回调：被弃牌时自动打出
		_playerCore.CombatDeckState.OnBeforeDiscard = HandleQiqiaoDiscard;

		Board = new Board();
		State = new GameState();

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

		// 状态变更事件：随从部署/移除时触发，驱动意图 UI 实时刷新
		Board.OnMinionPlaced += (_, _) => NotifyCombatStateChanged();
		Board.OnMinionRemoved += (_) => NotifyCombatStateChanged();

		// 机械蜈蚣-防空型自动拦截：敌方部署低费随从时自动触发战斗
		Board.OnMinionPlaced += OnMinionPlacedForCentipede;

		// 装配默认武器
		PlayerHero.Weapon = new IonPistol();

		// 初始化热力值系统
		_heatSystem = new HeatSystem();

		// 初始化藏品管理器（从 GameManager 获取，或创建空列表）
		_relicManager = GameManager.Instance.Relics ?? new RelicManager();
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

		_effectDispatcher = new CardEffectDispatcher(
			_playerCore,
			PlayerHero,
			Board,
			State,
			NotifyCombatStateChanged,
			_selectionSystem.HandleDiscoverEffect,
			_selectionSystem.BeginDiscardDiscoverSelection,
			_selectionSystem.BeginHandDiscardSelection,
			RequestDamageVfx);
		_deathHandler = new DeathHandler(Board, PlayerHero, _effectDispatcher, _attackTracker);
		_weaponAttack = new WeaponAttackSystem(
			Board,
			PlayerHero,
			EnemyUnits,
			State,
			_attackTracker,
			_deathHandler,
			NotifyCombatStateChanged,
			() => IsDiscovering,
			(m) => TriggerBaitTacticsOnAttacked(m, null),
			(won) => OnGameOver?.Invoke(won),
			this);
		_domainTriggerManager = new DomainTriggerManager(
			_playerCore,
			PlayerHero,
			Board,
			State,
			EnemyUnits,
			NotifyCombatStateChanged);
		PlayerHero.OnAttacked += HandlePlayerHeroAttacked;

		// 注册热力值伤害修改器到所有敌方单位
		var heatMod = new HeatDamageModifier(_heatSystem);
		foreach (var unit in EnemyUnits)
		{
			unit.Body._damageModifiers.Add(heatMod);
		}

		foreach (var unit in EnemyUnits)
		{
			unit.Body.Weapon = new RollingLog();
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
	/// 注入敌人被动技能 modifier 到对应的 Hero。
	/// 固璋(3) → DamageCapModifier；不破(1) 由 Hero._hasTakenDamageThisTurn 门控处理。
	/// </summary>
	private static void ApplyEnemyPassives(EnemyUnit unit)
	{
		if (unit.Brain is ZhangLang)
		{
			unit.Body._damageModifiers.Add(new DamageCapModifier(3));
			GD.Print($"[CombatManager] {unit.Brain.Name} 固璋(3) — 单次伤害上限 3");
		}
		// 不破(1)：需在 ApplyEnemyPassives 中设置 unit.Body.HasUnbreakable = true，
		// 并由 Hero._hasTakenDamageThisTurn 守卫 + StartPlayerTurn/EndPlayerTurn 的 ResetDamageTakenThisTurn 处理
	}

	// ===== 战斗开始 =====

	/// <summary>
	/// 开始战斗——仅处理战斗初始化（场景、牌堆、发牌）。
	/// 完成后调用 <see cref="StartPlayerTurn"/> 启动第一个玩家回合。
	/// </summary>
	public void StartCombat()
	{
		GD.Print("[CombatManager] ========== 战斗开始 ==========");

		// 从牌堆定义创建运行时卡牌并洗入抽牌堆
		_playerCore.SetupDrawPile();
		GD.Print($"[CombatManager] 抽牌堆已设置，共 {_playerCore.DrawPile.Count} 张牌");

		State.StartGame();

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
		// 重置英雄技能使用标记
		_heroPowerUsedThisTurn = false;

		// 检查英雄是否拥有「无限潜能」领域，决定自然增长上限
		int growthCap = PlayerHero.ActiveDomains.ContainsKey("unlimited_potential")
			? GameState.HardMaxManaCap
			: GameState.MaxManaCrystals;
		State.StartPlayerTurn(growthCap);
		_playerCore.SetMana(State.PlayerMana, State.PlayerMaxMana);

		// 藏品 — 回合开始时触发（在法力设置之后，以便战术核显卡等修改法力值）
		_relicManager.TriggerTurnStart(this);

		// 回合开始抽 1 张牌
		PlayerHero.DrawCards(1);

		// 重置随从攻击状态
		ResetAttackTracking();

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

		// 启动表情空闲计时器
		_emoteSystem?.StartIdleTimer();

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
	/// <param name="slotIndex">目标槽位索引（0-4）</param>
	/// <returns>成功返回 true；验证失败返回 false</returns>
	public bool PlayMinion(Card.Card card, int slotIndex)
	{
		if (IsDiscovering)
		{
			GD.PrintErr("[CombatManager] PlayMinion 失败 — 正在发现选牌阶段");
			return false;
		}

		// 验证：玩家回合
		if (!State.IsPlayerTurn)
		{
			GD.PrintErr("[CombatManager] PlayMinion 失败 — 不是玩家回合");
			return false;
		}

		// 验证：卡牌有效性
		if (card == null)
		{
			GD.PrintErr("[CombatManager] PlayMinion 失败 — 卡牌为 null");
			return false;
		}

		// 验证：是随从牌
		if (card.Type != CardType.Minion)
		{
			GD.PrintErr($"[CombatManager] PlayMinion 失败 — {card.CardName} 不是随从牌");
			return false;
		}

		// 验证：法力值充足
		int actualCost = ApplyRelicCostModifiers(card);
		if (!PlayerHero.CanSpendMana(actualCost))
		{
			GD.PrintErr($"[CombatManager] PlayMinion 失败 — 法力值不足（需 {actualCost}，现有 {PlayerHero.CurrentMana}）");
			return false;
		}

		// 验证：槽位可用
		if (!Board.CanPlaceMinion(isPlayerSide: true))
		{
			GD.PrintErr("[CombatManager] PlayMinion 失败 — 玩家战场已满（最多 5 个随从）");
			return false;
		}

		// 消耗法力值
		PlayerHero.SpendMana(actualCost);
		GD.Print($"[CombatManager] 消耗 {actualCost} 法力值（剩余 {PlayerHero.CurrentMana}）");

		// 通知藏品和热力值系统
		NotifyCardPlayed(card, actualCost);

		// 创建随从运行时实例，并保留牌堆中已有的运行时修饰
		var minion = new Minion(card, isPlayerSide: true);

		// 处理战吼效果
		if (minion.HasBattlecry)
		{
			GD.Print($"[CombatManager]   ◆ 触发战吼：{minion.CardName}");
			foreach (var effect in minion.BattlecryEffects)
			{
				ResolveBattlecryEffect(effect, minion);
			}
		}

		// 放置随从到战场
		Board.PlaceMinion(minion, slotIndex);

		// 我的刀盾：战吼复制到相邻槽位
		if (minion.HasBattlecry && minion.BattlecryEffects.Any(e => e.CustomEffectName == "CopyToAdjacentSlot"))
		{
			CopyMinionToAdjacentSlot(minion, slotIndex);
		}

		// 闪击关键词：召唤的回合即可攻击
		if (minion.HasCharge)
		{
			_attackTracker.AddCharged(minion);
			GD.Print($"[CombatManager]   ⚡ {minion.CardName} 具有闪击，本回合可以攻击");
		}

		// 从手牌中移除
		PlayerHero.RemoveFromHand(card);

		GD.Print($"[CombatManager] 玩家召唤了 {minion.CardName}（{minion.Attack}/{minion.CurrentHealth}）到槽位 {slotIndex}");

		// 触发领域效果 — 友方随从部署后
		_domainTriggerManager.OnMinionPlaced(minion);

		return true;
	}

	/// <summary>
	/// 处理战吼效果。
	/// 原型阶段仅输出日志，后续可扩展为完整的效果解析。
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
	/// <param name="sourceSlotIndex">源随从所在槽位</param>
	private void CopyMinionToAdjacentSlot(Minion sourceMinion, int sourceSlotIndex)
	{
		var tokenData = GD.Load<CardData>("res://Resources/Cards/Minion_WhatTheDogDoing.tres");
		if (tokenData == null)
		{ GD.PrintErr("[CombatManager] CopyToAdjacentSlot: 无法加载Token卡牌"); return; }

		// 优先相邻槽位（先左后右）
		int? targetSlot = null;
		if (sourceSlotIndex > 0 && Board.PlayerSlots[sourceSlotIndex - 1] == null)
			targetSlot = sourceSlotIndex - 1;
		else if (sourceSlotIndex < 4 && Board.PlayerSlots[sourceSlotIndex + 1] == null)
			targetSlot = sourceSlotIndex + 1;

		// 回退：任意空位
		if (!targetSlot.HasValue)
			targetSlot = Board.GetEmptySlotIndex(isPlayerSide: true);

		if (targetSlot.HasValue)
		{
			var copy = new Minion(tokenData, isPlayerSide: true);
			Board.PlaceMinion(copy, targetSlot.Value);
			GD.Print($"[CombatManager] 🐕 我的刀盾复制到槽位{targetSlot.Value}");
		}
		else
			GD.Print("[CombatManager] 🐕 我的刀盾：棋盘已满，无法复制");
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
		if (IsDiscovering)
		{
			GD.PrintErr("[CombatManager] PlaySpell 失败 — 正在发现选牌阶段");
			return false;
		}

		// 验证：玩家回合
		if (!State.IsPlayerTurn)
		{
			GD.PrintErr("[CombatManager] PlaySpell 失败 — 不是玩家回合");
			return false;
		}

		// 验证：卡牌有效性
		if (card == null)
		{
			GD.PrintErr("[CombatManager] PlaySpell 失败 — 卡牌为 null");
			return false;
		}

		// 验证：是法术牌或状态牌
		if (card.Type != CardType.Spell && card.Type != CardType.Status)
		{
			GD.PrintErr($"[CombatManager] PlaySpell 失败 — {card.CardName} 不是法术牌或状态牌");
			return false;
		}

		// 验证：法力值充足
		int actualCost = ApplyRelicCostModifiers(card);
		if (!PlayerHero.CanSpendMana(actualCost))
		{
			GD.PrintErr($"[CombatManager] PlaySpell 失败 — 法力值不足（需 {actualCost}，现有 {PlayerHero.CurrentMana}）");
			return false;
		}

		// 验证：目标合法性（tag 子集匹配）
		if (!ValidateTarget(card, target))
		{
			GD.PrintErr($"[CombatManager] PlaySpell 失败 — 目标不合法（{card.CardName} 的目标过滤：{card.Data.TargetFilter}）");
			return false;
		}

		// 消耗法力值
		PlayerHero.SpendMana(actualCost);
		GD.Print($"[CombatManager] 施放法术 {card.CardName}，消耗 {actualCost} 法力值");

		// 通知藏品和热力值系统
		NotifyCardPlayed(card, actualCost);

		// 解析每个法术效果
		bool selectionTriggered = false;
		foreach (var effect in card.Data.Effects)
		{
			ResolveSpellEffect(effect, target, card);
			if (IsDiscovering)
			{
				selectionTriggered = true;
				_selectionSystem.SetPendingDiscoverSpellCard(card);
			}
		}

		// 如果触发了选牌效果，延迟清理——确认选择后处理 RemoveFromHand/CheckDeaths
		if (selectionTriggered)
		{
			GD.Print("[CombatManager]   选牌效果已触发，等待玩家选择...");
			return true;
		}

		// 轮战法术：回到抽牌堆底部；普通法术：进入弃牌堆
		if (card.HasRecycle)
		{
			PlayerHero.ReturnToDrawPile(card);
			GD.Print($"[CombatManager]   ♻ {card.CardName}（轮战）回到抽牌堆底部");
		}
		// 「解释」：回到手牌（不进入弃牌堆）
		else if (card.Id == "spell_explain")
		{
			HandleExplainReturnToHand(card);
		}
		else
		{
			PlayerHero.DiscardCard(card);
			GD.Print($"[CombatManager]   🗑 {card.CardName} 进入弃牌堆");
		}

		// 法术可能造成随从死亡
		CheckDeaths();

		// 攻击完成
		return true;
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
		if (IsDiscovering)
		{
			GD.PrintErr("[CombatManager] PlayDomain 失败 — 正在发现选牌阶段");
			return false;
		}

		// 验证：玩家回合
		if (!State.IsPlayerTurn)
		{
			GD.PrintErr("[CombatManager] PlayDomain 失败 — 不是玩家回合");
			return false;
		}

		// 验证：卡牌有效性
		if (card == null)
		{
			GD.PrintErr("[CombatManager] PlayDomain 失败 — 卡牌为 null");
			return false;
		}

		// 验证：是领域牌
		if (card.Type != CardType.Domain)
		{
			GD.PrintErr($"[CombatManager] PlayDomain 失败 — {card.CardName} 不是领域牌");
			return false;
		}

		// 验证：法力值充足
		int actualCost = ApplyRelicCostModifiers(card);
		if (!PlayerHero.CanSpendMana(actualCost))
		{
			GD.PrintErr($"[CombatManager] PlayDomain 失败 — 法力值不足（需 {actualCost}，现有 {PlayerHero.CurrentMana}）");
			return false;
		}

		// 消耗法力值
		PlayerHero.SpendMana(actualCost);
		GD.Print($"[CombatManager] 展开领域 {card.CardName}，消耗 {actualCost} 法力值");

		// 通知藏品和热力值系统
		NotifyCardPlayed(card, actualCost);

		// 将领域效果附加到英雄
		string domainId = card.Data.DomainId;
		if (string.IsNullOrEmpty(domainId))
		{
			domainId = card.Data.Id; // fallback 用卡牌ID作为领域标识
		}

		foreach (var effect in card.Data.Effects)
		{
			// 先执行即时效果（如 RemoveNaturalManaCap），再存储为持久领域效果
			ExecuteEffect(effect, null, visualSource: card);
			PlayerHero.AddDomain(domainId, effect);
		}

		// 从手牌中移除（领域不进入弃牌堆）
		PlayerHero.RemoveFromHand(card);

		return true;
	}

	/// <summary>
	/// 执行单个卡牌效果，根据 EffectType 分发到对应的处理逻辑。
	/// 供法术、战吼、亡语等所有效果触发场景共用。
	/// </summary>
	/// <param name="effect">效果数据</param>
	/// <param name="target">效果目标对象（Minion 或 Hero，可为 null）</param>
	private void ExecuteEffect(CardEffectData effect, object? target, IDamageSource? source = null, object? visualSource = null)
	{
		_effectDispatcher.ExecuteEffect(effect, target, source, visualSource);
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
	/// 解析单个法术效果，委托给共享的 ExecuteEffect 执行。
	/// </summary>
	/// <param name="effect">效果数据</param>
	/// <param name="target">法术目标对象</param>
	private void ResolveSpellEffect(CardEffectData effect, object target, OdysseyCards.Card.Card card)
	{
		ExecuteEffect(effect, target, visualSource: card);
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

		bool ambushTriggers = defender.HasAmbush && !defender.AmbushUsedThisTurn;
		bool impactActive = attacker.HasImpact;

		GD.Print($"[CombatManager] ⚔ {attacker.CardName}（{attacker.Attack}攻/{attacker.CurrentHealth}血）攻击 " +
				  $"{defender.CardName}（{defender.Attack}攻/{defender.CurrentHealth}血）" +
				  (ambushTriggers ? " [伏击触发！]" : "") +
				  (impactActive ? " [冲击激活]" : ""));

		// ===== Phase 1: 伏击先手（防御者先于攻击者造成伤害） =====
		if (ambushTriggers)
		{
			defender.AmbushUsedThisTurn = true;

			if (impactActive)
			{
				// KARDS 规则：冲击免疫伏击的先手伤害
				GD.Print($"[CombatManager]   🛡 冲击免疫了 {defender.CardName} 的伏击伤害（{defender.Attack}）");
			}
			else
			{
				// 伏击先手：防御者先对攻击者造成伤害
				GD.Print($"[CombatManager]   ⚡ {defender.CardName} 伏击先手，造成 {defender.Attack} 伤害");
				attacker.TakeDamage(defender.Attack, defender);
			}

			// 伏击击杀攻击者 → 战斗结束，攻击被取消
			if (attacker.IsDead)
			{
				GD.Print($"[CombatManager]   ☠ {attacker.CardName} 被伏击击杀，攻击被取消（不造成伤害）");
				return false;
			}
		}

		// ===== Phase 2: 攻击者造成伤害 =====
		RequestDamageVfx(attacker, defender, DamageKind.Attack, CombatDamageVfxKind.Attack);
		defender.TakeDamage(attacker.Attack, attacker);

		// ===== Phase 3: 防御者反击（炉石规则：双方同时伤害，防御者被击杀仍能反击） =====
		// 注意：即使防御者在 Phase 2 中被击杀，也要造成反击伤害（炉石同时伤害规则）
		{
			if (impactActive)
			{
				// 冲击免疫正常反击伤害
				GD.Print($"[CombatManager]   🛡 冲击免疫了 {defender.CardName} 的反击伤害（{defender.Attack}）");
			}
			else
			{
				attacker.TakeDamage(defender.Attack, defender);
			}
		}

		// ===== Phase 4: 消耗冲击（一次性效果） =====
		if (impactActive)
		{
			attacker.HasImpact = false;
			GD.Print($"[CombatManager]   ✨ {attacker.CardName} 的冲击已被消耗");
		}

		defender.TriggerIdolTwilightOnAttacked();

		GD.Print($"[CombatManager]     交锋后 — {attacker.CardName}：{attacker.CurrentHealth}血，" +
				  $"{defender.CardName}：{defender.CurrentHealth}血");

		return true;
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

		GD.Print($"[CombatManager] ⚔ 战斗：{a.CardName}（{a.Attack}攻/{a.CurrentHealth}血）vs " +
				  $"{b.CardName}（{b.Attack}攻/{b.CurrentHealth}血）");

		// A 对 B 造成伤害（完整 DamageResolver 管线）
		RequestDamageVfx(a, b, DamageKind.Attack, CombatDamageVfxKind.Combat);
		b.TakeDamage(a.Attack, a);

		// B 对 A 造成伤害（即使 B 已被击杀，仍遵循同时伤害规则）
		RequestDamageVfx(b, a, DamageKind.Attack, CombatDamageVfxKind.Combat);
		a.TakeDamage(b.Attack, b);

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
		if (attackerHero != null && !attackerHero.IsPlayerSide)
		{
			attackerHero.ModifyDefense(-1);
			GD.Print($"[CombatManager] ◆ 诱饵战术触发：{target.CardName} 受到敌方英雄攻击，该英雄防御力-1（当前 {attackerHero.Defense}）");
		}
		else
		{
			var enemyUnit = GetDefaultEnemyTargetUnit();
			if (enemyUnit == null)
				return;
			var enemyBody = enemyUnit.Body;
			enemyBody.ModifyDefense(-1);
			GD.Print($"[CombatManager] ◆ 诱饵战术触发：{target.CardName} 受到攻击，敌方英雄防御力-1（当前 {enemyBody.Defense}）");
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
		if (IsDiscovering)
		{
			GD.PrintErr("[CombatManager] MinionAttack 失败 — 正在发现选牌阶段");
			return false;
		}

		// 验证：玩家回合
		if (!State.IsPlayerTurn)
		{
			GD.PrintErr("[CombatManager] MinionAttack 失败 — 不是玩家回合");
			return false;
		}

		// 验证：攻击方合法性
		if (attacker == null)
		{
			GD.PrintErr("[CombatManager] 攻击验证失败 — 攻击者为 null");
			return false;
		}
		if (!attacker.IsPlayerSide)
		{
			GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 不是玩家随从");
			return false;
		}
		if (attacker.IsDead)
		{
			GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 已死亡");
			return false;
		}
		if (!_attackTracker.CanAttack(attacker))
		{
			GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 本回合无法攻击");
			return false;
		}
		if (attacker.ActionCost > 0 && PlayerHero.CurrentMana < attacker.ActionCost)
		{
			GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 行动花费 {attacker.ActionCost}，当前法力不足（{PlayerHero.CurrentMana}）");
			return false;
		}

		// 验证：防御方有效性
		if (defender == null || defender.IsDead)
		{
			GD.PrintErr("[CombatManager] MinionAttack 失败 — 目标随从未知或已死亡");
			return false;
		}

		if (defender.IsPlayerSide)
		{
			GD.PrintErr("[CombatManager] MinionAttack 失败 — 不能攻击己方随从");
			return false;
		}

		// 嘲讽检测：敌方有嘲讽随从时，只能攻击嘲讽目标
		var enemyTaunts = Board.GetTaunts(ofEnemy: true);
		if (enemyTaunts.Count > 0 && !enemyTaunts.Contains(defender))
		{
			GD.PrintErr($"[CombatManager] MinionAttack 失败 — 敌方有 {enemyTaunts.Count} 个嘲讽随从阻挡，必须先攻击嘲讽目标");
			return false;
		}

		// 消耗行动花费法力
		if (attacker.ActionCost > 0)
		{
			PlayerHero.SpendMana(attacker.ActionCost);
			GD.Print($"[CombatManager]   {attacker.CardName} 行动花费 {attacker.ActionCost} 法力，剩余法力：{PlayerHero.CurrentMana}");
		}

		// 通过统一战斗序列执行随从间战斗（自动处理伏击、冲击）
		bool combatContinues = ResolveMinionCombat(attacker, defender);

		// 记录攻击次数（即使被伏击击杀也算消耗）
		_attackTracker.RecordAttack(attacker);

		// 检查防御方死亡
		if (defender.IsDead)
		{
			GD.Print($"[CombatManager]   ☠ {defender.CardName} 被击杀");
			Board.RemoveMinion(defender);
		}

		// 检查攻击方死亡
		if (attacker.IsDead)
		{
			GD.Print($"[CombatManager]   ☠ {attacker.CardName} 在攻击中阵亡");
			Board.RemoveMinion(attacker);
			_attackTracker.Remove(attacker);
		}

		// 全局死亡检查
		CheckDeaths();
		// 胜负判定由 Hero.OnDeath 事件驱动，不再手动调用
		return true;
	}

	/// <summary>
	/// 玩家随从攻击敌方英雄。
	/// 需要敌方无嘲讽随从阻挡。原型阶段仅支持玩家攻击敌方英雄（敌方 AI 攻击在 Phase 4 实现）。
	/// </summary>
	/// <param name="attacker">攻击方（必须是玩家随从）</param>
	/// <param name="hero">目标英雄</param>
	/// <returns>攻击成功返回 true</returns>
	public bool MinionAttackHero(Minion attacker, Hero hero)
	{
		if (IsDiscovering)
		{
			GD.PrintErr("[CombatManager] MinionAttackHero 失败 — 正在发现选牌阶段");
			return false;
		}

		// 验证：玩家回合
		if (!State.IsPlayerTurn)
		{
			GD.PrintErr("[CombatManager] MinionAttackHero 失败 — 不是玩家回合");
			return false;
		}

		// 验证：攻击方合法性
		if (attacker == null)
		{
			GD.PrintErr("[CombatManager] 攻击验证失败 — 攻击者为 null");
			return false;
		}
		if (!attacker.IsPlayerSide)
		{
			GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 不是玩家随从");
			return false;
		}
		if (attacker.IsDead)
		{
			GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 已死亡");
			return false;
		}
		if (!_attackTracker.CanAttack(attacker))
		{
			GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 本回合无法攻击");
			return false;
		}
		if (attacker.ActionCost > 0 && PlayerHero.CurrentMana < attacker.ActionCost)
		{
			GD.PrintErr($"[CombatManager] 攻击验证失败 — {attacker.CardName} 行动花费 {attacker.ActionCost}，当前法力不足（{PlayerHero.CurrentMana}）");
			return false;
		}

		// 嘲讽检测（攻击英雄）
		var enemyTaunts = Board.GetTaunts(ofEnemy: true);
		if (enemyTaunts.Count > 0)
		{
			GD.PrintErr($"[CombatManager] MinionAttackHero 失败 — 敌方有 {enemyTaunts.Count} 个嘲讽随从阻挡");
			return false;
		}

		// 消耗行动花费法力
		if (attacker.ActionCost > 0)
		{
			PlayerHero.SpendMana(attacker.ActionCost);
			GD.Print($"[CombatManager]   {attacker.CardName} 行动花费 {attacker.ActionCost} 法力，剩余法力：{PlayerHero.CurrentMana}");
		}

		GD.Print($"[CombatManager] ⚔ {attacker.CardName} 攻击敌方英雄，造成 {attacker.Attack} 点伤害" +
				  (attacker.HasImpact ? " [冲击]" : ""));

		// 冲击：攻击时免疫反击伤害（抑制敌方英雄武器反击）
		bool impactActive = attacker.HasImpact;
		if (impactActive)
			hero.SuppressWeaponCounter = true;

		RequestDamageVfx(attacker, hero, DamageKind.Attack, CombatDamageVfxKind.Attack);
		hero.TakeDamage(attacker.Attack, attacker);

		if (impactActive)
		{
			hero.SuppressWeaponCounter = false;
			attacker.HasImpact = false;
			GD.Print($"[CombatManager]   🛡 冲击免疫了反击伤害，冲击已消耗");
		}

		// 记录攻击次数
		_attackTracker.RecordAttack(attacker);

		GD.Print($"[CombatManager]   敌方英雄剩余生命值：{hero.CurrentHealth}（护甲：{hero.CurrentArmor}）");

		// 检查攻击方是否因敌方武器反击而死亡
		if (attacker.IsDead)
		{
			GD.Print($"[CombatManager]   ☠ {attacker.CardName} 在攻击英雄时被反击击杀");
			Board.RemoveMinion(attacker);
			_attackTracker.Remove(attacker);
		}

		return true;
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
	/// 供 CombatUI 和 DevConsole 调用。
	/// </summary>
	public bool CheckVictoryOrDefeat() => _victoryResolver.CheckVictoryOrDefeat();

	/// <summary>
	/// 战斗结束清理——移除所有状态牌，重置热力值等。
	/// </summary>
	private void CleanupCombat()
	{
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
		if (IsDiscovering)
		{
			GD.PrintErr("[CombatManager] EndPlayerTurn 失败 — 正在发现选牌阶段");
			return;
		}

		if (!State.IsPlayerTurn)
		{
			GD.PrintErr("[CombatManager] EndPlayerTurn 失败 — 当前不是玩家回合");
			return;
		}

		GD.Print("[CombatManager] ========== 玩家回合结束 ==========");

		// 停止表情空闲计时器
		_emoteSystem?.StopIdleTimer();

		// 清理本回合攻击追踪
		_attackTracker.Reset();

		// 触发领域效果 — 友方回合结束时
		_domainTriggerManager.OnPlayerTurnEnd();

		// 藏品 — 玩家回合结束时触发
		_relicManager.TriggerTurnEnd(this);

		// 状态效果衰减 — 友方回合结束时
		PlayerHero.TickStatusEffects(TickTiming.PlayerTurnEnd);
		PlayerHero.ResetDamageTakenThisTurn(); // 玩家每回合重置受伤标记
		foreach (var unit in EnemyUnits)
			unit.Body.TickStatusEffects(TickTiming.PlayerTurnEnd);
		// 重置每回合受伤标记（不破等被动用）
		foreach (var unit in EnemyUnits)
			unit.Body.ResetDamageTakenThisTurn();

		// Minion 状态效果衰减 — 友方回合结束时
		foreach (var minion in Board.GetPlayerMinions())
			minion.TickStatusEffects(TickTiming.PlayerTurnEnd);
		foreach (var minion in Board.GetEnemyMinions())
			minion.TickStatusEffects(TickTiming.PlayerTurnEnd);

		// 切换到敌方回合
		State.EndPlayerTurn();
		GD.Print($"[CombatManager] ---------- 敌方回合开始（{EnemyUnits.Count} 个敌人）----------");

		// 执行敌方回合
		ExecuteEnemyTurn();

		// 敌方回合结束，切换回玩家回合
		State.EndEnemyTurn();

		// 开始玩家新回合
		StartPlayerTurn();

		// 检查胜负
		_victoryResolver.CheckVictoryOrDefeat();
	}

	/// <summary>
	/// 尝试使用英雄技能。
	/// 检查是否玩家回合、未使用过、不在发现阶段、英雄技能可用。
	/// </summary>
	/// <returns>成功使用时返回 true</returns>
	public bool TryUseHeroPower()
	{
		if (!State.IsPlayerTurn)
		{
			GD.Print("[CombatManager] TryUseHeroPower 失败 — 非玩家回合");
			return false;
		}

		if (_heroPowerUsedThisTurn)
		{
			GD.Print("[CombatManager] TryUseHeroPower 失败 — 本回合已使用过英雄技能");
			return false;
		}

		if (IsDiscovering)
		{
			GD.Print("[CombatManager] TryUseHeroPower 失败 — 正在发现选牌阶段");
			return false;
		}

		var heroPower = PlayerHero.HeroPower;
		if (heroPower == null)
		{
			GD.Print("[CombatManager] TryUseHeroPower 失败 — 没有英雄技能");
			return false;
		}

		if (!heroPower.CanUse(PlayerHero))
		{
			GD.Print($"[CombatManager] TryUseHeroPower 失败 — 英雄技能无法使用（法力 {PlayerHero.CurrentMana}，需要 {heroPower.Cost}）");
			return false;
		}

		heroPower.Execute(PlayerHero, this);
		_heroPowerUsedThisTurn = true;

		GD.Print($"[CombatManager] 英雄技能「{heroPower.Name}」使用成功");
		NotifyCombatStateChanged();
		return true;
	}

	/// <summary>
	/// 本回合是否已使用过英雄技能。供 CombatUI 刷新按钮状态。
	/// </summary>
	public bool HeroPowerUsedThisTurn => _heroPowerUsedThisTurn;

	/// <summary>
	/// DevConsole 强制胜利——瞬间击杀所有敌方单位，触发正常胜利流程。
	/// 可通过 grantReward 跳过金币奖励。
	/// </summary>
	public void ForceVictory(bool grantReward = true)
	{
		if (State.IsGameOver)
			return;
		_victoryResolver.DevSkipGoldReward = !grantReward;

		// 击杀所有敌方随从 —— 绕过伤害管线，直接用 DevConsole 直伤确保击杀
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
		GD.Print($"[CombatManager] DevConsole 强制胜利（{(grantReward ? "含" : "跳过")}奖励）");
	}

	/// <summary>
	/// 执行敌方 AI 回合：依次执行每个敌人的意图 → 推进意图轮转 → 敌方随从攻击 → 死亡检查 → 胜负判定。
	/// 执行期间冻结意图 UI 刷新（_isEnemyTurnAnimating），防止动画中数值跳变。
	/// </summary>
	private void ExecuteEnemyTurn()
	{
		// 冻结意图 UI 刷新——防止执行动画期间数值跳变
		_isEnemyTurnAnimating = true;

		// 0. 快照本回合开始时已存在的敌方随从——只有它们可以攻击
		_enemyMinionsCanAttack.Clear();
		foreach (var m in Board.GetEnemyMinions())
		{
			if (!m.IsDead)
				_enemyMinionsCanAttack.Add(m);
		}

		// 1. 依次执行每个敌人的当前意图（攻击/防御/召唤等）——使用动态目标选择
		foreach (var unit in EnemyUnits)
		{
			// 跳过已死亡的敌人
			if (unit.Body.IsDead)
				continue;

			// 同步敌方攻击力到意图系统（考虑武器禁用等状态）
			unit.Brain.Attack = unit.Body.Weapon is { IsDisabled: false } ? unit.Body.Weapon.Attack : 0;
			unit.Brain.ExecuteIntent(this, unit.Body);

			// 2. 推进到下一意图——优先使用 MoveState 系统
			unit.Brain.AdvanceMove();
			GD.Print($"[CombatManager] {unit.Brain.Name} 下回合意图：{unit.Brain.GetCurrentIntent(this, unit.Body).Description}");

			// 每次执行后检查死亡（攻击意图可能杀死敌人自身或玩家）
			CheckDeaths();
			if (_victoryResolver.CheckVictoryOrDefeat())
			{
				_isEnemyTurnAnimating = false;
				return;
			}
		}

		// 3. 敌方随从攻击
		EnemyMinionsAttack();

		// 4. 全局死亡检查（意图/攻击可能造成随从死亡）
		CheckDeaths();

		// 5. 胜负判定
		_victoryResolver.CheckVictoryOrDefeat();

		// 6. 解冻——允许 UI 重新响应状态变更
		_isEnemyTurnAnimating = false;

		// 7. 状态效果衰减 — 敌方回合结束时（武器禁用等 debuff 在此触发）
		PlayerHero.TickStatusEffects(TickTiming.EnemyTurnEnd);
		foreach (var unit in EnemyUnits)
			unit.Body.TickStatusEffects(TickTiming.EnemyTurnEnd);

		// Minion 状态效果衰减 — 敌方回合结束时
		foreach (var minion in Board.GetPlayerMinions())
			minion.TickStatusEffects(TickTiming.EnemyTurnEnd);
		foreach (var minion in Board.GetEnemyMinions())
			minion.TickStatusEffects(TickTiming.EnemyTurnEnd);

		// 7.5 热力值自然增长 + 藏品敌方回合结束触发
		_heatSystem.OnEnemyTurnEnd();
		_relicManager.TriggerEnemyTurnEnd(this);

		// 8. 通知 UI 刷新意图显示（解冻后触发）
		NotifyCombatStateChanged();
	}

	/// <summary>
	/// 敌方所有随从依次攻击：优先走 MoveState.OnPerform 统一路径，无 MoveState 时回退 ExecuteIntent。
	/// </summary>
	private void EnemyMinionsAttack()
	{
		// 回合开始时已存在的随从可以攻击，有闪击的新召唤随从也可以
		var enemies = Board.GetEnemyMinions()
			.Where(m => !m.IsDead && (_enemyMinionsCanAttack.Contains(m) || m.HasCharge))
			.ToList();
		if (enemies.Count == 0)
			return;

		var playerTaunts = Board.GetTaunts(ofEnemy: false);
		bool hasPlayerTaunt = playerTaunts.Count > 0;

		foreach (var attacker in enemies)
		{
			if (attacker.IsDead)
				continue;

			// 确保所有敌方随从有意图大脑（供 UI 意图显示使用）
			attacker.IntentBrain ??= new DefaultAttackMinionBrain(attacker);

			// 统一执行路径：优先使用 MoveState.OnPerform
			if (attacker.IntentBrain != null)
			{
				var move = attacker.IntentBrain.GetCurrentMove(this);
				if (move?.OnPerform != null)
				{
					// MoveState 统一路径——Boss 与随从使用同一执行入口
					move.OnPerform(this, null);  // 随从无 Hero 身体
				}
				else
				{
					// 无 OnPerform → 回退传统 ExecuteIntent
					attacker.IntentBrain.ExecuteIntent(this);
				}
				attacker.IntentBrain.AdvanceMove();
				continue;
			}

			// 默认行为：嘲讽随从优先攻击嘲讽，否则攻击英雄
			if (hasPlayerTaunt)
			{
				// 攻击随机嘲讽随从
				var tauntTargets = playerTaunts.Where(t => !t.IsDead).ToList();
				if (tauntTargets.Count == 0)
					continue;
				var defender = tauntTargets[new Random().Next(tauntTargets.Count)];

				// 通过统一战斗序列执行（自动处理伏击、冲击）
				ResolveMinionCombat(attacker, defender);

				if (defender.IsDead)
				{
					Board.RemoveMinion(defender);
				}
				if (attacker.IsDead)
				{
					Board.RemoveMinion(attacker);
				}
			}
			else
			{
				// 攻击玩家英雄
				GD.Print($"[CombatManager] ⚔ 敌方 {attacker.CardName} 攻击玩家英雄，造成 {attacker.Attack} 伤" +
						  (attacker.HasImpact ? " [冲击]" : ""));

				// 冲击：攻击时免疫武器反击
				bool impactActive = attacker.HasImpact;
				if (impactActive)
					PlayerHero.SuppressWeaponCounter = true;

				RequestDamageVfx(attacker, PlayerHero, DamageKind.Attack, CombatDamageVfxKind.Attack);
				PlayerHero.TakeDamage(attacker.Attack, attacker);

				if (impactActive)
				{
					PlayerHero.SuppressWeaponCounter = false;
					attacker.HasImpact = false;
					GD.Print($"[CombatManager]   🛡 冲击免疫了武器反击，冲击已消耗");
				}
			}
		}
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
	/// 将一张卡牌直接加入玩家手牌（用于 DevConsole /token 命令）。
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
	private void OnMinionPlacedForCentipede(Minion placedMinion, int slotIndex)
	{
		// 仅敌方部署时触发
		if (placedMinion.IsPlayerSide)
			return;

		// 仅拦截费用 ≤2 的低费随从
		if (placedMinion.Cost > 2)
			return;

		// 收集玩家方所有存活的机械蜈蚣-防空型（战斗前快照，防止迭代中修改集合）
		var centipedes = new List<Minion>();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			if (Board.PlayerSlots[i] is Minion m
				&& !m.IsDead
				&& m.Id == "minion_centipede_aa")
			{
				centipedes.Add(m);
			}
		}

		if (centipedes.Count == 0)
			return;

		foreach (var centipede in centipedes)
		{
			if (centipede.IsDead)
				continue;
			if (placedMinion.IsDead)
				break;

			GD.Print($"[CombatManager] ◆ 机械蜈蚣-防空型拦截 {placedMinion.CardName}（{placedMinion.Cost}费）！");
			ResolveCombat(centipede, placedMinion);
		}

		// 清理战斗产生的死亡随从
		if (placedMinion.IsDead)
			Board.RemoveMinion(placedMinion);

		var deadPlayerMinions = new List<Minion>();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			if (Board.PlayerSlots[i] is Minion m && m.IsDead)
				deadPlayerMinions.Add(m);
		}
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
	private static bool ValidateTarget(OdysseyCards.Card.Card card, object target)
	{
		var require = card.Data.TargetFilter;
		var exclude = card.Data.ExcludeFilter;

		if (require == TargetTags.None && exclude == TargetTags.None)
			return true;

		// 已死亡的随从不可选为目标
		if (target is Minion minion && minion.IsDead)
			return false;

		TargetTags entityTags = target switch
		{
			Hero hero => hero.GetTargetTags(),
			Minion m => m.GetTargetTags(),
			_ => TargetTags.None
		};

		// 如果目标类型无法识别（entityTags 为 None），仅当无过滤条件时才放行
		if (entityTags == TargetTags.None)
			return require == TargetTags.None && exclude == TargetTags.None;

		return TargetTagsHelper.IsValidTarget(entityTags, require, exclude);
	}

	/// <summary>
	/// 奇巧关键词回调——卡牌被弃掉时自动打出。
	/// 参考 STS2 的 Sly 机制：CardCmd 收集 Sly 卡牌 → AutoPlay(SlyDiscard)。
	/// 返回 true 表示卡牌已处理（不进入弃牌堆），false 表示正常弃牌。
	/// </summary>
	private bool HandleQiqiaoDiscard(OdysseyCards.Card.Card card)
	{
		if (!card.HasQiqiao)
			return false;

		GD.Print($"[CombatManager] ◆ 奇巧触发：自动打出「{card.GetLocalizedName()}」");

		// 从手牌移除（自动打出消耗卡牌）
		PlayerHero.RemoveFromHand(card);

		switch (card.Type)
		{
			case CardType.Minion:
				AutoSummonQiqiaoMinion(card);
				break;
			case CardType.Domain:
				AutoPlayQiqiaoDomain(card);
				break;
			case CardType.Spell:
			default:
				AutoPlayQiqiaoSpell(card);
				break;
		}

		return true; // 已通过 RemoveFromHand 消耗，不进入弃牌堆
	}

	/// <summary>
	/// 奇巧自动召唤随从——放置到友方最左侧空余槽位。
	/// 若无空余槽位则消失。
	/// </summary>
	private void AutoSummonQiqiaoMinion(OdysseyCards.Card.Card card)
	{
		if (!Board.CanPlaceMinion(isPlayerSide: true))
		{
			GD.Print($"[CombatManager] 奇巧召唤失败——友方战场已满，「{card.CardName}」消失");
			return;
		}

		var minion = new Minion(card, isPlayerSide: true);
		int slot = Board.GetEmptySlotIndex(isPlayerSide: true);
		Board.PlaceMinion(minion, slot);
		GD.Print($"[CombatManager] 奇巧召唤「{minion.CardName}」到槽位 {slot}");
	}

	/// <summary>
	/// 奇巧自动打出领域——展开领域效果（0费）。
	/// </summary>
	private void AutoPlayQiqiaoDomain(OdysseyCards.Card.Card card)
	{
		GD.Print($"[CombatManager] 奇巧展开领域「{card.CardName}」");
		// 遍历效果数据执行
		foreach (var effect in card.Data.Effects)
		{
			// Custom 效果走 Custom handler
			if (effect.EffectType == CardEffectType.Custom)
			{
				_effectDispatcher.ExecuteEffect(effect, PlayerHero, PlayerHero);
				continue;
			}
			_effectDispatcher.ExecuteEffect(effect, PlayerHero, PlayerHero);
		}
		NotifyCombatStateChanged();
	}

	/// <summary>
	/// 奇巧自动打出法术——执行法术效果（0费，目标为敌方英雄）。
	/// </summary>
	private void AutoPlayQiqiaoSpell(OdysseyCards.Card.Card card)
	{
		GD.Print($"[CombatManager] 奇巧施放法术「{card.CardName}」");
		// 默认目标：敌方英雄
		var enemyUnit = GetDefaultEnemyTargetUnit();
		Hero? target = enemyUnit?.Body;
		if (target == null)
		{
			GD.Print($"[CombatManager] 奇巧法术无有效目标");
			return;
		}

		foreach (var effect in card.Data.Effects)
		{
			if (effect.EffectType == CardEffectType.Custom)
			{
				target = DetermineTargetForEffect(effect, target);
				_effectDispatcher.ExecuteEffect(effect, target, PlayerHero);
				continue;
			}
			_effectDispatcher.ExecuteEffect(effect, target, PlayerHero);
		}
		NotifyCombatStateChanged();
	}

	/// <summary>
	/// 根据效果类型确定合适的目标。
	/// </summary>
	private Hero DetermineTargetForEffect(CardEffectData effect, Hero defaultTarget)
	{
		return effect.EffectType switch
		{
			CardEffectType.DealDamageToFriendlyHero => PlayerHero,
			CardEffectType.Heal or CardEffectType.RestoreHealth => PlayerHero,
			_ => defaultTarget
		};
	}

	/// <summary>
	/// 「解释」打出后回到手牌。
	/// </summary>
	private void HandleExplainReturnToHand(OdysseyCards.Card.Card card)
	{
		// 从手牌移除，再重新加入（实现"回到手牌"）
		PlayerHero.RemoveFromHand(card);

		// 直接加入手牌（绕过 RemoveFromHand 的限制）
		if (PlayerHero.Hand.Count >= PlayerHero.DeckState.MaxHandSize)
		{
			GD.Print("[CombatManager] 手牌已满，「解释」被弃掉");
			PlayerHero.DiscardCard(card);
			return;
		}

		PlayerHero.DeckState.AddToHand(card);
		GD.Print("[CombatManager] 「解释」回到手牌");
	}
}
