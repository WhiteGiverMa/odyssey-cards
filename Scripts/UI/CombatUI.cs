#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Infrastructure;

namespace OdysseyCards.UI;

/// <summary>
/// 战斗主界面——炉石传说风格战斗画面编排器。
/// 负责管理棋盘（BoardUI）、手牌（HandUI）、双方英雄生命值/法力值/护甲显示、
/// 回合结束按钮，以及随从放置/法术施放/随从攻击的目标选择流程。
/// 所有 UI 元素均为程序化创建，无需 .tscn 依赖。
/// </summary>
public partial class CombatUI : Control
{
	// ===== 导出属性 =====

	/// <summary>
	/// 生命值条 PackedScene。从左到右填充的 ProgressBar 样式组件。
	/// </summary>
	[Export] public PackedScene? HealthBarScene { get; set; }

	// ===== 子组件 =====

	/// <summary>
	/// 棋盘 UI——屏幕中央，2×5 双方随从槽位。
	/// </summary>
	private BoardUI _boardUI = null!;

	/// <summary>
	/// 手牌 UI——屏幕底部，展示玩家手牌。
	/// </summary>
	private HandUI _handUI = null!;

	/// <summary>
	/// 手牌区域容器——高度由 COLLAPSED_VISIBLE * UIScaler.Scale 决定，
	/// 确保各分辨率下卡牌折叠态露出比例一致。
	/// </summary>
	private Control _handArea = null!;

	/// <summary>
	/// 玩家英雄生命值条——左下角。
	/// </summary>
	private HealthBar _playerHealthBar = null!;

	/// <summary>
	/// 敌方英雄身份卡列表——每张卡包含名字、HP、护甲、防御、武器、意图。
	/// 索引与 CombatManager.EnemyUnits 对应。
	/// </summary>
	private readonly List<EnemyIdentityCard> _enemyCards = new();

	// ===== 旧版敌方 UI（向后兼容，逐步替换为 EnemyIdentityCard） =====
	private HealthBar _enemyHealthBar = null!;
	private Label _enemyArmorLabel = null!;
	private Label _enemyDefenseLabel = null!;
	private Button _enemyHeroAttackButton = null!;
	private Button _enemyHeroSpellButton = null!;
	private Action? _enemyHeroCardAction;
	private Label _enemyIntentLabel = null!;
	private Panel _enemyHeroPanel = null!;
	private Label _enemyWeaponLabel = null!;
	/// <summary>
	/// 玩家效果图标栏——生命值条下方，显示 buff/debuff 图标。
	/// </summary>
	private EffectBar _playerEffectBar = null!;

	/// <summary>
	/// 敌方效果图标栏（单敌人旧版兼容层）——生命值条下方。
	/// </summary>
	private EffectBar _enemyEffectBar = null!;

	/// <summary>
	/// 热力值 UI 条——战斗界面全局位置，显示当前热力值百分比。
	/// </summary>
	private UI.HeatBar _heatBar = null!;

	/// <summary>
	/// 藏品栏——显示当前持有的藏品图标列表。
	/// </summary>
	private UI.RelicBar _relicBar = null!;

	/// <summary>
	/// 玩家法力值显示——底部中央，格式「3/3」。
	/// </summary>
	private Label _playerManaLabel = null!;

	/// <summary>
	/// 回合结束按钮——右下角，文本「结束回合」。
	/// </summary>
	private Button _endTurnButton = null!;

	/// <summary>
	/// 英雄技能按钮——位于玩家区域，显示英雄技能名称和费用。
	/// </summary>
	private Button _heroPowerButton = null!;

	/// <summary>
	/// 玩家护甲值显示——生命值条旁，护甲 > 0 时可见。
	/// </summary>
	private Label _playerArmorLabel = null!;

	/// <summary>
	/// 玩家防御力显示——生命值条旁，防御 != 0 时可见。
	/// </summary>
	private Label _playerDefenseLabel = null!;

	/// <summary>
	/// 暂停按钮——右上角，点击弹出暂停菜单。
	/// </summary>
	private Button _pauseButton = null!;

	/// <summary>
	/// 移动端取消按钮（✕）——仅移动端可见，用于替代右键取消。
	/// 在攻击选择/开发者伤害/手牌选择等可取消状态下显示。
	/// </summary>
	private Button? _mobileCancelButton;

	/// <summary>
	/// 暂停菜单覆盖层——ESC 或暂停按钮触发时创建。
	/// </summary>
	private PauseMenu? _pauseMenu;

	/// <summary>
	/// 综合信息管理界面——CapsLock 触发。
	/// </summary>
	private InfoScreen? _infoScreen;

	/// <summary>
	/// 暂停菜单是否正在显示。
	/// </summary>
	private bool _isPaused;

	/// <summary>
	/// 玩家英雄交互面板——有可见色块背景的容器。
	/// </summary>
	private Panel _playerHeroPanel = null!;

	/// <summary>
	/// 对己方英雄施法按钮——法术目标选择模式下可见。
	/// </summary>
	private Button _playerHeroSpellButton = null!;

	/// <summary>
	/// 抽牌堆按钮——显示当前抽牌堆牌数。
	/// </summary>
	private Button _drawPileBtn = null!;

	/// <summary>
	/// 弃牌堆按钮——显示当前弃牌堆牌数。
	/// </summary>
	private Button _discardPileBtn = null!;

	/// <summary>
	/// 牌堆查看弹窗——点击抽/弃牌堆按钮时复用。
	/// </summary>
	private AcceptDialog? _pileViewPopup;

	/// <summary>
	/// 游戏结束弹窗。
	/// </summary>
	private AcceptDialog? _gameOverPopup;

	/// <summary>
	/// 当前战斗结果是否为胜利（用于游戏结束弹窗的路由）。
	/// </summary>
	private bool _isVictory;

	/// <summary>
	/// 伤害跳字的父容器（独立 CanvasLayer，Layer=15），避免布局重算影响跳字位置。
	/// </summary>
	private Control _damageNumberContainer = null!;

	/// <summary>
	/// 攻击/法术弹道特效容器（独立 CanvasLayer，Layer=14），低于伤害跳字且不阻塞输入。
	/// </summary>
	private Control _attackVfxLayer = null!;

	/// <summary>
	/// 卡牌飞行 VFX 的父容器（独立 CanvasLayer，Layer=20），用于卡牌打出→弃牌堆飞行动画。
	/// </summary>
	private Control _cardFlyLayer = null!;

	/// <summary>
	/// 随从部署动画 VFX 的父容器（独立 CanvasLayer，Layer=18），用于随从打出→棋盘槽位飞行动画。
	/// 位于弹道层(14)和伤害数字层(15)之上，低于卡牌飞行层(20)。
	/// </summary>
	private Control _deployVfxLayer = null!;

	/// <summary>
	/// 棋盘运行时引用——用于订阅随从放置/移除事件。
	/// </summary>
	private Board? _board;

	/// <summary>
	/// 事件解绑列表。CombatUI 生命周期结束时统一释放，避免场景切换后悬空订阅。
	/// </summary>
	private readonly List<Action> _unsubscribeActions = new();

	private readonly Dictionary<Hero, (Action<DamageEventInfo, IDamageSource?> Damage, Action<int> Heal)> _heroDamageHandlers = new();
	private readonly Dictionary<Minion, (Action<DamageEventInfo, IDamageSource?> Damage, Action<int> Heal)> _minionDamageHandlers = new();

	/// <summary>
	/// 拖拽层——卡牌拖拽时重parent到此，使其脱离 HandUI 的 HBoxContainer 布局约束。
	/// </summary>
	private Control _dragLayer = null!;

	/// <summary>
	/// 箭头渲染器——攻击选择（橙色）和敌方意图（红/蓝）箭头的 Control 层绘制组件。
	/// 作为 _dragLayer 子节点，Z 层级最高，不拦截鼠标事件。
	/// </summary>
	private ArrowRenderer _arrowRenderer = null!;

