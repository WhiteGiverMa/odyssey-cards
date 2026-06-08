using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using OdysseyCards.Relic;
using OdysseyCards.Roguelike;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 综合信息管理界面——全屏覆盖层，对标崩坏星穹铁道「模拟宇宙」的祝福/奇物浏览界面。
///
/// 默认按 CapsLock 键触发，ESC 也可关闭。
/// 三个标签页：
///   1. 运行信息 — 当前层数、房间类型、位面名称
///   2. 卡组 — 左侧当前卡组（只读快照），右侧下局生效（可编辑 + CardGrid 添加卡牌）
///   3. 藏品 — 遍历 GameManager.Relics 显示所有已获得藏品
///
/// 不使用 GetTree().Paused——游戏在背后继续运行，仅通过 MouseFilter + ZIndex 阻断输入。
/// 牌组修改通过 GameManager.PlayerDeck 直接操作，修改在下一场战斗生效。
/// </summary>
public partial class InfoScreen : Control
{
	// ===== 常量 =====

	private const float PanelWidth = 960f;
	private const float PanelHeight = 580f;
	private const float TabButtonHeight = 38f;
	private const int OverlayZIndex = 250;

	// ===== 子控件 =====

	private ColorRect _bg = null!;
	private Panel _mainPanel = null!;
	private HBoxContainer _tabBar = null!;
	private Button _btnRun = null!;
	private Button _btnDeck = null!;
	private Button _btnRelic = null!;
	private Control _tabContent = null!;
	private Label _closeHint = null!;

	// ===== 标签页内容 =====

	private Control? _runTab;
	private Control? _deckTab;
	private Control? _relicTab;
	private VBoxContainer? _relicList;

	// ===== 状态 =====

	private int _activeTabIndex;
	private readonly List<Button> _tabButtons = new();
	private readonly List<Action> _unsubscribeActions = new();

	// ===== 牌组编辑状态 =====

	/// <summary>左侧当前卡组 CardUI 列表（只读）。</summary>
	private readonly List<CardUI> _currentDeckUIs = new();

	/// <summary>右侧编辑卡组 CardUI 列表。</summary>
	private readonly List<CardUI> _editingDeckUIs = new();

	private CardGrid? _cardGrid;
	private Control? _deckEditList;

	/// <summary>直接引用——左侧当前卡组列表容器（避免 FindChild 递归查找失败）。</summary>
	private VBoxContainer? _currentDeckListNode;
	private Label? _currentDeckCountLabel;

	// ===== 事件 =====

	/// <summary>界面关闭时触发。</summary>
	public event Action? OnClosed;

	// ===== 生命周期 =====

	public override void _Ready()
	{
		Name = "InfoScreen";
		ProcessMode = ProcessModeEnum.Always;

		// 全屏覆盖
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		ZIndex = OverlayZIndex;

		BuildOverlay();
		SwitchToTab(0);

		// 订阅事件
		GameManager.Instance.LanguageChanged += OnLanguageChanged;
		GameManager.Instance.OnDeckChanged += OnDeckChanged;
		GameManager.Instance.OnCollectionChanged += OnDeckChanged;

		RegisterHotkeyBindings();
	}

	public override void _ExitTree()
	{
		GameManager.Instance.LanguageChanged -= OnLanguageChanged;
		GameManager.Instance.OnDeckChanged -= OnDeckChanged;
		GameManager.Instance.OnCollectionChanged -= OnDeckChanged;

		UnregisterHotkeyBindings();
	}

	// ===== 界面构建 =====

	private void BuildOverlay()
	{
		// 半透明暗色背景
		_bg = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.65f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_bg);

