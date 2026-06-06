#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Infrastructure;
using Loc = OdysseyCards.Localization.Localization;

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
	/// 卡牌飞行 VFX 的父容器（独立 CanvasLayer，Layer=20），用于卡牌打出→弃牌堆飞行动画。
	/// </summary>
	private Control _cardFlyLayer = null!;

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

	// ===== 选择状态 =====

	/// <summary>
	/// 当前交互模式。
	/// </summary>
	private enum SelectionMode
	{
		/// <summary>默认——无选中，等待玩家操作。</summary>
		Normal,
		/// <summary>随从放置模式——手牌中选了一张随从牌，等待选择玩家槽位。</summary>
		PlacingMinion,
		/// <summary>法术目标模式——手牌中选了一张法术牌，等待选择目标。</summary>
		TargetingSpell,
		/// <summary>攻击目标模式——棋盘上选了一个己方随从，等待选择敌方目标。</summary>
		SelectingAttackTarget,
		/// <summary>武器攻击目标模式——玩家点击武器攻击后，等待选择敌方目标（随从或英雄）。</summary>
		SelectingWeaponTarget,
		/// <summary>武器主动技能目标模式——玩家点击主动技能后，等待选择敌方目标（随从或英雄）。无视嘲讽。</summary>
		SelectingActiveSkillTarget,
		/// <summary>无目标卡牌打出模式——拖拽到屏幕中央播放区域打出（类 STS2 风格）。</summary>
		PlayingNoTargetCard,
		/// <summary>开发者伤害模式——点击任意实体造成指定伤害。</summary>
		DevDamageTargeting,
	}

	private SelectionMode _selectionMode = SelectionMode.Normal;

	/// <summary>
	/// 当前从手牌中选中的卡牌（随从或法术）。
	/// </summary>
	private Card.Card? _selectedCard;

	/// <summary>
	/// 当前选中的攻击方随从（己方）。
	/// </summary>
	private Minion? _selectedAttacker;

	// ===== 攻击拖拽状态（点击选中 + 拖拽松手 双交互模式） =====

	/// <summary>
	/// 鼠标左键在当前攻击方随从槽位上按下中（未松开）。
	/// 用于区分「快速点击→选中等待第二击」和「按住拖动→松手攻击」。
	/// </summary>
	private bool _isAttackDragPressed;

	/// <summary>
	/// 攻击拖拽中鼠标位移是否已超过拖拽阈值。
	/// 未超过=快速点击，松手保持选中等待第二击；
	/// 超过=真正拖拽，松手时调用 HandleAttackDrop 执行攻击或取消。
	/// </summary>
	private bool _attackDragHasMoved;

	/// <summary>
	/// 攻击拖拽起始屏幕坐标（按下时的鼠标位置）。
	/// 用于计算是否超过 AttackDragThreshold。
	/// </summary>
	private Vector2 _attackDragStartPos;

	/// <summary>
	/// 攻击拖拽最小位移阈值（像素），与 CardUI.DragThreshold 一致。
	/// </summary>
	/// <summary>
	/// 攻击拖拽最小位移阈值（像素）。桌面端 10f，移动端 20f（触控精度较低，需更高阈值防误触）。
	/// </summary>
	private static float AttackDragThreshold => MobileInputRouter.IsMobile ? 20f : 10f;
	private bool _wasMobileAttackTouchActive;

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
	/// 鼠标 Y 小于此阈值即认为卡牌在播放区域内。默认 60%。
	/// </summary>
	private const float PlayZoneThresholdRatio = 0.60f;

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
			if (_selectionMode == SelectionMode.SelectingAttackTarget)
			{
				GD.Print("[CombatUI] 右键取消攻击选择");
				ResetSelection();
				_handUI.RefreshHand();
			}
			else if (_selectionMode == SelectionMode.DevDamageTargeting)
			{
				ExitDevDamageMode();
			}
		}
	}

	/// <summary>
	/// 全局输入处理——桌面端右键取消（移动端使用专用取消按钮替代右键）。
	/// 键盘热键已迁移至 HotkeyManager 回调，此方法仅处理鼠标右键。
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsInsideTree()) return;

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
				&& _selectionMode == SelectionMode.SelectingAttackTarget)
			{
				GD.Print("[CombatUI] 右键取消攻击选择");
				ResetSelection();
				_handUI.RefreshHand();
				GetViewport().SetInputAsHandled();
				return;
			}
		}
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
		if (SceneLifecycleGuard.ShouldSkip(this)) return;
		// --- 攻击选择箭头 ---
		if (_selectionMode == SelectionMode.SelectingAttackTarget && _selectedAttacker != null && _arrowRenderer != null)
		{
			var sourcePos = GetMinionScreenCenter(_selectedAttacker);
			var inputPos = GetInputPosition();
			_arrowRenderer.AddArrow("attack_select", sourcePos, inputPos, ArrowRenderer.AttackSelectColor);
		}
		else if (_arrowRenderer != null && _arrowRenderer.HasArrow("attack_select"))
		{
			_arrowRenderer.RemoveArrow("attack_select");
		}

		// --- 攻击拖拽追踪（双交互模式：点击选中→第二击攻击 / 按住拖动→松手攻击） ---
		if (_isAttackDragPressed)
		{
			var inputPos = GetInputPosition();

			// 位移超过阈值 → 升级为真正拖拽
			if (!_attackDragHasMoved && inputPos.DistanceTo(_attackDragStartPos) > AttackDragThreshold)
			{
				_attackDragHasMoved = true;
			}

			// 检测松手
			bool released;
			Vector2 releaseOrInputPos = inputPos;
			if (MobileInputRouter.IsMobile)
			{
				var router = MobileInputRouter.Instance;
				released = _wasMobileAttackTouchActive && !router.IsTouchActive;
				if (released)
					releaseOrInputPos = router.TouchReleasePosition;
			}
			else
			{
				// 桌面端：鼠标左键松开
				released = !Input.IsMouseButtonPressed(MouseButton.Left);
			}

			if (released)
			{
				_isAttackDragPressed = false;
				if (_attackDragHasMoved && _selectionMode == SelectionMode.SelectingAttackTarget)
				{
					// 拖拽路径：松手时检查落点，有效目标→攻击，无效→取消
					HandleAttackDrop(releaseOrInputPos);
				}
				// else: 快速点击无拖拽 → 保持选中状态，等待玩家第二击（现有行为）
			}
		}

		if (MobileInputRouter.IsMobile)
			_wasMobileAttackTouchActive = MobileInputRouter.Instance.IsTouchActive;

		// --- 移动端取消按钮可见性 ---
		UpdateMobileCancelButton();
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

		GD.Print($"[CombatUI] 初始化 — 玩家生命 {combat.PlayerHero.CurrentHealth}/{combat.PlayerHero.MaxHealth}，" +
				  $"敌方生命 {combat.EnemyUnits[0].Body.CurrentHealth}/{combat.EnemyUnits[0].Body.MaxHealth}");

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

		// 意图悬浮提示层（Layer=26，高于意图图标）
		var intentTooltipLayer = new CanvasLayer { Name = "IntentTooltipLayer", Layer = 26 };
		AddChild(intentTooltipLayer);

		// 创建并初始化子组件
		SetupBoardUI();
		SetupHandUI();
		CreateHealthBars();
		CreateManaLabels();
		CreateArmorLabels();
		CreateEndTurnButton();
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

	// ===== 布局构建 =====

	/// <summary>
	/// 构建完整战斗界面布局——全屏 VBoxContainer，
	/// 依次排列敌方区域、棋盘区域、玩家区域和手牌区域。
	/// </summary>
	private void BuildLayout()
	{
		AnchorLeft = 0;
		AnchorTop = 0;
		AnchorRight = 1;
		AnchorBottom = 1;

		// 战斗背景
		var bg = new ColorRect
		{
			Name = "CombatBackground",
			Color = new Color(0.08f, 0.08f, 0.12f, 1f),
			AnchorsPreset = (int)LayoutPreset.FullRect,
		};
		AddChild(bg);

		// 根容器
		var root = new VBoxContainer
		{
			Name = "CombatRoot",
			AnchorLeft = 0,
			AnchorTop = 0,
			AnchorRight = 1,
			AnchorBottom = 1,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};

		// 移动端安全区域边距（补偿刘海屏和手势导航栏）
		if (MobileInputRouter.IsMobile)
		{
			root.OffsetLeft = 24;
			root.OffsetRight = -24;
			root.OffsetTop = 12;
			root.OffsetBottom = -24;
		}

		AddChild(root);

		// 敌方区域（顶部）
		var enemyArea = CreateEnemyArea();
		root.AddChild(enemyArea);

		// 棋盘区域（中央）
		var boardArea = CreateBoardArea();
		root.AddChild(boardArea);

		// 玩家区域（底部偏上）
		var playerArea = CreatePlayerArea();
		root.AddChild(playerArea);

		// 手牌区域（最底部）
		var handArea = CreateHandArea();
		root.AddChild(handArea);

		// 拖拽层——卡牌拖拽时重parent到此，Z 层级最高
		_dragLayer = new Control
		{
			Name = "DragLayer",
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 100,
			AnchorLeft = 0,
			AnchorTop = 0,
			AnchorRight = 1,
			AnchorBottom = 1,
		};
		AddChild(_dragLayer);

		// 箭头渲染器——攻击选择和敌方意图可视化的 Control 层
		_arrowRenderer = new ArrowRenderer
		{
			Name = "ArrowRenderer",
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0,
			AnchorTop = 0,
			AnchorRight = 1,
			AnchorBottom = 1,
		};
		_dragLayer.AddChild(_arrowRenderer);
	}

	/// <summary>
	/// 创建敌方区域——敌方生命值条、护甲和英雄标签。
	/// 敌人使用尖塔式意图系统，不依赖法力水晶。
	/// </summary>
	private HBoxContainer CreateEnemyArea()
	{
		var container = new HBoxContainer
		{
			Name = "EnemyArea",
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 110),
		};

		// 暂停按钮（右上角）
		_pauseButton = new Button
		{
			Name = "PauseButton",
			Text = "⏸",
			CustomMinimumSize = new Vector2(40, 36),
			Flat = true,
		};
		_pauseButton.AddThemeFontSizeOverride("font_size", 20);
		_pauseButton.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
		_pauseButton.Pressed += OnPauseButtonPressed;
		container.AddChild(_pauseButton);

		// 移动端取消按钮（✕）——仅移动端可见，替代桌面端右键取消
		// 在攻击选择/开发者伤害/手牌选择等可取消状态下显示
		_mobileCancelButton = new Button
		{
			Name = "MobileCancelButton",
			Text = "✕",
			CustomMinimumSize = new Vector2(48, 48),
			Flat = true,
			Visible = false,
		};
		_mobileCancelButton.AddThemeFontSizeOverride("font_size", 24);
		_mobileCancelButton.AddThemeColorOverride("font_color", new Color(0.9f, 0.5f, 0.5f));
		_mobileCancelButton.Pressed += OnMobileCancelPressed;
		container.AddChild(_mobileCancelButton);

		return container;
	}

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
	/// 创建棋盘区域——BoardUI 居中，占满剩余垂直空间。
	/// </summary>
	private CenterContainer CreateBoardArea()
	{
		var container = new CenterContainer
		{
			Name = "BoardArea",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};

		_boardUI = new BoardUI
		{
			CustomMinimumSize = new Vector2(560, 300),
			SizeFlagsVertical = SizeFlags.Expand,
		};
		container.AddChild(_boardUI);

		return container;
	}

	/// <summary>
	/// 创建玩家区域——生命值条（左侧）、法力值（中央）、回合结束按钮（右侧）。
	/// </summary>
	private HBoxContainer CreatePlayerArea()
	{
		var container = new HBoxContainer
		{
			Name = "PlayerArea",
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 70),
		};

		// 生命值区域占位（垂直堆叠生命条 + 护甲标签）
		var healthPlaceholder = new VBoxContainer
		{
			Name = "PlayerHealthPlaceholder",
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		container.AddChild(healthPlaceholder);

		// 法力值区域占位
		var manaPlaceholder = new CenterContainer
		{
			Name = "PlayerManaPlaceholder",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		container.AddChild(manaPlaceholder);

		// 武器区域占位
		var weaponPlaceholder = new VBoxContainer
		{
			Name = "WeaponPlaceholder",
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		container.AddChild(weaponPlaceholder);

		// 牌堆区域占位
		var deckPlaceholder = new CenterContainer
		{
			Name = "DeckPlaceholder",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		container.AddChild(deckPlaceholder);

		// 按钮区域占位
		var buttonPlaceholder = new CenterContainer
		{
			Name = "EndTurnButtonPlaceholder",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		container.AddChild(buttonPlaceholder);

		return container;
	}

	/// <summary>
	/// 创建手牌区域——HandUI 全宽居中。
	/// 高度 = HandUI.COLLAPSED_VISIBLE * UIScaler.CurrentScale，
	/// 确保各分辨率下卡牌折叠态露出比例一致（约 30% 卡牌高度）。
	/// </summary>
	private Control CreateHandArea()
	{
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		float handHeight = HandUI.COLLAPSED_VISIBLE * s;

		_handArea = new Control
		{
			Name = "HandArea",
			CustomMinimumSize = new Vector2(0, handHeight),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};

		_handUI = new HandUI
		{
			AnchorLeft = 0,
			AnchorTop = 0,
			AnchorRight = 1,
			AnchorBottom = 1,
		};
		_handArea.AddChild(_handUI);

		return _handArea;
	}

	// ===== 子组件创建 =====

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

	/// <summary>
	/// 创建双方生命值条。
	/// 优先使用 PackedScene 实例化，回退到程序化创建（带主题样式）。
	/// </summary>
	private void CreateHealthBars()
	{
		// 玩家生命值条
		_playerHealthBar = InstantiateHealthBar("PlayerHealthBar");
		var playerHealthContainer = GetNode<VBoxContainer>("CombatRoot/PlayerArea/PlayerHealthPlaceholder");
		if (playerHealthContainer != null)
		{
			// 生命值前缀标签
			var hpLabel = new Label
			{
				Text = Localization.Localization.T("ui.combat.hp_label", "生命 "),
				CustomMinimumSize = new Vector2(50, 24),
			};
			hpLabel.AddThemeColorOverride("font_color", new Color(0.7f, 1f, 0.7f));
			hpLabel.AddThemeFontSizeOverride("font_size", 14);
			playerHealthContainer.AddChild(hpLabel);
			playerHealthContainer.AddChild(_playerHealthBar);
		}

		// 敌方生命值条
		_enemyHealthBar = InstantiateHealthBar("EnemyHealthBar");
		var enemyHealthContainer = GetNodeOrNull<VBoxContainer>("CombatRoot/EnemyArea/EnemyHealthContainer");
		if (enemyHealthContainer != null)
		{
			// 生命值前缀标签
			var hpLabel = new Label
			{
				Text = Localization.Localization.T("ui.combat.hp_label", "生命 "),
				CustomMinimumSize = new Vector2(50, 24),
			};
			hpLabel.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.6f));
			hpLabel.AddThemeFontSizeOverride("font_size", 14);
			enemyHealthContainer.AddChild(hpLabel);
			enemyHealthContainer.AddChild(_enemyHealthBar);
		}
	}

	/// <summary>
	/// 从 PackedScene 或程序化创建生命值条实例。
	/// 程序化创建时附加主题样式和百分比文本标签，尺寸使用 UIScaler 缩放。
	/// </summary>
	private HealthBar InstantiateHealthBar(string name)
	{
		HealthBar hb;
		float scale = UIScaler.Instance?.GetScaleFactor() ?? 1f;

		if (HealthBarScene != null)
		{
			hb = HealthBarScene.Instantiate<HealthBar>();
		}
		else
		{
			hb = new HealthBar
			{
				CustomMinimumSize = new Vector2(180 * scale, 22 * scale),
				SizeFlagsHorizontal = SizeFlags.Expand,
			};

			// 主题样式：暗底 + 绿色填充 + 圆角
			var bgStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.12f, 0.12f, 0.12f),
				CornerRadiusTopLeft = 3,
				CornerRadiusTopRight = 3,
				CornerRadiusBottomLeft = 3,
				CornerRadiusBottomRight = 3,
			};
			var fillStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.22f, 0.72f, 0.22f),
				CornerRadiusTopLeft = 3,
				CornerRadiusTopRight = 3,
				CornerRadiusBottomLeft = 3,
				CornerRadiusBottomRight = 3,
			};
			hb.AddThemeStyleboxOverride("background", bgStyle);
			hb.AddThemeStyleboxOverride("fill", fillStyle);
		}

		hb.Name = name;
		return hb;
	}

	/// <summary>
	/// 创建玩家法力值显示标签。
	/// 敌人使用意图系统，不显示法力值。
	/// </summary>
	private void CreateManaLabels()
	{
		// 玩家法力值（底部中央）
		_playerManaLabel = new Label
		{
			Name = "PlayerManaLabel",
			Text = Localization.Localization.T("ui.combat.mana_format", "法力 {current}/{max}").Replace("{current}", "0").Replace("{max}", "1"),
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(120, 32),
		};
		_playerManaLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.7f, 1f));
		_playerManaLabel.AddThemeFontSizeOverride("font_size", 22);

		var playerManaPlaceholder = GetNode<CenterContainer>("CombatRoot/PlayerArea/PlayerManaPlaceholder");
		playerManaPlaceholder?.AddChild(_playerManaLabel);
	}

	/// <summary>
	/// 创建双方护甲值显示标签——初始隐藏。
	/// </summary>
	private void CreateArmorLabels()
	{
		// 玩家护甲
		_playerArmorLabel = new Label
		{
			Name = "PlayerArmorLabel",
			Text = Localization.Localization.T("ui.combat.armor_format", "护甲: {value}").Replace("{value}", "0"),
			Visible = false,
			CustomMinimumSize = new Vector2(100, 20),
		};
		_playerArmorLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.3f));
		_playerArmorLabel.AddThemeFontSizeOverride("font_size", 14);

		var playerHealthContainer = GetNode<VBoxContainer>("CombatRoot/PlayerArea/PlayerHealthPlaceholder");
		playerHealthContainer?.AddChild(_playerArmorLabel);

		// 敌方护甲
		_enemyArmorLabel = new Label
		{
			Name = "EnemyArmorLabel",
			Text = Localization.Localization.T("ui.combat.armor_format", "护甲: {value}").Replace("{value}", "0"),
			Visible = false,
			CustomMinimumSize = new Vector2(100, 20),
		};
		_enemyArmorLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.3f));
		_enemyArmorLabel.AddThemeFontSizeOverride("font_size", 14);

		var enemyHealthContainer = GetNodeOrNull<VBoxContainer>("CombatRoot/EnemyArea/EnemyHealthContainer");
		enemyHealthContainer?.AddChild(_enemyArmorLabel);

		// 玩家防御
		_playerDefenseLabel = new Label
		{
			Name = "PlayerDefenseLabel",
			Text = "",
			Visible = false,
			CustomMinimumSize = new Vector2(80, 20),
			HorizontalAlignment = HorizontalAlignment.Left,
		};
		_playerDefenseLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.7f, 1f));
		_playerDefenseLabel.AddThemeFontSizeOverride("font_size", 13);
		playerHealthContainer?.AddChild(_playerDefenseLabel);

		// 敌方防御
		_enemyDefenseLabel = new Label
		{
			Name = "EnemyDefenseLabel",
			Text = "",
			Visible = false,
			CustomMinimumSize = new Vector2(80, 20),
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		_enemyDefenseLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.7f, 1f));
		_enemyDefenseLabel.AddThemeFontSizeOverride("font_size", 13);
		enemyHealthContainer?.AddChild(_enemyDefenseLabel);
	}

	/// <summary>
	/// 创建回合结束按钮——右下角，文本「结束回合」。
	/// </summary>
	private void CreateEndTurnButton()
	{
		_endTurnButton = new Button
		{
			Name = "EndTurnButton",
			Text = Localization.Localization.T("ui.combat.end_turn", "结束回合"),
			CustomMinimumSize = new Vector2(120, 48),
		};

		var buttonPlaceholder = GetNode<CenterContainer>("CombatRoot/PlayerArea/EndTurnButtonPlaceholder");
		buttonPlaceholder?.AddChild(_endTurnButton);
	}

	/// <summary>
	/// 创建游戏结束弹窗——胜利/失败时显示，含"返回主菜单"按钮。
	/// </summary>
	private void CreateGameOverPopup()
	{
		_gameOverPopup = new AcceptDialog
		{
			Name = "GameOverPopup",
			Title = Localization.Localization.T("ui.combat.game_over", "游戏结束"),
			OkButtonText = Localization.Localization.T("ui.combat.back_to_menu", "返回主菜单"),
			Exclusive = true,
			Visible = false,
			Size = new Vector2I(320, 180),
		};
		// 终身单次连接，通过 _isVictory flag 区分胜利/失败路由
		_gameOverPopup.Confirmed += OnGameOverConfirmed;
		AddChild(_gameOverPopup);
	}

	/// <summary>
	/// 创建敌方英雄交互面板——带可见色块背景和标签的区域，
	/// 攻击目标选择模式下整个面板可点击攻击。
	/// </summary>
	private void CreateEnemyHeroAttackButton()
	{
		var enemyHeroPlaceholder = GetNode<CenterContainer>("CombatRoot/EnemyArea/EnemyHeroLabelPlaceholder");
		if (enemyHeroPlaceholder == null) return;

		// 交互面板容器（CenteredContainer 居中内容）
		var panelContainer = new CenterContainer
		{
			Name = "EnemyHeroPanelContainer",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};

		// 可见色块面板
		_enemyHeroPanel = new Panel
		{
			Name = "EnemyHeroPanel",
			CustomMinimumSize = new Vector2(140, 70),
		};
		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.25f, 0.12f, 0.12f, 0.8f),
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			BorderColor = new Color(0.6f, 0.2f, 0.2f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
		};
		_enemyHeroPanel.AddThemeStyleboxOverride("panel", panelStyle);

		// 面板内部垂直布局
		var panelContent = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_enemyHeroPanel.AddChild(panelContent);

		// 英雄标签
		var heroLabel = new Label
		{
			Name = "EnemyHeroLabel",
			Text = Localization.Localization.T("ui.combat.enemy_hero", "敌方英雄"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		heroLabel.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.5f));
		heroLabel.AddThemeFontSizeOverride("font_size", 16);
		panelContent.AddChild(heroLabel);

		// 攻击按钮（攻击目标模式下可见）
		_enemyHeroAttackButton = new Button
		{
			Name = "EnemyHeroAttackButton",
			Text = Localization.Localization.T("ui.combat.attack_enemy_hero", "⚔ 攻击敌方英雄"),
			CustomMinimumSize = new Vector2(140, 44),
			Visible = false,
		};
		_enemyHeroAttackButton.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
		panelContent.AddChild(_enemyHeroAttackButton);

		// 对敌方英雄施法按钮（法术目标模式下可见）
		_enemyHeroSpellButton = new Button
		{
			Name = "EnemyHeroSpellButton",
			Text = Localization.Localization.T("ui.combat.spell_enemy_hero", "✦ 对敌方英雄施法"),
			CustomMinimumSize = new Vector2(140, 44),
			Visible = false,
		};
		_enemyHeroSpellButton.AddThemeColorOverride("font_color", new Color(1f, 0.7f, 0.2f));
		panelContent.AddChild(_enemyHeroSpellButton);

		panelContainer.AddChild(_enemyHeroPanel);
		enemyHeroPlaceholder.AddChild(panelContainer);
	}

	/// <summary>
	/// 创建玩家英雄交互面板——置于玩家生命值条上方。
	/// 蓝色边框色块，包含英雄标签和对己方英雄施法按钮。
	/// 仅在法术目标选择模式且目标过滤允许己方英雄时显示按钮。
	/// </summary>
	private void CreatePlayerHeroPanel()
	{
		var playerHealthPlaceholder = GetNode<VBoxContainer>("CombatRoot/PlayerArea/PlayerHealthPlaceholder");
		if (playerHealthPlaceholder == null) return;

		// 交互面板容器
		var panelContainer = new CenterContainer
		{
			Name = "PlayerHeroPanelContainer",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};

		// 可见色块面板（蓝色边框）
		_playerHeroPanel = new Panel
		{
			Name = "PlayerHeroPanel",
			CustomMinimumSize = new Vector2(140, 56),
		};
		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.15f, 0.28f, 0.7f),
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			BorderColor = new Color(0.25f, 0.5f, 0.9f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
		};
		_playerHeroPanel.AddThemeStyleboxOverride("panel", panelStyle);

		// 面板内部垂直布局
		var panelContent = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_playerHeroPanel.AddChild(panelContent);

		// 英雄标签
		var heroLabel = new Label
		{
			Name = "PlayerHeroLabel",
			Text = Localization.Localization.T("ui.combat.player_hero", "我方英雄"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		heroLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 1f));
		heroLabel.AddThemeFontSizeOverride("font_size", 16);
		panelContent.AddChild(heroLabel);

		// 对己方英雄施法按钮（法术目标模式下可见）
		_playerHeroSpellButton = new Button
		{
			Name = "PlayerHeroSpellButton",
			Text = Localization.Localization.T("ui.combat.spell_player_hero", "✦ 对己方英雄施法"),
			CustomMinimumSize = new Vector2(140, 30),
			Visible = false,
		};
		_playerHeroSpellButton.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 1f));
		panelContent.AddChild(_playerHeroSpellButton);

		panelContainer.AddChild(_playerHeroPanel);
		// 插入到 PlayerHealthPlaceholder 的第一个位置（生命值条之上）
		playerHealthPlaceholder.AddChild(panelContainer);
		playerHealthPlaceholder.MoveChild(panelContainer, 0);
	}

	/// <summary>
	/// 创建敌方意图显示标签——置于敌方英雄面板上方。
	/// </summary>
	private void CreateEnemyIntentLabel()
	{
		var enemyIntentPlaceholder = GetNode<Control>("CombatRoot/EnemyArea/EnemyIntentPlaceholder");
		if (enemyIntentPlaceholder == null)
		{
			GD.PrintErr("[CombatUI] EnemyIntentPlaceholder 未找到");
			return;
		}

		_enemyIntentLabel = new Label
		{
			Name = "EnemyIntentLabel",
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		_enemyIntentLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
		_enemyIntentLabel.AddThemeFontSizeOverride("font_size", 14);
		enemyIntentPlaceholder.AddChild(_enemyIntentLabel);
	}

	// ===== 牌堆按钮 =====

	/// <summary>
	/// 创建抽牌堆/弃牌堆按钮，放置在 PlayerArea 的 DeckPlaceholder 中。
	/// 点击后弹出牌列表窗口。
	/// </summary>
	private void CreateDeckButtons()
	{
		var deckPlaceholder = GetNode<CenterContainer>("CombatRoot/PlayerArea/DeckPlaceholder");
		if (deckPlaceholder == null) return;

		var btnContainer = new HBoxContainer
		{
			Name = "DeckButtonContainer",
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};

		// 抽牌堆按钮
		_drawPileBtn = new Button
		{
			Name = "DrawPileBtn",
			Text = Localization.Localization.T("ui.combat.draw_pile_format", "抽牌堆 ({count})").Replace("{count}", "0"),
			CustomMinimumSize = new Vector2(100, 44),
		};
		_drawPileBtn.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 1f));
		_drawPileBtn.AddThemeFontSizeOverride("font_size", 13);
		_drawPileBtn.Pressed += () => ShowDrawPileView();
		btnContainer.AddChild(_drawPileBtn);

		// 间距
		var spacer = new Control { CustomMinimumSize = new Vector2(8, 1) };
		btnContainer.AddChild(spacer);

		// 弃牌堆按钮
		_discardPileBtn = new Button
		{
			Name = "DiscardPileBtn",
			Text = Localization.Localization.T("ui.combat.discard_pile_format", "弃牌堆 ({count})").Replace("{count}", "0"),
			CustomMinimumSize = new Vector2(100, 44),
		};
		_discardPileBtn.AddThemeColorOverride("font_color", new Color(0.8f, 0.7f, 0.6f));
		_discardPileBtn.AddThemeFontSizeOverride("font_size", 13);
		_discardPileBtn.Pressed += () => ShowDiscardPileView();
		btnContainer.AddChild(_discardPileBtn);

		deckPlaceholder.AddChild(btnContainer);
	}

	/// <summary>
	/// 显示抽牌堆查看弹窗（热键 D 或按钮触发）。
	/// </summary>
	private void ShowDrawPileView()
	{
		if (_combat == null) return;
		if (_isPaused || _combat.IsDiscovering) return;

		var cards = _combat.PlayerHero.DeckState.DrawPile;
		ShowPileViewer(Localization.Localization.T("ui.combat.draw_pile", "抽牌堆"), cards, showOrderNumbers: true);
	}

	/// <summary>
	/// 显示弃牌堆查看弹窗（热键 S 或按钮触发）。
	/// </summary>
	private void ShowDiscardPileView()
	{
		if (_combat == null) return;
		if (_isPaused || _combat.IsDiscovering) return;

		var cards = _combat.PlayerHero.DeckState.DiscardPile;
		ShowPileViewer(Localization.Localization.T("ui.combat.discard_pile", "弃牌堆"), cards);
	}

	// ===== 武器 UI =====

	/// <summary>
	/// 创建武器相关 UI：信息标签、攻击按钮、主动技能按钮。
	/// 玩家武器 UI 放置在 WeaponPlaceholder，敌方武器信息显示在 EnemyIntentPlaceholder 下方。
	/// </summary>
	private void CreateWeaponUI()
	{
		// --- 玩家武器 UI ---
		var weaponPlaceholder = GetNode<VBoxContainer>("CombatRoot/PlayerArea/WeaponPlaceholder");
		if (weaponPlaceholder == null) return;

		// 武器信息标签
		_weaponInfoLabel = new Label
		{
			Name = "WeaponInfoLabel",
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(120, 18),
		};
		_weaponInfoLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 1f));
		_weaponInfoLabel.AddThemeFontSizeOverride("font_size", 12);
		weaponPlaceholder.AddChild(_weaponInfoLabel);

		// 按钮容器
		var weaponBtnContainer = new HBoxContainer
		{
			Name = "WeaponButtonContainer",
			Alignment = BoxContainer.AlignmentMode.Center,
		};

		// 武器攻击按钮
		_weaponAttackButton = new Button
		{
			Name = "WeaponAttackButton",
			Text = Localization.Localization.T("ui.combat.weapon_attack", "⚔ 武器攻击"),
			CustomMinimumSize = new Vector2(100, 44),
			Visible = false,
		};
		_weaponAttackButton.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.3f));
		_weaponAttackButton.AddThemeFontSizeOverride("font_size", 12);
		weaponBtnContainer.AddChild(_weaponAttackButton);

		// 主动技能按钮
		_weaponActiveSkillButton = new Button
		{
			Name = "WeaponActiveSkillButton",
			Text = Localization.Localization.T("ui.combat.weapon_skill", "✦ 技能"),
			CustomMinimumSize = new Vector2(100, 44),
			Visible = false,
		};
		_weaponActiveSkillButton.AddThemeColorOverride("font_color", new Color(0.8f, 0.6f, 1f));
		_weaponActiveSkillButton.AddThemeFontSizeOverride("font_size", 12);
		weaponBtnContainer.AddChild(_weaponActiveSkillButton);

		weaponPlaceholder.AddChild(weaponBtnContainer);

		// --- 敌方武器信息 ---
		var enemyArea = GetNode<HBoxContainer>("CombatRoot/EnemyArea");
		if (enemyArea == null) return;

		_enemyWeaponLabel = new Label
		{
			Name = "EnemyWeaponLabel",
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(120, 18),
		};
		_enemyWeaponLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
		_enemyWeaponLabel.AddThemeFontSizeOverride("font_size", 11);
		enemyArea.AddChild(_enemyWeaponLabel);
	}

	/// <summary>
	/// 创建状态效果图标容器。
	/// 放置在双方英雄生命值区域下方，用于显示 buff/debuff 图标。
	/// </summary>
	private void CreateStatusEffectUI()
	{
		// 玩家效果图标栏
		_playerEffectBar = new EffectBar { Name = "PlayerEffectBar" };
		var playerHealthContainer = GetNode<VBoxContainer>("CombatRoot/PlayerArea/PlayerHealthPlaceholder");
		playerHealthContainer?.AddChild(_playerEffectBar);

		// 敌方效果图标栏（旧版单敌人兼容层）
		_enemyEffectBar = new EffectBar { Name = "EnemyEffectBar" };
		var enemyHealthContainer = GetNodeOrNull<VBoxContainer>("CombatRoot/EnemyArea/EnemyHealthContainer");
		enemyHealthContainer?.AddChild(_enemyEffectBar);
	}

	/// <summary>
	/// 创建热力值 UI 条——放在法力值占位区域。
	/// </summary>
	private void CreateHeatBar()
	{
		_heatBar = new UI.HeatBar { Name = "HeatBar" };
		var manaPlaceholder = GetNodeOrNull<CenterContainer>("CombatRoot/PlayerArea/PlayerManaPlaceholder");
		manaPlaceholder?.AddChild(_heatBar);

		if (_combat.Heat != null)
			_heatBar.Bind(_combat.Heat);
	}

	/// <summary>
	/// 创建藏品栏——放在玩家区域顶部。
	/// </summary>
	private void CreateRelicBar()
	{
		_relicBar = new UI.RelicBar { Name = "RelicBar" };
		var playerArea = GetNodeOrNull<HBoxContainer>("CombatRoot/PlayerArea");
		playerArea?.AddChild(_relicBar);
		playerArea?.MoveChild(_relicBar, 0);

		if (_combat.Relics != null)
			_relicBar.Bind(_combat.Relics);
	}

	/// <summary>
	/// 弹出牌堆查看窗口，以列表形式展示所有卡牌名称和费用。
	/// 复用同一个弹窗实例，点击关闭按钮或 OK 即可关闭。
	/// </summary>
	/// <param name="title">弹窗标题（如"抽牌堆""弃牌堆"）</param>
	/// <param name="cards">要展示的卡牌列表</param>
	/// <param name="showOrderNumbers">是否在每张牌前显示序号（抽牌堆用，表示第几张被抽到）</param>
	private void ShowPileViewer(string title, List<OdysseyCards.Card.Card> cards, bool showOrderNumbers = false)
	{
		// 关闭之前的弹窗
		_pileViewPopup?.QueueFree();

		_pileViewPopup = new AcceptDialog
		{
			Title = title,
			Size = new Vector2I(300, 320),
			OkButtonText = Localization.Localization.T("ui.combat.close", "关闭"),
		};

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};

		var listContainer = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};

		if (cards.Count == 0)
		{
			var emptyLabel = new Label
			{
				Text = Localization.Localization.T("ui.combat.empty", "（空）"),
				HorizontalAlignment = HorizontalAlignment.Center,
			};
			emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
			emptyLabel.AddThemeFontSizeOverride("font_size", 14);
			listContainer.AddChild(emptyLabel);
		}
		else
		{
			for (int i = 0; i < cards.Count; i++)
			{
				var card = cards[i];
				string prefix = showOrderNumbers ? $"#{i + 1} " : "";
				var cardLabel = new Label
				{
					Text = prefix + Localization.Localization.T("ui.combat.card_pile_item", "[{cost}费] {name}")
						.Replace("{cost}", card.Cost.ToString())
						.Replace("{name}", card.GetLocalizedName()),
				};
				cardLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.8f));
				cardLabel.AddThemeFontSizeOverride("font_size", 14);
				listContainer.AddChild(cardLabel);
			}
		}

		scroll.AddChild(listContainer);
		_pileViewPopup.AddChild(scroll);

		AddChild(_pileViewPopup);
		_pileViewPopup.PopupCentered();
	}

	/// <summary>
	/// 根据当前牌堆状态更新按钮文字。
	/// </summary>
	private void UpdateDeckCounts()
	{
		if (_combat == null) return;

		var deckState = _combat.PlayerHero.DeckState;
		_drawPileBtn.Text = Localization.Localization.T("ui.combat.draw_pile_format", "抽牌堆 ({count})").Replace("{count}", deckState.DrawPile.Count.ToString());
		_discardPileBtn.Text = Localization.Localization.T("ui.combat.discard_pile_format", "弃牌堆 ({count})").Replace("{count}", deckState.DiscardPile.Count.ToString());
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

		// 攻击敌方英雄按钮
		_enemyHeroAttackButton.Pressed += OnEnemyHeroAttackPressed;
		_unsubscribeActions.Add(() => _enemyHeroAttackButton.Pressed -= OnEnemyHeroAttackPressed);

		// 对敌方英雄施法按钮
		_enemyHeroSpellButton.Pressed += OnEnemyHeroSpellTarget;
		_unsubscribeActions.Add(() => _enemyHeroSpellButton.Pressed -= OnEnemyHeroSpellTarget);

		// 对己方英雄施法按钮
		_playerHeroSpellButton.Pressed += OnPlayerHeroSpellTarget;
		_unsubscribeActions.Add(() => _playerHeroSpellButton.Pressed -= OnPlayerHeroSpellTarget);

		// 武器攻击按钮
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
		_combat.PlayerHero.DeckState.OnHandChanged += OnHandChanged;
		_unsubscribeActions.Add(() => _combat.PlayerHero.DeckState.OnHandChanged -= OnHandChanged);

		// 法力值变化 → 自动更新显示
		_combat.PlayerHero.OnManaChanged += OnManaChanged;
		_unsubscribeActions.Add(() => _combat.PlayerHero.OnManaChanged -= OnManaChanged);

		// 敌方意图变化 → 更新意图显示和箭头
		_combat.OnCombatStateChanged += OnCombatStateChangedRefresh;
		_unsubscribeActions.Add(() => _combat.OnCombatStateChanged -= OnCombatStateChangedRefresh);

		// 发现选牌阶段切换
		_combat.OnCombatStateChanged += OnCombatStateChangedForDiscover;
		_unsubscribeActions.Add(() => _combat.OnCombatStateChanged -= OnCombatStateChangedForDiscover);

		// 游戏结束 → 显示弹窗
		_combat.OnGameOver += ShowGameOverPopup;
		_unsubscribeActions.Add(() => _combat.OnGameOver -= ShowGameOverPopup);

		// 随从伤害跳字 — 随从放置时通过闭包捕获 minion 引用订阅事件
		_board!.OnMinionPlaced += OnBoardMinionPlacedSubscribeDamage;
		_unsubscribeActions.Add(() => _board.OnMinionPlaced -= OnBoardMinionPlacedSubscribeDamage);
		_board.OnMinionRemoved += OnBoardMinionRemovedUnsubscribeDamage;
		_unsubscribeActions.Add(() => _board.OnMinionRemoved -= OnBoardMinionRemovedUnsubscribeDamage);

		// 随从死亡飞行动画 — 在槽位清空前获取屏幕坐标
		_board.OnMinionPreRemove += OnBoardMinionPreRemove;
		_unsubscribeActions.Add(() => _board.OnMinionPreRemove -= OnBoardMinionPreRemove);

		// 敌方表情 — 空闲超时或 DevConsole 触发
		_combat.OnEnemyEmote += OnEnemyEmote;
		_unsubscribeActions.Add(() => _combat.OnEnemyEmote -= OnEnemyEmote);
	}

	/// <summary>
	/// 注册热键回调——通过 HotkeyManager 将键盘输入映射到 UI 操作。
	/// 所有回调通过 _unsubscribeActions 在 _ExitTree 时自动解绑。
	/// </summary>
	private void SubscribeHotkeys()
	{
		var hm = HotkeyManager.Instance;
		if (hm == null) return;

		hm.PushPressedBinding(OdysseyInput.EndTurn, TryEndTurn);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.EndTurn, TryEndTurn));

		hm.PushPressedBinding(OdysseyInput.ViewDeck, ShowDrawPileView);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.ViewDeck, ShowDrawPileView));

		hm.PushPressedBinding(OdysseyInput.ViewDiscard, ShowDiscardPileView);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.ViewDiscard, ShowDiscardPileView));

		hm.PushPressedBinding(OdysseyInput.Pause, TogglePause);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.Pause, TogglePause));

		hm.PushPressedBinding(OdysseyInput.Cancel, HandleCancel);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.Cancel, HandleCancel));
	}

	private void OnHandChanged()
	{
		_handUI.RefreshHand();
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

	/// <summary>
	/// 敌方表情事件——在第一个敌人身份卡上方显示浮动表情文本。
	/// </summary>
	private void OnEnemyEmote(string text)
	{
		if (string.IsNullOrEmpty(text)) return;
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

	private void OnBoardMinionRemovedUnsubscribeDamage(Minion minion)
	{
		UnsubscribeMinionDamageEvents(minion);
	}

	private void OnBoardMinionPreRemove(Minion minion, int slotIndex, bool isPlayerSide)
	{
		if (!isPlayerSide) return;

		var cardPos = _boardUI.GetSlotScreenCenter(slotIndex, isPlayerSide);
		if (cardPos == Vector2.Zero) return;

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
		if (_minionDamageHandlers.ContainsKey(minion)) return;

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
		if (!_minionDamageHandlers.TryGetValue(minion, out var handlers)) return;
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

	// ===== 刷新方法 =====

	/// <summary>
	/// 刷新所有子组件——棋盘、手牌、生命值、法力值和护甲。
	/// 在每次操作完成后调用以确保界面与游戏状态同步。
	/// </summary>
	public void RefreshAll()
	{
		// 发现选牌或手牌选择阶段：仅更新状态显示，跳过手牌和棋盘刷新
		if (_combat != null && _combat.IsDiscovering)
		{
			CleanupDragCard();
			UpdateHealthBars();
			UpdateManaDisplay();
			UpdateArmorDisplay();
			UpdateDefenseDisplay();
			UpdateDeckCounts();
			UpdateWeaponDisplay();
			UpdateStatusEffectDisplay();
			UpdateHeatDisplay();
			UpdateRelicDisplay();

			// 手牌选择模式下，额外更新卡牌高亮状态
			if (_isHandSelecting)
			{
				RefreshHandSelectionHighlights();
			}
			return;
		}

		if (_combat == null)
		{
			return;
		}

		// 清理拖拽中的卡牌 UI（含取消事件订阅）
		CleanupDragCard();

		_boardUI.RefreshBoard();
		var playerHero = _combat.PlayerHero;
		if (playerHero != null)
		{
			_boardUI.UpdateActionCostDimming(playerHero.CurrentMana);
		}
		_handUI.RefreshHand();
		UpdateHealthBars();
		UpdateManaDisplay();
		UpdateArmorDisplay();
		UpdateDefenseDisplay();
		UpdateDeckCounts();

		// 每次刷新时重置为正常模式（先重置再更新武器，避免显示被覆盖）
		ResetSelection();

		UpdateWeaponDisplay();
		UpdateStatusEffectDisplay();
		UpdateHeatDisplay();
		UpdateRelicDisplay();

		// 游戏结束时禁用操作
		if (_combat.State.IsGameOver)
		{
			_endTurnButton.Disabled = true;
		}
	}

	/// <summary>
	/// 刷新敌方意图显示——根据当前战场状态重新计算攻击目标和伤害数值。
	/// 若敌方回合动画进行中则跳过（冻结机制，参考 STS2 的 NIntent._isFrozen）。
	/// </summary>
	private void RefreshIntentDisplay()
	{
		if (_combat == null) return;

		// 冻结检查：敌方回合执行动画期间不刷新，防止数值跳变
		if (_combat.IsEnemyTurnAnimating) return;

		// 通过卡片刷新所有敌人意图（包括首个和额外）
		foreach (var card in _enemyCards)
			card.Refresh(_combat);

		// 向后兼容：首个敌人意图标签
		if (_enemyIntentLabel != null && _enemyCards.Count > 0)
			_enemyIntentLabel.Text = _combat.GetCurrentEnemyIntent().GetDisplayDescription(_combat);
	}

	/// <summary>
	/// 刷新敌方意图箭头——根据当前战场状态绘制红色攻击箭头和蓝色增益箭头。
	/// 每次 OnCombatStateChanged 触发时调用。
	/// </summary>
	private void RefreshIntentArrows()
	{
		if (_combat == null || _arrowRenderer == null) return;

		// 冻结检查：敌方回合执行动画期间不刷新
		if (_combat.IsEnemyTurnAnimating) return;

		// 清除旧的意图箭头（前缀 "intent_"）
		_arrowRenderer.ClearArrows();

		// 清除敌方槽位的旧意图文字（由下面的循环重新设置）
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
			_boardUI.SetSlotIntentText(i, isPlayerSide: false, null);

		for (int i = 0; i < _combat.EnemyUnits.Count; i++)
		{
			var unit = _combat.EnemyUnits[i];
			var intent = unit.GetCurrentIntent(_combat);
			switch (intent.Type)
			{
				case IntentType.Attack:
				{
					var target = intent.GetTarget(_combat);
					Vector2 targetPos;
					if (target is Minion minionTarget)
					{
						targetPos = GetMinionScreenCenter(minionTarget);
					}
					else if (target is Hero)
					{
						// 敌人攻击玩家英雄
						targetPos = GetPlayerHeroScreenCenter();
					}
					else
					{
						// 未能解析目标，默认指向玩家英雄
						targetPos = GetPlayerHeroScreenCenter();
					}

					if (targetPos != Vector2.Zero)
					{
						var source = GetEnemyIdentityCardAnchor(i, targetPos);
						if (source == Vector2.Zero) break;
						_arrowRenderer.AddArrow($"intent_attack_{i}", source, targetPos, ArrowRenderer.EnemyAttackColor);
					}
					break;
				}

				case IntentType.Buff:
				{
					// 增益意图：指向敌方战场上的首个友方随从
					Vector2? buffTarget = null;
					for (int slot = 0; slot < Board.MaxSlotsPerSide; slot++)
					{
						var friendly = _combat.Board.GetMinionAt(slot, isPlayerSide: false);
						if (friendly != null && !friendly.IsDead)
						{
							buffTarget = GetMinionScreenCenter(friendly);
							break;
						}
					}

					// 若无友方随从，指向敌人自身身份卡（自增益）
					buffTarget ??= GetEnemyIdentityCardCenter(i);

					var source = GetEnemyIdentityCardAnchor(i, buffTarget.Value);
					if (source != Vector2.Zero)
						_arrowRenderer.AddArrow($"intent_buff_{i}", source, buffTarget.Value, ArrowRenderer.BuffColor);
					break;
				}
			}
		}

		// === 敌方随从意图箭头 ===
		var enemyMinions = _combat.Board.GetEnemyMinions();
		foreach (var minion in enemyMinions)
		{
			if (minion.IsDead) continue;
			int slotIndex = minion.BoardSlotIndex;
			if (slotIndex < 0) continue;

			Vector2 sourcePos = _boardUI.GetSlotScreenCenter(slotIndex, isPlayerSide: false);

			// 获取意图：优先使用 IntentBrain，否则默认攻击英雄（遵守嘲讽）
			EnemyIntent intent;
			if (minion.IntentBrain != null)
			{
				intent = minion.IntentBrain.GetCurrentIntent(_combat);
			}
			else
			{
				var playerTaunts = _combat.Board.GetTaunts(ofEnemy: false);
				IDamageTarget? target;
				if (playerTaunts.Count > 0)
					target = playerTaunts[0];
				else
					target = _combat.PlayerHero;

				int dmg = DamageResolver.ResolvePreviewDamage(minion.Attack, minion, target);
				intent = new EnemyIntent(IntentType.Attack, dmg, Loc.T("intent.attack_format", "对{target}造成 {damage} 点伤害").Replace("{target}", "英雄").Replace("{damage}", dmg.ToString()));
				intent.TargetSelector = _ => target;
			}

			string key = $"intent_minion_{slotIndex}";

			if (intent.Type == IntentType.Attack)
			{
				var target = intent.TargetSelector?.Invoke(_combat);
				Vector2 targetPos = ResolveTargetScreenPos(target);
				_arrowRenderer.AddArrow(key, sourcePos, targetPos, ArrowRenderer.EnemyAttackColor);
			}
			else if (intent.Type == IntentType.Buff)
			{
				// 增益意图：指向敌方战场首个友方随从
				var friendlies = _combat.Board.GetEnemyMinions()
					.Where(m => !m.IsDead && m != minion).ToList();
				Vector2 targetPos = friendlies.Count > 0
					? _boardUI.GetSlotScreenCenter(friendlies[0].BoardSlotIndex, isPlayerSide: false)
					: sourcePos;
				_arrowRenderer.AddArrow(key, sourcePos, targetPos, ArrowRenderer.BuffColor);
			}

			// 设置槽位意图文字
			string desc = intent.GetDisplayDescription(_combat);
			_boardUI.SetSlotIntentText(slotIndex, isPlayerSide: false, desc);
		}
	}

	/// <summary>
	/// 更新双方英雄生命值条。
	/// </summary>
	private void UpdateHealthBars()
	{
		if (_combat == null) return;

		_playerHealthBar.UpdateHealth(_combat.PlayerHero.CurrentHealth, _combat.PlayerHero.MaxHealth);

		// 敌方 HP 也在此处刷新——攻击/法术施放后 RefreshAll 不会触发
		// OnCombatStateChanged（只有放置/移除随从才触发），因此需要显式更新。
		foreach (var card in _enemyCards)
		{
			var unit = _combat.EnemyUnits[card.EnemyIndex];
			card.RefreshHealth(unit.Body.CurrentHealth, unit.Body.MaxHealth);
			card.RefreshArmor(unit.Body.CurrentArmor);
		}
	}

	/// <summary>
	/// 更新玩家法力值显示，格式「法力 Current/Max」。
	/// 敌人使用意图系统，不跟踪法力值。
	/// </summary>
	private void UpdateManaDisplay()
	{
		if (_combat == null) return;

		_playerManaLabel.Text = Localization.Localization.T("ui.combat.mana_format", "法力 {current}/{max}")
			.Replace("{current}", _combat.PlayerHero.CurrentMana.ToString())
			.Replace("{max}", _combat.PlayerHero.MaxMana.ToString());
	}

	/// <summary>
	/// 更新双方护甲值显示——护甲 > 0 时显示标签，否则隐藏。
	/// </summary>
	private void UpdateArmorDisplay()
	{
		if (_combat == null) return;

		// 玩家护甲
		int playerArmor = _combat.PlayerHero.CurrentArmor;
		_playerArmorLabel.Visible = playerArmor > 0;
		if (playerArmor > 0)
		{
			_playerArmorLabel.Text = Localization.Localization.T("ui.combat.armor_format", "护甲: {value}").Replace("{value}", playerArmor.ToString());
		}

		// 敌方护甲（已迁移到 EnemyIdentityCard，旧版 UI 跳过）
		if (_enemyArmorLabel != null)
		{
			int enemyArmor = _combat.EnemyHero.CurrentArmor;
			_enemyArmorLabel.Visible = enemyArmor > 0;
			if (enemyArmor > 0)
				_enemyArmorLabel.Text = Localization.Localization.T("ui.combat.armor_format", "护甲: {value}").Replace("{value}", enemyArmor.ToString());
		}
	}

	/// <summary>
	/// 更新双方防御力显示——防御 != 0 时显示标签，否则隐藏。
	/// 正防御显示为蓝色（增益），负防御显示为红色（减益/脆弱）。
	/// </summary>
	private void UpdateDefenseDisplay()
	{
		if (_combat == null) return;

		// 玩家防御
		int playerDef = _combat.PlayerHero.Defense;
		_playerDefenseLabel.Visible = playerDef != 0;
		if (playerDef != 0)
		{
			_playerDefenseLabel.Text = Localization.Localization.T("ui.combat.defense_format", "防御: {value}").Replace("{value}", playerDef >= 0 ? $"+{playerDef}" : $"{playerDef}");
			_playerDefenseLabel.AddThemeColorOverride("font_color",
				playerDef > 0 ? new Color(0.3f, 0.7f, 1f) : new Color(1f, 0.3f, 0.3f));
		}

		// 敌方防御（已迁移到 EnemyIdentityCard，旧版 UI 跳过）
		if (_enemyDefenseLabel != null)
		{
			int enemyDef = _combat.EnemyHero.Defense;
			_enemyDefenseLabel.Visible = enemyDef != 0;
			if (enemyDef != 0)
			{
				_enemyDefenseLabel.Text = Localization.Localization.T("ui.combat.defense_format", "防御: {value}").Replace("{value}", enemyDef >= 0 ? $"+{enemyDef}" : $"{enemyDef}");
				_enemyDefenseLabel.AddThemeColorOverride("font_color",
					enemyDef > 0 ? new Color(0.3f, 0.7f, 1f) : new Color(1f, 0.3f, 0.3f));
			}
		}
	}

	// ===== 事件处理——棋盘点击 =====

	/// <summary>
	/// 棋盘槽位点击事件处理。
	/// 根据当前选择模式分发到不同的处理流程：
	/// <list type="bullet">
	/// <item>随从放置模式 → 在点击的玩家槽位召唤随从</item>
	/// <item>攻击目标模式 → 对点击的敌方槽位发动攻击</item>
	/// <item>普通模式 → 选中己方随从进入攻击目标模式</item>
	/// </list>
	/// </summary>
	/// <param name="slotIndex">被点击的槽位索引（0-4）</param>
	/// <param name="isPlayerSide">点击的槽位是否属于玩家方</param>
	private void OnBoardSlotClicked(int slotIndex, bool isPlayerSide)
	{
		if (_combat.State.IsGameOver) return;
		switch (_selectionMode)
		{
			case SelectionMode.PlacingMinion:
				HandleMinionPlacement(slotIndex, isPlayerSide);
				break;

			case SelectionMode.TargetingSpell:
				HandleSpellTarget(slotIndex, isPlayerSide);
				break;

			case SelectionMode.SelectingAttackTarget:
				HandleAttackTarget(slotIndex, isPlayerSide);
				break;

			case SelectionMode.SelectingWeaponTarget:
				HandleWeaponAttackTarget(slotIndex, isPlayerSide);
				break;

			case SelectionMode.SelectingActiveSkillTarget:
				HandleActiveSkillTarget(slotIndex, isPlayerSide);
				break;

			case SelectionMode.Normal:
				HandleNormalSlotClick(slotIndex, isPlayerSide);
				break;

			case SelectionMode.DevDamageTargeting:
				GD.Print($"[CombatUI] OnBoardSlotClicked → DevDamageTargeting, slot={slotIndex}, side={(isPlayerSide ? "P" : "E")}");
				HandleDevDamageSlot(slotIndex, isPlayerSide);
				break;
		}
	}

	/// <summary>
	/// 槽位右键点击回调——在目标选择模式下取消当前选择（等效 ESC）。
	/// 处理所有需要目标选择的模式：攻击、武器、法术、放置等。
	/// </summary>
	private void OnBoardSlotRightClicked(int slotIndex, bool isPlayerSide)
	{
		if (_combat.State.IsGameOver) return;

		// 开发者伤害模式——退出
		if (_selectionMode == SelectionMode.DevDamageTargeting)
		{
			ExitDevDamageMode();
			return;
		}

		// 攻击/武器/法术目标选择模式——重置选择
		if (_selectionMode == SelectionMode.SelectingAttackTarget
			|| _selectionMode == SelectionMode.SelectingWeaponTarget
			|| _selectionMode == SelectionMode.SelectingActiveSkillTarget
			|| _selectionMode == SelectionMode.TargetingSpell
			|| _selectionMode == SelectionMode.PlacingMinion
			|| _selectionMode == SelectionMode.PlayingNoTargetCard)
		{
			GD.Print("[CombatUI] 槽位右键→取消目标选择");
			ResetSelection();
			_handUI.RefreshHand();
			return;
		}
	}

	// ----- 普通模式下的槽位点击 -----

	/// <summary>
	/// 普通模式下点击己方有随从的槽位 → 将该随从设为攻击方，进入攻击目标选择模式。
	/// </summary>
	private void HandleNormalSlotClick(int slotIndex, bool isPlayerSide)
	{
		if (!isPlayerSide) return; // 普通模式下只响应己方槽位

		var minion = _combat.Board.GetMinionAt(slotIndex, isPlayerSide: true);
		if (minion == null || minion.IsDead) return;

		// 行动花费检查：法力不足时拒绝进入攻击模式
		if (minion.ActionCost > 0 && _combat.PlayerHero.CurrentMana < minion.ActionCost)
		{
			GD.Print($"[CombatUI] {minion.CardName} 行动花费 {minion.ActionCost}，当前法力不足（{_combat.PlayerHero.CurrentMana}），无法攻击");
			return;
		}

		// 设为攻击方
		_selectedAttacker = minion;
		_selectionMode = SelectionMode.SelectingAttackTarget;

		// 启动攻击拖拽追踪（支持按住拖动→松手攻击）
		_isAttackDragPressed = true;
		_attackDragHasMoved = false;
		_attackDragStartPos = GetInputPosition();

		GD.Print($"[CombatUI] 选中己方随从 {minion.CardName} 准备攻击");

		// 高亮合法攻击目标
		HighlightValidAttackTargets();

		// 启用键盘目标选择（仅敌方槽位）
		_boardUI.EnableKeyboardTargeting(includePlayerSlots: false, includeEnemySlots: true);
	}

	// ----- 随从放置 -----

	/// <summary>
	/// 随从放置模式下点击玩家槽位 → 召唤随从。
	/// </summary>
	private void HandleMinionPlacement(int slotIndex, bool isPlayerSide)
	{
		if (!isPlayerSide)
		{
			GD.Print("[CombatUI] 随从只能放置在己方槽位");
			return;
		}

		if (_selectedCard == null)
		{
			GD.PrintErr("[CombatUI] 内部错误：放置模式但 _selectedCard 为 null");
			ResetSelection();
			return;
		}

		GD.Print($"[CombatUI] 尝试放置随从 {_selectedCard.CardName}（{_selectedCard.Cost}费）到槽位 {slotIndex}");
		bool success = _combat.PlayMinion(_selectedCard, slotIndex);
		if (success)
		{
			GD.Print($"[CombatUI] ✓ 随从 {_selectedCard.CardName} 已放置到槽位 {slotIndex}");
			// 随从上场不走飞行动画——随从死亡时才飞向牌堆
		}
		else
		{
			GD.Print($"[CombatUI] ✗ PlayMinion 失败 — 查看上方 [CombatManager] 错误日志");
		}

		RefreshAll();
	}

	// ----- 法术目标 -----

	/// <summary>
	/// 法术目标选择模式下点击槽位 → 对槽位上的随从施放法术。
	/// </summary>
	private void HandleSpellTarget(int slotIndex, bool isPlayerSide)
	{
		if (_selectedCard == null)
		{
			ResetSelection();
			return;
		}

		var target = _combat.Board.GetMinionAt(slotIndex, isPlayerSide);
		if (target == null || target.IsDead)
		{
			GD.Print("[CombatUI] 法术目标无效（空槽位或已死亡）");
			return;
		}

		GD.Print($"[CombatUI] 对 {target.CardName} 施放 {_selectedCard.CardName}");
		bool success = _combat.PlaySpell(_selectedCard, target);
		if (success)
		{
			GD.Print($"[CombatUI] ✓ 法术 {_selectedCard.CardName} 已施放");
			AnimateCardToDiscardPile();
		}
		else
		{
			GD.Print($"[CombatUI] ✗ 法术施放失败");
		}
		RefreshAll();
	}

	// ----- 攻击目标 -----

	/// <summary>
	/// 攻击目标模式下点击敌方槽位 → 发动随从攻击。
	/// </summary>
	private void HandleAttackTarget(int slotIndex, bool isPlayerSide)
	{
		if (_selectedAttacker == null)
		{
			ResetSelection();
			return;
		}

		if (isPlayerSide)
		{
			GD.Print("[CombatUI] 不能攻击己方随从");
			return;
		}

		var defender = _combat.Board.GetMinionAt(slotIndex, isPlayerSide: false);
		if (defender == null || defender.IsDead)
		{
			GD.Print("[CombatUI] 攻击目标无效");
			return;
		}

		GD.Print($"[CombatUI] {_selectedAttacker.CardName} 攻击 {defender.CardName}");
		_combat.MinionAttack(_selectedAttacker, defender);
		RefreshAll();
	}

	/// <summary>
	/// 武器攻击目标模式下点击敌方槽位 → 发动武器攻击随从。
	/// </summary>
	private void HandleWeaponAttackTarget(int slotIndex, bool isPlayerSide)
	{
		if (isPlayerSide)
		{
			GD.Print("[CombatUI] 武器不能攻击己方随从");
			return;
		}

		var target = _combat.Board.GetMinionAt(slotIndex, isPlayerSide: false);
		if (target == null || target.IsDead)
		{
			GD.Print("[CombatUI] 武器攻击目标无效");
			return;
		}

		GD.Print($"[CombatUI] 武器攻击 {target.CardName}");

		// 清理敌方英雄按钮（可能在武器模式下修改过事件）
		_enemyHeroAttackButton.Pressed -= OnWeaponAttackHeroPressed;
		_enemyHeroAttackButton.Pressed += OnEnemyHeroAttackPressed;

		_combat.HeroWeaponAttackMinion(target);
		RefreshAll();
	}

	/// <summary>
	/// 武器主动技能目标模式下点击敌方槽位 → 对目标随从释放主动技能。
	/// 无视嘲讽限制。
	/// </summary>
	private void HandleActiveSkillTarget(int slotIndex, bool isPlayerSide)
	{
		if (isPlayerSide)
		{
			GD.Print("[CombatUI] 主动技能不能对己方随从释放");
			return;
		}

		var target = _combat.Board.GetMinionAt(slotIndex, isPlayerSide: false);
		if (target == null || target.IsDead)
		{
			GD.Print("[CombatUI] 主动技能目标无效");
			return;
		}

		GD.Print($"[CombatUI] 主动技能目标：{target.CardName}");

		// 恢复敌方英雄按钮的原始事件（如果在高亮时改了事件）
		_enemyHeroAttackButton.Pressed -= OnActiveSkillHeroPressed;
		_enemyHeroAttackButton.Pressed += OnEnemyHeroAttackPressed;

		// 设置目标并执行技能
		_combat.ActiveSkillTarget = target;
		_combat.UseWeaponActiveSkill();
		RefreshAll();
	}

	// ===== 事件处理——手牌选中 =====

	/// <summary>
	/// 手牌中卡牌被选中时的处理。
	/// 根据卡牌类型进入不同的选择模式，并将卡牌 UI 重 parent 到拖拽层使其可自由跟随鼠标。
	/// </summary>
	/// <param name="card">被选中的卡牌</param>
	private void OnCardSelectedFromHand(Card.Card card)
	{
		if (_combat.State.IsGameOver) return;
		if (card == null) return;

		// 取消之前的攻击选择
		_selectedAttacker = null;

		// 清理上一个拖拽中的卡牌（防止快速点击产生幽灵浮动卡）
		// 但先记住旧卡数据，以便后面归还手牌
		Card.Card? previousCardData = _dragCardUI?.Card;
		CleanupDragCard();

		// 将卡牌 UI 从 HandUI 移到 DragLayer，脱离 HBoxContainer 布局约束
		var cardUI = _handUI.GetCardUIFor(card);
		bool isKeyboardSel = _handUI.IsKeyboardSelection;
		Vector2 savedGlobalPos = Vector2.Zero;
		Vector2 savedSize = Vector2.Zero;
		Vector2 savedScale = Vector2.Zero;

		if (cardUI != null)
		{
			bool isMobileDrag = MobileInputRouter.IsMobile && cardUI.IsDragging;

			// 在 StopLayoutControl / Offset 清除之前保存全局位置——之后会变
			savedGlobalPos = cardUI.GlobalPosition;
			savedSize = cardUI.Size;
			savedScale = cardUI.Scale;

			_handUI.StopLayoutControl(cardUI);

			// 清除 HandUI 布局遗留的 OffsetTop（Select() 设的）。
			cardUI.OffsetTop = 0;
			cardUI.OffsetBottom = 0;
			cardUI.OffsetLeft = 0;
			cardUI.OffsetRight = 0;

			if (isMobileDrag)
			{
				// 移动端纯拖拽：卡牌已在跟随手指移动，用 Reparent 保持当前位置
				cardUI.Reparent(_dragLayer);
			}
			else if (isKeyboardSel)
			{
				// 键盘选牌：将卡牌中心对齐到选牌前它在手牌中的全局位置
				Vector2 preSize = savedSize * savedScale;
				Vector2 halfSize = preSize * 0.5f;
				cardUI.GetParent()?.RemoveChild(cardUI);
				_dragLayer.AddChild(cardUI);
				cardUI.Position = (savedGlobalPos + halfSize) - _dragLayer.GlobalPosition - halfSize;
			}
			else
			{
				Vector2 mousePosition = cardUI.LastClickGlobalPosition;
				Vector2 preSize = savedSize * savedScale;
				Vector2 halfSize = preSize * 0.5f;
				cardUI.GetParent()?.RemoveChild(cardUI);
				_dragLayer.AddChild(cardUI);
				cardUI.Position = mousePosition - halfSize - _dragLayer.GetGlobalRect().Position;
			}

			_dragCardUI = cardUI;

			// 从 HandUI 内部列表脱钩，防止 RefreshHand 误销毁拖拽中的卡片
			_handUI.DetachCardFromList(cardUI);

			// 订阅拖拽松手事件——用于拖拽→松手打出 / 松手取消
			cardUI.OnCardDropped += OnCardDroppedHandler;

			// 旧卡数据归还手牌——创建新的 CardUI 让它回到手牌可重新选中
			if (previousCardData != null)
			{
				_handUI.AddCardBack(previousCardData);
			}
		}

		switch (card.Type)
		{
			case CardType.Minion:
				EnterMinionPlacementMode(card);
				break;

			case CardType.Spell:
				if (card.Data.RequiresTarget)
				{
					EnterSpellTargetMode(card);
				}
				else
				{
					// 无目标法术（如「警戒」）：进入拖拽播放模式
					EnterNoTargetPlayMode(card);
				}
				break;

		case CardType.Domain:
			// 领域牌：进入拖拽播放模式（类 STS2 风格，拖到中央打出）
			EnterNoTargetPlayMode(card);
			break;

		case CardType.Status:
			// 状态牌：进入无目标播放模式（自动以玩家英雄为目标）
			EnterNoTargetPlayMode(card);
			break;

			default:
				GD.Print($"[CombatUI] 未知卡牌类型：{card.Type}");
				break;
		}

		// 键盘选牌 → 无目标卡牌（领域/无目标法术/状态牌）需启动拖拽使其跟随鼠标
		if (isKeyboardSel && _selectionMode == SelectionMode.PlayingNoTargetCard && _dragCardUI != null)
		{
			Vector2 clickCenter = savedGlobalPos + savedSize * savedScale * 0.5f;
			_dragCardUI.BeginDragFrom(clickCenter);
		}
	}

	/// <summary>
	/// 右键取消拖拽——卡牌回到手牌，退出所有选择模式。
	/// </summary>
	private void OnCardDragCancelled()
	{
		GD.Print("[CombatUI] 拖拽取消");
		CleanupDragCard();
		ResetSelection();

		// 重建手牌 UI（恢复卡牌到正确位置）
		_handUI.RefreshHand();
	}

	/// <summary>
	/// 拖拽中左键松开的事件处理。
	/// 根据松手位置判断：落在有效槽位→打出，否则→取消（等效右键）。
	/// 实现「拖拽松手打出」和「点击选中→点击目标打出」的等效性。
	/// </summary>
	private void OnCardDroppedHandler(CardUI cardUI, Vector2 screenPos)
	{
		if (_combat.State.IsGameOver) return;
		if (_dragCardUI != cardUI) return;

		GD.Print($"[CombatUI] OnCardDropped — 模式 {_selectionMode}, 坐标 ({screenPos.X:F0}, {screenPos.Y:F0})");

		switch (_selectionMode)
		{
			case SelectionMode.PlacingMinion:
				HandleMinionDrop(screenPos);
				break;

			case SelectionMode.TargetingSpell:
				HandleSpellDrop(screenPos);
				break;

			case SelectionMode.SelectingAttackTarget:
				HandleAttackDrop(screenPos);
				break;

			case SelectionMode.PlayingNoTargetCard:
				HandleNoTargetCardDrop(screenPos);
				break;

			default:
				// 未在有效选择模式 — 取消
				GD.Print("[CombatUI] 松手时未在选择模式，取消拖拽");
				OnCardDragCancelled();
				break;
		}
	}

	/// <summary>
	/// 随从放置模式下的松手处理：检查落点是否在玩家方槽位上。
	/// </summary>
	private void HandleMinionDrop(Vector2 screenPos)
	{
		GD.Print($"[CombatUI] 拖拽松手 — 坐标 ({screenPos.X:F0}, {screenPos.Y:F0})");
		var hit = _boardUI.GetSlotAtPosition(screenPos);
		if (hit != null && hit.Value.isPlayerSide)
		{
			GD.Print($"[CombatUI] 命中己方槽位 {hit.Value.slotIndex}，执行放置");
			HandleMinionPlacement(hit.Value.slotIndex, hit.Value.isPlayerSide);
		}
		else
		{
			GD.Print(hit != null
				? $"[CombatUI] 命中敌方槽位 {hit.Value.slotIndex}，但随从只能放在己方"
				: "[CombatUI] 未命中任何槽位，取消拖拽");
			OnCardDragCancelled();
		}
	}

	/// <summary>
	/// 法术目标模式下的松手处理：检查落点是否在有随从的槽位上。
	/// </summary>
	private void HandleSpellDrop(Vector2 screenPos)
	{
		// 优先检查是否落在敌方英雄面板上
		if (_enemyHeroSpellButton is { Visible: true }
			&& _enemyCards.Count > 0
			&& _enemyCards[0].GetGlobalRect().HasPoint(screenPos))
		{
			GD.Print("[CombatUI] 法术松手位置：敌方英雄");
			OnEnemyHeroSpellTarget();
			return;
		}

		// 检查是否落在己方英雄面板上
		if (_playerHeroSpellButton.Visible && _playerHeroPanel.GetGlobalRect().HasPoint(screenPos))
		{
			GD.Print("[CombatUI] 法术松手位置：己方英雄");
			OnPlayerHeroSpellTarget();
			return;
		}

		var hit = _boardUI.GetSlotAtPosition(screenPos);
		if (hit != null)
		{
			var target = _combat.Board.GetMinionAt(hit.Value.slotIndex, hit.Value.isPlayerSide);
			if (target != null && !target.IsDead)
			{
				HandleSpellTarget(hit.Value.slotIndex, hit.Value.isPlayerSide);
			}
			else
			{
				GD.Print("[CombatUI] 法术松手位置无有效随从目标");
				OnCardDragCancelled();
			}
		}
		else
		{
			GD.Print("[CombatUI] 法术松手位置无效，取消拖拽");
			OnCardDragCancelled();
		}
	}

	/// <summary>
	/// 无目标卡牌播放模式下的松手处理：检查落点是否在播放区域内。
	/// 在区域内→打出卡牌；在区域外→取消拖拽（等效右键）。
	/// </summary>
	private void HandleNoTargetCardDrop(Vector2 screenPos)
	{
		if (_selectedCard == null)
		{
			GD.PrintErr("[CombatUI] 内部错误：播放模式但 _selectedCard 为 null");
			ResetSelection();
			_handUI.RefreshHand();
			return;
		}

		bool inZone = IsInPlayZone(screenPos);
		GD.Print($"[CombatUI] 无目标卡牌松手 — {_selectedCard.CardName}，{(inZone ? "在播放区域内" : "不在播放区域内")}");

		if (!inZone)
		{
			OnCardDragCancelled();
			return;
		}

		PlaySelectedNoTargetCard();
	}

	/// <summary>
	/// 攻击目标模式下的松手处理：检查落点是否在敌方槽位或敌方英雄面板上。
	/// </summary>
	private void HandleAttackDrop(Vector2 screenPos)
	{
		var hit = _boardUI.GetSlotAtPosition(screenPos);
		if (hit != null && !hit.Value.isPlayerSide)
		{
			HandleAttackTarget(hit.Value.slotIndex, hit.Value.isPlayerSide);
		}
		else if (_enemyHeroAttackButton is { Visible: true }
			&& _enemyCards.Count > 0
			&& _enemyCards[0].GetGlobalRect().HasPoint(screenPos))
		{
			OnEnemyHeroAttackPressed();
		}
		else
		{
			GD.Print("[CombatUI] 攻击松手位置无效，取消选择");
			ResetSelection();
			_handUI.RefreshHand();
		}
	}

	/// <summary>
	/// 清理当前拖拽卡牌 UI 引用并取消订阅。
	/// 先隐藏再 QueueFree，避免与 RefreshHand 新建的卡牌产生视觉重叠。
	/// </summary>
	private void CleanupDragCard()
	{
		if (_dragCardUI != null)
		{
			_dragCardUI.OnCardDropped -= OnCardDroppedHandler;
			_dragCardUI.OnDragMove -= OnDragMoveForPlayZone;
			_dragCardUI.CancelDragSilent(); // 退出拖拽状态，防止 _Process 残留
			_dragCardUI.Visible = false;     // 立即隐藏，防止与 RefreshHand 新建卡牌重叠
			_dragCardUI.QueueFree();
			_dragCardUI = null;
		}
	}

	/// <summary>
	/// 将当前拖拽卡牌从 _dragLayer 提取出来并启动飞向牌堆的动画。
	/// 目标位置根据卡牌关键词自动选择：轮战卡牌 → 抽牌堆，普通卡牌 → 弃牌堆。
	/// 调用后 _dragCardUI 设为 null，CleanupDragCard 不再处理这张卡。
	/// 仅在卡牌成功打出后调用。
	/// </summary>
	private void AnimateCardToDiscardPile()
	{
		if (_dragCardUI == null) return;

		var cardUI = _dragCardUI;
		_dragCardUI = null; // 提取所有权，防止 CleanupDragCard 二次处理

		cardUI.OnCardDropped -= OnCardDroppedHandler;
		cardUI.OnDragMove -= OnDragMoveForPlayZone;
		cardUI.CancelDragSilent();

		// 领域 → 玩家效果栏，轮战 → 抽牌堆，普通 → 弃牌堆
		Vector2 targetPos;
		if (_selectedCard?.Type == CardType.Domain)
			targetPos = GetPlayerEffectBarCenter();
		else if (_selectedCard?.HasRecycle ?? false)
			targetPos = GetDrawPileCenter();
		else
			targetPos = GetDiscardPileCenter();
		CardFlyVfx.PlayToDiscard(cardUI, targetPos, _cardFlyLayer);
	}

	/// <summary>
	/// 获取弃牌堆按钮中心点的屏幕位置。
	/// </summary>
	private Vector2 GetDiscardPileCenter()
	{
		return _discardPileBtn.GlobalPosition + _discardPileBtn.Size / 2f;
	}

	/// <summary>
	/// 获取抽牌堆按钮中心点的屏幕位置，用于轮战卡牌飞行动画。
	/// </summary>
	private Vector2 GetDrawPileCenter()
	{
		return _drawPileBtn.GlobalPosition + _drawPileBtn.Size / 2f;
	}

	/// <summary>
	/// 获取玩家效果栏中心点的屏幕位置，用于领域卡牌飞行动画。
	/// 领域打出后附加到英雄，不进入任何牌堆。
	/// </summary>
	private Vector2 GetPlayerEffectBarCenter()
	{
		return _playerEffectBar.GlobalPosition + _playerEffectBar.Size / 2f;
	}

	/// <summary>
	/// 进入随从放置模式——高亮玩家方可用槽位（绿色）。
	/// </summary>
	private void EnterMinionPlacementMode(Card.Card card)
	{
		_selectionMode = SelectionMode.PlacingMinion;
		_selectedCard = card;

		// 收集玩家方空槽位
		var validSlots = new List<int>();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			if (_combat.Board.GetMinionAt(i, isPlayerSide: true) == null)
			{
				validSlots.Add(i);
			}
		}

		if (validSlots.Count > 0)
		{
			_boardUI.HighlightSlots(validSlots, isPlayerSide: true, highlight: true);
			GD.Print($"[CombatUI] 随从放置模式——可放置槽位：{string.Join(", ", validSlots)}");
		}
		else
		{
			GD.Print("[CombatUI] 随从放置模式——无可用槽位（战场已满）");
		}

		// 启用键盘目标选择（仅玩家方空槽位）
		_boardUI.EnableKeyboardTargeting(includePlayerSlots: true, includeEnemySlots: false);
	}

	/// <summary>
	/// 进入法术目标选择模式——根据卡牌的 TargetFilter 过滤合法目标。
	/// 高亮通过子集匹配的敌方/己方随从，显示对应英雄施法按钮。
	/// </summary>
	private void EnterSpellTargetMode(Card.Card card)
	{
		_selectionMode = SelectionMode.TargetingSpell;
		_selectedCard = card;

		var require = card.Data.TargetFilter;
		var exclude = card.Data.ExcludeFilter;

		// 高亮敌方随从 —— 仅高亮通过 TargetFilter 的目标
		var enemyTargets = new List<int>();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			var m = _combat.Board.GetMinionAt(i, isPlayerSide: false);
			if (m != null && !m.IsDead && TargetTagsHelper.IsValidTarget(m.GetTargetTags(), require, exclude))
			{
				enemyTargets.Add(i);
			}
		}
		_boardUI.HighlightSlots(enemyTargets, isPlayerSide: false, highlight: true);

		// 高亮己方随从 —— 仅高亮通过 TargetFilter 的目标
		var friendlyTargets = new List<int>();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			var m = _combat.Board.GetMinionAt(i, isPlayerSide: true);
			if (m != null && !m.IsDead && TargetTagsHelper.IsValidTarget(m.GetTargetTags(), require, exclude))
			{
				friendlyTargets.Add(i);
			}
		}
		if (friendlyTargets.Count > 0)
		{
			_boardUI.HighlightSlots(friendlyTargets, isPlayerSide: true, highlight: true);
		}

		// 高亮敌方英雄作为法术目标
		_enemyHeroSpellButton.Visible = TargetTagsHelper.IsValidTarget(
			_combat.EnemyHero.GetTargetTags(), require, exclude);

		// 高亮己方英雄作为法术目标
		_playerHeroSpellButton.Visible = TargetTagsHelper.IsValidTarget(
			_combat.PlayerHero.GetTargetTags(), require, exclude);

		GD.Print($"[CombatUI] 法术目标模式——{_selectedCard.CardName}（require={require} exclude={exclude}，" +
				  $"敌方随从 {enemyTargets.Count} + 己方随从 {friendlyTargets.Count} + " +
				  $"{(enemyTargets.Count > 0 || friendlyTargets.Count > 0 ? " + 英雄" : "")}）");

		// 启用键盘目标选择（根据高亮的阵营方）
		_boardUI.EnableKeyboardTargeting(
			includePlayerSlots: friendlyTargets.Count > 0,
			includeEnemySlots: enemyTargets.Count > 0);
	}

	/// <summary>
	/// 进入无目标卡牌播放模式——显示播放区域指示器，等待玩家拖拽到播放区域松手打出。
	/// 适用于：领域（Domain）、无目标的法术（Spell.RequiresTarget == false）。
	/// </summary>
	private void EnterNoTargetPlayMode(Card.Card card)
	{
		GD.Print($"[CombatUI] EnterNoTargetPlayMode — 类型={card.Type}, 名称={card.CardName}, playZonePanel={_playZonePanel != null}, isInsideTree={IsInsideTree()}");

		_selectionMode = SelectionMode.PlayingNoTargetCard;
		_selectedCard = card;

		ShowPlayZonePanel();

		// 订阅拖拽卡牌的逐帧位置更新（用于播放区域判定和视觉反馈）
		if (_dragCardUI != null)
		{
			_dragCardUI.OnDragMove += OnDragMoveForPlayZone;
		}

		GD.Print($"[CombatUI] 无目标播放模式——{card.CardName}（拖到绿色区域松手打出，右键取消）");
	}

	/// <summary>
	/// 高亮合法攻击目标——敌方有嘲讽随从时仅高亮嘲讽目标，
	/// 无嘲讽时高亮所有敌方随从并显示攻击英雄按钮。
	/// </summary>
	private void HighlightValidAttackTargets()
	{
		_boardUI.ClearHighlights();

		var enemyTaunts = _combat.Board.GetTaunts(ofEnemy: true);
		if (enemyTaunts.Count > 0)
		{
			// 有嘲讽——仅高亮嘲讽随从
			var tauntIndices = enemyTaunts
				.Where(m => m.BoardSlotIndex >= 0)
				.Select(m => m.BoardSlotIndex)
				.ToList();

			_boardUI.HighlightSlots(tauntIndices, isPlayerSide: false, highlight: true);
			_enemyHeroAttackButton.Visible = false;

			GD.Print($"[CombatUI] 攻击目标模式——敌方有 {enemyTaunts.Count} 个嘲讽随从阻挡");
		}
		else
		{
			// 无嘲讽——高亮所有敌方随从
			var allEnemyIndices = new List<int>();
			for (int i = 0; i < Board.MaxSlotsPerSide; i++)
			{
				var m = _combat.Board.GetMinionAt(i, isPlayerSide: false);
				if (m != null && !m.IsDead)
				{
					allEnemyIndices.Add(i);
				}
			}

			if (allEnemyIndices.Count > 0)
			{
				_boardUI.HighlightSlots(allEnemyIndices, isPlayerSide: false, highlight: true);
			}

			// 显示攻击英雄按钮
			_enemyHeroAttackButton.Visible = true;
			_enemyHeroAttackButton.Disabled = false;

			GD.Print("[CombatUI] 攻击目标模式——可攻击敌方英雄");
		}
	}

	/// <summary>
	/// 更新武器信息显示——攻击力、费用、冷却信息。
	/// 普通模式下显示武器攻击按钮（如果可用），技能按钮显示冷却状态。
	/// </summary>
	private void UpdateWeaponDisplay()
	{
		if (_combat == null) return;

		// --- 玩家武器 ---
		var weapon = _combat.PlayerHero.Weapon;
		if (weapon != null)
		{
			// 武器信息标签
			string costSuffix = Localization.Localization.T("ui.combat.cost_suffix", "费");
			string costText = weapon.AttackCost > 0 ? $"{weapon.AttackCost}{costSuffix}" : Localization.Localization.T("ui.combat.free_cost", "免费");
			string disabledText = weapon.IsDisabled ? Localization.Localization.T("ui.combat.disabled_suffix", " [禁用]") : "";
			string localWeaponName = !string.IsNullOrEmpty(weapon.NameKey)
				? Localization.Localization.T(weapon.NameKey, weapon.Name)
				: weapon.Name;
			_weaponInfoLabel.Text = Localization.Localization.T("ui.combat.weapon_format", "{name} {attack}攻 {cost}")
				.Replace("{name}", localWeaponName)
				.Replace("{attack}", weapon.Attack.ToString())
				.Replace("{cost}", costText) + disabledText;

			if (weapon.PassiveSkill != null)
			{
				string localPassiveDesc = !string.IsNullOrEmpty(weapon.PassiveSkill.DescKey)
					? Localization.Localization.T(weapon.PassiveSkill.DescKey, weapon.PassiveSkill.Description)
					: weapon.PassiveSkill.Description;
				_weaponInfoLabel.TooltipText = Localization.Localization.T("ui.combat.passive_skill", "被动：{desc}")
					.Replace("{desc}", localPassiveDesc);
			}

			// 武器攻击按钮——普通模式下显示
			if (_selectionMode == SelectionMode.Normal && !_combat.State.IsGameOver)
			{
				bool canAttack = _combat.PlayerHero.CanWeaponAttack()
					&& _combat.PlayerHero.CanSpendMana(weapon.AttackCost);
				_weaponAttackButton.Visible = true;
				_weaponAttackButton.Disabled = !canAttack || weapon.IsDisabled;
				_weaponAttackButton.Text = weapon.IsDisabled
					? Localization.Localization.T("ui.combat.weapon_disabled", "⚔ 武器攻击 [禁用]")
					: Localization.Localization.T("ui.combat.weapon_attack_cost", "⚔ 武器攻击 ({cost}费)").Replace("{cost}", weapon.AttackCost.ToString());
			}

			// 主动技能按钮
			if (weapon.ActiveSkill != null)
			{
				var active = weapon.ActiveSkill;
				_weaponActiveSkillButton.Visible = (_selectionMode == SelectionMode.Normal || _selectionMode == SelectionMode.SelectingActiveSkillTarget) && !_combat.State.IsGameOver;
				_weaponActiveSkillButton.Disabled = !active.CanUse(_combat.PlayerHero);

				if (active.CurrentCooldown > 0)
				{
					string localActiveName = !string.IsNullOrEmpty(active.NameKey)
						? Localization.Localization.T(active.NameKey, active.Name)
						: active.Name;
					_weaponActiveSkillButton.Text = Localization.Localization.T("ui.combat.skill_cooldown", "✦ {name} (冷却{cooldown})")
						.Replace("{name}", localActiveName).Replace("{cooldown}", active.CurrentCooldown.ToString());
				}
				else
				{
					string localActiveName = !string.IsNullOrEmpty(active.NameKey)
						? Localization.Localization.T(active.NameKey, active.Name)
						: active.Name;
					_weaponActiveSkillButton.Text = Localization.Localization.T("ui.combat.skill_cost", "✦ {name} ({cost}费)")
						.Replace("{name}", localActiveName).Replace("{cost}", active.Cost.ToString());
				}
			}
		}
		else
		{
			_weaponInfoLabel.Text = Localization.Localization.T("ui.combat.weapon_none", "无武器");
		}

		// --- 敌方武器（已迁移到 EnemyIdentityCard） ---
		if (_enemyWeaponLabel != null)
		{
			var enemyWeapon = _combat.EnemyHero.Weapon;
			if (enemyWeapon != null)
			{
				string disabledText = enemyWeapon.IsDisabled ? Localization.Localization.T("ui.combat.disabled_suffix", " [禁用]") : "";
				string localEnemyWeaponName = !string.IsNullOrEmpty(enemyWeapon.NameKey)
					? Localization.Localization.T(enemyWeapon.NameKey, enemyWeapon.Name)
					: enemyWeapon.Name;
				_enemyWeaponLabel.Text = Localization.Localization.T("ui.combat.enemy_weapon_format", "武器: {name} {attack}攻{disabled}")
					.Replace("{name}", localEnemyWeaponName).Replace("{attack}", enemyWeapon.Attack.ToString()).Replace("{disabled}", disabledText);
			}
			else
			{
				_enemyWeaponLabel.Text = "";
			}
		}
	}

	/// <summary>
	/// 更新双方英雄的效果图标显示。
	/// 通过 Hero.GetDisplayableEffects() 聚合所有效果，传入 EffectBar。
	/// </summary>
	private void UpdateStatusEffectDisplay()
	{
		if (_combat == null) return;

		// 玩家英雄效果
		_playerEffectBar.Populate(_combat.PlayerHero.GetDisplayableEffects());

		// 敌方英雄效果（旧版单敌人兼容层——多敌人时使用 EnemyIdentityCard 内的 EffectBar）
		if (_enemyEffectBar != null)
		{
			_enemyEffectBar.Populate(_combat.EnemyHero.GetDisplayableEffects());
		}
	}

	private void UpdateHeatDisplay() => _heatBar?.Refresh();
	private void UpdateRelicDisplay() => _relicBar?.Refresh();

	// ===== 事件处理——敌方英雄攻击 =====

	/// <summary>
	/// 攻击敌方英雄按钮点击——执行随从攻击英雄。
	/// </summary>
	private void OnEnemyHeroAttackPressed()
	{
		if (_combat.State.IsGameOver) return;
		if (_selectedAttacker == null)
		{
			GD.PrintErr("[CombatUI] 无攻击方随从");
			return;
		}

		GD.Print($"[CombatUI] {_selectedAttacker.CardName} 攻击敌方英雄");
		_combat.MinionAttackHero(_selectedAttacker, _combat.EnemyHero);
		RefreshAll();
	}

	/// <summary>
	/// 对敌方英雄施法按钮点击——执行法术对敌方英雄施放。
	/// </summary>
	private void OnEnemyHeroSpellTarget()
	{
		if (_combat.State.IsGameOver) return;

		// 开发者伤害模式：对敌方英雄造成伤害
		if (_selectionMode == SelectionMode.DevDamageTargeting)
		{
			_combat.EnemyHero.TakeDamage(_devDamageAmount, null);
			_combat.CheckVictoryOrDefeat();
			ExitDevDamageMode();
			return;
		}

		if (_selectedCard == null)
		{
			GD.PrintErr("[CombatUI] 无法术牌选中");
			return;
		}

		GD.Print($"[CombatUI] 对敌方英雄施放 {_selectedCard.CardName}");
		_combat.PlaySpell(_selectedCard, _combat.EnemyHero);
		RefreshAll();
	}

	/// <summary>
	/// 对己方英雄施法按钮点击——执行法术对己方英雄施放。
	/// </summary>
	private void OnPlayerHeroSpellTarget()
	{
		if (_combat.State.IsGameOver) return;

		if (_selectedCard == null)
		{
			GD.PrintErr("[CombatUI] 无法术牌选中");
			return;
		}

		GD.Print($"[CombatUI] 对己方英雄施放 {_selectedCard.CardName}");
		_combat.PlaySpell(_selectedCard, _combat.PlayerHero);
		RefreshAll();
	}

	// ===== 事件处理——武器攻击 =====

	/// <summary>
	/// 武器攻击按钮点击——进入武器目标选择模式。
	/// 高亮敌方随从并显示攻击敌方英雄按钮。
	/// </summary>
	private void OnWeaponAttackPressed()
	{
		if (_combat.State.IsGameOver) return;
		if (!_combat.PlayerHero.CanWeaponAttack()) return;

		var weapon = _combat.PlayerHero.Weapon;
		if (weapon == null || weapon.IsDisabled) return;
		if (!_combat.PlayerHero.CanSpendMana(weapon.AttackCost)) return;

		GD.Print($"[CombatUI] 进入武器攻击目标选择模式 — {weapon.Name}");

		_selectionMode = SelectionMode.SelectingWeaponTarget;
		_selectedAttacker = null;
		_selectedCard = null;

		// 高亮合法攻击目标
		HighlightWeaponTargets();

		// 启用键盘目标选择（仅敌方槽位）
		_boardUI.EnableKeyboardTargeting(includePlayerSlots: false, includeEnemySlots: true);
	}

	/// <summary>
	/// 武器主动技能按钮点击——进入主动技能目标选择模式。
	/// 所有敌方随从 + 敌方英雄都是合法目标（无视嘲讽——这是减益技能不是攻击）。
	/// </summary>
	private void OnWeaponActiveSkillPressed()
	{
		if (_combat.State.IsGameOver) return;

		var weapon = _combat.PlayerHero.Weapon;
		if (weapon?.ActiveSkill == null) return;
		if (!weapon.ActiveSkill.CanUse(_combat.PlayerHero)) return;

		GD.Print($"[CombatUI] 进入主动技能目标选择模式 — {weapon.ActiveSkill.Name}");

		_selectionMode = SelectionMode.SelectingActiveSkillTarget;
		_selectedAttacker = null;
		_selectedCard = null;

		HighlightActiveSkillTargets();

		// 启用键盘目标选择（仅敌方槽位）
		_boardUI.EnableKeyboardTargeting(includePlayerSlots: false, includeEnemySlots: true);
	}

	/// <summary>
	/// 高亮武器主动技能合法目标——敌方所有随从 + 敌方英雄。
	/// 无视嘲讽限制（主动技能是减益效果，不是攻击）。
	/// </summary>
	private void HighlightActiveSkillTargets()
	{
		_boardUI.ClearHighlights();

		// 高亮所有敌方随从（无视嘲讽）
		var allEnemyIndices = new List<int>();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			var m = _combat.Board.GetMinionAt(i, isPlayerSide: false);
			if (m != null && !m.IsDead)
			{
				allEnemyIndices.Add(i);
			}
		}

		if (allEnemyIndices.Count > 0)
		{
			_boardUI.HighlightSlots(allEnemyIndices, isPlayerSide: false, highlight: true);
		}

		// 显示敌方英雄按钮作为目标（复用，修改文本和事件）
		_enemyHeroAttackButton.Text = Localization.Localization.T("ui.combat.ion_pulse", "✦ 离子脉冲");
		_enemyHeroAttackButton.Visible = true;
		_enemyHeroAttackButton.Disabled = false;

		// 从 Normal 模式进入时只有 OnEnemyHeroAttackPressed 是连接的
		_enemyHeroAttackButton.Pressed -= OnEnemyHeroAttackPressed;
		_enemyHeroAttackButton.Pressed += OnActiveSkillHeroPressed;

		GD.Print("[CombatUI] 主动技能目标模式——可对敌方英雄或任意随从释放");
	}

	/// <summary>
	/// 武器主动技能对敌方英雄释放——在技能目标选择模式下点击敌方英雄按钮触发。
	/// 目标设为 null（IonPulse 默认行为：禁用敌方武器）。
	/// </summary>
	private void OnActiveSkillHeroPressed()
	{
		if (_combat.State.IsGameOver) return;

		GD.Print("[CombatUI] 主动技能目标：敌方英雄");

		// 恢复敌方英雄按钮的原始事件
		_enemyHeroAttackButton.Pressed -= OnActiveSkillHeroPressed;
		_enemyHeroAttackButton.Pressed += OnEnemyHeroAttackPressed;

		// 目标为 null 表示对英雄释放（IonPulse 的默认行为）
		_combat.ActiveSkillTarget = null;
		_combat.UseWeaponActiveSkill();
		RefreshAll();
	}

	/// <summary>
	/// 高亮武器攻击合法目标——敌方随从（受嘲讽限制）+ 敌方英雄。</summary>
	private void HighlightWeaponTargets()
	{
		_boardUI.ClearHighlights();

		var enemyTaunts = _combat.Board.GetTaunts(ofEnemy: true);
		if (enemyTaunts.Count > 0)
		{
			// 有嘲讽——仅高亮嘲讽随从
			var tauntIndices = enemyTaunts
				.Where(m => m.BoardSlotIndex >= 0)
				.Select(m => m.BoardSlotIndex)
				.ToList();

			_boardUI.HighlightSlots(tauntIndices, isPlayerSide: false, highlight: true);
			_enemyHeroAttackButton.Visible = false;

			GD.Print($"[CombatUI] 武器攻击模式——敌方有 {enemyTaunts.Count} 个嘲讽随从阻挡");
		}
		else
		{
			// 无嘲讽——高亮所有敌方随从
			var allEnemyIndices = new List<int>();
			for (int i = 0; i < Board.MaxSlotsPerSide; i++)
			{
				var m = _combat.Board.GetMinionAt(i, isPlayerSide: false);
				if (m != null && !m.IsDead)
				{
					allEnemyIndices.Add(i);
				}
			}

			if (allEnemyIndices.Count > 0)
			{
				_boardUI.HighlightSlots(allEnemyIndices, isPlayerSide: false, highlight: true);
			}

			// 显示攻击英雄按钮（复用已有的敌方英雄攻击按钮，修改文本）
			_enemyHeroAttackButton.Text = Localization.Localization.T("ui.combat.weapon_attack_cost", "⚔ 武器攻击 ({cost}费)").Replace("{cost}", _combat.PlayerHero.Weapon!.AttackCost.ToString());
			_enemyHeroAttackButton.Visible = true;
			_enemyHeroAttackButton.Disabled = false;

			// 断开旧事件，连接武器攻击事件
			_enemyHeroAttackButton.Pressed -= OnEnemyHeroAttackPressed;
			_enemyHeroAttackButton.Pressed += OnWeaponAttackHeroPressed;

			GD.Print("[CombatUI] 武器攻击模式——可攻击敌方英雄或随从");
		}
	}

	/// <summary>
	/// 武器攻击敌方英雄——在武器目标选择模式下点击敌方英雄按钮触发。
	/// </summary>
	private void OnWeaponAttackHeroPressed()
	{
		if (_combat.State.IsGameOver) return;

		GD.Print("[CombatUI] 武器攻击敌方英雄");
		_combat.HeroWeaponAttackHero(_combat.EnemyHero);

		// 恢复敌方英雄按钮的原始事件
		_enemyHeroAttackButton.Pressed -= OnWeaponAttackHeroPressed;
		_enemyHeroAttackButton.Pressed += OnEnemyHeroAttackPressed;

		RefreshAll();
	}

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
		if (_combat == null) return;
		if (_combat.State.IsGameOver) return;
		if (_endTurnButton.Disabled) return;
		if (_combat.IsDiscovering) return;
		if (!_combat.State.IsPlayerTurn) return;

		GD.Print("[CombatUI] 热键结束回合");
		_combat.EndPlayerTurn();
		RefreshAll();
	}

	/// <summary>
	/// 显示游戏结束弹窗。
	/// 胜利：跳转至路线选择地图；失败：返回主菜单。
	/// </summary>
	/// <param name="isVictory">是否胜利</param>
	private void ShowGameOverPopup(bool isVictory)
	{
		if (_gameOverPopup == null) return;

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
		if (_isVictory)
		{
			GD.Print("[CombatUI] 继续冒险 → 路线选择地图");
			var gm = GameManager.Instance;
			gm?.RunState?.CompleteRoom();

			// 弹出战后奖励界面
			ShowPostBattleReward();
		}
		else
		{
			GD.Print("[CombatUI] 返回主菜单");
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
		if (_pauseMenu != null) return;

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
		if (_pauseMenu == null) return;

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
		if (_combat == null || _combat.State.IsGameOver) return;
		ShowPauseMenu();
	}

	/// <summary>
	/// 切换暂停状态（热键 ESC 或按钮触发）。
	/// 游戏结束、发现选牌或不在场景树内时不响应。
	/// </summary>
	private void TogglePause()
	{
		if (!IsInsideTree()) return;
		if (_combat == null || _combat.State.IsGameOver) return;
		if (_combat.IsDiscovering) return;

		if (_isPaused)
			HidePauseMenu();
		else
			ShowPauseMenu();
	}

	/// <summary>
	/// 全局取消操作（热键 ESC/右键或移动端取消按钮触发）。
	/// 按优先级依次检查：手牌选择 → 开发者伤害 → 攻击/武器/法术选择。
	/// </summary>
	private void HandleCancel()
	{
		if (!IsInsideTree()) return;

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

		// 攻击/武器/法术目标选择模式——重置选择
		if (_selectionMode == SelectionMode.SelectingAttackTarget
			|| _selectionMode == SelectionMode.SelectingWeaponTarget
			|| _selectionMode == SelectionMode.SelectingActiveSkillTarget
			|| _selectionMode == SelectionMode.TargetingSpell
			|| _selectionMode == SelectionMode.PlacingMinion
			|| _selectionMode == SelectionMode.PlayingNoTargetCard)
		{
			GD.Print("[CombatUI] 热键取消选择");
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
			gm?.SavePlayerHealth(_combat.PlayerHero.CurrentHealth, _combat.PlayerHero.MaxHealth);
		}

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

		GetTree().ChangeSceneToFile("res://Scenes/Combat.tscn");
	}

	// ===== 开发者伤害模式 =====

	/// <summary>
	/// 进入开发者伤害目标选择模式（由 DevConsole /damage -c N 触发）。
	/// 高亮所有合法目标，点击任意实体造成指定伤害，右键取消。
	/// </summary>
	public void EnterDevDamageMode(int damageAmount)
	{
		if (_combat.State.IsGameOver) return;

		_devDamageAmount = damageAmount;
		_selectionMode = SelectionMode.DevDamageTargeting;
		_selectedCard = null;
		_selectedAttacker = null;

		// 高亮所有存活随从 + 显示敌方英雄按钮
		_boardUI.ClearHighlights();
		var playerSlots = new List<int>();
		var enemySlots = new List<int>();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			var pm = _combat.Board.GetMinionAt(i, isPlayerSide: true);
			if (pm != null && !pm.IsDead) playerSlots.Add(i);
			var em = _combat.Board.GetMinionAt(i, isPlayerSide: false);
			if (em != null && !em.IsDead) enemySlots.Add(i);
		}
		_boardUI.HighlightSlots(playerSlots, isPlayerSide: true, highlight: true);
		_boardUI.HighlightSlots(enemySlots, isPlayerSide: false, highlight: true);

		// 敌方英雄按钮
		_enemyHeroSpellButton.Text = $"⚡ 对敌方英雄造成 {damageAmount} 点伤害";
		_enemyHeroSpellButton.Visible = true;

		// 启用键盘目标选择（双方槽位）
		_boardUI.EnableKeyboardTargeting(includePlayerSlots: true, includeEnemySlots: true);

		GD.Print($"[CombatUI] 开发者伤害模式 — 点击目标造成 {damageAmount} 点伤害（右键取消）");
	}

	private void ExitDevDamageMode()
	{
		_boardUI.DisableKeyboardTargeting();

		_boardUI.ClearHighlights();
		_enemyHeroSpellButton.Visible = false;
		_playerHeroSpellButton.Visible = false;
		_selectionMode = SelectionMode.Normal;
		RefreshAll();

		var h = OnDevDamageModeCompleted;
		OnDevDamageModeCompleted = null;
		h?.Invoke();
	}

	/// <summary>
	/// 开发者模式：对指定位置的随从造成伤害。
	/// </summary>
	private void HandleDevDamageSlot(int slotIndex, bool isPlayerSide)
	{
		GD.Print($"[CombatUI] DevDamageSlot: slot={slotIndex}, side={(isPlayerSide ? "player" : "enemy")}");
		var target = _combat.Board.GetMinionAt(slotIndex, isPlayerSide);
		if (target == null || target.IsDead)
		{
			GD.Print($"[CombatUI] DevDamageSlot: no valid target");
			return;
		}

		GD.Print($"[CombatUI] DevDamage: {_devDamageAmount} dmg → {(isPlayerSide ? "己方" : "敌方")} {target.CardName}");
		target.TakeDamage(_devDamageAmount, null);
		_combat.CheckDeaths();
		_combat.CheckVictoryOrDefeat();
		ExitDevDamageMode();
	}

	// ===== 箭头位置辅助方法 =====

	/// <summary>
	/// 获取随从所在槽位的屏幕中心坐标。
	/// </summary>
	/// <param name="minion">战场上的随从</param>
	/// <returns>槽位屏幕中心坐标；随从无有效槽位时返回 Vector2.Zero</returns>
	private Vector2 GetMinionScreenCenter(Minion minion)
	{
		int slotIndex = minion.BoardSlotIndex;
		if (slotIndex < 0 || slotIndex >= Board.MaxSlotsPerSide) return Vector2.Zero;
		return _boardUI.GetSlotScreenCenter(slotIndex, minion.IsPlayerSide);
	}

	/// <summary>
	/// 获取敌方身份卡（EnemyIdentityCard）的屏幕中心坐标。
	/// </summary>
	/// <param name="enemyIndex">敌人索引（对应 EnemyUnits 列表）</param>
	/// <returns>身份卡屏幕中心坐标；索引越界返回 Vector2.Zero</returns>
	private Vector2 GetEnemyIdentityCardCenter(int enemyIndex)
	{
		if (enemyIndex >= 0 && enemyIndex < _enemyCards.Count)
		{
			var rect = _enemyCards[enemyIndex].GetGlobalRect();
			return rect.Position + rect.Size / 2;
		}
		return Vector2.Zero;
	}

	/// <summary>
	/// 获取敌方身份卡朝向目标一侧的边缘锚点。
	/// 意图箭头应从攻击者卡片边缘伸出，而不是从卡片中心穿出来。
	/// </summary>
	private Vector2 GetEnemyIdentityCardAnchor(int enemyIndex, Vector2 targetPos)
	{
		if (enemyIndex < 0 || enemyIndex >= _enemyCards.Count)
			return Vector2.Zero;

		var rect = _enemyCards[enemyIndex].GetGlobalRect();
		var center = rect.Position + rect.Size / 2;
		var direction = targetPos - center;
		if (direction.LengthSquared() < 0.01f)
			return center;

		var half = rect.Size / 2;
		float tx = direction.X == 0 ? float.PositiveInfinity : half.X / MathF.Abs(direction.X);
		float ty = direction.Y == 0 ? float.PositiveInfinity : half.Y / MathF.Abs(direction.Y);
		float t = MathF.Min(tx, ty);
		return center + direction * t;
	}

	/// <summary>
	/// 返回当前意图箭头调试信息，供 godot-mcp 手动验证使用。
	/// </summary>
	public string GetIntentArrowDebugInfo()
	{
		return _arrowRenderer?.GetDebugSnapshot() ?? "";
	}

	/// <summary>
	/// 获取玩家英雄生命值条的屏幕中心坐标。
	/// 用于敌方意图箭头指向玩家英雄的场景。
	/// </summary>
	/// <returns>生命值条屏幕中心坐标；未初始化返回 Vector2.Zero</returns>
	private Vector2 GetPlayerHeroScreenCenter()
	{
		if (_playerHealthBar != null)
		{
			var rect = _playerHealthBar.GetGlobalRect();
			return new Vector2(rect.Position.X + rect.Size.X / 2, rect.Position.Y + rect.Size.Y / 2);
		}
		return Vector2.Zero;
	}

	/// <summary>
	/// 根据目标类型解析其屏幕中心坐标。
	/// 用于意图箭头从敌方随从指向其攻击目标的终点计算。
	/// </summary>
	/// <param name="target">伤害目标（Minion/Hero/null）</param>
	/// <returns>目标屏幕中心坐标；无法解析时返回 Vector2.Zero</returns>
	private Vector2 ResolveTargetScreenPos(IDamageTarget? target)
	{
		return target switch
		{
			Minion m => m.BoardSlotIndex >= 0
				? _boardUI.GetSlotScreenCenter(m.BoardSlotIndex, m.IsPlayerSide)
				: Vector2.Zero,
			Hero h => h.IsPlayerSide ? GetPlayerHeroScreenCenter() : GetEnemyIdentityCardCenter(0),
			_ => Vector2.Zero
		};
	}

	// ===== 选择状态管理 =====

	/// <summary>
	/// 重置所有选择状态——取消卡牌选中、攻击方选中、清除高亮、重置模式。
	/// </summary>
	private void ResetSelection()
	{
		_boardUI.DisableKeyboardTargeting();

		_arrowRenderer?.RemoveArrow("attack_select");
		_selectionMode = SelectionMode.Normal;
		_selectedCard = null;
		_selectedAttacker = null;
		_boardUI.ClearHighlights();
		_enemyHeroAttackButton.Visible = false;
		_enemyHeroSpellButton.Visible = false;
		_playerHeroSpellButton.Visible = false;
		_weaponAttackButton.Visible = false;
		_weaponActiveSkillButton.Visible = false;
		_handUI.DeselectCard();
		HidePlayZonePanel();

		// 清除攻击拖拽状态
		_isAttackDragPressed = false;
		_attackDragHasMoved = false;
	}

	/// <summary>
	/// 更新移动端取消按钮的可见性。
	/// 在非 Normal 选择模式或手牌选择模式下显示，帮助移动端用户取消当前操作（替代桌面端右键）。
	/// </summary>
	private void UpdateMobileCancelButton()
	{
		if (_mobileCancelButton == null) return;

		bool shouldShow = MobileInputRouter.IsMobile
			&& (_selectionMode != SelectionMode.Normal || _isHandSelecting);
		_mobileCancelButton.Visible = shouldShow;
	}

	/// <summary>
	/// 移动端取消按钮按下回调。
	/// 根据当前状态执行不同的取消操作：手牌选择、开发者伤害、攻击选择等。
	/// </summary>
	private void OnMobileCancelPressed()
	{
		GD.Print("[CombatUI] 移动端取消按钮按下");

		// 手牌选择模式：取消手牌弃牌选择
		if (_isHandSelecting)
		{
			_combat?.CancelHandDiscardSelection();
			return;
		}

		// 开发者伤害模式：退出开发者伤害模式
		if (_selectionMode == SelectionMode.DevDamageTargeting)
		{
			ExitDevDamageMode();
			return;
		}

		// 其他选择模式（攻击目标、武器目标、法术目标、随从放置等）：重置选择
		ResetSelection();
		_handUI.RefreshHand();
	}

	// ===== 发现选牌 UI =====

	/// <summary>
	/// 战斗状态变化时的发现/手牌选择 UI 切换。
	/// 优先检查手牌选择模式，其次检查发现选牌。
	/// </summary>
	private void OnCombatStateChangedForDiscover()
	{
		if (_combat == null) return;

		if (_combat.IsDiscovering)
		{
			if (_combat.IsHandSelecting)
			{
				EnterHandSelectionMode();
			}
			else
			{
				ShowDiscoverUI();
			}
		}
		else if (_isHandSelecting)
		{
			ExitHandSelectionMode();
		}
		else if (_discoverUI != null && _discoverUI.Visible)
		{
			HideDiscoverUI();
			RefreshAll();
		}
	}

	/// <summary>
	/// 显示发现选牌覆盖层。
	/// </summary>
	private void ShowDiscoverUI()
	{
		if (_combat?.DiscoverOptions == null) return;

		_discoverUI ??= new DiscoverUI();
		if (_discoverUI.GetParent() == null)
			AddChild(_discoverUI);

		_discoverUI.CustomTitle = null;

		if (_combat.DiscoverRuntimeOptions != null && _combat.DiscoverPickCount > 1)
		{
			bool canSkip = true;
			var mode = _combat.CurrentSelectionMode;

			if (mode == Combat.CombatManager.PendingSelectionMode.ChooseDiscard)
			{
				_discoverUI.CustomTitle = Loc.T("ui.combat.discard_select_format", "选择 {count} 张手牌弃掉")
					.Replace("{count}", _combat.DiscoverPickCount.ToString());
				canSkip = false;
			}
			else if (mode == Combat.CombatManager.PendingSelectionMode.BladeCrisis)
			{
				_discoverUI.CustomTitle = Loc.T("ui.combat.discard_select_format_blade", "选择最多 {count} 张手牌弃掉")
					.Replace("{count}", _combat.DiscoverPickCount.ToString());
				canSkip = true;
			}

			_discoverUI.ShowCards(_combat.DiscoverRuntimeOptions, _combat.DiscoverPickCount, canSkip: canSkip, onChosen: chosen =>
			{
				_combat.ConfirmDiscoverCards(chosen);
			});
		}
		else
		{
			_discoverUI.ShowCards(_combat.DiscoverOptions, canSkip: true, onChosen: chosen =>
			{
				_combat.ConfirmDiscoverChoice(chosen);
			});
		}

		// 发现选牌期间禁用回合结束按钮（热键已由 HotkeyManager + _combat.IsDiscovering 守卫）
		_endTurnButton.Disabled = true;
		GD.Print("[CombatUI] 发现选牌 UI 已显示");
	}

	/// <summary>
	/// 隐藏发现选牌覆盖层。
	/// </summary>
	private void HideDiscoverUI()
	{
		if (_discoverUI != null)
		{
			_discoverUI.Hide();
		}
		_endTurnButton.Disabled = false;
		GD.Print("[CombatUI] 发现选牌 UI 已隐藏");
	}

	// ===== 手牌选择模式（STS2 风格） =====

	/// <summary>
	/// 进入手牌选择模式——卡牌留在原位，点击切换选中，确认按钮出现。
	/// </summary>
	private void EnterHandSelectionMode()
	{
		if (_isHandSelecting) return;
		_isHandSelecting = true;
		_selectedHandCards.Clear();

		// 禁用回合结束按钮
		_endTurnButton.Disabled = true;

		// 隐藏播放区域
		HidePlayZonePanel();

	// 设置 HandUI 为选择模式
	_handUI.SetHandSelectionMode(true);
	_handUI.OnCardSelectionToggled += OnHandCardSelectionToggled;

		// 创建头部提示标签
		float scale = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		var viewportSize = GetViewport().GetVisibleRect().Size;
		string headerText;
		if (_combat.HandSelectMin == _combat.HandSelectMax)
		{
			headerText = Loc.T("ui.combat.discard_select_format", "选择 {count} 张手牌弃掉")
				.Replace("{count}", _combat.HandSelectMax.ToString());
		}
		else
		{
			headerText = Loc.T("ui.combat.discard_select_format_blade", "选择最多 {count} 张手牌弃掉")
				.Replace("{count}", _combat.HandSelectMax.ToString());
		}

		_handSelectHeaderLabel = new Label
		{
			Name = "HandSelectHeader",
			Text = headerText,
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(400, 36),
		};
		_handSelectHeaderLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
		_handSelectHeaderLabel.AddThemeFontSizeOverride("font_size", 20);
		AddChild(_handSelectHeaderLabel);

		float headerY = viewportSize.Y - 210 * scale;
		_handSelectHeaderLabel.Position = new Vector2((viewportSize.X - 400) / 2, headerY);

		// 创建确认按钮（初始隐藏，选择足够时显示）
		_handSelectConfirmBtn = new Button
		{
			Name = "HandSelectConfirmBtn",
			Text = Loc.T("ui.hand_select.confirm", "确认"),
			CustomMinimumSize = new Vector2(120, 40),
			Visible = false,
			Disabled = true,
		};
		_handSelectConfirmBtn.Pressed += OnHandSelectConfirmPressed;
		AddChild(_handSelectConfirmBtn);

		float btnY = headerY + 44 * scale;
		_handSelectConfirmBtn.Position = new Vector2((viewportSize.X - 120) / 2, btnY);

		GD.Print("[CombatUI] 进入手牌选择模式");
	}

	/// <summary>
	/// 退出手牌选择模式——清理 UI，恢复正常状态。
	/// </summary>
	private void ExitHandSelectionMode()
	{
		_isHandSelecting = false;
		_selectedHandCards.Clear();

		// 移除头部标签和确认按钮
		_handSelectHeaderLabel?.QueueFree();
		_handSelectHeaderLabel = null;
		if (_handSelectConfirmBtn != null)
		{
			_handSelectConfirmBtn.Pressed -= OnHandSelectConfirmPressed;
			_handSelectConfirmBtn.QueueFree();
			_handSelectConfirmBtn = null;
		}

		// 恢复 HandUI 正常模式
		_handUI.HandSelectMode = false;
		_handUI.OnCardSelectionToggled -= OnHandCardSelectionToggled;

		// 恢复回合结束按钮
		_endTurnButton.Disabled = false;

		// 全量刷新恢复所有 UI
		RefreshAll();

		GD.Print("[CombatUI] 退出手牌选择模式");
	}

	/// <summary>
	/// 手牌选择模式：点击卡牌切换选中/取消。
	/// </summary>
	private void OnHandCardSelectionToggled(Card.Card card, bool toggled)
	{
		if (_selectedHandCards.Contains(card))
		{
			// 取消选中
			_selectedHandCards.Remove(card);
			var cardUI = _handUI.GetCardUIFor(card);
			cardUI?.SetHandSelectionHighlight(false);
		}
		else
		{
			// 选中
			_selectedHandCards.Add(card);
			var cardUI = _handUI.GetCardUIFor(card);
			cardUI?.SetHandSelectionHighlight(true);
		}

		UpdateHandSelectConfirmButton();
	}

	/// <summary>
	/// 刷新手牌选择模式下的卡牌高亮（用于 RefreshAll 中）。
	/// </summary>
	private void RefreshHandSelectionHighlights()
	{
		if (_combat?.PlayerHero?.Hand == null) return;

		foreach (var card in _combat.PlayerHero.Hand)
		{
			var cardUI = _handUI.GetCardUIFor(card);
			if (cardUI != null)
			{
				bool isSelected = _selectedHandCards.Contains(card);
				cardUI.SetHandSelectionHighlight(isSelected);
			}
		}
	}

	/// <summary>
	/// 更新确认按钮的可见性和可用状态。
	/// </summary>
	private void UpdateHandSelectConfirmButton()
	{
		if (_handSelectConfirmBtn == null) return;

		int count = _selectedHandCards.Count;
		bool canConfirm = count >= _combat.HandSelectMin && count <= _combat.HandSelectMax;
		_handSelectConfirmBtn.Visible = canConfirm;
		_handSelectConfirmBtn.Disabled = !canConfirm;
	}

	/// <summary>
	/// 确认按钮点击——提交选中的卡牌给 CombatManager 结算。
	/// </summary>
	private void OnHandSelectConfirmPressed()
	{
		if (_combat == null || !_isHandSelecting) return;
		GD.Print($"[CombatUI] 手牌选择确认 — 选中 {_selectedHandCards.Count} 张");
		_combat.ConfirmHandDiscardSelection(_selectedHandCards);
	}

	// ===== 播放区域（类 STS2 风格） =====

	/// <summary>
	/// 判断屏幕坐标是否在播放区域内。
	/// 播放区域 = 屏幕顶部到高度×PlayZoneThresholdRatio 的区间。
	/// 类 STS2 NMouseCardPlay.IsCardInPlayZone() 的 Y 阈值判定。
	/// </summary>
	private bool IsInPlayZone(Vector2 screenPos)
	{
		float threshold = GetViewport().GetVisibleRect().Size.Y * PlayZoneThresholdRatio;
		return screenPos.Y < threshold;
	}

	/// <summary>
	/// 创建播放区域视觉指示器——半透明面板 + 提示文字。
	/// 在 Initialize 中调用一次，默认隐藏。
	/// </summary>
	private void CreatePlayZonePanel()
	{
		float scale = UIScaler.Instance?.GetScaleFactor() ?? 1f;

		_playZonePanel = new Panel
		{
			Name = "PlayZonePanel",
			Visible = false,
			ZIndex = 50,
		};

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.2f, 0.6f, 0.2f, 0.12f),
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			BorderColor = new Color(0.3f, 0.8f, 0.3f, 0.4f),
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomLeft = 12,
			CornerRadiusBottomRight = 12,
		};
		_playZonePanel.AddThemeStyleboxOverride("panel", style);

		_playZoneLabel = new Label
		{
			Name = "PlayZoneLabel",
			Text = Localization.Localization.T("ui.combat.play_zone_hint", "松手打出\n（或点击此处）"),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_playZoneLabel.AddThemeColorOverride("font_color", new Color(0.5f, 1f, 0.5f, 0.6f));
		_playZoneLabel.AddThemeFontSizeOverride("font_size", (int)(20 * scale));
		_playZonePanel.AddChild(_playZoneLabel);

		AddChild(_playZonePanel);
	}

	/// <summary>
	/// 显示播放区域面板并计算其位置（在棋盘区域上方）。
	/// 设置鼠标过滤为 Stop 以接收点击事件（用于 click-select 模式打出）。
	/// </summary>
	private void ShowPlayZonePanel()
	{
		if (_playZonePanel == null) return;

		var viewport = GetViewport().GetVisibleRect().Size;
		float threshold = viewport.Y * PlayZoneThresholdRatio;
		float panelH = 80f * (UIScaler.Instance?.GetScaleFactor() ?? 1f);
		float margin = 20f;

		_playZonePanel.Position = new Vector2(margin, threshold - panelH - margin);
		_playZonePanel.Size = new Vector2(viewport.X - margin * 2, panelH);
		_playZonePanel.MouseFilter = MouseFilterEnum.Stop;
		_playZonePanel.Visible = true;

		GD.Print($"[CombatUI] ShowPlayZonePanel — viewport=({viewport.X:F0},{viewport.Y:F0}), threshold={threshold:F0}, panel=({_playZonePanel.Position.X:F0},{_playZonePanel.Position.Y:F0}), size=({_playZonePanel.Size.X:F0},{panelH:F0}), visible={_playZonePanel.Visible}");

		if (!_playZonePanelConnected)
		{
			_playZonePanel.GuiInput += OnPlayZoneGuiInput;
			_playZonePanelConnected = true;
		}
	}

	/// <summary>
	/// 隐藏播放区域面板并断开点击事件。
	/// </summary>
	private void HidePlayZonePanel()
	{
		if (_playZonePanel == null) return;

		if (_playZonePanelConnected)
		{
			_playZonePanel.GuiInput -= OnPlayZoneGuiInput;
			_playZonePanelConnected = false;
		}

		_playZonePanel.MouseFilter = MouseFilterEnum.Ignore;
		_playZonePanel.Visible = false;
	}

	/// <summary>
	/// 播放区域面板点击事件——click-select 模式下点击播放区域即打出卡牌。
	/// </summary>
	private void OnPlayZoneGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			if (_selectionMode == SelectionMode.PlayingNoTargetCard && _selectedCard != null)
			{
				GD.Print($"[CombatUI] 播放区域被点击 — 打出 {_selectedCard.CardName}");
				PlaySelectedNoTargetCard();
			}
		}
	}

	/// <summary>
	/// 执行无目标卡牌打出——拖拽松手或点击播放区域后的统一入口。
	/// </summary>
	private void PlaySelectedNoTargetCard()
	{
		if (_selectedCard == null || _combat.State.IsGameOver) return;

		bool success;
		switch (_selectedCard.Type)
		{
		case CardType.Spell:
			success = _combat.PlaySpell(_selectedCard, _combat.PlayerHero);
			break;
		case CardType.Status:
			// 状态牌：自动以玩家英雄为目标
			success = _combat.PlaySpell(_selectedCard, _combat.PlayerHero);
			break;
		case CardType.Domain:
				success = _combat.PlayDomain(_selectedCard);
				break;
			default:
				GD.PrintErr($"[CombatUI] 不支持的类型：{_selectedCard.Type}");
				OnCardDragCancelled();
				return;
		}

		if (success)
		{
			GD.Print($"[CombatUI] ✓ 无目标卡牌 {_selectedCard.CardName} 已打出");
			AnimateCardToDiscardPile();
			RefreshAll();
		}
		else
		{
			GD.Print($"[CombatUI] ✗ 打出失败，取消");
			OnCardDragCancelled();
		}
	}

	/// <summary>
	/// 拖拽卡牌逐帧位置更新回调——检查是否进入/离开播放区域并更新视觉反馈。
	/// </summary>
	private void OnDragMoveForPlayZone(CardUI cardUI, Vector2 screenPos)
	{
		if (_selectionMode != SelectionMode.PlayingNoTargetCard) return;

		bool inZone = IsInPlayZone(screenPos);
		cardUI.SetPlayZoneHighlight(inZone);
	}
}