	/// <summary>
	/// 当前正在拖拽的卡牌 UI。
	/// </summary>
	private CardUI? _dragCardUI;
	private Card.Card? _pendingSpellVfxCard;
	private Vector2 _pendingSpellVfxOrigin;
	private const string CardTargetArrowKey = "card_target_select";
	private const float TargetingCardScale = 0.75f;
	// ===== 外部引用 =====

	/// <summary>
	/// 战斗管理器引用。
	/// </summary>
	private CombatManager _combat = null!;

	/// <summary>
	/// 玩家角色引用。
	/// </summary>
	private Player _player = null!;

	/// <summary>
	/// 发现选牌覆盖层（全屏半透明遮罩 + N 张卡牌选择）。
	/// </summary>
	private DiscoverUI? _discoverUI;

	// ===== 手牌选择模式（STS2 风格） =====

	/// <summary>
	/// 缓存上一次手牌快照，用于在 <see cref="OnHandChanged"/> 时机检测新抽到的卡牌
	/// 并播放从抽牌堆飞向手牌的贝塞尔曲线动画（参考 STS2 的 NCardFlyVfx 模式）。
	/// </summary>
	private List<OdysseyCards.Card.Card> _previousHandCards = new();

	/// <summary>
	/// 是否正在手牌选择模式。
	/// </summary>
	private bool _isHandSelecting;

	/// <summary>
	/// 手牌选择模式中已被选中的卡牌。
	/// </summary>
	private readonly List<Card.Card> _selectedHandCards = new();

	/// <summary>
	/// 手牌选择确认按钮。
	/// </summary>
	private Button? _handSelectConfirmBtn;

	/// <summary>
	/// 手牌选择头部提示标签。
	/// </summary>
	private Label? _handSelectHeaderLabel;

	/// <summary>
	/// 手牌选择模式遮罩（半透明暗色背景，STS2 _selectModeBackstop 风格）。
	/// </summary>
	private ColorRect? _handSelectMask;

	/// <summary>
	/// 已选中卡牌的展示容器（STS2 _selectedHandCardContainer 风格），位于手牌上方。
	/// </summary>
	private Control? _selectedHandCardContainer;

	/// <summary>
	/// 已选中卡牌的 UI 引用列表（与 _selectedHandCards 数据同步）。
	/// </summary>
	private readonly List<CardUI> _selectedHandCardUIs = new();


	private SelectionMode _selectionMode = SelectionMode.Normal;

	/// <summary>
	/// 当前从手牌中选中的卡牌（随从或法术）。
	/// </summary>
	private Card.Card? _selectedCard;

	/// <summary>
	/// 当前选中的攻击方随从（己方）。
	/// </summary>
	private Minion? _selectedAttacker;

	// ===== 攻击拖拽状态（委托给 InteractionFsm） =====

	/// <summary>
	/// 攻击拖拽交互状态机——管理攻击/武器目标选择的拖拽/点击双交互模式。
	/// </summary>
	private InteractionFsm _attackDragFsm = new InteractionFsm();

	/// <summary>
	/// 获取当前输入坐标（屏幕空间）。桌面端返回鼠标位置，移动端返回触控位置。
	/// </summary>
	private Vector2 GetInputPosition()
	{
		if (MobileInputRouter.IsMobile)
			return MobileInputRouter.Instance.TouchPosition;
		return GetGlobalMousePosition();
	}

	/// <summary>
	/// 开发者伤害模式参数。
	/// </summary>
	private int _devDamageAmount;

	/// <summary>
	/// 开发者伤害模式完成事件（一次性）。
	/// </summary>
	public event Action? OnDevDamageModeCompleted;

	// ===== 武器 UI 字段 =====

	/// <summary>
	/// 玩家武器信息标签（攻击力 + 费用）。
	/// </summary>
	private Label _weaponInfoLabel = null!;

	/// <summary>
	/// 武器攻击按钮——点击后进入武器目标选择模式。
	/// </summary>
	private Button _weaponAttackButton = null!;
	private bool _ignoreNextWeaponAttackPressed;

	/// <summary>
	/// 武器主动技能按钮——点击后使用武器主动技能。
	/// </summary>
	private Button _weaponActiveSkillButton = null!;

	/// <summary>
	/// 播放区域视觉指示器——拖拽无目标卡牌时在屏幕中央显示。
	/// </summary>
	private Panel? _playZonePanel;

	/// <summary>
	/// 播放区域 Label——指示器内的提示文字。
	/// </summary>
	private Label? _playZoneLabel;

	/// <summary>
	/// 播放区域面板的 GuiInput 事件是否已连接（防止重复连接/断开）。
	/// </summary>
	private bool _playZonePanelConnected;

	/// <summary>
	/// 播放区域 Y 坐标阈值比例（屏幕高度的百分比，从顶部算起）。
	/// 鼠标 Y 小于此阈值即认为卡牌在播放区域内。STS2 基准 75%，本项目用 60% 以适应较小的棋盘布局。
	/// </summary>
	private const float PlayZoneBaseRatio = 0.60f;

	/// <summary>
	/// 取消区域比例——拖拽回到底部此比例以下视为取消（STS2 基准 95%）。
	/// 必须曾离开过取消区域（上滑进入播放区）再回来才触发，防止开局误触。
	/// </summary>
	private const float CancelZoneScreenProportion = 0.95f;

	/// <summary>进入无目标模式时的拖拽起始 Y 坐标（用于计算自适应 PlayZone 阈值）。</summary>
	private float _playZoneDragStartY;

	/// <summary>是否曾离开过取消区域（必须上滑过才能触发取消，防止开局误触）。</summary>
	private bool _hasLeftCancelZone;

	// ===== Godot 生命周期 =====

	/// <summary>
	/// Godot 节点就绪回调。创建布局并将自身加入 "CombatUI" 分组。
	/// 订阅分辨率变化事件以支持自适应布局。
	/// </summary>
	public override void _Ready()
	{
		Name = "CombatUI";
		AddToGroup("CombatUI");
		GD.Print("[CombatUI] _Ready");

		BuildLayout();

		// 订阅分辨率变化——窗口缩放时重新计算尺寸
		if (UIScaler.Instance != null)
		{
			UIScaler.Instance.OnResolutionChanged += OnResolutionChanged;
		}
	}

