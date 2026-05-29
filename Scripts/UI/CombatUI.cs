using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Character;
using OdysseyCards.Combat;

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
    [Export] public PackedScene HealthBarScene { get; set; }

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
    /// 玩家英雄生命值条——左下角。
    /// </summary>
    private HealthBar _playerHealthBar = null!;

    /// <summary>
    /// 敌方英雄生命值条——左上角。
    /// </summary>
    private HealthBar _enemyHealthBar = null!;

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
    /// 敌方护甲值显示——敌方生命值条旁。
    /// </summary>
    private Label _enemyArmorLabel = null!;

    /// <summary>
    /// 攻击敌方英雄按钮——攻击目标选择模式下可见。
    /// </summary>
    private Button _enemyHeroAttackButton = null!;

    /// <summary>
    /// 对敌方英雄施法按钮——法术目标选择模式下可见。
    /// </summary>
    private Button _enemyHeroSpellButton = null!;

    /// <summary>
    /// 敌方意图显示标签。
    /// </summary>
    private Label _enemyIntentLabel = null!;

    /// <summary>
    /// 敌方英雄交互面板——有可见色块背景的容器。
    /// </summary>
    private Panel _enemyHeroPanel = null!;

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
    /// 拖拽层——卡牌拖拽时重parent到此，使其脱离 HandUI 的 HBoxContainer 布局约束。
    /// </summary>
    private Control _dragLayer = null!;

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
    /// 敌方武器信息标签。
    /// </summary>
    private Label _enemyWeaponLabel = null!;

    /// <summary>
    /// 玩家状态效果图标容器（HBoxContainer）。
    /// </summary>
    private HBoxContainer _playerStatusContainer = null!;

    /// <summary>
    /// 敌方状态效果图标容器（HBoxContainer）。
    /// </summary>
    private HBoxContainer _enemyStatusContainer = null!;

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
    /// 全局输入处理——开发者伤害模式下右键取消。
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb
            && mb.ButtonIndex == MouseButton.Right
            && mb.Pressed
            && _selectionMode == SelectionMode.DevDamageTargeting)
        {
            ExitDevDamageMode();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// 分辨率变化时刷新所有 UI 尺寸和布局。
    /// </summary>
    private void OnResolutionChanged()
    {
        GD.Print($"[CombatUI] 分辨率变化 — 缩放因子 {UIScaler.Instance?.GetScaleFactor() ?? 1f:F2}");
        RefreshAll();
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
                  $"敌方生命 {combat.EnemyHero.CurrentHealth}/{combat.EnemyHero.MaxHealth}");

        // 创建并初始化子组件
        SetupBoardUI();
        SetupHandUI();
        CreateHealthBars();
        CreateManaLabels();
        CreateArmorLabels();
        CreateEndTurnButton();
        CreateEnemyHeroAttackButton();
        CreateEnemyIntentLabel();
        CreateDeckButtons();
        CreateWeaponUI();
        CreateStatusEffectUI();
        CreateGameOverPopup();
        CreatePlayZonePanel();

        // 订阅事件
        SubscribeEvents();

        // 首次刷新
        RefreshAll();

        GD.Print("[CombatUI] 初始化完成");
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
            CustomMinimumSize = new Vector2(0, 90),
        };

        // 敌方生命值区域（左侧）
        var enemyHealthContainer = new VBoxContainer
        {
            Name = "EnemyHealthContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        container.AddChild(enemyHealthContainer);
        // 生命值条和护甲标签的占位——会在 Initialize 中创建

        // 敌方意图占位（中间）
        var intentPlaceholder = new CenterContainer
        {
            Name = "EnemyIntentPlaceholder",
            SizeFlagsHorizontal = SizeFlags.Fill,
            CustomMinimumSize = new Vector2(120, 24),
        };
        container.AddChild(intentPlaceholder);

        // 英雄标签占位（右侧）
        var heroLabelPlaceholder = new CenterContainer
        {
            Name = "EnemyHeroLabelPlaceholder",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        container.AddChild(heroLabelPlaceholder);

        return container;
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
    /// </summary>
    private Control CreateHandArea()
    {
        var container = new Control
        {
            Name = "HandArea",
            CustomMinimumSize = new Vector2(0, 170),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        _handUI = new HandUI
        {
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        container.AddChild(_handUI);

        return container;
    }

    // ===== 子组件创建 =====

    /// <summary>
    /// 设置棋盘 UI 绑定——将 BoardUI 关联到 CombatManager.Board。
    /// </summary>
    private void SetupBoardUI()
    {
        _boardUI.SetBoard(_combat.Board);
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
                Text = "生命 ",
                CustomMinimumSize = new Vector2(50, 24),
            };
            hpLabel.AddThemeColorOverride("font_color", new Color(0.7f, 1f, 0.7f));
            hpLabel.AddThemeFontSizeOverride("font_size", 14);
            playerHealthContainer.AddChild(hpLabel);
            playerHealthContainer.AddChild(_playerHealthBar);
        }

        // 敌方生命值条
        _enemyHealthBar = InstantiateHealthBar("EnemyHealthBar");
        var enemyHealthContainer = GetNode<VBoxContainer>("CombatRoot/EnemyArea/EnemyHealthContainer");
        if (enemyHealthContainer != null)
        {
            // 生命值前缀标签
            var hpLabel = new Label
            {
                Text = "生命 ",
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
            Text = "法力 0/1",
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
            Text = "护甲: 0",
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
            Text = "护甲: 0",
            Visible = false,
            CustomMinimumSize = new Vector2(100, 20),
        };
        _enemyArmorLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.3f));
        _enemyArmorLabel.AddThemeFontSizeOverride("font_size", 14);

        var enemyHealthContainer = GetNode<VBoxContainer>("CombatRoot/EnemyArea/EnemyHealthContainer");
        enemyHealthContainer?.AddChild(_enemyArmorLabel);
    }

    /// <summary>
    /// 创建回合结束按钮——右下角，文本「结束回合」。
    /// </summary>
    private void CreateEndTurnButton()
    {
        _endTurnButton = new Button
        {
            Name = "EndTurnButton",
            Text = "结束回合",
            CustomMinimumSize = new Vector2(120, 40),
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
            Title = "游戏结束",
            OkButtonText = "返回主菜单",
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
            Text = "敌方英雄",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        heroLabel.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.5f));
        heroLabel.AddThemeFontSizeOverride("font_size", 16);
        panelContent.AddChild(heroLabel);

        // 攻击按钮（攻击目标模式下可见）
        _enemyHeroAttackButton = new Button
        {
            Name = "EnemyHeroAttackButton",
            Text = "⚔ 攻击敌方英雄",
            CustomMinimumSize = new Vector2(140, 36),
            Visible = false,
        };
        _enemyHeroAttackButton.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
        panelContent.AddChild(_enemyHeroAttackButton);

        // 对敌方英雄施法按钮（法术目标模式下可见）
        _enemyHeroSpellButton = new Button
        {
            Name = "EnemyHeroSpellButton",
            Text = "✦ 对敌方英雄施法",
            CustomMinimumSize = new Vector2(140, 36),
            Visible = false,
        };
        _enemyHeroSpellButton.AddThemeColorOverride("font_color", new Color(1f, 0.7f, 0.2f));
        panelContent.AddChild(_enemyHeroSpellButton);

        panelContainer.AddChild(_enemyHeroPanel);
        enemyHeroPlaceholder.AddChild(panelContainer);
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
            Text = "抽牌堆 (0)",
            CustomMinimumSize = new Vector2(100, 32),
        };
        _drawPileBtn.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 1f));
        _drawPileBtn.AddThemeFontSizeOverride("font_size", 13);
        _drawPileBtn.Pressed += () =>
        {
            if (_combat != null)
            {
                var cards = _combat.PlayerHero.DeckState.DrawPile;
                ShowPileViewer("抽牌堆", cards);
            }
        };
        btnContainer.AddChild(_drawPileBtn);

        // 间距
        var spacer = new Control { CustomMinimumSize = new Vector2(8, 1) };
        btnContainer.AddChild(spacer);

        // 弃牌堆按钮
        _discardPileBtn = new Button
        {
            Name = "DiscardPileBtn",
            Text = "弃牌堆 (0)",
            CustomMinimumSize = new Vector2(100, 32),
        };
        _discardPileBtn.AddThemeColorOverride("font_color", new Color(0.8f, 0.7f, 0.6f));
        _discardPileBtn.AddThemeFontSizeOverride("font_size", 13);
        _discardPileBtn.Pressed += () =>
        {
            if (_combat != null)
            {
                var cards = _combat.PlayerHero.DeckState.DiscardPile;
                ShowPileViewer("弃牌堆", cards);
            }
        };
        btnContainer.AddChild(_discardPileBtn);

        deckPlaceholder.AddChild(btnContainer);
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
            Text = "⚔ 武器攻击",
            CustomMinimumSize = new Vector2(100, 28),
            Visible = false,
        };
        _weaponAttackButton.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.3f));
        _weaponAttackButton.AddThemeFontSizeOverride("font_size", 12);
        weaponBtnContainer.AddChild(_weaponAttackButton);

        // 主动技能按钮
        _weaponActiveSkillButton = new Button
        {
            Name = "WeaponActiveSkillButton",
            Text = "✦ 技能",
            CustomMinimumSize = new Vector2(100, 28),
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
        // 玩家状态效果容器
        _playerStatusContainer = new HBoxContainer
        {
            Name = "PlayerStatusContainer",
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        var playerHealthContainer = GetNode<VBoxContainer>("CombatRoot/PlayerArea/PlayerHealthPlaceholder");
        playerHealthContainer?.AddChild(_playerStatusContainer);

        // 敌方状态效果容器
        _enemyStatusContainer = new HBoxContainer
        {
            Name = "EnemyStatusContainer",
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        var enemyHealthContainer = GetNode<VBoxContainer>("CombatRoot/EnemyArea/EnemyHealthContainer");
        enemyHealthContainer?.AddChild(_enemyStatusContainer);
    }

    /// <summary>
    /// 弹出牌堆查看窗口，以列表形式展示所有卡牌名称和费用。
    /// 复用同一个弹窗实例，点击关闭按钮或 OK 即可关闭。
    /// </summary>
    /// <param name="title">弹窗标题（如"抽牌堆""弃牌堆"）</param>
    /// <param name="cards">要展示的卡牌列表</param>
    private void ShowPileViewer(string title, List<OdysseyCards.Card.Card> cards)
    {
        // 关闭之前的弹窗
        _pileViewPopup?.QueueFree();

        _pileViewPopup = new AcceptDialog
        {
            Title = title,
            Size = new Vector2I(280, 320),
            OkButtonText = "关闭",
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
                Text = "（空）",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            emptyLabel.AddThemeFontSizeOverride("font_size", 14);
            listContainer.AddChild(emptyLabel);
        }
        else
        {
            foreach (var card in cards)
            {
                var cardLabel = new Label
                {
                    Text = $"[{card.Cost}费] {card.CardName}",
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
        _drawPileBtn.Text = $"抽牌堆 ({deckState.DrawPile.Count})";
        _discardPileBtn.Text = $"弃牌堆 ({deckState.DiscardPile.Count})";
    }

    // ===== 事件订阅 =====

    /// <summary>
    /// 订阅所有子组件事件。
    /// </summary>
    private void SubscribeEvents()
    {
        // 棋盘槽位点击
        _boardUI.OnSlotClicked += OnBoardSlotClicked;

        // 手牌卡牌选中
        _handUI.OnCardSelectedForPlay += OnCardSelectedFromHand;

        // 手牌取消（右键）
        _handUI.OnCardCancelled += OnCardDragCancelled;

        // 回合结束按钮
        _endTurnButton.Pressed += OnEndTurnPressed;

        // 攻击敌方英雄按钮
        _enemyHeroAttackButton.Pressed += OnEnemyHeroAttackPressed;

        // 对敌方英雄施法按钮
        _enemyHeroSpellButton.Pressed += OnEnemyHeroSpellTarget;

        // 武器攻击按钮
        _weaponAttackButton.Pressed += OnWeaponAttackPressed;

        // 武器主动技能按钮
        _weaponActiveSkillButton.Pressed += OnWeaponActiveSkillPressed;

        // 牌堆/手牌状态变化 → 自动刷新 UI
        _combat.PlayerHero.DeckState.OnDrawPileChanged += UpdateDeckCounts;
        _combat.PlayerHero.DeckState.OnDiscardPileChanged += UpdateDeckCounts;
        _combat.PlayerHero.DeckState.OnHandChanged += () => _handUI.RefreshHand();

        // 法力值变化 → 自动更新显示
        _combat.PlayerHero.OnManaChanged += (_, _) => UpdateManaDisplay();

        // 敌方意图变化 → 更新意图显示
        _combat.OnCombatStateChanged += RefreshIntentDisplay;

        // 游戏结束 → 显示弹窗
        _combat.OnGameOver += ShowGameOverPopup;
    }

    // ===== 刷新方法 =====

    /// <summary>
    /// 刷新所有子组件——棋盘、手牌、生命值、法力值和护甲。
    /// 在每次操作完成后调用以确保界面与游戏状态同步。
    /// </summary>
    public void RefreshAll()
    {
        // 清理拖拽中的卡牌 UI（含取消事件订阅）
        CleanupDragCard();

        _boardUI.RefreshBoard();
        _boardUI.UpdateActionCostDimming(_combat.PlayerHero.CurrentMana);
        _handUI.RefreshHand();
        UpdateHealthBars();
        UpdateManaDisplay();
        UpdateArmorDisplay();
        UpdateDeckCounts();

        // 每次刷新时重置为正常模式（先重置再更新武器，避免显示被覆盖）
        ResetSelection();

        UpdateWeaponDisplay();
        UpdateStatusEffectDisplay();

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

        var intent = _combat.GetCurrentEnemyIntent();
        _enemyIntentLabel.Text = intent.GetDisplayDescription(_combat);
    }

    /// <summary>
    /// 更新双方英雄生命值条。
    /// </summary>
    private void UpdateHealthBars()
    {
        if (_combat == null) return;

        _playerHealthBar.UpdateHealth(_combat.PlayerHero.CurrentHealth, _combat.PlayerHero.MaxHealth);
        _enemyHealthBar.UpdateHealth(_combat.EnemyHero.CurrentHealth, _combat.EnemyHero.MaxHealth);
    }

    /// <summary>
    /// 更新玩家法力值显示，格式「法力 Current/Max」。
    /// 敌人使用意图系统，不跟踪法力值。
    /// </summary>
    private void UpdateManaDisplay()
    {
        if (_combat == null) return;

        _playerManaLabel.Text = $"法力 {_combat.PlayerHero.CurrentMana}/{_combat.PlayerHero.MaxMana}";
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
            _playerArmorLabel.Text = $"护甲: {playerArmor}";
        }

        // 敌方护甲
        int enemyArmor = _combat.EnemyHero.CurrentArmor;
        _enemyArmorLabel.Visible = enemyArmor > 0;
        if (enemyArmor > 0)
        {
            _enemyArmorLabel.Text = $"护甲: {enemyArmor}";
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

            case SelectionMode.Normal:
                HandleNormalSlotClick(slotIndex, isPlayerSide);
                break;

            case SelectionMode.DevDamageTargeting:
                GD.Print($"[CombatUI] OnBoardSlotClicked → DevDamageTargeting, slot={slotIndex}, side={(isPlayerSide ? "P" : "E")}");
                HandleDevDamageSlot(slotIndex, isPlayerSide);
                break;
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

        GD.Print($"[CombatUI] 选中己方随从 {minion.CardName} 准备攻击");

        // 高亮合法攻击目标
        HighlightValidAttackTargets();
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
        _combat.PlaySpell(_selectedCard, target);
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
        if (_dragCardUI != null)
        {
            _dragCardUI.OnCardDropped -= OnCardDroppedHandler;
            _dragCardUI.CancelDragSilent();
            _dragCardUI.Visible = false;
            _dragCardUI.QueueFree();
            _dragCardUI = null;
        }

        // 将卡牌 UI 从 HandUI 移到 DragLayer，脱离 HBoxContainer 布局约束
        var cardUI = _handUI.GetCardUIFor(card);
        if (cardUI != null)
        {
            cardUI.GetParent()?.RemoveChild(cardUI);
            _dragLayer.AddChild(cardUI);
            _dragCardUI = cardUI;

            // 从 HandUI 内部列表脱钩，防止 RefreshHand 误销毁拖拽中的卡片
            _handUI.DetachCardFromList(cardUI);

            // 订阅拖拽松手事件——用于拖拽→松手打出 / 松手取消
            cardUI.OnCardDropped += OnCardDroppedHandler;
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

            default:
                GD.Print($"[CombatUI] 未知卡牌类型：{card.Type}");
                break;
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
        if (_enemyHeroSpellButton.Visible && _enemyHeroPanel.GetGlobalRect().HasPoint(screenPos))
        {
            GD.Print("[CombatUI] 法术松手位置：敌方英雄");
            OnEnemyHeroSpellTarget();
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
        else if (_enemyHeroAttackButton.Visible && _enemyHeroPanel.GetGlobalRect().HasPoint(screenPos))
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
    }

    /// <summary>
    /// 进入法术目标选择模式——高亮敌方随从和英雄作为合法目标。
    /// </summary>
    private void EnterSpellTargetMode(Card.Card card)
    {
        _selectionMode = SelectionMode.TargetingSpell;
        _selectedCard = card;

        // 高亮敌方有随从的槽位
        var enemyTargets = new List<int>();
        for (int i = 0; i < Board.MaxSlotsPerSide; i++)
        {
            var m = _combat.Board.GetMinionAt(i, isPlayerSide: false);
            if (m != null && !m.IsDead)
            {
                enemyTargets.Add(i);
            }
        }

        _boardUI.HighlightSlots(enemyTargets, isPlayerSide: false, highlight: true);

        // 同时高亮我方随从（治疗/增益类法术目标）
        var friendlyTargets = new List<int>();
        for (int i = 0; i < Board.MaxSlotsPerSide; i++)
        {
            var m = _combat.Board.GetMinionAt(i, isPlayerSide: true);
            if (m != null && !m.IsDead)
            {
                friendlyTargets.Add(i);
            }
        }

        if (friendlyTargets.Count > 0)
        {
            _boardUI.HighlightSlots(friendlyTargets, isPlayerSide: true, highlight: true);
        }

        // 高亮敌方英雄作为法术目标
        _enemyHeroSpellButton.Visible = true;

        GD.Print($"[CombatUI] 法术目标模式——{_selectedCard.CardName}（可用目标：{enemyTargets.Count + friendlyTargets.Count} + 英雄）");
    }

    /// <summary>
    /// 进入无目标卡牌播放模式——显示播放区域指示器，等待玩家拖拽到播放区域松手打出。
    /// 适用于：领域（Domain）、无目标的法术（Spell.RequiresTarget == false）。
    /// </summary>
    private void EnterNoTargetPlayMode(Card.Card card)
    {
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

        var enemyTaunts = _combat.Board.GetTaunts(isEnemy: true);
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
            string costText = weapon.AttackCost > 0 ? $"{weapon.AttackCost}费" : "免费";
            string disabledText = weapon.IsDisabled ? " [禁用]" : "";
            _weaponInfoLabel.Text = $"{weapon.Name} {weapon.Attack}攻 {costText}{disabledText}";

            if (weapon.PassiveSkill != null)
                _weaponInfoLabel.TooltipText = $"被动：{weapon.PassiveSkill.Description}";

            // 武器攻击按钮——普通模式下显示
            if (_selectionMode == SelectionMode.Normal && !_combat.State.IsGameOver)
            {
                bool canAttack = _combat.PlayerHero.CanWeaponAttack()
                    && _combat.PlayerHero.CanSpendMana(weapon.AttackCost);
                _weaponAttackButton.Visible = true;
                _weaponAttackButton.Disabled = !canAttack || weapon.IsDisabled;
                _weaponAttackButton.Text = weapon.IsDisabled
                    ? "⚔ 武器攻击 [禁用]"
                    : $"⚔ 武器攻击 ({weapon.AttackCost}费)";
            }

            // 主动技能按钮
            if (weapon.ActiveSkill != null)
            {
                var active = weapon.ActiveSkill;
                _weaponActiveSkillButton.Visible = _selectionMode == SelectionMode.Normal && !_combat.State.IsGameOver;
                _weaponActiveSkillButton.Disabled = !active.CanUse(_combat.PlayerHero);

                if (active.CurrentCooldown > 0)
                    _weaponActiveSkillButton.Text = $"✦ {active.Name} (冷却{active.CurrentCooldown})";
                else
                    _weaponActiveSkillButton.Text = $"✦ {active.Name} ({active.Cost}费)";
            }
        }
        else
        {
            _weaponInfoLabel.Text = "无武器";
        }

        // --- 敌方武器 ---
        var enemyWeapon = _combat.EnemyHero.Weapon;
        if (enemyWeapon != null)
        {
            string disabledText = enemyWeapon.IsDisabled ? " [禁用]" : "";
            _enemyWeaponLabel.Text = $"武器: {enemyWeapon.Name} {enemyWeapon.Attack}攻{disabledText}";
        }
        else
        {
            _enemyWeaponLabel.Text = "";
        }
    }

    /// <summary>
    /// 更新双方英雄的状态效果图标显示。
    /// 每个状态效果显示为一个小标签（层数 + ID 缩写）。
    /// </summary>
    private void UpdateStatusEffectDisplay()
    {
        if (_combat == null) return;

        // 清空现有图标
        foreach (var child in _playerStatusContainer.GetChildren())
            child.QueueFree();
        foreach (var child in _enemyStatusContainer.GetChildren())
            child.QueueFree();

        // 玩家状态效果
        foreach (var (id, effect) in _combat.PlayerHero.StatusEffects)
        {
            var label = CreateStatusIcon(id, effect.Stacks);
            _playerStatusContainer.AddChild(label);
        }

        // 敌方状态效果
        foreach (var (id, effect) in _combat.EnemyHero.StatusEffects)
        {
            var label = CreateStatusIcon(id, effect.Stacks);
            _enemyStatusContainer.AddChild(label);
        }
    }

    /// <summary>
    /// 创建单个状态效果图标标签。
    /// </summary>
    private static Label CreateStatusIcon(string id, int stacks)
    {
        var label = new Label
        {
            Text = $"{id}({stacks})",
            CustomMinimumSize = new Vector2(60, 18),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
        label.AddThemeFontSizeOverride("font_size", 10);
        return label;
    }

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
    }

    /// <summary>
    /// 武器主动技能按钮点击——使用武器主动技能。
    /// </summary>
    private void OnWeaponActiveSkillPressed()
    {
        if (_combat.State.IsGameOver) return;

        GD.Print("[CombatUI] 武器主动技能按钮按下");
        _combat.UseWeaponActiveSkill();
        RefreshAll();
    }

    /// <summary>
    /// 高亮武器攻击合法目标——敌方随从（受嘲讽限制）+ 敌方英雄。
    /// </summary>
    private void HighlightWeaponTargets()
    {
        _boardUI.ClearHighlights();

        var enemyTaunts = _combat.Board.GetTaunts(isEnemy: true);
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
            _enemyHeroAttackButton.Text = $"⚔ 武器攻击 ({_combat.PlayerHero.Weapon!.AttackCost}费)";
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
        _combat.HeroWeaponAttackHero();

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
        if (_combat == null) return;
        if (_combat.State.IsGameOver) return;

        GD.Print("[CombatUI] 玩家结束回合");
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
            _gameOverPopup.Title = "★ 胜利！";
            _gameOverPopup.OkButtonText = "继续冒险";
        }
        else
        {
            _gameOverPopup.Title = "☠ 失败";
            _gameOverPopup.OkButtonText = "返回主菜单";
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
            GetTree().ChangeSceneToFile("res://Scenes/Map.tscn");
        }
        else
        {
            GD.Print("[CombatUI] 返回主菜单");
            GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
        }
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

        GD.Print($"[CombatUI] 开发者伤害模式 — 点击目标造成 {damageAmount} 点伤害（右键取消）");
    }

    private void ExitDevDamageMode()
    {
        _boardUI.ClearHighlights();
        _enemyHeroSpellButton.Visible = false;
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

    // ===== 选择状态管理 =====

    /// <summary>
    /// 重置所有选择状态——取消卡牌选中、攻击方选中、清除高亮、重置模式。
    /// </summary>
    private void ResetSelection()
    {
        _selectionMode = SelectionMode.Normal;
        _selectedCard = null;
        _selectedAttacker = null;
        _boardUI.ClearHighlights();
        _enemyHeroAttackButton.Visible = false;
        _enemyHeroSpellButton.Visible = false;
        _weaponAttackButton.Visible = false;
        _weaponActiveSkillButton.Visible = false;
        _handUI.DeselectCard();
        HidePlayZonePanel();
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
            Text = "松手打出\n（或点击此处）",
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

        if (!_playZonePanelConnected)
        {
            _playZonePanel.GuiInput += OnPlayZoneGuiInput;
            _playZonePanelConnected = true;
        }

        _playZonePanel.Visible = true;
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