		// 居中容器
		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		// 主面板
		_mainPanel = new Panel
		{
			CustomMinimumSize = new Vector2(PanelWidth, PanelHeight),
		};
		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f),
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			BorderColor = new Color(0.4f, 0.5f, 0.7f, 0.8f),
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
		};
		_mainPanel.AddThemeStyleboxOverride("panel", panelStyle);
		center.AddChild(_mainPanel);

		var mainVBox = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_mainPanel.AddChild(mainVBox);

		// 标题栏 + 标签页按钮
		var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, TabButtonHeight + 4) };
		mainVBox.AddChild(header);

		_tabBar = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Begin,
		};
		_tabBar.AddThemeConstantOverride("separation", 4);
		header.AddChild(_tabBar);

		// 间隔
		header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

		// 关闭提示
		_closeHint = new Label
		{
			Text = Loc.T("ui.info_screen.close", "关闭 (CapsLock/ESC)"),
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		_closeHint.AddThemeFontSizeOverride("font_size", 12);
		_closeHint.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
		header.AddChild(_closeHint);

		// 标签页内容区
		_tabContent = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Pass,
		};
		_tabContent.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		mainVBox.AddChild(_tabContent);

		// 创建三个标签页按钮
		_btnRun = CreateTabButton("ui.info_screen.tab_run", "运行信息", 0);
		_btnDeck = CreateTabButton("ui.info_screen.tab_deck", "卡组", 1);
		_btnRelic = CreateTabButton("ui.info_screen.tab_relic", "藏品", 2);

		// 构建三个标签页内容
		BuildRunTab();
		BuildDeckTab();
		BuildRelicTab();
	}

	private Button CreateTabButton(string key, string defaultText, int index)
	{
		var btn = new Button
		{
			Text = Loc.T(key, defaultText),
			CustomMinimumSize = new Vector2(100, TabButtonHeight),
		};
		btn.AddThemeFontSizeOverride("font_size", 16);
		btn.Pressed += () => SwitchToTab(index);
		_tabBar.AddChild(btn);
		_tabButtons.Add(btn);
		return btn;
	}

	// ===== 标签页切换 =====

	private void SwitchToTab(int index)
	{
		_activeTabIndex = index;

		for (int i = 0; i < _tabButtons.Count; i++)
		{
			_tabButtons[i].ButtonPressed = (i == index);
		}

		_runTab!.Visible = (index == 0);
		_deckTab!.Visible = (index == 1);
		_relicTab!.Visible = (index == 2);

		if (index == 2)
			RefreshRelicTab();
		if (index == 1)
			RefreshDeckTab();
	}

	// ===== 运行信息标签页 =====

	private void BuildRunTab()
	{
		_runTab = new VBoxContainer
		{
			Visible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_runTab.AddThemeConstantOverride("separation", 16);
		_tabContent.AddChild(_runTab);

		// 间距
		var runVBox = (VBoxContainer)_runTab;
		runVBox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 20) });

		AddInfoRow(runVBox, "ui.info_screen.run.layer", "当前层数", () =>
		{
			var rs = GameManager.Instance.RunState;
			if (rs == null) return null;
			return $"{rs.CurrentLayerIndex + 1} / {rs.TotalLayers}";
		});

		AddInfoRow(runVBox, "ui.info_screen.run.room", "当前房间", () =>
		{
			var rs = GameManager.Instance.RunState;
			return rs?.SelectedRoom?.DisplayName;
		});

		AddInfoRow(runVBox, "ui.info_screen.run.plane", "当前位面", () =>
		{
			var rs = GameManager.Instance.RunState;
			return rs?.CurrentPlane?.PlaneName;
		});

		AddInfoRow(runVBox, "ui.info_screen.run.gold", "金币", () =>
		{
			var gm = GameManager.Instance;
			return gm.RunGold.ToString();
		});

		// 空状态
		var noDataLabel = new Label
		{
			Text = Loc.T("ui.info_screen.run.no_data", "暂无运行数据"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		noDataLabel.AddThemeFontSizeOverride("font_size", 16);
		noDataLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
		noDataLabel.Name = "NoDataLabel";
		runVBox.AddChild(noDataLabel);
	}

	private static void AddInfoRow(VBoxContainer parent, string key, string defaultLabel, Func<string?> valueGetter)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		parent.AddChild(row);

		var label = new Label
		{
			Text = Loc.T(key, defaultLabel),
			CustomMinimumSize = new Vector2(120, 0),
		};
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
		row.AddChild(label);

		var value = new Label
		{
			Text = valueGetter() ?? "—",
		};
		value.AddThemeFontSizeOverride("font_size", 18);
		value.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.6f));
		value.Name = $"Value_{key}";
		row.AddChild(value);
	}

	// ===== 藏品标签页 =====

	private void BuildRelicTab()
	{
		_relicTab = new VBoxContainer
		{
			Visible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_tabContent.AddChild(_relicTab);

		var scroll = new ScrollContainer
		{
			Name = "RelicScroll",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		scroll.AddThemeConstantOverride("separation", 8);
		_relicTab.AddChild(scroll);

		var list = new VBoxContainer
		{
			Name = "RelicList",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		list.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(list);
		_relicList = list;
	}

	private void RefreshRelicTab()
	{
		if (_relicTab == null || _relicList == null) return;

		// 清除旧内容
		foreach (var child in _relicList.GetChildren())
			child.QueueFree();

		var relics = GameManager.Instance.Relics.Relics;
		if (relics.Count == 0)
		{
			var emptyLabel = new Label
			{
				Text = Loc.T("ui.info_screen.relic.empty", "暂无藏品"),
				HorizontalAlignment = HorizontalAlignment.Center,
			};
			emptyLabel.AddThemeFontSizeOverride("font_size", 16);
			emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
			_relicList.AddChild(emptyLabel);
			return;
		}

		foreach (var relic in relics)
		{
			var item = CreateRelicItem(relic);
			_relicList.AddChild(item);
		}
	}

	private static Control CreateRelicItem(AbstractRelic relic)
	{
		var item = new HBoxContainer();
		item.AddThemeConstantOverride("separation", 12);

		// 颜色标识圆点
		var dot = new ColorRect
		{
			CustomMinimumSize = new Vector2(14, 14),
		};
		dot.AddThemeConstantOverride("corner_radius", 7); // 圆形

		if (relic.IsNegative)
			dot.Color = new Color(1f, 0.3f, 0.3f);
		else if (relic.IsSubtle)
			dot.Color = new Color(1f, 0.8f, 0.3f);
		else
			dot.Color = new Color(0.3f, 0.8f, 0.3f);

		item.AddChild(dot);

		// 名称
		var nameLabel = new Label
		{
			Text = relic.Name,
			CustomMinimumSize = new Vector2(160, 0),
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 16);
		nameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.8f));
		item.AddChild(nameLabel);

		// 描述
		var descLabel = new Label
		{
			Text = relic.Description,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		descLabel.AddThemeFontSizeOverride("font_size", 13);
		descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
		descLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		item.AddChild(descLabel);

		return item;
	}

	// ===== 卡组标签页 =====

	private void BuildDeckTab()
	{
		_deckTab = new VBoxContainer
		{
			Visible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_deckTab.AddThemeConstantOverride("separation", 8);
		_tabContent.AddChild(_deckTab);

		// 操作提示
		var hintLabel = new Label
		{
			Text = Loc.T("ui.info_screen.deck.add_hint", "点击右侧卡牌添加到牌组，点击左侧卡牌移除"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		hintLabel.AddThemeFontSizeOverride("font_size", 12);
		hintLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
		_deckTab.AddChild(hintLabel);

		// 左右分栏
		var split = new HSplitContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SplitOffset = (int)(PanelWidth * 0.35f), // 左侧 35%
		};
		_deckTab.AddChild(split);

		// === 左侧：当前卡组（只读）===
		var leftPanel = BuildDeckListPanel(
			"ui.info_screen.deck.current_title",
			"当前卡组（本局）",
			isReadOnly: true,
			out _currentDeckListNode,
			out _currentDeckCountLabel);
		split.AddChild(leftPanel);

		// === 右侧：编辑卡组 + CardGrid ===
		var rightVBox = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		rightVBox.AddThemeConstantOverride("separation", 6);
		split.AddChild(rightVBox);

		// 标题
		var rightTitle = new Label
		{
			Text = Loc.T("ui.info_screen.deck.next_title", "下局生效"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		rightTitle.AddThemeFontSizeOverride("font_size", 16);
		rightTitle.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 0.3f));
		rightVBox.AddChild(rightTitle);

		// 编辑牌组列表（可点击移除）
		var editScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 200),
		};
		rightVBox.AddChild(editScroll);

		_deckEditList = new FlowContainer
		{
			Name = "EditDeckList",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_deckEditList.AddThemeConstantOverride("h_separation", 6);
		_deckEditList.AddThemeConstantOverride("v_separation", 4);
		editScroll.AddChild(_deckEditList);

		// CardGrid — 浏览已解锁卡牌并添加
		_cardGrid = new CardGrid
		{
			ShowFilterBar = true,
			ShowPagination = true,
			Clickable = true,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(300, 180), // 最小宽 300，高 180
		};
		_cardGrid.OnCardClicked += OnCardGridCardClicked;
		rightVBox.AddChild(_cardGrid);

		// 设置 CardGrid 数据
		RefreshCardGrid();
	}

	private static Control BuildDeckListPanel(string titleKey, string defaultTitle, bool isReadOnly,
		out VBoxContainer cardList, out Label countLabel)
	{
		var panel = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		panel.AddThemeConstantOverride("separation", 6);

		var title = new Label
		{
			Text = Loc.T(titleKey, defaultTitle),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		title.AddThemeFontSizeOverride("font_size", 16);
		title.AddThemeColorOverride("font_color", isReadOnly
			? new Color(0.7f, 0.7f, 0.7f)
			: new Color(0.3f, 0.9f, 0.3f));
		panel.AddChild(title);

		countLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		countLabel.AddThemeFontSizeOverride("font_size", 13);
		countLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
		panel.AddChild(countLabel);

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		panel.AddChild(scroll);

		cardList = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		cardList.AddThemeConstantOverride("separation", 4);
		scroll.AddChild(cardList);

		return panel;
	}

	private void RefreshDeckTab()
	{
		RefreshCurrentDeckList();
		RefreshEditingDeckList();

		// CardGrid 需要一个帧的延迟来等待 HSplitContainer 布局完成。
		// GetTree().CreateTimer(0) 在 idle 帧末尾触发，此时所有 Control 的 rect 已由布局系统计算完毕。
		if (IsInsideTree())
		{
			GetTree().CreateTimer(0.0f).Timeout += RefreshCardGrid;
		}
	}

	/// <summary>刷新左侧只读当前卡组列表。</summary>
	private void RefreshCurrentDeckList()
	{
		if (_currentDeckListNode == null) return;

		// 清除旧内容
		foreach (var child in _currentDeckListNode.GetChildren())
			if (child is not Label) child.QueueFree();

		_currentDeckUIs.Clear();

		var snapshot = GameManager.Instance.CombatStartDeckSnapshot;
		var cards = snapshot?.Cards ?? new List<CardData>();

		if (_currentDeckCountLabel != null)
			_currentDeckCountLabel.Text = string.Format(Loc.T("ui.info_screen.deck.count", "{0} 张"), cards.Count);

		if (cards.Count == 0)
		{
			var emptyLabel = new Label
			{
				Text = snapshot == null
					? Loc.T("ui.info_screen.deck.current_empty", "（无快照）")
					: $"{Loc.T("ui.info_screen.deck.current_empty", "（无快照）")} [{cards.Count}张]",
				HorizontalAlignment = HorizontalAlignment.Center,
			};
			emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
			_currentDeckListNode.AddChild(emptyLabel);
			return;
		}

		// 按 ID 分组显示
		var grouped = cards.GroupBy(c => c.Id)
			.OrderBy(g => g.First().Cost)
			.ThenBy(g => g.Key);

		foreach (var group in grouped)
		{
			var cardData = group.First();
			var row = CreateReadOnlyDeckCardRow(cardData, group.Count());
			_currentDeckListNode.AddChild(row);
		}
	}

	private static Control CreateReadOnlyDeckCardRow(CardData cardData, int count)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		// 费用
		var costLabel = new Label
		{
			Text = cardData.Cost.ToString(),
			CustomMinimumSize = new Vector2(24, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		costLabel.AddThemeFontSizeOverride("font_size", 16);
		costLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.7f, 1f));
		row.AddChild(costLabel);

		// 名称
		var nameLabel = new Label
		{
			Text = count > 1 ? $"{cardData.GetLocalizedName()} ×{count}" : cardData.GetLocalizedName(),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 14);
		row.AddChild(nameLabel);

		// 类型标记
		var typeLabel = new Label
		{
			Text = cardData.Type switch
			{
				CardType.Minion => "随从",
				CardType.Spell => "法术",
				CardType.Domain => "领域",
				_ => ""
			},
			CustomMinimumSize = new Vector2(36, 0),
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		typeLabel.AddThemeFontSizeOverride("font_size", 11);
		typeLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
		row.AddChild(typeLabel);

		return row;
	}

	/// <summary>刷新右侧可编辑牌组列表。</summary>
	private void RefreshEditingDeckList()
	{
		if (_deckEditList == null) return;

		// 清除旧内容
		foreach (var child in _deckEditList.GetChildren())
			child.QueueFree();
		_editingDeckUIs.Clear();

		var deck = GameManager.Instance.PlayerDeck;
		var cards = deck?.Cards ?? new List<CardData>();

		// 按 ID 分组
		var grouped = cards.GroupBy(c => c.Id)
			.OrderBy(g => g.First().Cost)
			.ThenBy(g => g.Key);

		foreach (var group in grouped)
		{
			var cardData = group.First();
			var row = CreateEditingDeckCardRow(cardData, group.Count());
			_deckEditList.AddChild(row);
		}
	}

	private Control CreateEditingDeckCardRow(CardData cardData, int count)
	{
		var row = new Panel
		{
			CustomMinimumSize = new Vector2(200, 30),
			MouseFilter = MouseFilterEnum.Stop,
		};

		var rowStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.15f, 0.15f, 0.2f, 0.8f),
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
		};
		row.AddThemeStyleboxOverride("panel", rowStyle);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 8);
		row.AddChild(hbox);

		// 费用
		var costLabel = new Label
		{
			Text = cardData.Cost.ToString(),
			CustomMinimumSize = new Vector2(24, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		costLabel.AddThemeFontSizeOverride("font_size", 14);
		costLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.7f, 1f));
		hbox.AddChild(costLabel);

		// 名称
		var nameLabel = new Label
		{
			Text = count > 1 ? $"{cardData.GetLocalizedName()} ×{count}" : cardData.GetLocalizedName(),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 13);
		hbox.AddChild(nameLabel);

		// 点击移除
		row.GuiInput += (InputEvent @event) =>
		{
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				GameManager.Instance.RemoveCardFromDeck(cardData);
			}
		};

		return row;
	}

	/// <summary>CardGrid 中卡牌被点击 → 添加到牌组。</summary>
	private void OnCardGridCardClicked(CardData cardData)
	{
		var gm = GameManager.Instance;
		if (gm.PlayerDeck == null) return;

		bool added = gm.PlayerDeck.AddCardWithCheck(cardData);
		if (!added)
		{
			// 超限提示
			GD.Print($"[InfoScreen] 牌组已满，无法添加 {cardData.GetLocalizedName()}");
		}
	}

	/// <summary>设置 CardGrid 的数据源。</summary>
	private void RefreshCardGrid()
	{
		if (_cardGrid == null) return;
		var gm = GameManager.Instance;
		var ownedCards = gm.GetAllCards()
			.Where(c => gm.OwnedCardIds.Contains(c.Id))
			.ToList();
		_cardGrid.SetCards(ownedCards);
	}

	// ===== 键盘绑定 =====

	private void RegisterHotkeyBindings()
	{
		// InfoScreen 自身不注册 CapsLock——由持有者（CombatUI/MapUI）负责切换。
		// 仅注册 ESC 关闭绑定（注册时立即生效，在 Close() 中注销）。
	}

	private void UnregisterHotkeyBindings()
	{
		foreach (var unsubscribe in _unsubscribeActions)
			unsubscribe();
		_unsubscribeActions.Clear();
	}

	/// <summary>注册 ESC 关闭绑定 + 拦截 Pause（Open 时调用）。</summary>
	private void RegisterCancelBinding()
	{
		var hm = HotkeyManager.Instance;
		if (hm == null) return;

		// ESC → Cancel → 关闭 InfoScreen
		Action cancelAction = OnCancelPressed;
		hm.PushPressedBinding(OdysseyInput.Cancel, cancelAction);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.Cancel, cancelAction));

		// ESC → Pause（同一物理键）→ 拦截，防止 InfoScreen 打开时同时弹出暂停菜单
		Action blockPause = () => { };
		hm.PushPressedBinding(OdysseyInput.Pause, blockPause);
		_unsubscribeActions.Add(() => hm.RemovePressedBinding(OdysseyInput.Pause, blockPause));
	}

	private void OnCancelPressed()
	{
		if (!Visible) return;
		Close();
	}

	// ===== 公共方法 =====

	/// <summary>打开并显示信息界面。由 CombatUI/MapUI 调用。</summary>
	public void Open()
	{
		Show();
		RegisterCancelBinding();
		RefreshRunTab();
		if (_activeTabIndex == 1) RefreshDeckTab();
		if (_activeTabIndex == 2) RefreshRelicTab();
	}

	/// <summary>关闭信息界面。</summary>
	public void Close()
	{
		// 注销 ESC 绑定，恢复下层场景的 ESC 处理
		UnregisterHotkeyBindings();
		Hide();
		OnClosed?.Invoke();
	}

	/// <summary>刷新运行信息标签页。</summary>
	private void RefreshRunTab()
	{
		if (_runTab == null) return;
		UpdateInfoRowValue("Value_ui.info_screen.run.layer", () =>
		{
			var rs = GameManager.Instance.RunState;
			if (rs == null) return null;
			return $"{rs.CurrentLayerIndex + 1} / {rs.TotalLayers}";
		});
		UpdateInfoRowValue("Value_ui.info_screen.run.room", () =>
		{
			var rs = GameManager.Instance.RunState;
			return rs?.SelectedRoom?.DisplayName;
		});
		UpdateInfoRowValue("Value_ui.info_screen.run.plane", () =>
		{
			var rs = GameManager.Instance.RunState;
			return rs?.CurrentPlane?.PlaneName;
		});
		UpdateInfoRowValue("Value_ui.info_screen.run.gold", () =>
		{
			var gm = GameManager.Instance;
			return gm.RunGold.ToString();
		});
	}

	private void UpdateInfoRowValue(string nodeName, Func<string?> valueGetter)
	{
		var valueLabel = _runTab?.FindChild(nodeName, recursive: true) as Label;
		if (valueLabel != null)
			valueLabel.Text = valueGetter() ?? "—";
	}

	// ===== 事件响应 =====

	private void OnLanguageChanged(string lang)
	{
		if (!IsInsideTree()) return;

		// 刷新按钮文本
		_btnRun.Text = Loc.T("ui.info_screen.tab_run", "运行信息");
		_btnDeck.Text = Loc.T("ui.info_screen.tab_deck", "卡组");
		_btnRelic.Text = Loc.T("ui.info_screen.tab_relic", "藏品");
		_closeHint.Text = Loc.T("ui.info_screen.close", "关闭 (CapsLock/ESC)");

		// 刷新当前标签页
		switch (_activeTabIndex)
		{
			case 0: RefreshRunTab(); break;
			case 1: RefreshDeckTab(); break;
			case 2: RefreshRelicTab(); break;
		}
	}

	private void OnDeckChanged()
	{
		if (!IsInsideTree()) return;
		if (_activeTabIndex == 1)
			RefreshEditingDeckList();
	}
}