	/// <summary>
	/// 右键取消——必须在 _Input 层处理。BoardSlot/CardUI 的 _GuiInput.AcceptEvent()
	/// 会吞噬右键事件，导致 _UnhandledInput 永远收不到右键。
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (!MobileInputRouter.IsMobile && @event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
		{
		if (_selectionMode == SelectionMode.SelectingAttackTarget || _selectionMode == SelectionMode.SelectingWeaponTarget)
			{
				GD.Print($"[CombatUI] 右键取消攻击选择，mode={_selectionMode}");
				_attackDragFsm.Cancel(); // 触发 OnCancel → OnAttackDragCancel → ResetSelection + RefreshHand
				GetViewport().SetInputAsHandled();
			}
			else if (_selectionMode == SelectionMode.DevDamageTargeting)
			{
				ExitDevDamageMode();
			}
			else if (_selectionMode == SelectionMode.PlacingMinion
				|| _selectionMode == SelectionMode.TargetingSpell
				|| _selectionMode == SelectionMode.PlayingNoTargetCard)
			{
				GD.Print("[CombatUI] 右键取消卡牌打出选择");
				OnCardDragCancelled();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	/// <summary>
	/// 全局输入处理——桌面端右键取消（移动端使用专用取消按钮替代右键）。
	/// 键盘热键已迁移至 HotkeyManager 回调，此方法仅处理鼠标右键。
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsInsideTree())
			return;

		// 手牌选择模式——桌面端右键取消选择（移动端用取消按钮）
		if (_isHandSelecting)
		{
			// 桌面端右键取消——移动端无右键，跳过
			if (!MobileInputRouter.IsMobile)
			{
				if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
				{
					_combat?.CancelHandDiscardSelection();
					GetViewport().SetInputAsHandled();
					return;
				}
			}
		}

		// 开发者伤害模式——桌面端右键取消（移动端用取消按钮）
		if (!MobileInputRouter.IsMobile)
		{
			if (@event is InputEventMouseButton mb2
				&& mb2.ButtonIndex == MouseButton.Right
				&& mb2.Pressed
				&& _selectionMode == SelectionMode.DevDamageTargeting)
			{
				ExitDevDamageMode();
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		// 攻击目标选择模式——桌面端右键取消（移动端用取消按钮）
		if (!MobileInputRouter.IsMobile)
		{
			if (@event is InputEventMouseButton mb3
				&& mb3.ButtonIndex == MouseButton.Right
				&& mb3.Pressed
				&& (_selectionMode == SelectionMode.SelectingAttackTarget || _selectionMode == SelectionMode.SelectingWeaponTarget))
			{
				GD.Print($"[CombatUI] _UnhandledInput 右键取消攻击选择，mode={_selectionMode}");
				_attackDragFsm.Cancel(); // 触发 OnCancel → OnAttackDragCancel → ResetSelection + RefreshHand
				GetViewport().SetInputAsHandled();
				return;
			}
		}
	}

	private void OnWeaponAttackButtonGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mb || mb.ButtonIndex != MouseButton.Left || !mb.Pressed)
			return;

		if (_combat == null || _combat.State.IsGameOver)
			return;

		if (_selectionMode == SelectionMode.SelectingWeaponTarget)
		{
			GD.Print("[CombatUI] 武器攻击按钮再次点击，取消目标选择");
			_ignoreNextWeaponAttackPressed = true;
			_attackDragFsm.Cancel();
			RefreshAll();
			GetViewport().SetInputAsHandled();
			return;
		}

		GD.Print($"[CombatUI] 武器攻击按钮左键按下，进入可拖拽目标选择，pos=({mb.GlobalPosition.X:F0},{mb.GlobalPosition.Y:F0})");
		_ignoreNextWeaponAttackPressed = true;
		EnterWeaponAttackTargetMode(mb.GlobalPosition);
		GetViewport().SetInputAsHandled();
	}

	/// <summary>
	/// 分辨率变化时刷新所有 UI 尺寸和布局，包括手牌区域高度。
	/// </summary>
	private void OnResolutionChanged()
	{
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		GD.Print($"[CombatUI] 分辨率变化 — 缩放因子 {s:F2}");

		// 更新手牌区域高度以保持折叠态露出比例一致
		if (_handArea != null)
		{
			_handArea.CustomMinimumSize = new Vector2(0, HandUI.COLLAPSED_VISIBLE * s);
		}

		RefreshAll();
	}

	/// <summary>
	/// 每帧更新攻击选择箭头——从攻击方随从槽位指向当前输入位置。
	/// 仅在 SelectingAttackTarget 模式下有效。
	/// 同时追踪攻击拖拽状态：按住拖动超过阈值→松手时执行攻击或取消。
	/// 移动端使用 MobileInputRouter 触控状态替代鼠标轮询。
	/// </summary>
	public override void _Process(double delta)
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;

		// --- 攻击选择箭头 ---
		if ((_selectionMode == SelectionMode.SelectingAttackTarget && _selectedAttacker != null)
			|| _selectionMode == SelectionMode.SelectingWeaponTarget)
		{
			if (_arrowRenderer != null)
			{
				var sourcePos = _selectionMode == SelectionMode.SelectingWeaponTarget
					? _weaponAttackButton.GlobalPosition + _weaponAttackButton.Size * 0.5f
					: GetMinionScreenCenter(_selectedAttacker!);
				var inputPos = GetInputPosition();
				_arrowRenderer.AddArrow("attack_select", sourcePos, inputPos, ArrowRenderer.AttackSelectColor);
			}
		}
		else if (_arrowRenderer != null && _arrowRenderer.HasArrow("attack_select"))
		{
			_arrowRenderer.RemoveArrow("attack_select");
		}

		// --- 卡牌目标选择箭头（随从放置 / 单目标法术） ---
		if ((_selectionMode == SelectionMode.PlacingMinion || _selectionMode == SelectionMode.TargetingSpell)
			&& _dragCardUI != null && _arrowRenderer != null)
		{
			Vector2 sourcePos = _dragCardUI.GlobalPosition + _dragCardUI.Size * _dragCardUI.Scale * 0.5f;
			_arrowRenderer.AddArrow(CardTargetArrowKey, sourcePos, GetInputPosition(), ArrowRenderer.AttackSelectColor);
		}
		else if (_arrowRenderer != null && _arrowRenderer.HasArrow(CardTargetArrowKey))
		{
			_arrowRenderer.RemoveArrow(CardTargetArrowKey);
		}

		// --- 攻击拖拽追踪（委托给 InteractionFsm） ---
		if (_attackDragFsm.CurrentPhase != InteractionPhase.Idle)
		{
			var inputPos = GetInputPosition();
			// 攻击拖拽始终通过主指针（鼠标/触控）追踪——Godot 移动端触控→鼠标模拟保证一致性
			bool pointerDown = Input.IsMouseButtonPressed(MouseButton.Left);

			float viewportH = GetViewportRect().Size.Y;
			_attackDragFsm.Tick(inputPos, pointerDown, isRightDown: false, viewportH, dragStartY: 0f);
		}

		// --- 移动端取消按钮可见性 ---
		UpdateMobileCancelButton();
	}

	// ===== 攻击拖拽 FSM 事件处理 =====

	/// <summary>
	/// 攻击拖拽移动回调——箭头渲染由 <see cref="_Process"/> 中的 GetInputPosition 处理。
	/// </summary>
	private void OnAttackDragMove(Vector2 pos, bool inPlay, bool inCancel)
	{
		// 箭头渲染已通过 _Process 中的 GetInputPosition() 实时绘制，无需额外处理。
	}

	/// <summary>
	/// 攻击拖拽松手回调——区分快速点击（保持选中等待第二击）和拖拽松手（执行攻击）。
	/// </summary>
	private void OnAttackDragDrop(Vector2 pos, bool wasDrag)
	{
		if (wasDrag && (_selectionMode == SelectionMode.SelectingAttackTarget || _selectionMode == SelectionMode.SelectingWeaponTarget))
		{
			HandleAttackDrop(pos);
		}
		// else: 快速点击无拖拽 → 保持选中状态，等待玩家第二击（现有行为）
	}

	/// <summary>
	/// 攻击拖拽取消回调（右键/拖回底部/ESC）。
	/// </summary>
	private void OnAttackDragCancel()
	{
		ResetSelection();
		_handUI.RefreshHand();
	}

	// ===== 初始化 =====

