#nullable enable
using System.Collections.Generic;
using Godot;
using OdysseyCards.Infrastructure;

namespace OdysseyCards.UI;

/// <summary>
/// CombatUI 布局构建——所有 UI 控件的程序化创建代码。
/// </summary>
public partial class CombatUI
{
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

		// 战斗背景——稀疏星空 + 战场中央微光晕
		var bg = new StarfieldBackground
		{
			Name = "CombatBackground",
			StarCount = 60,
			EnableMeteors = false,
			CenterGlow = true,
		};
		bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
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
			CustomMinimumSize = new Vector2(0, 170),
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

		// 法力值区域占位——VBox 垂直堆叠法力标签和热力条，避免重叠
		var manaPlaceholder = new VBoxContainer
		{
			Name = "PlayerManaPlaceholder",
			Alignment = BoxContainer.AlignmentMode.Center,
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

		// 英雄技能区域占位
		var hpPlaceholder = new CenterContainer
		{
			Name = "HeroPowerPlaceholder",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		container.AddChild(hpPlaceholder);

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
	/// 创建玩家英雄身份卡——包装玩家生命值条、护甲、防御、效果图标和施法按钮。
	/// 替代旧的 CreateHealthBars（玩家部分）+ CreateArmorLabels（玩家部分）+ CreatePlayerHeroPanel。
	/// </summary>
	private void CreatePlayerIdentityCard()
	{
		_playerIdentityCard = new PlayerIdentityCard();

		// 存入 PlayerHealthPlaceholder
		var playerHealthContainer = GetNode<VBoxContainer>("CombatRoot/PlayerArea/PlayerHealthPlaceholder");
		playerHealthContainer?.AddChild(_playerIdentityCard);

		// 将 PlayerHeroSpellButton 引用绑定到卡内的按钮（供 CombatUI 直接使用）
		_playerHeroSpellButton = _playerIdentityCard.HeroSpellButton;
	}

	/// <summary>
	/// 从 PackedScene 或程序化创建生命值条实例。
	/// 样式统一由 HealthBar._Ready 应用星途底座（此处不再覆盖）。
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
		}

		hb.Name = name;
		return hb;
	}

	/// <summary>
	/// 创建玩家法力值显示——六边形水晶阵列（主体）+ 数字标签（辅助小字）。
	/// 敌人使用意图系统，不显示法力值。
	/// </summary>
	private void CreateManaLabels()
	{
		var playerManaPlaceholder = GetNode<Container>("CombatRoot/PlayerArea/PlayerManaPlaceholder");

		// 法力水晶条（图形化主体）
		float scale = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		_manaCrystalBar = new ManaCrystalBar
		{
			Name = "ManaCrystalBar",
			CustomMinimumSize = new Vector2(140 * scale, 24 * scale),
		};
		playerManaPlaceholder?.AddChild(_manaCrystalBar);

		// 玩家法力数字（辅助小字）
		_playerManaLabel = new Label
		{
			Name = "PlayerManaLabel",
			Text = Localization.Localization.T("ui.combat.mana_format", "法力 {current}/{max}").Replace("{current}", "0").Replace("{max}", "1"),
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(120, 18),
		};
		_playerManaLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1f, 0.8f));
		_playerManaLabel.AddThemeFontSizeOverride("font_size", 13);

		playerManaPlaceholder?.AddChild(_playerManaLabel);
	}

	/// <summary>
	/// 创建双方护甲值显示标签——玩家护甲/防御已迁移到 PlayerIdentityCard。
	/// </summary>
	private void CreateArmorLabels()
	{
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
		var buttonRow = new HBoxContainer
		{
			Name = "CombatCommandButtonRow",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		buttonRow.AddThemeConstantOverride("separation", 8);

		_smartAttackButton = new Button
		{
			Name = "SmartAttackButton",
			Text = Localization.Localization.T("ui.combat.smart_attack", "⚡ 智能攻击"),
			CustomMinimumSize = new Vector2(120, 48),
			Disabled = true,
		};

		_endTurnButton = new Button
		{
			Name = "EndTurnButton",
			Text = Localization.Localization.T("ui.combat.end_turn", "结束回合"),
			CustomMinimumSize = new Vector2(120, 48),
		};

		var buttonPlaceholder = GetNode<CenterContainer>("CombatRoot/PlayerArea/EndTurnButtonPlaceholder");
		buttonRow.AddChild(_smartAttackButton);
		buttonRow.AddChild(_endTurnButton);
		buttonPlaceholder?.AddChild(buttonRow);
	}

	/// <summary>
	/// 创建英雄技能按钮——置于玩家区域的 HeroPowerPlaceholder 中。
	/// 显示英雄技能名称和法力消耗，点击或按 H 键激活。
	/// </summary>
	private void CreateHeroPowerButton()
	{
		_heroPowerButton = new Button
		{
			Name = "HeroPowerButton",
			Text = Localization.Localization.T("hero_power.use_button", "英雄技能"),
			CustomMinimumSize = new Vector2(120, 48),
			Disabled = true, // 初始禁用，回合开始时启用
		};

		var hpPlaceholder = GetNode<CenterContainer>("CombatRoot/PlayerArea/HeroPowerPlaceholder");
		hpPlaceholder?.AddChild(_heroPowerButton);
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
		if (enemyHeroPlaceholder == null)
			return;

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
	/// 创建玩家英雄交互面板——已被 CreatePlayerIdentityCard 替代。
	/// 保留空壳以避免删除调用点带来的连锁修改。
	/// </summary>
	private void CreatePlayerHeroPanel()
	{
		// 不再单独创建面板；玩家身份卡已包含英雄标签和施法按钮。
		// _playerHeroSpellButton 由 CreatePlayerIdentityCard 赋值。
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
		if (deckPlaceholder == null)
			return;

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
		if (_combat == null)
			return;
		if (_isPaused || _combat.IsDiscovering)
			return;

		var cards = _combat.PlayerHero.DeckState.DrawPile;
		ShowPileViewer(Localization.Localization.T("ui.combat.draw_pile", "抽牌堆"), cards, showOrderNumbers: true);
	}

	/// <summary>
	/// 显示弃牌堆查看弹窗（热键 S 或按钮触发）。
	/// </summary>
	private void ShowDiscardPileView()
	{
		if (_combat == null)
			return;
		if (_isPaused || _combat.IsDiscovering)
			return;

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
		if (weaponPlaceholder == null)
			return;

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
	}

	/// <summary>
	/// 创建状态效果图标容器。
	/// 玩家效果图标已迁移到 PlayerIdentityCard。
	/// </summary>
	private void CreateStatusEffectUI()
	{
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
		var manaPlaceholder = GetNodeOrNull<Container>("CombatRoot/PlayerArea/PlayerManaPlaceholder");
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
						.Replace("{cost}", card.GetEffectiveCost().ToString())
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

	// ===== 播放区域 UI =====

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
		if (_playZonePanel == null)
			return;

		var viewport = GetViewport().GetVisibleRect().Size;
		float threshold = viewport.Y * PlayZoneBaseRatio;
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
		if (_playZonePanel == null)
			return;

		if (_playZonePanelConnected)
		{
			_playZonePanel.GuiInput -= OnPlayZoneGuiInput;
			_playZonePanelConnected = false;
		}

		_playZonePanel.MouseFilter = MouseFilterEnum.Ignore;
		_playZonePanel.Visible = false;
	}
}
