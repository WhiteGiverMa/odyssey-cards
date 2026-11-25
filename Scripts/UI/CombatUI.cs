#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Godot;
using OdysseyCards.Audio;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Core;
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

	/// <summary>顶部敌方指挥官手牌栏；仅拥有手牌/法力/牌库体系的敌人显示。</summary>
	private EnemyHandBar _enemyHandBar = null!;

	/// <summary>
	/// 手牌区域容器——高度随 UIScaler 缩放，
	/// 确保各分辨率下卡牌折叠态露出比例一致。
	/// </summary>
	private Control _handArea = null!;

	/// <summary>
	/// 玩家英雄身份卡——包装玩家名字、生命值条、护甲、防御和效果图标。
	/// 与 EnemyIdentityCard 对称，统一双方英雄的 UI 结构。
	/// </summary>
	private PlayerIdentityCard _playerIdentityCard = null!;

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
	/// <summary>敌方武器标签（已迁移到 EnemyIdentityCard 内）。</summary>
	// private Label _enemyWeaponLabel = null!; — replaced by EnemyIdentityCard weapon label
	/// <summary>
	/// 玩家效果图标栏——现由 PlayerIdentityCard 管理。
	/// </summary>
	// private EffectBar _playerEffectBar = null!; — replaced by _playerIdentityCard.EffectBar

	/// <summary>
	/// 敌方效果图标栏（单敌人旧版兼容层）——生命值条下方。
	/// </summary>
	private EffectBar _enemyEffectBar = null!;

	/// <summary>
	/// 藏品栏——显示当前持有的藏品图标列表。
	/// </summary>
	private UI.RelicBar _relicBar = null!;

	/// <summary>
	/// 玩家法力值显示——底部中央，格式「3/3」。
	/// </summary>
	private Label _playerManaLabel = null!;
	private ManaCrystalBar _manaCrystalBar = null!;

	/// <summary>
	/// 回合结束按钮——右下角，文本「结束回合」。
	/// </summary>
	private Button _endTurnButton = null!;
	private Button _smartAttackButton = null!;

	/// <summary>
	/// 英雄技能按钮——位于玩家区域，显示英雄技能名称和费用。
	/// </summary>
	private Button _heroPowerButton = null!;

	/// <summary>
	/// 玩家护甲值显示——现由 PlayerIdentityCard 管理。
	/// </summary>
	// private Label _playerArmorLabel = null!; — replaced by PlayerIdentityCard

	/// <summary>
	/// 玩家防御力显示——现由 PlayerIdentityCard 管理。
	/// </summary>
	// private Label _playerDefenseLabel = null!; — replaced by PlayerIdentityCard

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
	/// 玩家英雄交互面板——有可见色块背景的容器。现被 PlayerIdentityCard 替代，此字段移除。
	/// </summary>
	// private Panel _playerHeroPanel = null!; — replaced by _playerIdentityCard

	/// <summary>
	/// 对己方英雄施法按钮——法术目标选择模式下可见。按钮在 PlayerIdentityCard 内。
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
	private readonly Dictionary<Minion, DeployPresentation> _deployments = new(ReferenceEqualityComparer.Instance);
	private (Card.Card Card, BoardPosition Position, DeployOriginKind Kind, DeployPose? Pose, CardUI? View)? _playerDeployHandoff;
	private CombatLogDrawer _combatLogDrawer = null!;

	private sealed record DeployPresentation(BoardPosition Position, DeployOriginKind Kind, DeployPose? Origin,
		Card.Card? HandCard, CardUI? HandView)
	{
		public MinionDeployVfx? Flight { get; set; }
	}

	/// <summary>
	/// 棋盘运行时引用——用于订阅随从放置/移除事件。
	/// </summary>
	private Board? _board;

	/// <summary>
	/// 事件解绑列表。CombatUI 生命周期结束时统一释放，避免场景切换后悬空订阅。
	/// </summary>
	private readonly List<Action> _unsubscribeActions = new();

	private readonly Dictionary<Hero, (Action<DamageEventInfo, IDamageSource?> Damage, Action<int> Heal, Action<int> Armor)> _heroDamageHandlers = new();
	private readonly Dictionary<Minion, (Action<DamageEventInfo, IDamageSource?> Damage, Action<int> Heal, Action<int> Armor)> _minionDamageHandlers = new();

	/// <summary>
	/// 拖拽层——暂时借出一张真实手牌，在手牌布局之外展示。
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
	/// <summary>本次出牌起点的屏幕中心；由选择流程写入，亮牌演出消费后清零。</summary>
	private Vector2 _lastPlayedCardOrigin;
	private const string CardTargetArrowKey = "card_target_select";
	private const float TargetingCardScale = 0.75f;
	// ===== 外部引用 =====

	/// <summary>
	/// 战斗管理器引用。
	/// </summary>
	private CombatManager _combat = null!;

	/// <summary>当前绑定到顶部手牌栏的敌方英雄；null 表示普通意图战。</summary>
	private Hero? _enemyHandSource;
	private readonly List<Card.Card> _enemyHandCache = new();
	private readonly List<bool> _enemyHandRevealCache = new();
	private int _enemyHandManaCache = -1;
	private int _enemyHandMaxManaCache = -1;
	private int _enemyHandDeckCache = -1;

	/// <summary>一步突击推进前所在支援槽，供“停稳/行进间射击”动画还原起点。</summary>
	private readonly Dictionary<Minion, BoardPosition> _assaultOrigins = new();
	/// <summary>正在播放的突击演出；同时用于抑制同一击的普通攻击弹道。</summary>
	private readonly Dictionary<Minion, AssaultAdvanceVfx> _assaultVfx = new();
	/// <summary>普通推进前线的平滑替身；一步突击接管时立即撤销，避免重影。</summary>
	private readonly Dictionary<Minion, MinionMoveVfx> _moveVfx = new();
	/// <summary>当前正在播放的亮牌展示；新亮牌会先收掉旧的，保证同一时刻只有一张。</summary>
	private CardShowcaseVfx? _activeShowcase;
	/// <summary>直接点选攻击时的真实目标，仅在一次攻击调用栈内有效。</summary>
	private IDamageTarget? _pendingAssaultShotTarget;

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
		// 战斗表面采用 Godot 主指针（触控模拟鼠标），不能依赖未注册 zone 的 Router 状态。
		// 自动化 QA 时 EvalGateway 的虚拟指针优先，确保合成拖拽与 _Process 轮询一致。
		return EvalGateway.GetEffectiveMousePosition(this);
	}

	private bool IsPointerTargetSelectionMode()
	{
		return _selectionMode == SelectionMode.SelectingAttackTarget
			|| _selectionMode == SelectionMode.SelectingWeaponTarget
			|| _selectionMode == SelectionMode.SelectingActiveSkillTarget;
	}

	private Vector2 GetPointerTargetSourcePos()
	{
		return _selectionMode switch
		{
			SelectionMode.SelectingWeaponTarget => _weaponAttackButton.GlobalPosition + _weaponAttackButton.Size * 0.5f,
			SelectionMode.SelectingActiveSkillTarget => _weaponActiveSkillButton.GlobalPosition + _weaponActiveSkillButton.Size * 0.5f,
			_ when _selectedAttacker != null => GetMinionScreenCenter(_selectedAttacker),
			_ => GetInputPosition(),
		};
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
	private bool _ignoreNextWeaponActiveSkillPressed;

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
	private bool _hasEnteredPlayZone;

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

		// 场景淡入（星途微交互）
		MicroFX.FadeIn(this);

		BuildLayout();

		// 订阅分辨率变化——窗口缩放时重新计算尺寸
		if (UIScaler.Instance != null)
		{
			UIScaler.Instance.OnResolutionChanged += OnResolutionChanged;
			UIScaler.Instance.OnEmojiIconsChanged += OnEmojiIconsChanged;
			UIScaler.Instance.OnFontScaleChanged += OnFontScaleChanged;
		}
	}

	/// <summary>
	/// 右键取消——必须在 _Input 层处理。BoardSlot/CardUI 的 _GuiInput.AcceptEvent()
	/// 会吞噬右键事件，导致 _UnhandledInput 永远收不到右键。
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (MobileInputRouter.IsMobile)
		{
			if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } touch)
			{
				_handUI?.CollapseTouchPreviewOutside(touch.Position);
			}
			return;
		}
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
		{
			if (IsPointerTargetSelectionMode())
			{
				GD.Print($"[CombatUI] 右键取消目标选择，mode={_selectionMode}");
				ResetSelection();
				GetViewport().SetInputAsHandled();
			}
			else if (_selectionMode == SelectionMode.DevDamageTargeting)
			{
				ExitDevDamageMode();
			}
			else if (IsSelectingHandCardForPlay())
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

		// 指针目标选择模式——桌面端右键取消（移动端用取消按钮）
		if (!MobileInputRouter.IsMobile)
		{
			if (@event is InputEventMouseButton mb3
				&& mb3.ButtonIndex == MouseButton.Right
				&& mb3.Pressed
				&& IsPointerTargetSelectionMode())
			{
				GD.Print($"[CombatUI] _UnhandledInput 右键取消目标选择，mode={_selectionMode}");
				ResetSelection();
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

	private void OnWeaponActiveSkillButtonGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mb || mb.ButtonIndex != MouseButton.Left || !mb.Pressed)
			return;

		if (_combat == null || _combat.State.IsGameOver)
			return;

		var weapon = _combat.PlayerHero.Weapon;
		if (weapon?.ActiveSkill == null || !weapon.ActiveSkill.RequiresTarget)
			return;

		if (_selectionMode == SelectionMode.SelectingActiveSkillTarget)
		{
			GD.Print("[CombatUI] 武器主动技能按钮再次点击，取消目标选择");
			_ignoreNextWeaponActiveSkillPressed = true;
			_attackDragFsm.Cancel();
			RefreshAll();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!weapon.ActiveSkill.CanUse(_combat.PlayerHero))
			return;

		GD.Print($"[CombatUI] 武器主动技能按钮左键按下，进入可拖拽目标选择，pos=({mb.GlobalPosition.X:F0},{mb.GlobalPosition.Y:F0})");
		_ignoreNextWeaponActiveSkillPressed = true;
		EnterWeaponActiveSkillTargetMode(mb.GlobalPosition);
		GetViewport().SetInputAsHandled();
	}

	/// <summary>
	/// 分辨率变化时刷新所有 UI 尺寸和布局，包括手牌区域高度。
	/// </summary>
	private void OnResolutionChanged()
	{
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		GD.Print($"[CombatUI] 分辨率变化 — 缩放因子 {s:F2}");

		// 五段式布局高度在分辨率变化时保持一致比例。
		if (_handArea != null)
			_handArea.CustomMinimumSize = new Vector2(0, 110f * s);
		if (_enemyHandBar != null)
			_enemyHandBar.CustomMinimumSize = new Vector2(0, 70f * s);
		Control? enemyArea = GetNodeOrNull<Control>("CombatRoot/EnemyArea");
		if (enemyArea != null)
			enemyArea.CustomMinimumSize = new Vector2(0, 120f * s);
		Control? playerArea = GetNodeOrNull<Control>("CombatRoot/PlayerArea");
		if (playerArea != null)
			playerArea.CustomMinimumSize = new Vector2(0, 96f * s);

		RefreshAll();
		SceneLifecycleGuard.CallDeferredSafe(this, nameof(RefreshIntentArrowsAfterLayout));
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

		// --- 攻击选择箭头：空前线槽显示青绿推进箭头，合法敌方目标显示橙色攻击箭头 ---
		if (IsPointerTargetSelectionMode())
		{
			if (_arrowRenderer != null)
			{
				var sourcePos = GetPointerTargetSourcePos();
				var inputPos = GetInputPosition();
				var hit = _boardUI.GetSlotAtPosition(inputPos);
				bool moving = _selectedAttacker != null && hit is BoardPosition { Row: BoardRow.Frontline } position
					&& _combat.CanMoveMinionToFrontline(_selectedAttacker, position.SlotIndex);
				bool legalAttack = !moving && IsLegalAttackHover(hit, inputPos);
				_arrowRenderer.RemoveArrow(moving ? "attack_select" : "move_select");
				_arrowRenderer.AddArrow(moving ? "move_select" : "attack_select", sourcePos, inputPos,
					moving ? ArrowRenderer.MoveSelectColor
						: legalAttack ? ArrowRenderer.AttackSelectColor
						: new Color(ArrowRenderer.AttackSelectColor, 0.42f));
			}
		}
		else if (_arrowRenderer != null)
		{
			_arrowRenderer.RemoveArrow("attack_select");
			_arrowRenderer.RemoveArrow("move_select");
		}

		// --- 卡牌目标选择箭头（随从放置 / 单目标法术 / 目标型部署战吼） ---
		if ((_selectionMode == SelectionMode.PlacingMinion
			|| _selectionMode == SelectionMode.TargetingSpell
			|| _selectionMode == SelectionMode.TargetingMinionBattlecry)
			&& HandUI.IsLive(_dragCardUI) && _arrowRenderer != null)
		{
			Vector2 sourcePos = _dragCardUI!.ScreenCenter;
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

		// --- 敌方指挥官手牌栏：轻量变更检测，避免每帧重建手牌视觉 ---
		UpdateEnemyHandBar();
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
		if (wasDrag && IsPointerTargetSelectionMode())
		{
			if (_selectionMode == SelectionMode.SelectingActiveSkillTarget)
				HandleActiveSkillDrop(pos);
			else
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
		ArgumentNullException.ThrowIfNull(player);
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

		// 战斗日志抽屉（Layer=24）：只拦截抽屉矩形，预览层 25，低于意图提示 26。
		var combatLogLayer = new CanvasLayer { Name = "CombatLogLayer", Layer = 24 };
		AddChild(combatLogLayer);
		_combatLogDrawer = new CombatLogDrawer();
		combatLogLayer.AddChild(_combatLogDrawer);
		_combatLogDrawer.Initialize(_combat.CombatLog);

		// 意图悬浮提示层（Layer=26，高于意图图标）
		var intentTooltipLayer = new CanvasLayer { Name = "IntentTooltipLayer", Layer = 26 };
		var tooltipParent = new Control { Name = "IntentTooltipContent", MouseFilter = MouseFilterEnum.Ignore };
		tooltipParent.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		intentTooltipLayer.AddChild(tooltipParent);
		AddChild(intentTooltipLayer);

		// 创建并初始化子组件
		SetupBoardUI();
		SetupHandUI();
		CreatePlayerIdentityCard();
		CreateManaLabels();
		CreateArmorLabels();
		CreateEndTurnButton();
		CreateHeroPowerButton();
		CreatePlayerHeroPanel();
		CreateDeckButtons();
		CreateWeaponUI();
		CreateStatusEffectUI();
		// 0.2.3「早期战争」：热力值机制已降为死代码，战斗界面不再创建热力条。
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

		// 首次刷新；布局完成后再刷新一次意图箭头，避免用零尺寸身份卡计算错误起点。
		RefreshAll();
		SceneLifecycleGuard.CallDeferredSafe(this, nameof(RefreshIntentArrowsAfterLayout));

		// 订阅语言变更事件
		GameManager.Instance.LanguageChanged += OnLanguageChanged;

		// 订阅热键（HotkeyManager 回调）
		SubscribeHotkeys();

		// 星途全局主题 + 按钮悬停微交互（在全部子组件创建后统一应用；
		// 散点 AddThemeColorOverride 优先于 Theme，既有视觉不回归）
		UIThemeFactory.ApplyTo(this);

		// 界面字体缩放：统一刷新战斗 HUD 的显式字号覆盖
		ApplyHudFontScale();

		GD.Print("[CombatUI] 初始化完成");
	}

	/// <summary>
	/// 退出场景树时取消语言变更事件订阅。
	/// </summary>
	public override void _ExitTree()
	{
		CancelAllDeployments();
		CleanupTransientInputState();

		if (UIScaler.Instance != null)
		{
			UIScaler.Instance.OnResolutionChanged -= OnResolutionChanged;
			UIScaler.Instance.OnEmojiIconsChanged -= OnEmojiIconsChanged;
			UIScaler.Instance.OnFontScaleChanged -= OnFontScaleChanged;
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

	private void CleanupTransientInputState()
	{
		if (_playZonePanelConnected)
			HidePlayZonePanel();

		_arrowRenderer?.RemoveArrow("attack_select");
		_arrowRenderer?.RemoveArrow("move_select");
		_arrowRenderer?.RemoveArrow(CardTargetArrowKey);
		_attackDragFsm?.ForceReset();
		_boardUI?.DisableKeyboardTargeting();
		_boardUI?.ClearHighlights();

		foreach (var card in _enemyCards)
		{
			if (GodotObject.IsInstanceValid(card))
				card.SetAttackTargetHighlight(false);
		}
	}

	/// <summary>
	/// 语言变更时刷新所有 UI 文本标签。
	/// </summary>
	private void OnLanguageChanged(string lang)
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;
		RefreshAll();
	}

	private void OnEmojiIconsChanged()
	{
		if (!IsInsideTree() || IsQueuedForDeletion() || _combat == null)
			return;
		RefreshAll();
	}

	/// <summary>界面字体倍率变化：刷新战斗核心 HUD 的显式字号覆盖（主题默认字号由 UIThemeFactory 原地刷新）。</summary>
	private void OnFontScaleChanged()
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;
		ApplyHudFontScale();
	}

	/// <summary>把玩家区域、敌方意图与牌堆按钮等战斗 HUD 的字号统一接入界面字体缩放。</summary>
	private void ApplyHudFontScale()
	{
		ApplyScaleFont(_playerManaLabel, 13);
		ApplyScaleFont(_weaponInfoLabel, 12);
		ApplyScaleFont(_weaponAttackButton, 13);
		ApplyScaleFont(_weaponActiveSkillButton, 13);
		ApplyScaleFont(_drawPileBtn, 14);
		ApplyScaleFont(_discardPileBtn, 14);
		ApplyScaleFont(_enemyArmorLabel, 14);
		ApplyScaleFont(_enemyDefenseLabel, 13);
		ApplyScaleFont(_enemyIntentLabel, 14);
		ApplyScaleFont(_pauseButton, 20);
		ApplyScaleFont(_mobileCancelButton, 24);
		ApplyScaleFont(_enemyHeroAttackButton, 16);
		ApplyScaleFont(_enemyHeroSpellButton, 16);

		_playerIdentityCard?.ApplyFontScale();
		foreach (EnemyIdentityCard card in _enemyCards)
			card.ApplyFontScale();
	}

	/// <summary>单个控件按界面字体缩放刷新字号；节点未创建或已销毁时安全跳过。</summary>
	private static void ApplyScaleFont(Control? control, int baseSize)
	{
		if (control == null || !GodotObject.IsInstanceValid(control))
			return;
		control.AddThemeFontSizeOverride("font_size", UIScaler.ApplyFontScale(baseSize));
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
		_handUI.Initialize(_combat);
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
		_smartAttackButton.Pressed += OnSmartAttackPressed;
		_unsubscribeActions.Add(() => _smartAttackButton.Pressed -= OnSmartAttackPressed);

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
		_weaponActiveSkillButton.GuiInput += OnWeaponActiveSkillButtonGuiInput;
		_unsubscribeActions.Add(() => _weaponActiveSkillButton.GuiInput -= OnWeaponActiveSkillButtonGuiInput);
		_weaponActiveSkillButton.Pressed += OnWeaponActiveSkillPressed;
		_unsubscribeActions.Add(() => _weaponActiveSkillButton.Pressed -= OnWeaponActiveSkillPressed);

		// 牌堆/手牌状态变化 → 自动刷新 UI
		_combat.PlayerHero.DeckState.OnDrawPileChanged += UpdateDeckCounts;
		_unsubscribeActions.Add(() => _combat.PlayerHero.DeckState.OnDrawPileChanged -= UpdateDeckCounts);
		_combat.PlayerHero.DeckState.OnDiscardPileChanged += UpdateDeckCounts;
		_unsubscribeActions.Add(() => _combat.PlayerHero.DeckState.OnDiscardPileChanged -= UpdateDeckCounts);

		// 抽牌事件先于手牌变更，只有真实抽到的牌带牌库出发点。
		_combat.PlayerHero.DeckState.OnCardsDrawn += OnCardsDrawn;
		_unsubscribeActions.Add(() => _combat.PlayerHero.DeckState.OnCardsDrawn -= OnCardsDrawn);
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

		// 亮牌展示 — 指令/领域/反制结算时由规则层广播，UI 只负责演出
		_combat.OnCardPlayPresented += OnCardPlayPresented;
		_unsubscribeActions.Add(() => _combat.OnCardPlayPresented -= OnCardPlayPresented);

		// 发现选牌阶段切换
		_combat.OnCombatStateChanged += OnCombatStateChangedForDiscover;
		_unsubscribeActions.Add(() => _combat.OnCombatStateChanged -= OnCombatStateChangedForDiscover);

		// 游戏结束 → 显示弹窗
		_combat.OnGameOver += ShowGameOverPopup;
		_unsubscribeActions.Add(() => _combat.OnGameOver -= ShowGameOverPopup);
		_combat.OnCombatStarting += CancelAllDeployments;
		_unsubscribeActions.Add(() => _combat.OnCombatStarting -= CancelAllDeployments);

		// 随从伤害跳字 — 随从放置时通过闭包捕获 minion 引用订阅事件
		_board!.OnMinionPlaced += OnBoardMinionPlacedSubscribeDamage;
		_unsubscribeActions.Add(() => _board.OnMinionPlaced -= OnBoardMinionPlacedSubscribeDamage);
		// 先挂藏显标记，再由规则结算后的部署事件播放；拦截/亡语重入不会闪露真槽。
		_board.OnMinionEntering += OnBoardMinionEntering;
		_unsubscribeActions.Add(() => _board.OnMinionEntering -= OnBoardMinionEntering);
		_board.OnMinionPlaced += OnBoardMinionPlacedForDeployAnimation;
		_unsubscribeActions.Add(() => _board.OnMinionPlaced -= OnBoardMinionPlacedForDeployAnimation);
		_board.OnMinionRemoved += OnBoardMinionRemovedUnsubscribeDamage;
		_unsubscribeActions.Add(() => _board.OnMinionRemoved -= OnBoardMinionRemovedUnsubscribeDamage);

		// 随从死亡飞行动画 — 在槽位清空前获取屏幕坐标
		_board.OnMinionPreRemove += OnBoardMinionPreRemove;
		_unsubscribeActions.Add(() => _board.OnMinionPreRemove -= OnBoardMinionPreRemove);

		// 一步突击演出：OnMinionMoved 记录起点，OnAssaultAdvance 触发停稳/行进间射击。
		_board.OnMinionMoved += OnBoardMinionMoved;
		_unsubscribeActions.Add(() => _board.OnMinionMoved -= OnBoardMinionMoved);
		_combat.OnAssaultAdvance += OnAssaultAdvance;
		_unsubscribeActions.Add(() => _combat.OnAssaultAdvance -= OnAssaultAdvance);


		// 战斗消息/表情 — 空闲超时或信息发送界面触发
		_combat.OnCombatEmote += OnCombatEmote;
		_unsubscribeActions.Add(() => _combat.OnCombatEmote -= OnCombatEmote);

		// 攻击拖拽 FSM 事件订阅
		_attackDragFsm.OnDragMove += OnAttackDragMove;
		_unsubscribeActions.Add(() => _attackDragFsm.OnDragMove -= OnAttackDragMove);
		_attackDragFsm.OnDrop += OnAttackDragDrop;
		_unsubscribeActions.Add(() => _attackDragFsm.OnDrop -= OnAttackDragDrop);
		_attackDragFsm.OnCancel += OnAttackDragCancel;
		_unsubscribeActions.Add(() => _attackDragFsm.OnCancel -= OnAttackDragCancel);

		// 抉择（参考炉石 Choose One）—— CombatManager 经其回调链弹出 ChooseOneUI
		_combat.ChooseOneRequested += OnChooseOneRequested;
		_unsubscribeActions.Add(() => _combat.ChooseOneRequested -= OnChooseOneRequested);

	}

	/// <summary>CombatManager 抉择请求——弹出 ChooseOneUI 异步等待玩家选择，完成后通过 request.OnChosen 回调返回。</summary>
	private async void OnChooseOneRequested(CombatManager.ChooseOneRequest request)
	{
		if (request == null)
			return;
		var ui = new ChooseOneUI();
		AddChild(ui);
		int result = await ui.ShowAsync(
			request.TitleKey,
			request.TitleFallback,
			request.OptionLabels,
			request.OptionDescriptions,
			canSkip: false);
		request.OnChosen.Invoke(result);
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

		hm.PushPressedBinding(OdysseyInput.SmartAttack, TrySmartAttack);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.SmartAttack, TrySmartAttack));

		hm.PushPressedBinding(OdysseyInput.HeroPower, TryHeroPower);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.HeroPower, TryHeroPower));

		hm.PushPressedBinding(OdysseyInput.ViewDeck, ShowDrawPileView);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.ViewDeck, ShowDrawPileView));

		hm.PushPressedBinding(OdysseyInput.ViewDiscard, ShowDiscardPileView);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.ViewDiscard, ShowDiscardPileView));

		// 暂停与取消绑定可独立配置；共享物理键时由取消路由统一决定优先级。
		hm.PushPressedBinding(OdysseyInput.Pause, OnPauseHotkey);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.Pause, OnPauseHotkey));

		hm.PushPressedBinding(OdysseyInput.InfoScreen, ToggleInfoScreen);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.InfoScreen, ToggleInfoScreen));

		hm.PushPressedBinding(OdysseyInput.CombatLog, ToggleCombatLog);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.CombatLog, ToggleCombatLog));

		hm.PushPressedBinding(OdysseyInput.Cancel, HandleCancelHotkey);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.Cancel, HandleCancelHotkey));
	}

	private void ToggleCombatLog()
	{
		if (_combatLogDrawer != null && GodotObject.IsInstanceValid(_combatLogDrawer))
			_combatLogDrawer.Toggle();
	}

	private void OnCardsDrawn(IReadOnlyList<Card.Card> cards)
	{
		if (!SceneLifecycleGuard.ShouldSkip(this) && IsInstanceValid(_drawPileBtn) && _drawPileBtn.Size.X > 0f)
		{
			_handUI.QueueDrawOrigins(cards, GetDrawPileCenter());
		}
	}

	private void OnHandChanged()
	{
		_handUI.OnHandMembershipChanged();
	}

	private void OnManaChanged(int currentMana, int maxMana)
	{
		_handUI.RefreshCardCosts();
		UpdateManaDisplay();
		UpdateSmartAttackButton();
	}

	private void OnCombatStateChangedRefresh()
	{
		_handUI.RefreshHand();
		_boardUI.RefreshBoard();
		// 状态变化同样影响英雄/随从面板：血量、护甲、防御、武器与效果栏都必须同步，
		// 否则领域开启（如无限火力）后 PlayerIdentityCard 的效果栏不刷新。
		UpdateHealthBars();
		UpdateArmorDisplay();
		UpdateDefenseDisplay();
		UpdateWeaponDisplay();
		UpdateStatusEffectDisplay();
		RefreshIntentDisplay();
		RefreshIntentArrows();
		UpdateSmartAttackButton();
	}

	private void OnDamageVfxRequested(object? visualSource, IDamageTarget target, DamageKind kind, CombatDamageVfxKind vfxKind)
	{
		if (_attackVfxLayer == null)
			return;

		Vector2 from = ResolveVfxSourceScreenPos(visualSource, vfxKind);
		Vector2 to = ResolveTargetScreenPos(target);
		if (from == Vector2.Zero || to == Vector2.Zero)
			return;

		// 突击演出已自带停稳/滑行炮口与曳光弹道；用真实伤害目标校正落点并跳过重复普通弹道。
		if (visualSource is Minion assaultSource
			&& _assaultVfx.TryGetValue(assaultSource, out AssaultAdvanceVfx? assaultVfx))
		{
			assaultVfx.SetShotTarget(to);
			return;
		}

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

		// 记录本次出牌的屏幕起点：亮牌演出会从拖拽卡当前位置弹入，再飞向牌堆/效果栏。
		_lastPlayedCardOrigin = ResolveSelectedCardVfxOrigin(_selectedCard);
		if (_lastPlayedCardOrigin == Vector2.Zero)
			_lastPlayedCardOrigin = GetPlayerHandAreaCenter();
		_pendingSpellVfxCard = _selectedCard;
		_pendingSpellVfxOrigin = _lastPlayedCardOrigin;
		try
		{
			bool played = _combat.PlaySpell(_selectedCard, target);
			// 被反制拦截等“无亮牌回调”的成功路径同样必须释放拖拽卡；
			// 正常路径已在 OnCardPlayPresented 里释放，重复调用为空操作。
			if (played)
			{
				ReleaseDragCard(played: true);
			}
			return played;
		}
		finally
		{
			_pendingSpellVfxCard = null;
			_pendingSpellVfxOrigin = Vector2.Zero;
			// 演出已在 PresentCardPlay 内同步消费起点；无论成功失败都清零，避免污染下一次出牌。
			_lastPlayedCardOrigin = Vector2.Zero;
		}
	}

	private Vector2 ResolveSelectedCardVfxOrigin(Card.Card card)
	{
		CardUI? view = _handUI.GetCardUIFor(card);
		if (HandUI.IsLive(view) && view!.IsVisibleInTree())
		{
			return view.ScreenCenter;
		}
		return GetPlayerHeroScreenCenter();
	}

	/// <summary>
	/// 战斗消息事件——根据发送者显示在己方或敌方英雄区域。
	/// </summary>
	private void OnCombatEmote(CombatEmoteMessage message)
	{
		if (string.IsNullOrEmpty(message.Text))
			return;

		var pos = message.Speaker == CombatEmoteSpeaker.Player
			? GetPlayerHeroScreenCenter()
			: GetEnemyIdentityCardCenter(Mathf.Max(0, message.EnemyIndex));

		if (pos == Vector2.Zero)
		{
			GD.PrintErr("[CombatUI] 消息位置无效");
			return;
		}

		pos.Y += message.Speaker == CombatEmoteSpeaker.Player ? -45 : 70;
		FloatingEmote.Show(message.Text, pos, _damageNumberContainer, message.IsOfficialCollection);
	}

	private void OnBoardMinionPlacedSubscribeDamage(Minion minion, BoardPosition position)
	{
		if (_board?.Contains(minion) == true && !minion.IsDead)
		{
			SubscribeMinionDamageEvents(minion);
		}
	}

	/// <summary>规则监听广播前先登记藏槽。仅真实手牌部署带源牌引用，复制或其他召唤单独入场。</summary>
	private void OnBoardMinionEntering(Minion minion, BoardPosition position, Card.Card? fromHand)
	{
		if (SceneLifecycleGuard.ShouldSkip(this) || SceneLifecycleGuard.ShouldSkip(_deployVfxLayer)
			|| _combat.State.IsGameOver || !_boardUI.BeginPendingDeploy(position, minion))
		{
			return;
		}
		DeployOriginKind kind = DeployOriginKind.OffscreenSummon;
		DeployPose? origin = null;
		CardUI? handView = null;
		if (fromHand != null)
		{
			if (!minion.IsPlayerSide)
			{
				kind = DeployOriginKind.EnemyHand;
				origin = ResolveEnemyDeployPose(fromHand, minion.OwnerHero != null ? GetEnemyHeroIndex(minion.OwnerHero) : -1);
			}
			else if (_playerDeployHandoff is { } handoff && ReferenceEquals(handoff.Card, fromHand) && handoff.Position == position)
			{
				kind = handoff.Kind;
				origin = handoff.Pose;
				handView = handoff.View;
			}
			else
			{
				kind = DeployOriginKind.PlayerClick;
				handView = _handUI.GetCardUIFor(fromHand);
				origin = CaptureDeploymentPose(handView);
				if (origin == null)
				{
					handView = null;
				}
			}
		}
		_deployments.Add(minion, new DeployPresentation(position, kind, origin, fromHand, handView));
	}

	private static DeployPose? CaptureDeploymentPose(CardUI? view)
	{
		return HandUI.IsLive(view) && view!.IsVisibleInTree() ? MinionDeployVfx.CapturePose(view) : null;
	}

	private DeployPose? ResolveEnemyDeployPose(Card.Card fromHand, int enemyIndex)
	{
		if (IsInstanceValid(_enemyHandBar)
			&& (_enemyHandBar.GetCardScreenRect(fromHand) ?? _enemyHandBar.GetFallbackCardScreenRect()) is Rect2 rect)
		{
			return new DeployPose(MinionDeployVfx.ToMotion(rect.GetCenter()),
				MinionDeployVfx.CalculateFitRenderScale(rect.Size), 0f, 0.15f);
		}

		Vector2 identity = ResolveEnemyPlayOrigin(enemyIndex);
		if (identity == Vector2.Zero)
		{
			return null;
		}
		float scale = (UIScaler.Instance?.CurrentScale ?? 1f) * GetGlobalTransformWithCanvas().X.Length();
		return new DeployPose(MinionDeployVfx.ToMotion(identity), 0.36f * scale, 0f, 0.20f);
	}

	/// <summary>规则拦截完成后才挂飞行视图，已经离槽的单位不再生成替身。</summary>
	private void OnBoardMinionPlacedForDeployAnimation(Minion minion, BoardPosition position)
	{
		if (!_deployments.TryGetValue(minion, out DeployPresentation? deploy) || deploy.Position != position || deploy.Flight != null)
		{
			return;
		}
		Rect2 GetDestination()
		{
			return SceneLifecycleGuard.ShouldSkip(this) || SceneLifecycleGuard.ShouldSkip(_boardUI) || minion.IsDead
				|| !ReferenceEquals(_board?.GetMinionAt(position), minion) ? default : _boardUI.GetSlotScreenRect(position);
		}

		float speed = ResolveDeploySpeed();
		// Card.Cost 读取基础费用，不含运行时 CostModifier；非手牌召唤也从同一单位数据取值。
		DeployImpactProfile impact = MinionDeployMath.ResolveImpactProfile(minion.Cost);
		CardUI? handView = HandUI.IsLive(deploy.HandView) ? deploy.HandView : null;
		Card.Card face = deploy.HandCard?.CreateSnapshot() ?? minion.ToRuntimeCard();
		deploy.Flight = MinionDeployVfx.Play(face, deploy.Kind, deploy.Origin, minion.IsPlayerSide, impact, speed,
			_deployVfxLayer, GetDestination, touchedDown => CompleteDeploy(minion, deploy, touchedDown, impact, speed), handView);
		if (deploy.Flight == null)
		{
			CancelDeploy(minion);
		}
		else if (handView != null && ReferenceEquals(_dragCardUI, handView))
		{
			ReleaseDragCard(played: true);
		}
	}

	private void CompleteDeploy(Minion minion, DeployPresentation deploy, bool touchedDown, DeployImpactProfile impact, float speed)
	{
		if (!_deployments.TryGetValue(minion, out DeployPresentation? current) || !ReferenceEquals(current, deploy))
		{
			return;
		}
		_ = _deployments.Remove(minion);
		if (SceneLifecycleGuard.ShouldSkip(this) || SceneLifecycleGuard.ShouldSkip(_boardUI))
		{
			return;
		}
		if (touchedDown)
		{
			_boardUI.RevealDeployedMinion(deploy.Position, minion, impact, speed);
			RefreshIntentArrows();
		}
		else
		{
			_ = _boardUI.ClearPendingDeploy(deploy.Position, minion);
		}
	}

	private void CancelDeploy(Minion minion)
	{
		if (!_deployments.Remove(minion, out DeployPresentation? deploy))
		{
			return;
		}
		if (IsInstanceValid(deploy.Flight))
		{
			deploy.Flight!.CancelAndFree();
		}
		if (!SceneLifecycleGuard.ShouldSkip(_boardUI))
		{
			_ = _boardUI.ClearPendingDeploy(deploy.Position, minion);
		}
	}

	private void CancelAllDeployments()
	{
		_playerDeployHandoff = null;
		foreach (Minion minion in _deployments.Keys)
		{
			CancelDeploy(minion); // Dictionary 支持迭代期间移除当前项。
		}
	}

	/// <summary>敌方出牌起点：可见手牌栏中心 &gt; 敌方身份卡中心 &gt; 零（调用方回退）。</summary>
	private Vector2 ResolveEnemyPlayOrigin(int enemyIndex)
	{
		if (!SceneLifecycleGuard.ShouldSkip(_enemyHandBar) && _enemyHandBar.IsVisibleInTree() && _enemyHandBar.Size.X > 0f)
		{
			return _enemyHandBar.GetGlobalTransformWithCanvas() * (_enemyHandBar.Size * 0.5f);
		}
		if (enemyIndex >= 0)
		{
			Vector2 identity = GetEnemyIdentityCardCenter(enemyIndex);
			if (identity != Vector2.Zero)
				return identity;
		}
		// 没有手牌栏时回退到最先存活的敌人身份卡。
		for (int i = 0; i < _enemyCards.Count; i++)
		{
			Vector2 center = GetEnemyIdentityCardCenter(i);
			if (center != Vector2.Zero)
				return center;
		}
		return Vector2.Zero;
	}

	private void OnBoardMinionRemovedUnsubscribeDamage(Minion minion)
	{
		CancelDeploy(minion);
		_assaultOrigins.Remove(minion);
		CancelMoveVfx(minion);
		UnsubscribeMinionDamageEvents(minion);
	}

	// ===== 亮牌展示（指令 / 领域 / 反制） =====

	/// <summary>
	/// 规则层结算出牌时广播的亮牌演出：起点 → 左侧展示位定格 → 飞向牌堆/效果栏。
	/// 玩家自己的指令/领域在此顺手释放拖拽卡，统一由此处收尾，避免与飞牌动画叠影。
	/// </summary>
	private void OnCardPlayPresented(CardPlayPresentationEvent presented)
	{
		if (presented == null || SceneLifecycleGuard.ShouldSkip(this))
			return;
		if (_cardFlyLayer == null || !GodotObject.IsInstanceValid(_cardFlyLayer))
			return;

		bool playerSide = presented.IsPlayerSide;
		Vector2 origin = ResolveCardPlayOrigin(presented);
		Vector2 targetCenter = presented.Target != null ? ResolveTargetScreenPos(presented.Target) : Vector2.Zero;
		Vector2 endCenter = ResolveCardPlayEnd(presented);
		string badge = ResolveCardPlayBadge(presented);

		// 同一时刻只保留一张亮牌：敌方连续两步之间的等待可能略短于上一张的收尾。
		if (_activeShowcase != null && GodotObject.IsInstanceValid(_activeShowcase))
			_activeShowcase.CancelAndFree();
		_activeShowcase = CardShowcaseVfx.Play(presented.Card, playerSide, origin, targetCenter, endCenter, badge,
			presented.IsCountermeasureTrigger || presented.IsCanceled, ResolveDeploySpeed(), _cardFlyLayer);
		if (_activeShowcase != null)
		{
			SfxManager.Instance?.PlayOrderCast();
		}

		// 亮牌接管后释放交互层。牌仍在模型里（例如还在等待选择）时隐藏保留同一张视图。
		if (playerSide && !presented.IsCountermeasureTrigger && _dragCardUI?.Card?.Id == presented.Card.Id)
		{
			ReleaseDragCard(played: true);
		}
	}

	/// <summary>亮牌起点：玩家取本次出牌记录/拖拽卡中心；敌方取顶栏手牌或身份卡中心。</summary>
	private Vector2 ResolveCardPlayOrigin(CardPlayPresentationEvent presented)
	{
		if (presented.IsPlayerSide)
		{
			if (presented.IsCountermeasureTrigger)
				return GetPlayerHandAreaCenter();
			Vector2 recorded = _lastPlayedCardOrigin;
			_lastPlayedCardOrigin = Vector2.Zero;
			if (recorded != Vector2.Zero)
				return recorded;
			if (HandUI.IsLive(_dragCardUI) && _dragCardUI!.Card?.Id == presented.Card.Id)
			{
				return _dragCardUI.ScreenCenter;
			}
			return GetPlayerHeroScreenCenter();
		}
		return ResolveEnemyPlayOrigin(GetEnemyHeroIndex(presented.CasterHero));
	}

	/// <summary>亮牌收尾：领域→效果栏，轮战→抽牌堆，其余→弃牌堆；敌方飞向身份卡侧。</summary>
	private Vector2 ResolveCardPlayEnd(CardPlayPresentationEvent presented)
	{
		if (presented.IsCountermeasureTrigger)
		{
			return presented.IsPlayerSide
				? GetPlayerEffectBarCenter()
				: GetEnemyEffectBarCenter(GetEnemyHeroIndex(presented.CasterHero));
		}
		if (presented.Card.Type == CardType.Domain)
		{
			return presented.IsPlayerSide
				? GetPlayerEffectBarCenter()
				: GetEnemyEffectBarCenter(GetEnemyHeroIndex(presented.CasterHero));
		}
		if (!presented.IsPlayerSide)
			return GetEnemyEffectBarCenter(GetEnemyHeroIndex(presented.CasterHero));
		return presented.Card.HasRecycle ? GetDrawPileCenter() : GetDiscardPileCenter();
	}

	/// <summary>卡面下方标签；全部走本地化键，默认态不含任何 Emoji。</summary>
	private static string ResolveCardPlayBadge(CardPlayPresentationEvent presented)
	{
		if (presented.IsCountermeasureTrigger)
		{
			return presented.IsCanceled
				? Localization.Localization.T("ui.combat.log_counter_canceled", "已被反制")
				: Localization.Localization.T("ui.combat.log_counter_trigger", "反制触发");
		}
		if (presented.IsCanceled)
			return Localization.Localization.T("ui.combat.log_counter_canceled", "已被反制");
		if (presented.Card.Type == CardType.Domain)
			return Localization.Localization.T("ui.combat.log_domain_deploy", "展开领域");
		return presented.IsPlayerSide
			? Localization.Localization.T("ui.combat.log_player_order", "我方指令")
			: Localization.Localization.T("ui.combat.log_enemy_order", "敌方指令");
	}

	/// <summary>玩家手牌区中心；反制触发等不来自拖拽卡的演出以它为起点。</summary>
	private Vector2 GetPlayerHandAreaCenter()
	{
		if (_handArea != null && GodotObject.IsInstanceValid(_handArea))
		{
			Rect2 rect = _handArea.GetGlobalRect();
			if (rect.Size.X > 0f && rect.Size.Y > 0f)
				return rect.Position + rect.Size * 0.5f;
		}
		return GetPlayerHeroScreenCenter();
	}

	/// <summary>敌方身份卡效果栏中心；越界或未布局时退到身份卡中心。</summary>
	private Vector2 GetEnemyEffectBarCenter(int enemyIndex)
	{
		if (enemyIndex < 0 || enemyIndex >= _enemyCards.Count)
			return Vector2.Zero;
		EnemyIdentityCard card = _enemyCards[enemyIndex];
		if (!GodotObject.IsInstanceValid(card))
			return Vector2.Zero;
		EffectBar? bar = card.EffectBar;
		if (bar != null && GodotObject.IsInstanceValid(bar))
		{
			Rect2 barRect = bar.GetGlobalRect();
			if (barRect.Size.X > 0f && barRect.Size.Y > 0f)
				return barRect.Position + barRect.Size * 0.5f;
		}
		return GetEnemyIdentityCardCenter(enemyIndex);
	}

	private void OnBoardMinionPreRemove(Minion minion, BoardPosition position)
	{
		if (!minion.IsPlayerSide || _deployments.ContainsKey(minion))
		{
			return;
		}

		var cardPos = _boardUI.GetSlotScreenCenter(position);
		if (cardPos == Vector2.Zero)
			return;

		var cardUI = new CardUI { DisplayOnly = true };
		cardUI.SetCard(minion.ToRuntimeCard());

		bool toDrawPile = minion.HasRecycle;
		Vector2 targetPos = toDrawPile ? GetDrawPileCenter() : GetDiscardPileCenter();
		CardFlyVfx.PlayToDiscard(cardUI, targetPos, _cardFlyLayer, cardPos);
	}

	/// <summary>记录突击单位推进前所在支援槽；非突击移动立即清除旧记录。</summary>
	private void OnBoardMinionMoved(Minion minion, BoardPosition from, BoardPosition to)
	{
		CancelDeploy(minion);
		if (minion.UnitClass == UnitClass.Assault
			&& from.Row == BoardRow.PlayerSupport
			&& to.Row == BoardRow.Frontline)
		{
			_assaultOrigins[minion] = from;
		}
		else
		{
			_assaultOrigins.Remove(minion);
		}

		// 普通推进前线：在部署层生成替身平滑滑行。突击单位由 OnAssaultAdvance 在同一栈帧接管并撤销替身。
		bool advanceFrontline = to.Row == BoardRow.Frontline
			&& from.Row is BoardRow.PlayerSupport or BoardRow.EnemySupport;
		if (!advanceFrontline || _deployVfxLayer == null || !GodotObject.IsInstanceValid(_deployVfxLayer))
			return;
		// 一步突击会立即换掉下方的滑行替身，实际推进只在这里响一次。
		SfxManager.Instance?.PlayUnitMoveAdvance();
		CancelMoveVfx(minion);
		Rect2 fromRect = _boardUI.GetSlotScreenRect(from);
		Rect2 toRect = _boardUI.GetSlotScreenRect(to);
		MinionMoveVfx? vfx = MinionMoveVfx.Play(minion, fromRect, toRect, minion.IsPlayerSide,
			ResolveDeploySpeed(), _deployVfxLayer);
		if (vfx != null)
			_moveVfx[minion] = vfx;
	}

	/// <summary>撤销并移除某随从的普通推进替身；一步突击接管与离场清理共用。</summary>
	private void CancelMoveVfx(Minion minion)
	{
		if (!_moveVfx.TryGetValue(minion, out MinionMoveVfx? vfx))
			return;
		_moveVfx.Remove(minion);
		if (GodotObject.IsInstanceValid(vfx))
			vfx.CancelAndFree();
	}

	private static float ResolveDeploySpeed() => Mathf.Clamp(
		UIScaler.Instance?.DeployAnimationSpeed ?? UIScaler.DefaultDeployAnimationSpeed,
		UIScaler.MinDeployAnimationSpeed, UIScaler.MaxDeployAnimationSpeed);

	/// <summary>
	/// 一步突击演出：50% 停稳射击、50% 行进间射击。
	/// 演出只读棋盘坐标，不改变战斗结算；所有 Tween 回调检查实例有效性。
	/// </summary>
	private void OnAssaultAdvance(Minion minion)
	{
		if (SceneLifecycleGuard.ShouldSkip(this)
			|| _deployVfxLayer == null || !GodotObject.IsInstanceValid(_deployVfxLayer))
			return;
		// 后续仅当突击演出成功创建时才撤销普通推进替身（见下方 vfx != null 分支）。
		BoardPosition origin = _assaultOrigins.TryGetValue(minion, out BoardPosition recorded)
			? recorded
			: new BoardPosition(BoardRow.PlayerSupport, Mathf.Clamp(minion.BoardSlotIndex, 0, Board.SupportCapacity - 1));
		_assaultOrigins.Remove(minion);
		var originRect = _boardUI.GetSlotScreenRect(origin);
		var targetRect = _boardUI.GetSlotScreenRect(new BoardPosition(BoardRow.Frontline, minion.BoardSlotIndex));
		if (originRect.Size == Vector2.Zero || targetRect.Size == Vector2.Zero)
			return;

		Vector2 shotTarget = ResolveAssaultShotTarget(minion, targetRect);
		bool fireOnMove = AssaultAdvanceVfx.IsFireOnMove(Random.Shared.Next(100));
		AssaultAdvanceVfx? vfx = AssaultAdvanceVfx.Play(
			minion.ToRuntimeCard(), originRect, targetRect, shotTarget, fireOnMove, _deployVfxLayer,
			() =>
			{
				_assaultVfx.Remove(minion);
				_assaultOrigins.Remove(minion);
			});
		if (vfx != null)
		{
			// 突击演出已接管：撤销 OnMinionMoved 刚创建的普通推进替身，避免两段动画叠影。
			CancelMoveVfx(minion);
			_assaultVfx[minion] = vfx;
		}
	}

	/// <summary>优先使用玩家本次点选目标；智能攻击由后续伤害事件校正真实弹着点。</summary>
	private Vector2 ResolveAssaultShotTarget(Minion attacker, Rect2 fallbackRect)
	{
		if (_pendingAssaultShotTarget is Minion minionTarget)
			return GetMinionScreenCenter(minionTarget);
		if (_pendingAssaultShotTarget is Hero heroTarget)
		{
			return heroTarget.IsPlayerSide ? GetPlayerHeroScreenCenter() : GetEnemyIdentityCardCenter(GetEnemyHeroIndex(heroTarget));
		}

		foreach (var target in GetLegalEnemyAttackTargets(attacker))
		{
			Vector2 position = ResolveTargetScreenPos(target);
			if (position != Vector2.Zero)
				return position;
		}
		return fallbackRect.Position + fallbackRect.Size * 0.5f + Vector2.Down * 70f;
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

		Action<int> onArmor = amount =>
		{
			var pos = ResolveTargetScreenPos(hero);
			if (pos != Vector2.Zero)
				FloatingDamageNumber.CreateArmorGained(amount, pos, _damageNumberContainer);
		};

		hero.OnDamageTaken += onDamage;
		hero.OnHealed += onHeal;
		hero.OnArmorGained += onArmor;
		_heroDamageHandlers[hero] = (onDamage, onHeal, onArmor);
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

		Action<int> onArmor = amount =>
		{
			var pos = GetMinionScreenCenter(minion);
			if (pos != Vector2.Zero)
				FloatingDamageNumber.CreateArmorGained(amount, pos, _damageNumberContainer);
		};

		minion.OnDamageTaken += onDamage;
		minion.OnHealed += onHeal;
		minion.OnArmorGained += onArmor;
		_minionDamageHandlers[minion] = (onDamage, onHeal, onArmor);
	}

	private void UnsubscribeMinionDamageEvents(Minion minion)
	{
		if (!_minionDamageHandlers.TryGetValue(minion, out var handlers))
			return;
		minion.OnDamageTaken -= handlers.Damage;
		minion.OnHealed -= handlers.Heal;
		minion.OnArmorGained -= handlers.Armor;
		_minionDamageHandlers.Remove(minion);
	}

	private void UnsubscribeAllDamageEvents()
	{
		foreach (var (hero, handlers) in _heroDamageHandlers)
		{
			hero.OnDamageTaken -= handlers.Damage;
			hero.OnHealed -= handlers.Heal;
			hero.OnArmorGained -= handlers.Armor;
		}
		_heroDamageHandlers.Clear();

		foreach (var (minion, handlers) in _minionDamageHandlers)
		{
			minion.OnDamageTaken -= handlers.Damage;
			minion.OnHealed -= handlers.Heal;
			minion.OnArmorGained -= handlers.Armor;
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

	private void OnSmartAttackPressed()
	{
		TrySmartAttack();
	}

	private void TrySmartAttack()
	{
		if (_combat == null)
			return;
		if (_combat.State.IsGameOver)
			return;
		if (_smartAttackButton.Disabled)
			return;
		if (_combat.IsDiscovering)
			return;
		if (!_combat.State.IsPlayerTurn)
			return;

		GD.Print("[CombatUI] 智能攻击触发");
		if (_combat.ExecuteSmartPlayerAttack())
			RefreshAll();
	}

	/// <summary>
	/// 尝试结束当前回合（按钮或热键触发）。
	/// 比 OnEndTurnPressed 多做一层守卫：检查按钮禁用态、发现选牌、非玩家回合。
	/// </summary>
	[SuppressMessage("Reliability", "CA2007:DoNotConfigureAwait", Justification = "Godot 按钮/热键顶层包装，后续刷新必须留在主线程。")]
	private async void TryEndTurn()
	{
		if (_combat == null || _combat.State.IsGameOver || _endTurnButton.Disabled
			|| _combat.IsDiscovering || !_combat.State.IsPlayerTurn || _combat.IsEnemyTurnStepping)
			return;

		GD.Print("[CombatUI] 热键结束回合（UI 步进）");
		_endTurnButton.Disabled = true;
		_smartAttackButton.Disabled = true;
		_heroPowerButton.Disabled = true;
		_weaponAttackButton.Disabled = true;
		_weaponActiveSkillButton.Disabled = true;
		try
		{
			await _combat.EndPlayerTurnAsync(PlayEnemyStepDelayAsync);
		}
		catch (Exception ex)
		{
			_combat.CancelEnemyTurnStepping();
			GD.PrintErr($"[CombatUI] 敌方回合步进异常，已安全中止：{ex}");
		}
		finally
		{
			if (GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion())
			{
				_endTurnButton.Disabled = !_combat.State.IsPlayerTurn || _combat.State.IsGameOver;
				RefreshAll();
			}
		}
	}

	[SuppressMessage("Reliability", "CA2007:DoNotConfigureAwait", Justification = "Godot SceneTreeTimer 必须在主线程等待。")]
	private async Task PlayEnemyStepDelayAsync(EnemyStepKind kind)
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;
		// 每一步先刷新：顶部手牌张数、法力水晶与棋盘在等待前就反映真实结算结果。
		RefreshAll();
		float speed = ResolveDeploySpeed();
		float duration = kind switch
		{
			EnemyStepKind.TurnPrepare => 0.40f,
			// 与敌方 CardShowcaseVfx（0.22 + 0.95 + 0.34 = 1.51）对齐，留足完整阅读时间。
			EnemyStepKind.PlayCard => 1.45f,
			// 飞行和短促沉降完整结束；光尘尾迹可以与下一步重叠。
			EnemyStepKind.Deploy => MinionDeployMath.EnemyStepDelay,
			EnemyStepKind.Move => 0.55f,
			EnemyStepKind.Attack or EnemyStepKind.Weapon or EnemyStepKind.HeroPower
				or EnemyStepKind.MinionAction => 0.68f,
			EnemyStepKind.TurnEnd => 0.30f,
			_ => 0.30f,
		} / speed;
		_ = await ToSignal(GetTree().CreateTimer(duration, processAlways: false), SceneTreeTimer.SignalName.Timeout);
		// 等待期间场景可能被销毁或快速 SL；下一步统一安排自己的刷新。
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;
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
		CancelAllDeployments();
		if (_gameOverPopup == null)
			return;

		_isVictory = isVictory;

		if (isVictory)
		{
			_gameOverPopup.Title = UiSymbolMode.Prefix(Localization.Localization.T("ui.combat.victory_title", "胜利！"), "★ ");
			_gameOverPopup.OkButtonText = Localization.Localization.T("ui.combat.continue_adventure", "继续冒险");
		}
		else
		{
			_gameOverPopup.Title = UiSymbolMode.Prefix(Localization.Localization.T("ui.combat.defeat_title", "失败"), "☠ ");
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

		// 战报抽屉展开时 ESC 优先收起，避免和卡牌取消抢同一个键
		if (_combatLogDrawer != null && GodotObject.IsInstanceValid(_combatLogDrawer) && _combatLogDrawer.IsOpen)
		{
			_combatLogDrawer.SetOpen(false);
			return;
		}

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

		_handUI.ClearKeyboardFocus();
		if (IsPointerTargetSelectionMode() || IsSelectingHandCardForPlay())
		{
			GD.Print("[CombatUI] 热键取消目标/出牌选择");
			ResetSelection();
		}
	}

	private void HandleCancelHotkey()
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
		{
			return;
		}
		if (SharesPauseKey() && (_isPaused || !HasCancellableInteraction()))
		{
			TogglePause();
			return;
		}
		HandleCancel();
	}

	private void OnPauseHotkey()
	{
		if (!SharesPauseKey())
		{
			TogglePause();
		}
	}

	private static bool SharesPauseKey() => InputManager.Instance is InputManager input
		&& input.GetKey(OdysseyInput.Pause) == input.GetKey(OdysseyInput.Cancel);

	private bool HasCancellableInteraction() => _isHandSelecting || _selectionMode != SelectionMode.Normal
		|| (_combatLogDrawer != null && IsInstanceValid(_combatLogDrawer) && _combatLogDrawer.IsOpen);

	/// <summary>
	/// 「保存并退出」——保存玩家生命值到 GameManager，返回主菜单。
	/// 不标记当前房间为完成（RunState 保持不变，可重新进入此房间）。
	/// </summary>
	private void OnSaveAndExit()
	{
		GD.Print("[CombatUI] 保存并退出 → 返回主菜单");
		_combat?.CancelEnemyTurnStepping();

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
		_combat?.CancelEnemyTurnStepping();

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