	/// <summary>
	/// 初始化战斗界面，绑定所有子组件和事件订阅。
	/// 此方法在 CombatManager.Initialize 之后调用。
	/// </summary>
	/// <param name="player">玩家角色</param>
	/// <param name="combat">战斗管理器</param>
	public void Initialize(Player player, CombatManager combat)
	{
		_player = player ?? throw new ArgumentNullException(nameof(player));
		_combat = combat ?? throw new ArgumentNullException(nameof(combat));

		var defaultEnemy = combat.GetDefaultEnemyTargetUnit()?.Body;
		string enemyHealthText = defaultEnemy != null
			? $"敌方生命 {defaultEnemy.CurrentHealth}/{defaultEnemy.MaxHealth}"
			: "无存活敌方英雄";
		GD.Print($"[CombatUI] 初始化 — 玩家生命 {combat.PlayerHero.CurrentHealth}/{combat.PlayerHero.MaxHealth}，{enemyHealthText}");

		// 攻击/法术弹道层（低于跳字，纯表现，不拦截输入）
		var attackVfxCanvasLayer = new CanvasLayer { Name = "AttackVfxLayer", Layer = 14 };
		AddChild(attackVfxCanvasLayer);
		_attackVfxLayer = new Control { Name = "AttackVfxContainer", MouseFilter = MouseFilterEnum.Ignore };
		_attackVfxLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		attackVfxCanvasLayer.AddChild(_attackVfxLayer);

		// 伤害跳字层（介于棋盘效果层和拖拽层之间，Layer=15）
		var damageNumberLayer = new CanvasLayer { Name = "DamageNumberLayer", Layer = 15 };
		AddChild(damageNumberLayer);
		_damageNumberContainer = new Control { Name = "DamageNumberContainer", MouseFilter = MouseFilterEnum.Ignore };
		damageNumberLayer.AddChild(_damageNumberContainer);

		// 卡牌飞行 VFX 层（Layer=20，高于伤害跳字，低于拖拽层 Z=100）
		var cardFlyLayer = new CanvasLayer { Name = "CardFlyLayer", Layer = 20 };
		AddChild(cardFlyLayer);
		_cardFlyLayer = new Control { Name = "CardFlyContainer", MouseFilter = MouseFilterEnum.Ignore };
		_cardFlyLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		cardFlyLayer.AddChild(_cardFlyLayer);

		// 随从部署动画层（Layer=18，介于伤害跳字 15 和卡牌飞行 20 之间）
		var deployVfxLayer = new CanvasLayer { Name = "DeployVfxLayer", Layer = 18 };
		AddChild(deployVfxLayer);
		_deployVfxLayer = new Control { Name = "DeployVfxContainer", MouseFilter = MouseFilterEnum.Ignore };
		_deployVfxLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		deployVfxLayer.AddChild(_deployVfxLayer);

		// 意图悬浮提示层（Layer=26，高于意图图标）
		var intentTooltipLayer = new CanvasLayer { Name = "IntentTooltipLayer", Layer = 26 };
		var tooltipParent = new Control { Name = "IntentTooltipContent", MouseFilter = MouseFilterEnum.Ignore };
		tooltipParent.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		intentTooltipLayer.AddChild(tooltipParent);
		AddChild(intentTooltipLayer);

		// 创建并初始化子组件
		SetupBoardUI();
		SetupHandUI();
		CreateHealthBars();
		CreateManaLabels();
		CreateArmorLabels();
		CreateEndTurnButton();
		CreateHeroPowerButton();
		CreatePlayerHeroPanel();
		CreateDeckButtons();
		CreateWeaponUI();
		CreateStatusEffectUI();
		CreateHeatBar();
		CreateRelicBar();
		CreateEnemyCards();
		CreateGameOverPopup();
		CreatePlayZonePanel();

		// 订阅事件
		SubscribeEvents();

		// 英雄伤害跳字
		SubscribeHeroDamageEvents(_combat.PlayerHero, -1);
		for (int i = 0; i < _combat.EnemyUnits.Count; i++)
		{
			SubscribeHeroDamageEvents(_combat.EnemyUnits[i].Body, i);
		}

		// 首次刷新
		RefreshAll();

		// 订阅语言变更事件
		GameManager.Instance.LanguageChanged += OnLanguageChanged;

		// 订阅热键（HotkeyManager 回调）
		SubscribeHotkeys();

		GD.Print("[CombatUI] 初始化完成");
	}

	/// <summary>
	/// 退出场景树时取消语言变更事件订阅。
	/// </summary>
	public override void _ExitTree()
	{
		if (UIScaler.Instance != null)
		{
			UIScaler.Instance.OnResolutionChanged -= OnResolutionChanged;
		}

		foreach (var unsubscribe in _unsubscribeActions)
		{
			unsubscribe();
		}
		_unsubscribeActions.Clear();
		UnsubscribeAllDamageEvents();

		if (GameManager.Instance != null)
			GameManager.Instance.LanguageChanged -= OnLanguageChanged;
	}

	/// <summary>
	/// 语言变更时刷新所有 UI 文本标签。
	/// </summary>
	private void OnLanguageChanged(string lang)
	{
		RefreshAll();
	}

	// ===== 子组件创建（依赖 _combat 运行时数据） =====

	/// <summary>
	/// 为每个敌方单位创建一张 EnemyIdentityCard 并插入到 EnemyArea。
	/// 在 Initialize 中 _combat 已设置后调用。
	/// </summary>
	private void CreateEnemyCards()
	{
		var enemyArea = GetNode<HBoxContainer>("CombatRoot/EnemyArea");

		for (int i = 0; i < _combat.EnemyUnits.Count; i++)
		{
			var card = new EnemyIdentityCard(i, _combat);
			_enemyCards.Add(card);

			// 插入到暂停按钮之前
			int insertIndex = enemyArea.GetChildCount() - 1; // before pause button
			enemyArea.AddChild(card);
			enemyArea.MoveChild(card, insertIndex);
		}

		// 首个敌人的按钮作为向后兼容引用
		if (_enemyCards.Count > 0)
		{
			_enemyHeroAttackButton = _enemyCards[0].AttackButton;
			_enemyHeroSpellButton = _enemyCards[0].SpellButton;
		}
	}

	/// <summary>
	/// 设置棋盘 UI 绑定——将 BoardUI 关联到 CombatManager.Board。
	/// </summary>
	private void SetupBoardUI()
	{
		_board = _combat.Board;
		_boardUI.SetBoard(_board);
	}

	/// <summary>
	/// 设置手牌 UI 绑定——初始化并刷新。
	/// </summary>
	private void SetupHandUI()
	{
		_handUI.Initialize(_player, _combat);
	}

	// ===== 事件订阅 =====

	/// <summary>
	/// 订阅所有子组件事件。
	/// </summary>
	private void SubscribeEvents()
	{
		// 棋盘槽位点击
		_boardUI.OnSlotClicked += OnBoardSlotClicked;
		_unsubscribeActions.Add(() => _boardUI.OnSlotClicked -= OnBoardSlotClicked);

		// 棋盘槽位右键（取消攻击选择）
		_boardUI.OnSlotRightClicked += OnBoardSlotRightClicked;
		_unsubscribeActions.Add(() => _boardUI.OnSlotRightClicked -= OnBoardSlotRightClicked);

		// 手牌卡牌选中
		_handUI.OnCardSelectedForPlay += OnCardSelectedFromHand;
		_unsubscribeActions.Add(() => _handUI.OnCardSelectedForPlay -= OnCardSelectedFromHand);

		// 手牌取消（右键）
		_handUI.OnCardCancelled += OnCardDragCancelled;
		_unsubscribeActions.Add(() => _handUI.OnCardCancelled -= OnCardDragCancelled);

		// 回合结束按钮
		_endTurnButton.Pressed += OnEndTurnPressed;
		_unsubscribeActions.Add(() => _endTurnButton.Pressed -= OnEndTurnPressed);

		// 英雄技能按钮
		_heroPowerButton.Pressed += OnHeroPowerPressed;
		_unsubscribeActions.Add(() => _heroPowerButton.Pressed -= OnHeroPowerPressed);

		// 敌方身份卡点击攻击（按钮 + 整个卡片覆盖层）
		// 每个敌人独立创建闭包，捕获 enemyIndex 传递给统一分发入口
		_enemyHeroCardAction = OnEnemyHeroAttackPressed;
		foreach (var enemyCard in _enemyCards)
		{
			int idx = enemyCard.EnemyIndex;
			void OnAttackButton() => OnEnemyHeroCardActionPressed(idx);
			void OnAttackOverlay(int _) => OnEnemyHeroCardActionPressed(idx);
			enemyCard.AttackButton.Pressed += OnAttackButton;
			enemyCard.OnAttackTargetClicked += OnAttackOverlay;
			_unsubscribeActions.Add(() => enemyCard.AttackButton.Pressed -= OnAttackButton);
			_unsubscribeActions.Add(() => enemyCard.OnAttackTargetClicked -= OnAttackOverlay);
		}

		// 对敌方英雄施法按钮 — 每个敌人独立闭包，捕获 enemyIndex
		foreach (var enemyCard in _enemyCards)
		{
			int idx = enemyCard.EnemyIndex;
			void OnSpellButton() => OnEnemyHeroSpellTargetForIndex(idx);
			enemyCard.SpellButton.Pressed += OnSpellButton;
			_unsubscribeActions.Add(() => enemyCard.SpellButton.Pressed -= OnSpellButton);
		}

		// 对己方英雄施法按钮
		_playerHeroSpellButton.Pressed += OnPlayerHeroSpellTarget;
		_unsubscribeActions.Add(() => _playerHeroSpellButton.Pressed -= OnPlayerHeroSpellTarget);

		// 武器攻击按钮
		_weaponAttackButton.GuiInput += OnWeaponAttackButtonGuiInput;
		_unsubscribeActions.Add(() => _weaponAttackButton.GuiInput -= OnWeaponAttackButtonGuiInput);
		_weaponAttackButton.Pressed += OnWeaponAttackPressed;
		_unsubscribeActions.Add(() => _weaponAttackButton.Pressed -= OnWeaponAttackPressed);

		// 武器主动技能按钮
		_weaponActiveSkillButton.Pressed += OnWeaponActiveSkillPressed;
		_unsubscribeActions.Add(() => _weaponActiveSkillButton.Pressed -= OnWeaponActiveSkillPressed);

		// 牌堆/手牌状态变化 → 自动刷新 UI
		_combat.PlayerHero.DeckState.OnDrawPileChanged += UpdateDeckCounts;
		_unsubscribeActions.Add(() => _combat.PlayerHero.DeckState.OnDrawPileChanged -= UpdateDeckCounts);
		_combat.PlayerHero.DeckState.OnDiscardPileChanged += UpdateDeckCounts;
		_unsubscribeActions.Add(() => _combat.PlayerHero.DeckState.OnDiscardPileChanged -= UpdateDeckCounts);

		// 快照当前手牌——用于后续 OnHandChanged 时 diff 检测新抽到的牌
		_previousHandCards = new List<OdysseyCards.Card.Card>(_combat.PlayerHero.Hand);

		_combat.PlayerHero.DeckState.OnHandChanged += OnHandChanged;
		_unsubscribeActions.Add(() => _combat.PlayerHero.DeckState.OnHandChanged -= OnHandChanged);

		// 法力值变化 → 自动更新显示
		_combat.PlayerHero.OnManaChanged += OnManaChanged;
		_unsubscribeActions.Add(() => _combat.PlayerHero.OnManaChanged -= OnManaChanged);

		// 敌方意图变化 → 更新意图显示和箭头
		_combat.OnCombatStateChanged += OnCombatStateChangedRefresh;
		_unsubscribeActions.Add(() => _combat.OnCombatStateChanged -= OnCombatStateChangedRefresh);

		// 主动伤害弹道特效 — 规则层只发请求，UI 层非阻塞播放
		_combat.OnDamageVfxRequested += OnDamageVfxRequested;
		_unsubscribeActions.Add(() => _combat.OnDamageVfxRequested -= OnDamageVfxRequested);

		// 发现选牌阶段切换
		_combat.OnCombatStateChanged += OnCombatStateChangedForDiscover;
		_unsubscribeActions.Add(() => _combat.OnCombatStateChanged -= OnCombatStateChangedForDiscover);

		// 游戏结束 → 显示弹窗
		_combat.OnGameOver += ShowGameOverPopup;
		_unsubscribeActions.Add(() => _combat.OnGameOver -= ShowGameOverPopup);

		// 随从伤害跳字 — 随从放置时通过闭包捕获 minion 引用订阅事件
		_board!.OnMinionPlaced += OnBoardMinionPlacedSubscribeDamage;
		_unsubscribeActions.Add(() => _board.OnMinionPlaced -= OnBoardMinionPlacedSubscribeDamage);
		// 随从部署动画 — 双方随从上场时播放飞行→落地动画
		_board.OnMinionPlaced += OnBoardMinionPlacedForDeployAnimation;
		_unsubscribeActions.Add(() => _board.OnMinionPlaced -= OnBoardMinionPlacedForDeployAnimation);
		_board.OnMinionRemoved += OnBoardMinionRemovedUnsubscribeDamage;
		_unsubscribeActions.Add(() => _board.OnMinionRemoved -= OnBoardMinionRemovedUnsubscribeDamage);

		// 随从死亡飞行动画 — 在槽位清空前获取屏幕坐标
		_board.OnMinionPreRemove += OnBoardMinionPreRemove;
		_unsubscribeActions.Add(() => _board.OnMinionPreRemove -= OnBoardMinionPreRemove);

		// 敌方表情 — 空闲超时或 DevConsole 触发
		_combat.OnEnemyEmote += OnEnemyEmote;
		_unsubscribeActions.Add(() => _combat.OnEnemyEmote -= OnEnemyEmote);

		// 攻击拖拽 FSM 事件订阅
		_attackDragFsm.OnDragMove += OnAttackDragMove;
		_unsubscribeActions.Add(() => _attackDragFsm.OnDragMove -= OnAttackDragMove);
		_attackDragFsm.OnDrop += OnAttackDragDrop;
		_unsubscribeActions.Add(() => _attackDragFsm.OnDrop -= OnAttackDragDrop);
		_attackDragFsm.OnCancel += OnAttackDragCancel;
		_unsubscribeActions.Add(() => _attackDragFsm.OnCancel -= OnAttackDragCancel);

	}

	/// <summary>
	/// 注册热键回调——通过 HotkeyManager 将键盘输入映射到 UI 操作。
	/// 所有回调通过 _unsubscribeActions 在 _ExitTree 时自动解绑。
	/// </summary>
	private void SubscribeHotkeys()
	{
		var hm = HotkeyManager.Instance;
		if (hm == null)
			return;

		hm.PushPressedBinding(OdysseyInput.EndTurn, TryEndTurn);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.EndTurn, TryEndTurn));

		hm.PushPressedBinding(OdysseyInput.HeroPower, TryHeroPower);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.HeroPower, TryHeroPower));

		hm.PushPressedBinding(OdysseyInput.ViewDeck, ShowDrawPileView);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.ViewDeck, ShowDrawPileView));

		hm.PushPressedBinding(OdysseyInput.ViewDiscard, ShowDiscardPileView);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.ViewDiscard, ShowDiscardPileView));

		hm.PushPressedBinding(OdysseyInput.Pause, TogglePause);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.Pause, TogglePause));

		hm.PushPressedBinding(OdysseyInput.InfoScreen, ToggleInfoScreen);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.InfoScreen, ToggleInfoScreen));

		hm.PushPressedBinding(OdysseyInput.Cancel, HandleCancel);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.Cancel, HandleCancel));
	}

	private void OnHandChanged()
	{
		var currentHand = _combat.PlayerHero.Hand;

		// 检测新抽到的卡牌（当前手牌减去上次快照）
		var newCards = currentHand.Except(_previousHandCards).ToList();

		if (newCards.Count > 0)
		{
			PlayDrawCardAnimations(newCards, currentHand.Count);
		}

		// 更新快照以供下次 diff
		_previousHandCards = new List<OdysseyCards.Card.Card>(currentHand);

		_handUI.RefreshHand();
	}

	/// <summary>
	/// 播放抽牌飞行特效：为每张刚抽到的卡牌创建从抽牌堆到目标手牌位置的贝塞尔曲线飞行动画。
	/// 动画在独立的 CardFlyLayer (CanvasLayer=20) 上运行，与手牌 UI 刷新（RefreshHand）并行。
	/// 参考 STS2 的 NCardFlyVfx 装饰性抽牌动画模式——动画只负责视觉过渡，
	/// 不影响实际手牌数据模型。
	/// </summary>
	/// <param name="newCards">刚抽到的卡牌列表（顺序：先抽到的在前，即手牌中靠左）</param>
	/// <param name="totalHandSize">抽牌后的手牌总数，用于计算每张牌在风扇布局中的目标位置</param>
	private void PlayDrawCardAnimations(List<OdysseyCards.Card.Card> newCards, int totalHandSize)
	{
		if (_cardFlyLayer == null || _drawPileBtn == null || newCards.Count == 0)
			return;

		// 抽牌堆按钮的屏幕中心——所有飞行起始点
		Vector2 drawPileCenter = _drawPileBtn.GlobalPosition + _drawPileBtn.Size * 0.5f;

		for (int i = 0; i < newCards.Count; i++)
		{
			// 新卡在手中从末尾开始排列（最后一张的索引 = totalHandSize - 1）
			int handIndex = totalHandSize - newCards.Count + i;
			Vector2 targetPos = _handUI.GetHandCardGlobalCenter(handIndex, totalHandSize);

			CardFlyVfx.PlayDrawToHand(newCards[i], drawPileCenter, targetPos, _cardFlyLayer);
		}
	}

	private void OnManaChanged(int currentMana, int maxMana)
	{
		UpdateManaDisplay();
	}

	private void OnCombatStateChangedRefresh()
	{
		RefreshIntentDisplay();
		RefreshIntentArrows();
	}

	private void OnDamageVfxRequested(object? visualSource, IDamageTarget target, DamageKind kind, CombatDamageVfxKind vfxKind)
	{
		if (_attackVfxLayer == null)
			return;

		Vector2 from = ResolveVfxSourceScreenPos(visualSource, vfxKind);
		Vector2 to = ResolveTargetScreenPos(target);
		if (from == Vector2.Zero || to == Vector2.Zero)
			return;

		AttackProjectileVfx.Play(from, to, vfxKind, _attackVfxLayer);
	}

	private Vector2 ResolveVfxSourceScreenPos(object? visualSource, CombatDamageVfxKind vfxKind)
	{
		return visualSource switch
		{
			Minion minion => GetMinionScreenCenter(minion),
			Hero hero => ResolveTargetScreenPos(hero),
			Card.Card card when ReferenceEquals(card, _pendingSpellVfxCard) && _pendingSpellVfxOrigin != Vector2.Zero => _pendingSpellVfxOrigin,
			Card.Card => GetPlayerHeroScreenCenter(),
			_ when vfxKind == CombatDamageVfxKind.Spell => GetPlayerHeroScreenCenter(),
			_ => GetPlayerHeroScreenCenter(),
		};
	}

	private bool PlaySelectedSpellWithVfxOrigin(object target)
	{
		if (_selectedCard == null)
			return false;

		_pendingSpellVfxCard = _selectedCard;
		_pendingSpellVfxOrigin = ResolveSelectedCardVfxOrigin(_selectedCard);
		try
		{
			return _combat.PlaySpell(_selectedCard, target);
		}
		finally
		{
			_pendingSpellVfxCard = null;
			_pendingSpellVfxOrigin = Vector2.Zero;
		}
	}

	private Vector2 ResolveSelectedCardVfxOrigin(Card.Card card)
	{
		if (_dragCardUI != null)
		{
			var rect = _dragCardUI.GetGlobalRect();
			return rect.Position + rect.Size * 0.5f;
		}

		var hand = _combat.PlayerHero.Hand;
		for (int i = 0; i < hand.Count; i++)
		{
			if (ReferenceEquals(hand[i], card))
				return _handUI.GetHandCardGlobalCenter(i, hand.Count);
		}

		return GetPlayerHeroScreenCenter();
	}

	/// <summary>
	/// 敌方表情事件——在第一个敌人身份卡上方显示浮动表情文本。
	/// </summary>
	private void OnEnemyEmote(string text)
	{
		if (string.IsNullOrEmpty(text))
			return;
		var pos = GetEnemyIdentityCardCenter(0);
		GD.Print($"[CombatUI] 收到表情「{text}」，敌人位置={pos}");
		if (pos == Vector2.Zero)
		{
			GD.PrintErr("[CombatUI] 表情位置无效（_enemyCards 可能为空）");
			return;
		}
		// 敌人身份卡在屏幕顶部，表情显示在卡片下方
		pos.Y += 70;
		FloatingEmote.Show(text, pos, _damageNumberContainer);
	}

	private void OnBoardMinionPlacedSubscribeDamage(Minion minion, int slotIndex)
	{
		SubscribeMinionDamageEvents(minion);
	}

	/// <summary>
	/// 随从放置时触发部署动画——从来源中心飞向棋盘槽位中心，落地弹性缩放。
	/// 玩家侧：动画由 HandleMinionPlacement 在 RefreshAll 前显式调用，使用拖拽卡牌位置作为起点。
	/// 敌方侧：由 Board.OnMinionPlaced 事件触发，从屏幕上方飞入。
	/// </summary>
	private void OnBoardMinionPlacedForDeployAnimation(Minion minion, int slotIndex)
	{
		// 玩家侧：由 HandleMinionPlacement 显式调用，不在此处重复触发
		if (minion.IsPlayerSide)
			return;

		// 敌方侧：从屏幕上方飞入
		var card = minion.ToRuntimeCard();
		var viewportSize = GetViewportRect().Size;
		Vector2 fromPos = new(viewportSize.X * 0.5f, -CardUI.DESIGN_HEIGHT * 0.5f);
		PlayMinionDeployAnimation(card, fromPos, slotIndex, isPlayerSide: false);
	}

	/// <summary>
	/// 播放随从部署动画：在 fromPos 创建临时 CardUI，沿贝塞尔曲线飞向目标槽位，
	/// 到达后弹性缩放落地并溅射微尘粒子。动画不阻塞游戏操作。
	/// </summary>
	/// <param name="card">要展示的卡牌数据（仅用于渲染）</param>
	/// <param name="fromPos">动画起始屏幕中心位置</param>
	/// <param name="slotIndex">目标槽位索引（0-4）</param>
	/// <param name="isPlayerSide">是否玩家方（控制贝塞尔曲线拱起方向）</param>
	public void PlayMinionDeployAnimation(OdysseyCards.Card.Card card, Vector2 fromPos, int slotIndex, bool isPlayerSide)
	{
		Rect2 slotRect = _boardUI.GetSlotScreenRect(slotIndex, isPlayerSide);
		if (slotRect.Size == Vector2.Zero)
		{
			GD.Print("[CombatUI] PlayMinionDeployAnimation 跳过——目标槽位屏幕坐标无效");
			return;
		}

		Vector2 toPos = slotRect.Position + slotRect.Size * 0.5f;
		float speed = UIScaler.Instance?.DeployAnimationSpeed ?? 1.0f;
		MinionDeployVfx.Play(card, fromPos, toPos, slotRect.Size, isPlayerSide, speed, _deployVfxLayer);
	}

	private void OnBoardMinionRemovedUnsubscribeDamage(Minion minion)
	{
		UnsubscribeMinionDamageEvents(minion);
	}

	private void OnBoardMinionPreRemove(Minion minion, int slotIndex, bool isPlayerSide)
	{
		if (!isPlayerSide)
			return;

		var cardPos = _boardUI.GetSlotScreenCenter(slotIndex, isPlayerSide);
		if (cardPos == Vector2.Zero)
			return;

		var cardUI = new CardUI { DisplayOnly = true };
		cardUI.SetCard(minion.ToRuntimeCard());
		cardUI.GlobalPosition = cardPos;

		bool toDrawPile = minion.HasRecycle;
		Vector2 targetPos = toDrawPile ? GetDrawPileCenter() : GetDiscardPileCenter();
		CardFlyVfx.PlayToDiscard(cardUI, targetPos, _cardFlyLayer);
	}

	/// <summary>
	/// 为英雄订阅伤害/治疗事件，在其上方生成跳字。
	/// </summary>
	/// <param name="hero">英雄实例</param>
	/// <param name="enemyIndex">敌方英雄索引（玩家英雄传 -1）</param>
	private void SubscribeHeroDamageEvents(Hero hero, int enemyIndex)
	{
		Action<DamageEventInfo, IDamageSource?> onDamage = (info, source) =>
		{
			var pos = ResolveTargetScreenPos(hero);
			if (pos != Vector2.Zero)
				FloatingDamageNumber.CreateDamage(info, pos, _damageNumberContainer);
		};

		Action<int> onHeal = amount =>
		{
			var pos = ResolveTargetScreenPos(hero);
			if (pos != Vector2.Zero)
				FloatingDamageNumber.CreateHeal(amount, pos, _damageNumberContainer);
		};

		hero.OnDamageTaken += onDamage;
		hero.OnHealed += onHeal;
		_heroDamageHandlers[hero] = (onDamage, onHeal);
	}

	private void SubscribeMinionDamageEvents(Minion minion)
	{
		if (_minionDamageHandlers.ContainsKey(minion))
			return;

		Action<DamageEventInfo, IDamageSource?> onDamage = (info, source) =>
		{
			var pos = GetMinionScreenCenter(minion);
			if (pos != Vector2.Zero)
				FloatingDamageNumber.CreateDamage(info, pos, _damageNumberContainer);
		};

		Action<int> onHeal = amount =>
		{
			var pos = GetMinionScreenCenter(minion);
			if (pos != Vector2.Zero)
				FloatingDamageNumber.CreateHeal(amount, pos, _damageNumberContainer);
		};

		minion.OnDamageTaken += onDamage;
		minion.OnHealed += onHeal;
		_minionDamageHandlers[minion] = (onDamage, onHeal);
	}

	private void UnsubscribeMinionDamageEvents(Minion minion)
	{
		if (!_minionDamageHandlers.TryGetValue(minion, out var handlers))
			return;
		minion.OnDamageTaken -= handlers.Damage;
		minion.OnHealed -= handlers.Heal;
		_minionDamageHandlers.Remove(minion);
	}

	private void UnsubscribeAllDamageEvents()
	{
		foreach (var (hero, handlers) in _heroDamageHandlers)
		{
			hero.OnDamageTaken -= handlers.Damage;
			hero.OnHealed -= handlers.Heal;
		}
		_heroDamageHandlers.Clear();

		foreach (var (minion, handlers) in _minionDamageHandlers)
		{
			minion.OnDamageTaken -= handlers.Damage;
			minion.OnHealed -= handlers.Heal;
		}
		_minionDamageHandlers.Clear();
	}

	/// <summary>
	/// 刷新所有子组件——棋盘、手牌、生命值、法力值和护甲。
	/// 在每次操作完成后调用以确保界面与游戏状态同步。
	/// </summary>

	/// <summary>
	/// 刷新敌方意图显示——根据当前战场状态重新计算攻击目标和伤害数值。
	/// 若敌方回合动画进行中则跳过（冻结机制，参考 STS2 的 NIntent._isFrozen）。
	/// </summary>

	/// <summary>
	/// 刷新敌方意图箭头——根据当前战场状态绘制红色攻击箭头和蓝色增益箭头。
	/// 每次 OnCombatStateChanged 触发时调用。
	/// </summary>

	/// <summary>
	/// 从 MoveState 的 AbstractIntent 列表构建用于显示/箭头的 EnemyIntent。
	/// 新系统统一入口：Boss 与随从的意图均通过此方法转为显示格式。
	/// </summary>

	/// <summary>
	/// 更新双方英雄生命值条。
	/// </summary>

	/// <summary>
	/// 更新玩家法力值显示，格式「法力 Current/Max」。
	/// 敌人使用意图系统，不跟踪法力值。
	/// </summary>

	/// <summary>
	/// 更新双方护甲值显示——护甲 > 0 时显示标签，否则隐藏。
	/// </summary>

	/// <summary>
	/// 更新双方防御力显示——防御 != 0 时显示标签，否则隐藏。
	/// 正防御显示为蓝色（增益），负防御显示为红色（减益/脆弱）。
	/// </summary>

	// ===== 事件处理——回合结束 =====

	/// <summary>
	/// 回合结束按钮点击——结束当前玩家回合并刷新所有 UI。
	/// </summary>
	private void OnEndTurnPressed()
	{
		TryEndTurn();
	}

	/// <summary>
	/// 尝试结束当前回合（按钮或热键触发）。
	/// 比 OnEndTurnPressed 多做一层守卫：检查按钮禁用态、发现选牌、非玩家回合。
	/// </summary>
	private void TryEndTurn()
	{
		if (_combat == null)
			return;
		if (_combat.State.IsGameOver)
			return;
		if (_endTurnButton.Disabled)
			return;
		if (_combat.IsDiscovering)
			return;
		if (!_combat.State.IsPlayerTurn)
			return;

		GD.Print("[CombatUI] 热键结束回合");
		_combat.EndPlayerTurn();
		RefreshAll();
	}

	/// <summary>
	/// 英雄技能按钮点击处理。
	/// </summary>
	private void OnHeroPowerPressed()
	{
		TryHeroPower();
	}

	/// <summary>
	/// 尝试使用英雄技能（按钮或热键 H 触发）。
	/// </summary>
	private void TryHeroPower()
	{
		if (_combat == null)
			return;
		if (_combat.State.IsGameOver)
			return;
		if (_heroPowerButton.Disabled)
			return;
		if (_combat.IsDiscovering)
			return;
		if (!_combat.State.IsPlayerTurn)
			return;

		GD.Print("[CombatUI] 热键/按钮 — 尝试使用英雄技能");
		bool success = _combat.TryUseHeroPower();
		if (success)
		{
			RefreshAll();
		}
	}

	/// <summary>
	/// 显示游戏结束弹窗。
	/// 胜利：跳转至路线选择地图；失败：返回主菜单。
	/// </summary>
	/// <param name="isVictory">是否胜利</param>
	private void ShowGameOverPopup(bool isVictory)
	{
		if (_gameOverPopup == null)
			return;

		_isVictory = isVictory;

		if (isVictory)
		{
			_gameOverPopup.Title = Localization.Localization.T("ui.combat.victory_title", "★ 胜利！");
			_gameOverPopup.OkButtonText = Localization.Localization.T("ui.combat.continue_adventure", "继续冒险");
		}
		else
		{
			_gameOverPopup.Title = Localization.Localization.T("ui.combat.defeat_title", "☠ 失败");
			_gameOverPopup.OkButtonText = Localization.Localization.T("ui.combat.back_to_menu", "返回主菜单");
		}

		_gameOverPopup.PopupCentered();
		GD.Print($"[CombatUI] 游戏结束 — {(isVictory ? "胜利" : "失败")}");
	}

	/// <summary>
	/// 游戏结束弹窗确认回调。根据 <see cref="_isVictory"/> 决定跳转目标。
	/// 胜利：完成房间 → 路线选择地图；失败：返回主菜单。
	/// </summary>
	private void OnGameOverConfirmed()
	{
		// 战斗结束，清除牌组快照
		GameManager.Instance?.ClearCombatDeckSnapshot();

		if (_isVictory)
		{
			GD.Print("[CombatUI] 继续冒险 → 路线选择地图");
			var gm = GameManager.Instance;
			gm?.RunState?.CompleteRoom();
			gm?.SaveRun(); // 立即持久化完成状态，防止重启后运行仍可继续

			// 弹出战后奖励界面
			ShowPostBattleReward();
		}
		else
		{
			GD.Print("[CombatUI] 返回主菜单");
			GameManager.Instance?.ClearActiveRun();
			GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
		}
	}

	/// <summary>
	/// 显示战后卡牌奖励界面。
	/// 奖励选择完成后跳转到路线选择地图。
	/// </summary>
	private void ShowPostBattleReward()
	{
		var rewardUI = new RewardUI();
		rewardUI.OnRewardCompleted += () =>
		{
			GD.Print("[CombatUI] 奖励已选择 → 路线选择地图");
			GetTree().ChangeSceneToFile("res://Scenes/Map.tscn");
		};

		AddChild(rewardUI);
		rewardUI.ShowRewards();
	}

	// ===== 暂停菜单 =====

	/// <summary>
	/// 显示暂停菜单——创建全屏覆盖层，订阅其事件。
	/// </summary>
	private void ShowPauseMenu()
	{
		if (_pauseMenu != null)
			return;

		GD.Print("[CombatUI] 暂停菜单 — 显示");

		// 暂停时取消开发者伤害模式，避免恢复后状态混乱
		if (_selectionMode == SelectionMode.DevDamageTargeting)
		{
			ExitDevDamageMode();
		}

		_pauseMenu = new PauseMenu();
		_pauseMenu.OnContinue += HidePauseMenu;
		_pauseMenu.OnSaveAndExit += OnSaveAndExit;
		_pauseMenu.OnQuickSL += OnQuickSL;

		AddChild(_pauseMenu);
		_isPaused = true;

		// 真正暂停场景树——停止所有 Timer、_Process、_Input
		GetTree().Paused = true;
	}

	/// <summary>
	/// 隐藏暂停菜单——清理覆盖层。
	/// </summary>
	private void HidePauseMenu()
	{
		if (_pauseMenu == null)
			return;

		GD.Print("[CombatUI] 暂停菜单 — 关闭");

		// 恢复场景树处理
		GetTree().Paused = false;

		_pauseMenu.OnContinue -= HidePauseMenu;
		_pauseMenu.OnSaveAndExit -= OnSaveAndExit;
		_pauseMenu.OnQuickSL -= OnQuickSL;
		_pauseMenu.QueueFree();
		_pauseMenu = null;
		_isPaused = false;
	}

	/// <summary>
	/// 暂停按钮点击——显示暂停菜单。
	/// </summary>
	private void OnPauseButtonPressed()
	{
		if (_combat == null || _combat.State.IsGameOver)
			return;
		ShowPauseMenu();
	}

	/// <summary>
	/// 切换暂停状态（热键 ESC 或按钮触发）。
	/// 游戏结束、发现选牌或不在场景树内时不响应。
	/// </summary>
	private void TogglePause()
	{
		if (!IsInsideTree())
			return;
		if (_combat == null || _combat.State.IsGameOver)
			return;
		if (_combat.IsDiscovering)
			return;

		if (_isPaused)
			HidePauseMenu();
		else
			ShowPauseMenu();
	}

	// ===== 综合信息界面 =====

	/// <summary>
	/// 切换综合信息界面（CapsLock 热键触发）。
	/// 游戏结束、发现选牌或暂停菜单打开时不响应。
	/// </summary>
	private void ToggleInfoScreen()
	{
		if (!IsInsideTree())
			return;
		if (_combat == null || _combat.State.IsGameOver)
			return;
		if (_combat.IsDiscovering)
			return;
		if (_isPaused)
			return; // 暂停菜单打开时不响应

		if (_infoScreen != null)
			HideInfoScreen();
		else
			ShowInfoScreen();
	}

	/// <summary>
	/// 显示综合信息界面——创建覆盖层，注册其事件。
	/// </summary>
	private void ShowInfoScreen()
	{
		if (_infoScreen != null)
			return;

		GD.Print("[CombatUI] 综合信息界面 — 显示");

		_infoScreen = new InfoScreen();
		_infoScreen.OnClosed += HideInfoScreen;
		AddChild(_infoScreen);
		_infoScreen.Open();
	}

	/// <summary>
	/// 隐藏综合信息界面——销毁覆盖层，注销事件。
	/// </summary>
	private void HideInfoScreen()
	{
		if (_infoScreen == null)
			return;

		GD.Print("[CombatUI] 综合信息界面 — 关闭");

		_infoScreen.OnClosed -= HideInfoScreen;
		_infoScreen.QueueFree();
		_infoScreen = null;
	}

	/// <summary>
	/// 全局取消操作（热键 ESC/右键或移动端取消按钮触发）。
	/// 按优先级依次检查：手牌选择 → 开发者伤害 → 攻击/武器/法术选择。
	/// </summary>
	private void HandleCancel()
	{
		if (!IsInsideTree())
			return;

		// 手牌选择模式——取消弃牌选择
		if (_isHandSelecting)
		{
			_combat?.CancelHandDiscardSelection();
			return;
		}

		// 开发者伤害模式——退出
		if (_selectionMode == SelectionMode.DevDamageTargeting)
		{
			ExitDevDamageMode();
			return;
		}

		// 攻击/武器/主动技能目标选择模式——通过 FSM 取消
		if (_selectionMode == SelectionMode.SelectingAttackTarget
			|| _selectionMode == SelectionMode.SelectingWeaponTarget
			|| _selectionMode == SelectionMode.SelectingActiveSkillTarget)
		{
			GD.Print("[CombatUI] 热键取消攻击/武器/技能选择");
			_attackDragFsm.Cancel(); // 触发 OnCancel → OnAttackDragCancel → ResetSelection + RefreshHand
			return;
		}

		// 卡牌相关模式（法术目标、随从放置、无目标卡牌）——直接重置 + FSM 安全清理
		if (_selectionMode == SelectionMode.TargetingSpell
			|| _selectionMode == SelectionMode.PlacingMinion
			|| _selectionMode == SelectionMode.PlayingNoTargetCard)
		{
			GD.Print("[CombatUI] 热键取消卡牌选择");
			_attackDragFsm.ForceReset(); // 安全清理：确保攻击拖拽状态也重置
			ResetSelection();
			_handUI.RefreshHand();
			return;
		}
	}

	/// <summary>
	/// 「保存并退出」——保存玩家生命值到 GameManager，返回主菜单。
	/// 不标记当前房间为完成（RunState 保持不变，可重新进入此房间）。
	/// </summary>
	private void OnSaveAndExit()
	{
		GD.Print("[CombatUI] 保存并退出 → 返回主菜单");

		// 切换场景前必须先恢复暂停，否则新场景加载后按钮不响应
		GetTree().Paused = false;

		if (_combat != null)
		{
			var gm = GameManager.Instance;
			gm?.ClearCombatDeckSnapshot();
			gm?.SavePlayerHealth(_combat.PlayerHero.CurrentHealth, _combat.PlayerHero.MaxHealth);
		}

		GameManager.Instance?.SaveRun();
		GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
	}

	/// <summary>
	/// 「快速SL」——重载战斗场景，同一个房间从头开始。
	/// CombatManager.BootstrapCombat 会从 RunState 读取相同敌人类型重新初始化。
	/// </summary>
	private void OnQuickSL()
	{
		GD.Print("[CombatUI] 快速SL → 重新加载战斗场景");

		// 切换场景前必须先恢复暂停
		GetTree().Paused = false;

		// 清除战斗牌组快照（新战斗会重新创建）
		GameManager.Instance?.ClearCombatDeckSnapshot();

		GetTree().ChangeSceneToFile("res://Scenes/Combat.tscn");
	}


	/// <summary>
	/// 返回当前意图箭头调试信息，供 godot-mcp 手动验证使用。
	/// </summary>
	public string GetIntentArrowDebugInfo()
	{
		return _arrowRenderer?.GetDebugSnapshot() ?? "";
	}
}
